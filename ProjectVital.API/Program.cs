using System.Text;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
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

var builder = WebApplication.CreateBuilder(args);

// ── Logging estruturado ───────────────────────────────────────────────────────
if (!builder.Environment.IsDevelopment())
    builder.Logging.AddJsonConsole();

// ── Connection string ─────────────────────────────────────────────────────────
var connectionString = builder.Configuration["DB_CONNECTION_STRING"]
    ?? builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException(
        "Connection string ausente. Defina DB_CONNECTION_STRING no .env.");

// ── JWT Secret ────────────────────────────────────────────────────────────────
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
            ClockSkew = TimeSpan.Zero
        };
    });

builder.Services.AddAuthorization();
builder.Services.AddHttpContextAccessor();

// ── CORS ──────────────────────────────────────────────────────────────────────
var allowedOrigins = builder.Configuration
    .GetSection("Cors:AllowedOrigins")
    .Get<string[]>() ?? [];

builder.Services.AddCors(options =>
{
    options.AddPolicy("Frontend", policy =>
    {
        if (allowedOrigins.Length > 0)
            policy.WithOrigins(allowedOrigins).AllowAnyHeader().AllowAnyMethod();
        else
            policy.WithOrigins("https://configure-allowed-origins-in-env");
    });
});

// ── Rate Limiting ─────────────────────────────────────────────────────────────
builder.Services.AddRateLimiter(options =>
{
    // Limite geral por IP: 100 requests/min
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(ctx =>
        RateLimitPartition.GetFixedWindowLimiter(
            ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 100,
                Window = TimeSpan.FromMinutes(1),
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit = 0
            }));

    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
});

// ── Hot Chocolate — GraphQL Server ────────────────────────────────────────────
builder.Services
    .AddGraphQLServer()
    .AddQueryType(d => d.Name(OperationTypeNames.Query))
    .AddMutationType(d => d.Name(OperationTypeNames.Mutation))
    .AddTypeExtension<StudentQueryResolver>()
    .AddTypeExtension<CheckInQueryResolver>()
    .AddTypeExtension<PredictionQueryResolver>()
    .AddTypeExtension<KPIResolver>()
    .AddTypeExtension<AuthResolver>()
    .AddTypeExtension<StudentMutationResolver>()
    .AddTypeExtension<CheckInMutationResolver>()
    .AddTypeExtension<PredictionMutationResolver>()
    .AddType<StudentType>()
    .AddType<CheckInType>()
    .AddType<PredictionType>()
    .AddType<AcademyKPIsType>()
    .AddType<UserType>()
    .AddType<AuthPayloadType>()
    .AddType<RetrainResultType>()
    .AddAuthorization()
    .AddErrorFilter<AppErrorFilter>()
    // Bloqueia introspection em produção (SEC-02)
    .AllowIntrospection(builder.Environment.IsDevelopment())
    .ModifyRequestOptions(opt =>
        opt.IncludeExceptionDetails = builder.Environment.IsDevelopment());

// ── Health Check ──────────────────────────────────────────────────────────────
builder.Services.AddHealthChecks()
    .AddDbContextCheck<ProjectVitalDbContext>("mysql");

var app = builder.Build();

// ── Middleware pipeline — ORDEM É CRÍTICA ────────────────────────────────────
app.UseCors("Frontend");
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

app.MapGraphQL("/graphql");
app.MapHealthChecks("/health");

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
    if (await db.Users.AnyAsync()) return;

    logger.LogInformation("Inserindo dados de seed para desenvolvimento...");

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

    var academy = new Academy
    {
        Name = "Academia Project Vital",
        Address = "Rua Teste, 123 — Americana/SP",
        ManagerId = manager.Id,
        CreatedAt = DateTime.UtcNow
    };
    db.Academies.Add(academy);
    await db.SaveChangesAsync();

    var plan = new Plan
    {
        AcademyId = academy.Id,
        Name = "Plano Básico",
        Price = 99.90m,
        DurationDays = 30
    };
    db.Plans.Add(plan);
    await db.SaveChangesAsync();

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
    // SEC-01: Nunca logar senha em texto plano — apenas email e academyId
    logger.LogInformation(
        "Seed concluído. Manager: admin@projectvital.dev | AcademyId={AcademyId}",
        academy.Id);
}
