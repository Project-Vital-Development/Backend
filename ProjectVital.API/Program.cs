using Microsoft.EntityFrameworkCore;
using ProjectVital.Domain.Interfaces;
using ProjectVital.Infrastructure.Persistence;
using ProjectVital.Infrastructure.Repositories;

// ─────────────────────────────────────────────────────────────────────────────
// FASE 1 — API mínimo apenas o suficiente para suportar EF Core migrations
//          e expor um health check. JWT, GraphQL, CORS e resolvers virão na FASE 2.
// ─────────────────────────────────────────────────────────────────────────────

var builder = WebApplication.CreateBuilder(args);

// ── Configuração da connection string ───────────────────────────────────────
// Lê a env var DB_CONNECTION_STRING (definida via .env / docker-compose).
// Em dev local sem Docker, fallback para o appsettings.Development.json.
var connectionString = builder.Configuration["DB_CONNECTION_STRING"]
    ?? Environment.GetEnvironmentVariable("DB_CONNECTION_STRING")
    ?? builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException(
        "Connection string ausente. Defina DB_CONNECTION_STRING no .env ou em appsettings.Development.json.");

// ── EF Core + MySQL (Pomelo) ────────────────────────────────────────────────
// ServerVersion fixa em MySQL 8.0 (mesma do docker-compose) — evita conexão
// ao banco em design-time (ex: ao gerar migrations).
var mysqlVersion = ServerVersion.Create(
    new Version(8, 0, 0),
    Pomelo.EntityFrameworkCore.MySql.Infrastructure.ServerType.MySql);

builder.Services.AddDbContext<ProjectVitalDbContext>(options =>
    options.UseMySql(
        connectionString,
        mysqlVersion,
        mySqlOptions => mySqlOptions
            .EnableRetryOnFailure(maxRetryCount: 3, maxRetryDelay: TimeSpan.FromSeconds(5), errorNumbersToAdd: null)
            .MigrationsAssembly("ProjectVital.Infrastructure")
    ));

// ── Repositórios (DI) ───────────────────────────────────────────────────────
builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<IAcademyRepository, AcademyRepository>();
builder.Services.AddScoped<IPlanRepository, PlanRepository>();
builder.Services.AddScoped<IStudentRepository, StudentRepository>();
builder.Services.AddScoped<ICheckInRepository, CheckInRepository>();
builder.Services.AddScoped<IPredictionRepository, PredictionRepository>();

// ── Health Check ────────────────────────────────────────────────────────────
builder.Services.AddHealthChecks()
    .AddDbContextCheck<ProjectVitalDbContext>("database");

var app = builder.Build();

// ── Endpoints ───────────────────────────────────────────────────────────────
// Health check para o docker-compose monitorar o serviço.
app.MapHealthChecks("/health");

// Endpoint raiz informativo (será substituído pelo /graphql na FASE 2)
app.MapGet("/", () => Results.Ok(new
{
    service = "ProjectVital.API",
    phase = 1,
    status = "ok",
    nextPhase = "FASE 2 — GraphQL + JWT"
}));

app.Run();
