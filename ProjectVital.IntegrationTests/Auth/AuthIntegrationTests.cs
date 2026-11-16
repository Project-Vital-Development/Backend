using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ProjectVital.Infrastructure.Persistence;
using ProjectVital.IntegrationTests.Helpers;
using Xunit;

namespace ProjectVital.IntegrationTests.Auth;

public class AuthIntegrationTests : IClassFixture<TestFactory>
{
    private readonly TestFactory _factory;
    private readonly HttpClient _client;

    public AuthIntegrationTests(TestFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    private async Task SeedManagerAndAcademyAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ProjectVitalDbContext>();

        if (!await db.Users.AnyAsync(u => u.Email == "gestor@vital.dev"))
        {
            var manager = new ProjectVital.Domain.Entities.User
            {
                Name = "Gestor Teste",
                Email = "gestor@vital.dev",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("Senha@123"),
                Role = ProjectVital.Domain.Enums.UserRole.Manager,
                CreatedAt = DateTime.UtcNow
            };
            db.Users.Add(manager);
            await db.SaveChangesAsync();

            var academy = new ProjectVital.Domain.Entities.Academy
            {
                Name = "Academia Teste",
                Address = "Rua Teste, 123",
                ManagerId = manager.Id,
                CreatedAt = DateTime.UtcNow
            };
            db.Academies.Add(academy);
            await db.SaveChangesAsync();
        }
    }

    [Fact]
    public async Task Login_ValidCredentials_ReturnsJwtAndAcademyId()
    {
        await SeedManagerAndAcademyAsync();

        var mutation = """
            mutation {
                login(email: "gestor@vital.dev", password: "Senha@123") {
                    token
                    expiresAt
                    academyId
                    user { id name email role }
                }
            }
            """;

        var result = await GqlHelper.ExecuteQueryAsync(_client, mutation);

        result.RootElement.TryGetProperty("errors", out _).Should().BeFalse("não deve ter erros");
        var login = result.RootElement.GetProperty("data").GetProperty("login");
        login.GetProperty("token").GetString().Should().NotBeNullOrEmpty();
        login.GetProperty("academyId").GetString().Should().NotBeNullOrEmpty();
        var expiresAt = DateTime.Parse(login.GetProperty("expiresAt").GetString()!);
        expiresAt.Should().BeAfter(DateTime.UtcNow);
    }

    [Fact]
    public async Task Login_WrongPassword_ReturnsError()
    {
        await SeedManagerAndAcademyAsync();

        var mutation = """
            mutation {
                login(email: "gestor@vital.dev", password: "SenhaErrada!") {
                    token
                }
            }
            """;

        var result = await GqlHelper.ExecuteQueryAsync(_client, mutation);

        result.RootElement.TryGetProperty("errors", out var errors).Should().BeTrue();
        var code = errors[0].GetProperty("extensions").GetProperty("code").GetString();
        code.Should().BeOneOf("AUTH_FAILED", "INVALID_CREDENTIALS", "AUTH_ERROR", "BUSINESS_ERROR");
    }

    [Fact]
    public async Task Login_UnknownEmail_ReturnsError()
    {
        var mutation = """
            mutation {
                login(email: "naoexiste@exemplo.com", password: "Qualquer123!") {
                    token
                }
            }
            """;

        var result = await GqlHelper.ExecuteQueryAsync(_client, mutation);

        result.RootElement.TryGetProperty("errors", out var errors).Should().BeTrue();
        errors.GetArrayLength().Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task ProtectedQuery_WithoutToken_ReturnsAuthRequired()
    {
        var query = """
            query {
                students(academyId: "1") { id name }
            }
            """;

        // No token — remove any auth header
        _client.DefaultRequestHeaders.Authorization = null;
        var result = await GqlHelper.ExecuteQueryAsync(_client, query);

        result.RootElement.TryGetProperty("errors", out var errors).Should().BeTrue();
        var code = errors[0].GetProperty("extensions").GetProperty("code").GetString();
        code.Should().BeOneOf("AUTH_NOT_AUTHORIZED", "AUTH_REQUIRED", "UNAUTHENTICATED", "UNAUTHORIZED");
    }

    [Fact]
    public async Task ProtectedQuery_WithInvalidToken_ReturnsAuthRequired()
    {
        var query = """
            query {
                students(academyId: "1") { id name }
            }
            """;

        var result = await GqlHelper.ExecuteQueryAsync(_client, query, token: "invalid.jwt.token.here");

        result.RootElement.TryGetProperty("errors", out var errors).Should().BeTrue();
        var code = errors[0].GetProperty("extensions").GetProperty("code").GetString();
        code.Should().BeOneOf("AUTH_NOT_AUTHORIZED", "AUTH_REQUIRED", "UNAUTHENTICATED", "UNAUTHORIZED");
    }
}
