using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using ProjectVital.API.GraphQL.Errors;
using ProjectVital.API.GraphQL.Resolvers;
using ProjectVital.API.GraphQL.Types;
using ProjectVital.Application.Interfaces;
using ProjectVital.Application.Services;
using ProjectVital.Domain.Entities;
using ProjectVital.Domain.Enums;
using ProjectVital.Domain.Interfaces;
using ProjectVital.Infrastructure.Persistence;
using ProjectVital.Infrastructure.Repositories;

// ─────────────────────────────────────────────────────────────────────────────
// FASE 2 — GraphQL + JWT + CORS + Services
// ─────────────────────────────────────────────────────────────────────────────

var builder = WebApplication.CreateBuilder(args);

// ── Connection string ─────────────────────────────────────────────────────────
// Prioridade: env var > appsettings.Development.json
var connectionString = builder.Configuration["DB_CONNECTION_STRING"]
    ?? builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException(
        "Connection string ausente. Defina DB_CONNECTION_STRING no .env.");

// ── JWT Secret ────────────────────────────────────────────────────────────────
// NUNCA hardcodado — obrigatoriamente via variável de ambiente
var jwtSecret = builder.Configuration["JWT_SECRET"]
    ?? throw new InvalidOperationException(
        "JWT_SECRET não configurado. Defina a variável de ambiente JWT_SECRET " +
        "(mínimo 32 caracteres).");

if (jwtSecret.Length < 32)
    throw new InvalidOperationException(
        "JWT_SECRET deve ter no mínimo 32 caracteres para garantir segurança.");

// ── EF Core + MySQL (Pomelo) ──────────────────────────────────────────────────
var mysqlVersion = ServerVersion.Create(
    new Version(8, 0, 0),
    Pomelo.EntityFrameworkCore.MySql.Infrastructure.ServerType.MySql);

builder.Services.AddDbContext<ProjectVitalDbContext>(options =>
    options.UseMySql(
        connectionString,
        mysqlVersion,
        o => o
            .EnableRetryOnFailure(maxRetryCount: 3, maxRetryDelay: TimeSpan.FromSeconds(5), errorNumbersToAdd: null)
            .MigrationsAssembly("ProjectVital.Infrastructure")));

// ── Repositórios (DI) ─────────────────────────────────────────────────────────
builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<IAcademyRepository, AcademyRepository>();
builder.Services.AddScoped<IPlanRepository, PlanRepository>();
builder.Services.AddScoped<IStudentRepository, StudentRepository>();
builder.Services.AddScoped<ICheckInRepository, CheckInRepository>();
builder.Services.AddScoped<IPredictionRepository, PredictionRepository>();

// ── Application Services (DI) ─────────────────────────────────────────────────
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IStudentService, StudentService>();
builder.Services.AddScoped<ICheckInService, CheckInService>();
builder.Services.AddScoped<IPredictionService, PredictionService>();
builder.Services.AddScoped<IAcademyService, AcademyService>();

// ── JWT Authentication ────────────────────────────────────────────────────────
builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret)),
            ValidateIssuer = false,
            ValidateAudience = false,
            // Zero tolerância — token expirado é rejeitado imediatamente
            ClockSkew = TimeSpan.Zero
        };
    });

builder.Services.AddAuthorization();
builder.Services.AddHttpContextAccessor();

// ── CORS ──────────────────────────────────────────────────────────────────────
// Origins lidas de appsettings — NUNCA usar AllowAnyOrigin() em produção
var allowedOrigins = builder.Configuration
    .GetSection("Cors:AllowedOrigins")
    .Get<string[]>() ?? [];

builder.Services.AddCors(options =>
{
    options.AddPolicy("Frontend", policy =>
    {
        if (allowedOrigins.Length > 0)
        {
            policy
                .WithOrigins(allowedOrigins)
                .AllowAnyHeader()
                .AllowAnyMethod();
        }
        else
        {
            // Sem origins configuradas: bloqueia tudo (fail-safe para produção)
            policy.WithOrigins("https://configure-allowed-origins-in-env");
        }
    });
});

// ── Hot Chocolate — GraphQL Server ────────────────────────────────────────────
builder.Services
    .AddGraphQLServer()
    // Tipos raiz — resolvers são adicionados como extensões (ExtendObjectType)
    .AddQueryType(d => d.Name(OperationTypeNames.Query))
    .AddMutationType(d => d.Name(OperationTypeNames.Mutation))
    // Query resolvers
    .AddTypeExtension<StudentQueryResolver>()
    .AddTypeExtension<CheckInQueryResolver>()
    .AddTypeExtension<PredictionQueryResolver>()
    .AddTypeExtension<KPIResolver>()
    // Mutation resolvers
    .AddTypeExtension<AuthResolver>()
    .AddTypeExtension<StudentMutationResolver>()
    .AddTypeExtension<CheckInMutationResolver>()
    .AddTypeExtension<PredictionMutationResolver>()
    // Tipos de output
    .AddType<StudentType>()
    .AddType<CheckInType>()
    .AddType<PredictionType>()
    .AddType<AcademyKPIsType>()
    .AddType<UserType>()
    .AddType<AuthPayloadType>()
    .AddType<RetrainResultType>()
    // Integração com ASP.NET Core Authorization — lê HttpContext.User (JWT)
    .AddAuthorization()
    // Filtro de erros: mapeia exceções para códigos GraphQL, remove stack traces
    .AddErrorFilter<AppErrorFilter>()
    // Detalhes de exceção no payload apenas em desenvolvimento
    .ModifyRequestOptions(opt =>
        opt.IncludeExceptionDetails = builder.Environment.IsDevelopment());

