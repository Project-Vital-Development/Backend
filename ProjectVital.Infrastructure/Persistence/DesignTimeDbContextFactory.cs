using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace ProjectVital.Infrastructure.Persistence;

/// <summary>
/// Factory usada APENAS pelo EF Core CLI em design-time (ex: ao gerar migrations).
/// Em runtime, o DbContext é criado via DI no Program.cs do projeto API.
///
/// Permite executar:
///   dotnet ef migrations add &lt;Nome&gt; --project ProjectVital.Infrastructure
/// sem precisar do projeto API montado.
///
/// Lê a connection string da variável de ambiente DB_CONNECTION_STRING ou
/// usa um fallback de desenvolvimento local.
/// </summary>
public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<ProjectVitalDbContext>
{
    public ProjectVitalDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("DB_CONNECTION_STRING")
            ?? "Server=localhost;Port=3306;Database=projectvital;Uid=root;Pwd=root;";

        var optionsBuilder = new DbContextOptionsBuilder<ProjectVitalDbContext>();

        // ServerVersion fixa para design-time — evita conexão real ao banco no momento do scaffold
        var serverVersion = ServerVersion.Create(new Version(8, 0, 0), Pomelo.EntityFrameworkCore.MySql.Infrastructure.ServerType.MySql);

        optionsBuilder.UseMySql(connectionString, serverVersion);

        return new ProjectVitalDbContext(optionsBuilder.Options, ProjectVital.Application.Interfaces.SystemTenantContext.Instance);
    }
}
