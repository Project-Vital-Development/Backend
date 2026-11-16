// ─────────────────────────────────────────────────────────────────────────────
// Project Vital — API (ASP.NET Core 8 + Hot Chocolate GraphQL)
//
// Ordem do pipeline: ForwardedHeaders → SecurityHeaders → CORS → RateLimiter →
// Authentication → Authorization → GraphQL. CORS antes da autenticação (preflight
// sem token); autenticação antes da autorização.
//
// Isolamento entre academias em duas camadas:
//   1. Resolvers: ResolverAuthHelper exige gestor e usa a academia do JWT.
//   2. Banco: filtros globais do EF Core (ProjectVitalDbContext + HttpTenantContext).
// ─────────────────────────────────────────────────────────────────────────────
using System.Text;
using System.Threading.RateLimiting;
using HotChocolate.AspNetCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using ProjectVital.API.Background;
using ProjectVital.API.GraphQL.Errors;
using ProjectVital.API.GraphQL.Resolvers;
using ProjectVital.API.GraphQL.Types;
using ProjectVital.API.Security;
using ProjectVital.API.Seed;
using ProjectVital.Application.Interfaces;
using ProjectVital.Application.Services;
using ProjectVital.Domain.Interfaces;
using ProjectVital.Infrastructure.Persistence;
using ProjectVital.Infrastructure.Repositories;
using ProjectVital.ML;

var builder = WebApplication.CreateBuilder(args);
var config = builder.Configuration;
var isDev = builder.Environment.IsDevelopment();
var isTest = builder.Environment.IsEnvironment("Test");

// ── Hospedagem ────────────────────────────────────────────────────────────────
var platformPort = Environment.GetEnvironmentVariable("PORT");
if (!string.IsNullOrWhiteSpace(platformPort))
    builder.WebHost.UseUrls($"http://+:{platformPort}");

builder.WebHost.ConfigureKestrel(k =>
{
    k.AddServerHeader = false;                       // não anunciar "Kestrel"
    k.Limits.MaxRequestBodySize = 256 * 1024;        // GraphQL legítimo cabe com folga
    k.Limits.MaxRequestHeadersTotalSize = 32 * 1024;
});

if (!isDev)
    builder.Logging.AddJsonConsole(o => o.IncludeScopes = true);

// ── Configuração obrigatória ──────────────────────────────────────────────────
var connectionString = config["DB_CONNECTION_STRING"]
    ?? config.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Connection string ausente. Defina DB_CONNECTION_STRING.");

var jwtSecret = config["JWT_SECRET"]
    ?? throw new InvalidOperationException("JWT_SECRET não configurado (mínimo 32 caracteres).");
if (jwtSecret.Length < 32)
    throw new InvalidOperationException("JWT_SECRET deve ter no mínimo 32 caracteres.");
var jwtIssuer = config["JWT_ISSUER"] ?? AuthService.DefaultIssuer;
var jwtAudience = config["JWT_AUDIENCE"] ?? AuthService.DefaultAudience;

var mlApiKey = config["ML_API_KEY"];
if (string.IsNullOrWhiteSpace(mlApiKey) && !isDev && !isTest)
    throw new InvalidOperationException("ML_API_KEY não configurada (chave compartilhada com o ML Service).");

// ── Banco ─────────────────────────────────────────────────────────────────────
var mysqlVersion = ServerVersion.Create(new Version(8, 0, 0),
    Pomelo.EntityFrameworkCore.MySql.Infrastructure.ServerType.MySql);

builder.Services.AddDbContext<ProjectVitalDbContext>(options =>
    options.UseMySql(connectionString, mysqlVersion, o => o
        .EnableRetryOnFailure(maxRetryCount: 3, maxRetryDelay: TimeSpan.FromSeconds(5), errorNumbersToAdd: null)
        .CommandTimeout(30)
        .MigrationsAssembly("ProjectVital.Infrastructure")));

builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<IAcademyRepository, AcademyRepository>();
builder.Services.AddScoped<IPlanRepository, PlanRepository>();
builder.Services.AddScoped<IStudentRepository, StudentRepository>();
builder.Services.AddScoped<ICheckInRepository, CheckInRepository>();
builder.Services.AddScoped<IPredictionRepository, PredictionRepository>();

// ── ML Service ────────────────────────────────────────────────────────────────
var mlServiceUrl = config["ML_SERVICE_URL"] ?? "http://ml-service:8000";
var mlTimeoutSeconds = int.TryParse(config["ML_SERVICE_TIMEOUT_SECONDS"], out var t) ? t : 30;

builder.Services.AddTransient<CorrelationIdHandler>();
builder.Services.AddHttpClient<IMLServiceClient, MLServiceClient>(client =>
{
    client.BaseAddress = new Uri(mlServiceUrl);
    // O retreino compara três algoritmos com validação cruzada: piso de 300 s.
    client.Timeout = TimeSpan.FromSeconds(Math.Max(mlTimeoutSeconds, 300));
    client.DefaultRequestHeaders.Add("Accept", "application/json");
    if (!string.IsNullOrWhiteSpace(mlApiKey))
        client.DefaultRequestHeaders.Add("X-Internal-Key", mlApiKey);
}).AddHttpMessageHandler<CorrelationIdHandler>();

// ── Serviços de aplicação ─────────────────────────────────────────────────────
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IStudentService, StudentService>();
builder.Services.AddScoped<ICheckInService, CheckInService>();
builder.Services.AddScoped<IPredictionService, PredictionService>();
builder.Services.AddScoped<IAcademyService, AcademyService>();
builder.Services.AddMemoryCache();
builder.Services.AddSingleton<LoginThrottle>();
builder.Services.AddSingleton<OperationThrottle>();

