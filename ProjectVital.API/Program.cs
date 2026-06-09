// ╔═══════════════════════════════════════════════════════════════════════════╗
// ║  Program.cs — Entry Point do Backend ProjectVital (.NET 8)               ║
// ║                                                                           ║
// ║  RESPONSABILIDADE:                                                        ║
// ║  Configura e inicializa toda a aplicação ASP.NET Core:                    ║
// ║  1. Leitura de variáveis de ambiente (.env / appsettings.json)            ║
// ║  2. Registro de serviços no container de DI (builder.Services)            ║
// ║  3. Configuração do pipeline de middleware (app.Use*)                     ║
// ║  4. Seed de dados de desenvolvimento (apenas em Development)              ║
// ║                                                                           ║
// ║  QUANDO ADICIONAR UM NOVO RESOLVER GRAPHQL:                               ║
// ║  1. Criar a classe do resolver em API/GraphQL/Resolvers/                  ║
// ║  2. Adicionar .AddTypeExtension<NovoResolver>() na seção Hot Chocolate    ║
// ║  3. Se o resolver tiver tipo GraphQL dedicado, adicionar .AddType<>()     ║
// ║                                                                           ║
// ║  QUANDO ADICIONAR UM NOVO SERVICE/REPOSITORY:                             ║
// ║  1. Criar interface em Domain/Interfaces/                                 ║
// ║  2. Criar implementação em Infrastructure/Repositories/ ou                ║
// ║     Application/Services/                                                 ║
// ║  3. Registrar com AddScoped<IInterface, Implementacao>() abaixo           ║
// ║                                                                           ║
// ║  VARIÁVEIS DE AMBIENTE OBRIGATÓRIAS:                                      ║
// ║  - DB_CONNECTION_STRING  → conexão MySQL                                  ║
// ║  - JWT_SECRET            → chave JWT (≥32 chars)                          ║
// ║  Opcionais: ML_SERVICE_URL, ML_SERVICE_TIMEOUT_SECONDS, JWT_EXPIRY_HOURS  ║
// ║  Todas definidas no .env na raiz do projeto.                              ║
// ╚═══════════════════════════════════════════════════════════════════════════╝

using System.Text;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using ProjectVital.API.GraphQL.Errors;
using ProjectVital.API.GraphQL.Resolvers;
using ProjectVital.Application.DTOs;
using ProjectVital.API.GraphQL.Types;
using ProjectVital.Application.Interfaces;
using ProjectVital.Application.Services;
using ProjectVital.Domain.Entities;
using ProjectVital.Domain.Enums;
using ProjectVital.Domain.Interfaces;
using ProjectVital.Infrastructure.Persistence;
using ProjectVital.Infrastructure.Repositories;
using ProjectVital.ML;

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

// ── ML Service HTTP Client ────────────────────────────────────────────────────
var mlServiceUrl = builder.Configuration["ML_SERVICE_URL"]
    ?? "http://ml-service:8000";

var mlTimeoutSeconds = int.TryParse(builder.Configuration["ML_SERVICE_TIMEOUT_SECONDS"], out var t) ? t : 30;

builder.Services.AddHttpClient<IMLServiceClient, MLServiceClient>(client =>
{
    client.BaseAddress = new Uri(mlServiceUrl);
    client.Timeout = TimeSpan.FromSeconds(mlTimeoutSeconds);
    client.DefaultRequestHeaders.Add("Accept", "application/json");
});

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
    .AddTypeExtension<PlanQueryResolver>()
    .AddType<StudentType>()
    .AddType<CheckInType>()
    .AddType<PredictionType>()
    .AddType<PlanType>()
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
// A ORDEM ABAIXO NÃO PODE SER ALTERADA SEM CONSEQUÊNCIAS:
//
// 1. UseCors     → precisa ser antes de qualquer autenticação para que o browser
//                  receba os headers CORS antes de ser bloqueado por auth
// 2. UseRateLimiter → antes de auth para bloquear antes de processar credenciais
// 3. UseAuthentication → lê e valida o Bearer token do header Authorization
// 4. UseAuthorization  → usa as claims do passo anterior para verificar [Authorize]
//                        DEVE VIR DEPOIS de UseAuthentication
//
// SE INVERTER UseAuthentication e UseAuthorization: [Authorize] não terá claims
// e bloqueará requests válidos.
// SE COLOCAR UseCors depois de UseAuthentication: preflight OPTIONS falha com 401.
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
    var mlClient = scope.ServiceProvider.GetRequiredService<IMLServiceClient>();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

    await db.Database.MigrateAsync();
    logger.LogInformation("Migrations aplicadas com sucesso.");

    await SeedDevelopmentDataAsync(db, authSvc, mlClient, logger);
}