// ── Health Check ──────────────────────────────────────────────────────────────
builder.Services.AddHealthChecks()
    .AddDbContextCheck<ProjectVitalDbContext>("database");

// ─────────────────────────────────────────────────────────────────────────────

var app = builder.Build();

// ── Middleware pipeline — ORDEM É CRÍTICA ────────────────────────────────────
// 1. CORS deve ser o primeiro para que preflight OPTIONS passe antes do JWT
app.UseCors("Frontend");
// 2. JWT: valida o Bearer token e popula HttpContext.User
app.UseAuthentication();
// 3. Authorization: aplica políticas de autorização
app.UseAuthorization();

// ── Endpoints ─────────────────────────────────────────────────────────────────
// GraphQL endpoint principal — todas as queries e mutations
app.MapGraphQL("/graphql");

// Health check para Docker Compose e load balancers
app.MapHealthChecks("/health");

// Interface visual do Banana Cake Pop apenas em desenvolvimento
if (app.Environment.IsDevelopment())
    app.MapBananaCakePop("/graphql/ui");

// ── Migration automática + Seed (apenas em Development) ──────────────────────
if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<ProjectVitalDbContext>();
    var authSvc = scope.ServiceProvider.GetRequiredService<IAuthService>();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

    await db.Database.MigrateAsync();
    logger.LogInformation("Migrations aplicadas com sucesso.");

    await SeedDevelopmentDataAsync(db, authSvc, logger);
}

app.Run();

// ── Seed de dados para desenvolvimento ───────────────────────────────────────
static async Task SeedDevelopmentDataAsync(
    ProjectVitalDbContext db,
    IAuthService auth,
    ILogger logger)
{
    // Só executa se não houver dados (idempotente)
    if (await db.Users.AnyAsync()) return;

    logger.LogInformation("Inserindo dados de seed para desenvolvimento...");

    // Manager da academia de teste
    var manager = new User
    {
        Name = "Admin Teste",
        Email = "admin@projectvital.dev",
        PasswordHash = auth.HashPassword("Senha@123"),
        Role = UserRole.Manager,
        CreatedAt = DateTime.UtcNow
    };
    db.Users.Add(manager);
    await db.SaveChangesAsync();

    // Academia vinculada ao manager
    var academy = new Academy
    {
        Name = "Academia Project Vital",
        Address = "Rua Teste, 123 — Americana/SP",
        ManagerId = manager.Id,
        CreatedAt = DateTime.UtcNow
    };
    db.Academies.Add(academy);
    await db.SaveChangesAsync();

    // Plano básico
    var plan = new Plan
    {
        AcademyId = academy.Id,
        Name = "Plano Básico",
        Price = 99.90m,
        DurationDays = 30
    };
    db.Plans.Add(plan);
    await db.SaveChangesAsync();

    // 3 alunos de teste
    var alunoNames = new[] { ("João Silva", "joao@test.dev"), ("Maria Santos", "maria@test.dev"), ("Carlos Lima", "carlos@test.dev") };
    foreach (var (name, email) in alunoNames)
    {
        var userAluno = new User
        {
            Name = name,
            Email = email,
            PasswordHash = auth.HashPassword("Senha@123"),
            Role = UserRole.Student,
            CreatedAt = DateTime.UtcNow
        };
        db.Users.Add(userAluno);
        await db.SaveChangesAsync();

        var student = new Student
        {
            UserId = userAluno.Id,
            AcademyId = academy.Id,
            PlanId = plan.Id,
            EnrollmentDate = DateTime.UtcNow.AddDays(-60),
            Active = true
        };
        db.Students.Add(student);
        await db.SaveChangesAsync();

        // Alguns check-ins para cada aluno
        var random = new Random(student.Id);
        for (int i = 0; i < 12; i++)
        {
            db.CheckIns.Add(new CheckIn
            {
                StudentId = student.Id,
                Timestamp = DateTime.UtcNow.AddDays(-(random.Next(1, 55)))
            });
        }
    }

    await db.SaveChangesAsync();
    logger.LogInformation(
        "Seed concluído. Manager: admin@projectvital.dev / Senha@123 | AcademyId={AcademyId}",
        academy.Id);
}