if (!isTest)
{
    builder.Services.AddHostedService<ModelBootstrapService>();
    builder.Services.AddHostedService<PredictionRefreshService>();
}

// ── Autenticação ──────────────────────────────────────────────────────────────
builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.MapInboundClaims = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret)),
            ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
            ValidateIssuer = true,
            ValidIssuer = jwtIssuer,
            ValidateAudience = true,
            ValidAudience = jwtAudience,
            ValidateLifetime = true,
            RequireExpirationTime = true,
            RequireSignedTokens = true,
            ClockSkew = TimeSpan.FromSeconds(30),
            RoleClaimType = System.Security.Claims.ClaimTypes.Role,
            NameClaimType = "sub",
        };
    });

builder.Services.AddAuthorization();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ITenantContext, HttpTenantContext>();

// ── CORS ──────────────────────────────────────────────────────────────────────
var allowedOrigins = (config.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [])
    .Concat((config["CORS_ALLOWED_ORIGINS"] ?? string.Empty)
        .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
    .Select(o => o.TrimEnd('/'))
    .Where(o => o.StartsWith("https://", StringComparison.Ordinal) || isDev || isTest)
    .Distinct()
    .ToArray();

builder.Services.AddCors(options => options.AddPolicy("Frontend", policy =>
{
    if (allowedOrigins.Length > 0)
        policy.WithOrigins(allowedOrigins)
              .WithMethods("POST", "OPTIONS")
              .WithHeaders("Content-Type", "Authorization", "GraphQL-Preflight", CorrelationIdMiddleware.Header)
              .WithExposedHeaders(CorrelationIdMiddleware.Header)
              .SetPreflightMaxAge(TimeSpan.FromHours(1));
}));

// ── Proxy reverso (Railway / nginx) ───────────────────────────────────────────
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.ForwardLimit = 1;      // só o proxy imediatamente à frente
    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();
});

// ── Rate limiting global por IP ───────────────────────────────────────────────
var perIpLimit = int.TryParse(config["RATE_LIMIT_PER_MINUTE"], out var rl) ? rl : 120;
builder.Services.AddRateLimiter(options =>
{
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(ctx =>
        RateLimitPartition.GetFixedWindowLimiter(
            ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = perIpLimit,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
            }));
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
});

// ── GraphQL ───────────────────────────────────────────────────────────────────
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
    .AddMaxExecutionDepthRule(8)                  // consultas aninhadas abusivas
    .AllowIntrospection(isDev)                    // schema oculto em produção
    .ModifyRequestOptions(opt =>
    {
        opt.IncludeExceptionDetails = isDev;
        opt.ExecutionTimeout = TimeSpan.FromSeconds(isDev ? 300 : 120);
        opt.Complexity.Enable = true;             // custo máximo por operação
        opt.Complexity.MaximumAllowed = 1500;
    });

builder.Services.AddHealthChecks().AddDbContextCheck<ProjectVitalDbContext>("mysql");

var app = builder.Build();

// ── Pipeline ──────────────────────────────────────────────────────────────────
app.UseForwardedHeaders();
app.UseMiddleware<CorrelationIdMiddleware>();
app.UseMiddleware<SecurityHeadersMiddleware>();
app.UseCors("Frontend");
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

app.MapGraphQL("/graphql").WithOptions(new GraphQLServerOptions
{
    EnableGetRequests = false,                    // só POST (evita CSRF por GET/links)
    EnableSchemaRequests = isDev,
    EnableMultipartRequests = false,
    Tool = { Enable = isDev },                    // Banana Cake Pop só em desenvolvimento
});
app.MapHealthChecks("/health");

// ── Migrations + dados de demonstração ────────────────────────────────────────
var runMigrations = isDev || string.Equals(config["RUN_MIGRATIONS"], "true", StringComparison.OrdinalIgnoreCase);
var seedDemoData = isDev || string.Equals(config["SEED_DEMO_DATA"], "true", StringComparison.OrdinalIgnoreCase);

if (!isTest && (runMigrations || seedDemoData))
{
    using var scope = app.Services.CreateScope();   // sem HttpContext → escopo de sistema
    var db = scope.ServiceProvider.GetRequiredService<ProjectVitalDbContext>();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

    // O MySQL pode aceitar conexões alguns segundos depois de ficar "healthy".
    for (var attempt = 1; !await db.Database.CanConnectAsync(); attempt++)
    {
        if (attempt == 30) throw new InvalidOperationException("Banco de dados indisponível após 60 s.");
        logger.LogInformation("Aguardando o banco de dados ({Attempt}/30)...", attempt);
        await Task.Delay(TimeSpan.FromSeconds(2));
    }
    await db.Database.MigrateAsync();
    logger.LogInformation("Migrations aplicadas com sucesso.");

    if (seedDemoData)
    {
        var password = config["DEMO_MANAGER_PASSWORD"] ?? (isDev ? "Senha@123" : null);
        if (string.IsNullOrWhiteSpace(password) || (!isDev && password.Length < 12))
            throw new InvalidOperationException(
                "SEED_DEMO_DATA=true exige DEMO_MANAGER_PASSWORD com pelo menos 12 caracteres.");
        await new DemoDataSeeder(db, scope.ServiceProvider.GetRequiredService<IAuthService>(), logger)
            .SeedAsync(password);
    }
}

app.Run();

// Acessível para WebApplicationFactory nos testes de integração
public partial class Program { }
