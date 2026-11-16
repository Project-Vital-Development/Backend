using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ProjectVital.Infrastructure.Persistence;
using Xunit;

namespace ProjectVital.IntegrationTests.Helpers;

public class TestFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly string _dbName = $"projectvital_test_{Guid.NewGuid():N}";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Test");

        builder.ConfigureServices(services =>
        {
            // Remove existing DbContext registration
            var descriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(DbContextOptions<ProjectVitalDbContext>));
            if (descriptor != null) services.Remove(descriptor);

            // Docker MySQL expõe na porta 3307 do host (mapeado de 3306 interno)
            var connStr = $"Server=localhost;Port=3307;Database={_dbName};Uid=root;Pwd=rootpassword123;";
            services.AddDbContext<ProjectVitalDbContext>(options =>
            {
                options.UseMySql(connStr, ServerVersion.AutoDetect(connStr));
            });

            // Override ML_SERVICE_URL to WireMock
            Environment.SetEnvironmentVariable("ML_SERVICE_URL", "http://localhost:9999");
        });
    }

    public async Task InitializeAsync()
    {
        // Create database and apply migrations
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ProjectVitalDbContext>();
        await db.Database.EnsureCreatedAsync();
    }

    public new async Task DisposeAsync()
    {
        // Drop test database
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ProjectVitalDbContext>();
        await db.Database.EnsureDeletedAsync();
        await base.DisposeAsync();
    }
}