app.Run();

// ── Seed de dados para desenvolvimento ───────────────────────────────────────
static async Task SeedDevelopmentDataAsync(
    ProjectVitalDbContext db,
    IAuthService auth,
    IMLServiceClient mlClient,
    ILogger logger)
{
    // Idempotência: guarda pelo e-mail do novo manager
    if (await db.Users.AnyAsync(u => u.Email == "gestor@vital.dev")) return;

    logger.LogInformation("Inserindo dados de seed realista (20 alunos, 3 planos)...");

    var agora = DateTime.UtcNow;

    // ── 1. Manager ────────────────────────────────────────────────────────────
    var manager = new User
    {
        Name = "Gestor Principal",
        Email = "gestor@vital.dev",
        PasswordHash = auth.HashPassword("Senha@123"),
        Role = UserRole.Manager,
        CreatedAt = agora
    };
    db.Users.Add(manager);
    await db.SaveChangesAsync();

    // ── 2. Academia ───────────────────────────────────────────────────────────
    var academy = new Academy
    {
        Name = "Academia Central Americana",
        Address = "Av. Brasil, 450 — Americana/SP",
        ManagerId = manager.Id,
        CreatedAt = agora
    };
    db.Academies.Add(academy);
    await db.SaveChangesAsync();

    // ── 3. Planos ─────────────────────────────────────────────────────────────
    var planMensal     = new Plan { AcademyId = academy.Id, Name = "Mensal",      Price = 89.90m,  DurationDays = 30  };
    var planTrimestral = new Plan { AcademyId = academy.Id, Name = "Trimestral",  Price = 239.70m, DurationDays = 90  };
    var planAnual      = new Plan { AcademyId = academy.Id, Name = "Anual",       Price = 799.00m, DurationDays = 365 };
    db.Plans.AddRange(planMensal, planTrimestral, planAnual);
    await db.SaveChangesAsync();

    // ── Helpers locais ────────────────────────────────────────────────────────

    async Task<Student> CriarAluno(string nome, string email, Plan plano, int diasMatricula, bool ativo = true)
    {
        var user = new User
        {
            Name = nome,
            Email = email,
            PasswordHash = auth.HashPassword("Senha@123"),
            Role = UserRole.Student,
            CreatedAt = agora
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();

        var student = new Student
        {
            UserId = user.Id,
            AcademyId = academy.Id,
            PlanId = plano.Id,
            EnrollmentDate = agora.AddDays(-diasMatricula),
            Active = ativo
        };
        db.Students.Add(student);
        await db.SaveChangesAsync();
        return student;
    }

    // Soft-delete: desativa Student + User (Global Query Filter filtrará ambos)
    async Task SoftDeleteAluno(Student student)
    {
        student.Active = false;
        student.SoftDelete();
        var user = await db.Users.FindAsync(student.UserId);
        if (user is not null) user.SoftDelete();
        await db.SaveChangesAsync();
    }

    void AdicionarCheckIns(Student s, IEnumerable<int> diasAtras, int baseDuracao = 60)
    {
        var rng = new Random(s.Id);
        foreach (var dia in diasAtras)
        {
            db.CheckIns.Add(new CheckIn
            {
                StudentId = s.Id,
                Timestamp = agora
                    .AddDays(-dia)
                    .AddHours(rng.Next(6, 21))
                    .AddMinutes(rng.Next(0, 60)),
                DurationMinutes = baseDuracao + rng.Next(-10, 20)
            });
        }
    }

    // ── 4. Perfil A — "Ativo Regular" (6 alunos) ─────────────────────────────
    // Check-in a cada 2 dias por 60 dias (≈30 entradas); último ≤ 2 dias.
    // Matriculados há 90 dias.
    int[] DiasPerfilA(int inicio) { var r = new List<int>(); for (int d = inicio; d <= 60; d += 2) r.Add(d); return r.ToArray(); }

    var pA1 = await CriarAluno("Lucas Oliveira",   "lucas.oliveira@vital.dev",   planTrimestral, 90);
    var pA2 = await CriarAluno("Ana Beatriz Costa","ana.costa@vital.dev",        planTrimestral, 90);
    var pA3 = await CriarAluno("Rafael Mendes",    "rafael.mendes@vital.dev",    planAnual,      90);
    var pA4 = await CriarAluno("Fernanda Lima",    "fernanda.lima@vital.dev",    planTrimestral, 90);
    var pA5 = await CriarAluno("Bruno Carvalho",   "bruno.carvalho@vital.dev",   planAnual,      90);
    var pA6 = await CriarAluno("Camila Rodrigues", "camila.rodrigues@vital.dev", planTrimestral, 90);

    AdicionarCheckIns(pA1, DiasPerfilA(1));   // último: 1 dia atrás
    AdicionarCheckIns(pA2, DiasPerfilA(2));   // último: 2 dias atrás
    AdicionarCheckIns(pA3, DiasPerfilA(1));
    AdicionarCheckIns(pA4, DiasPerfilA(2));
    AdicionarCheckIns(pA5, DiasPerfilA(1));
    AdicionarCheckIns(pA6, DiasPerfilA(2));
    await db.SaveChangesAsync();

    // ── 5. Perfil B — "Em Risco Médio" (5 alunos) ────────────────────────────
    // Primeiras 4 semanas: 2x/semana (a cada 3–4 dias).
    // Semanas 5–10: 1x/semana (a cada 7 dias). Último: 8–12 dias atrás.
    // Matriculados há 75 dias. Último check-in ≤ 75.
    int[] DiasPerfilB(int ultimoHaDias)
    {
        var r = new List<int>();
        // Fase 1 — 4 semanas iniciais (dias 75 até 48): a cada 3–4 dias
        for (int d = 75; d >= 48; d -= 4) r.Add(d);
        // Fase 2 — frequência caindo (dias 42 até 15): 1x/semana
        for (int d = 42; d >= 15; d -= 7) r.Add(d);
        // Último check-in recente
        r.Add(ultimoHaDias);
        return r.ToArray();
    }

    var pB1 = await CriarAluno("Juliana Ferreira", "juliana.ferreira@vital.dev", planMensal,     75);
    var pB2 = await CriarAluno("Marcos Santos",    "marcos.santos@vital.dev",    planTrimestral, 75);
    var pB3 = await CriarAluno("Patricia Alves",   "patricia.alves@vital.dev",   planMensal,     75);
    var pB4 = await CriarAluno("Diego Souza",      "diego.souza@vital.dev",      planTrimestral, 75);
    var pB5 = await CriarAluno("Renata Nunes",     "renata.nunes@vital.dev",     planMensal,     75);

    AdicionarCheckIns(pB1, DiasPerfilB(8));
    AdicionarCheckIns(pB2, DiasPerfilB(10));
    AdicionarCheckIns(pB3, DiasPerfilB(9));
    AdicionarCheckIns(pB4, DiasPerfilB(12));
    AdicionarCheckIns(pB5, DiasPerfilB(11));
    await db.SaveChangesAsync();

    // ── 6. Perfil C — "Alto Risco" (4 alunos) ────────────────────────────────
    // 5 check-ins nos primeiros 15 dias após matrícula.
    // Depois: silêncio de 18–35 dias (último check-in = 18 a 35 dias atrás).
    // Matriculados há 60 dias. Plano Mensal.
    // Nota: "primeiros 15 dias" = dias 60 até 45 atrás. Último = 18–35 dias.
    int[] DiasPerfilC(int ultimoHaDias)
    {
        var r = new List<int> { 60, 57, 54, 51, 48 };  // 5 check-ins nos primeiros 15 dias
        r.Add(ultimoHaDias);                            // único check-in recente (18–35 dias)
        return r.ToArray();
    }

    var pC1 = await CriarAluno("Thiago Barbosa", "thiago.barbosa@vital.dev", planMensal, 60);
    var pC2 = await CriarAluno("Larissa Martins","larissa.martins@vital.dev", planMensal, 60);
    var pC3 = await CriarAluno("Felipe Costa",   "felipe.costa@vital.dev",   planMensal, 60);
    var pC4 = await CriarAluno("Amanda Silva",   "amanda.silva@vital.dev",   planMensal, 60);

    AdicionarCheckIns(pC1, DiasPerfilC(18));
    AdicionarCheckIns(pC2, DiasPerfilC(25));
    AdicionarCheckIns(pC3, DiasPerfilC(30));
    AdicionarCheckIns(pC4, DiasPerfilC(35));
    await db.SaveChangesAsync();

    // ── 7. Perfil D — "Inativo Confirmado" (3 alunos) ────────────────────────
    // active=false + soft delete. Último check-in: 45–70 dias atrás.
    // Matriculados há 120 dias.
    // ATENÇÃO: SoftDelete() é chamado em Student + User → invisíveis no dashboard.
    var pD1 = await CriarAluno("Rodrigo Lima",     "rodrigo.lima@vital.dev",     planMensal,     120, ativo: false);
    var pD2 = await CriarAluno("Vanessa Pereira",  "vanessa.pereira@vital.dev",  planTrimestral, 120, ativo: false);
    var pD3 = await CriarAluno("Gustavo Almeida",  "gustavo.almeida@vital.dev",  planMensal,     120, ativo: false);

    AdicionarCheckIns(pD1, new[] { 47, 68, 95, 112 });   // último: 47 dias atrás
    AdicionarCheckIns(pD2, new[] { 52, 75, 100, 118 });  // último: 52 dias atrás
    AdicionarCheckIns(pD3, new[] { 70, 90, 108, 115 });  // último: 70 dias atrás
    await db.SaveChangesAsync();

    await SoftDeleteAluno(pD1);
    await SoftDeleteAluno(pD2);
    await SoftDeleteAluno(pD3);

    // ── 8. Perfil E — "Cold Start" (2 alunos) ────────────────────────────────
    // Matriculados há 8 e 12 dias. Apenas 1–2 check-ins.
    var pE1 = await CriarAluno("Isabela Torres",       "isabela.torres@vital.dev", planMensal, 8);
    var pE2 = await CriarAluno("Pedro Henrique Gomes", "pedro.gomes@vital.dev",    planMensal, 12);

    AdicionarCheckIns(pE1, new[] { 6 });           // 1 check-in
    AdicionarCheckIns(pE2, new[] { 10, 7 });       // 2 check-ins
    await db.SaveChangesAsync();

    logger.LogInformation(
        "Seed concluído: 20 alunos em 5 perfis (A=6,B=5,C=4,D=3 soft-deleted,E=2) | " +
        "Manager: gestor@vital.dev | AcademyId={AcademyId}",
        academy.Id);

    // ── 9. Predições iniciais (falha graciosamente se ML offline) ─────────────
    try
    {
        var predicoes = await mlClient.PredictBatchAsync(academy.Id);
        logger.LogInformation("Predições iniciais: {Count} alunos avaliados.", predicoes.Count);
    }
    catch (Exception ex)
    {
        logger.LogWarning(
            "ML Service indisponível no seed — execute POST /predict/batch manualmente. Erro: {Msg}",
            ex.Message);
    }
}

// Make Program accessible for WebApplicationFactory in integration tests
public partial class Program { }
