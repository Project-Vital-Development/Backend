using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ProjectVital.Infrastructure.Persistence;
using ProjectVital.IntegrationTests.Helpers;
using Xunit;

namespace ProjectVital.IntegrationTests.CheckIns;

public class CheckInIntegrationTests : IClassFixture<TestFactory>
{
    private readonly TestFactory _factory;
    private readonly HttpClient _client;

    public CheckInIntegrationTests(TestFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    private async Task<(int academyId, int studentId, string token)> SeedStudentAsync(string email = "ci@test.com")
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ProjectVitalDbContext>();

        var manager = new ProjectVital.Domain.Entities.User
        {
            Name = "Manager CI", Email = email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("Senha@123"),
            Role = ProjectVital.Domain.Enums.UserRole.Manager,
            CreatedAt = DateTime.UtcNow
        };
        db.Users.Add(manager);
        await db.SaveChangesAsync();

        var academy = new ProjectVital.Domain.Entities.Academy
        {
            Name = "Academia CI", Address = "Rua CI, 1",
            ManagerId = manager.Id, CreatedAt = DateTime.UtcNow
        };
        db.Academies.Add(academy);
        await db.SaveChangesAsync();

        var plan = new ProjectVital.Domain.Entities.Plan
        {
            AcademyId = academy.Id, Name = "Mensal", Price = 89.90m, DurationDays = 30
        };
        db.Plans.Add(plan);
        await db.SaveChangesAsync();

        var studentUser = new ProjectVital.Domain.Entities.User
        {
            Name = "Aluno CI", Email = $"aluno.{email}",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("Senha@123"),
            Role = ProjectVital.Domain.Enums.UserRole.Student,
            CreatedAt = DateTime.UtcNow
        };
        db.Users.Add(studentUser);
        await db.SaveChangesAsync();

        var student = new ProjectVital.Domain.Entities.Student
        {
            UserId = studentUser.Id, AcademyId = academy.Id, PlanId = plan.Id,
            EnrollmentDate = DateTime.UtcNow, Active = true
        };
        db.Students.Add(student);
        await db.SaveChangesAsync();

        var token = await GqlHelper.LoginAsync(_client, email, "Senha@123");
        return (academy.Id, student.Id, token!);
    }

    [Fact]
    public async Task CheckIn_ActiveStudent_PersistsWithTimestamp()
    {
        var (_, studentId, token) = await SeedStudentAsync("ci1@test.com");
        var beforeCall = DateTime.UtcNow.AddSeconds(-5);

        var mutation = $$"""mutation { checkIn(studentId: "{{studentId}}") { id timestamp } }""";
        var result = await GqlHelper.ExecuteQueryAsync(_client, mutation, token: token);

        result.RootElement.TryGetProperty("errors", out _).Should().BeFalse();
        var checkIn = result.RootElement.GetProperty("data").GetProperty("checkIn");
        var id = int.Parse(checkIn.GetProperty("id").GetString()!);
        id.Should().BeGreaterThan(0);

        var timestamp = DateTime.Parse(checkIn.GetProperty("timestamp").GetString()!);
        timestamp.Should().BeAfter(beforeCall).And.BeBefore(DateTime.UtcNow.AddSeconds(5));

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ProjectVitalDbContext>();
        (await db.CheckIns.AnyAsync(c => c.Id == id)).Should().BeTrue();
    }

    [Fact]
    public async Task CheckIn_InactiveStudent_ReturnsError()
    {
        var (_, studentId, token) = await SeedStudentAsync("ci2@test.com");

        // Deactivate the student
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ProjectVitalDbContext>();
        var student = await db.Students.FindAsync(studentId);
        student!.Active = false;
        await db.SaveChangesAsync();

        var mutation = $$"""mutation { checkIn(studentId: "{{studentId}}") { id } }""";
        var result = await GqlHelper.ExecuteQueryAsync(_client, mutation, token: token);

        result.RootElement.TryGetProperty("errors", out var errors).Should().BeTrue();
        errors.GetArrayLength().Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task CheckIn_UnknownStudent_ReturnsNotFound()
    {
        var (_, _, token) = await SeedStudentAsync("ci3@test.com");

        var mutation = """mutation { checkIn(studentId: "99999") { id } }""";
        var result = await GqlHelper.ExecuteQueryAsync(_client, mutation, token: token);

        result.RootElement.TryGetProperty("errors", out var errors).Should().BeTrue();
        errors.GetArrayLength().Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task GetCheckIns_FilterByDateRange_ReturnsCorrectSubset()
    {
        var (_, studentId, token) = await SeedStudentAsync("ci4@test.com");

        // Insert check-ins with known timestamps
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ProjectVitalDbContext>();
        var oldDate = DateTime.UtcNow.AddDays(-10);
        var recentDate = DateTime.UtcNow.AddHours(-2);

        db.CheckIns.Add(new ProjectVital.Domain.Entities.CheckIn
        {
            StudentId = studentId, Timestamp = oldDate, DurationMinutes = 60
        });
        db.CheckIns.Add(new ProjectVital.Domain.Entities.CheckIn
        {
            StudentId = studentId, Timestamp = recentDate, DurationMinutes = 45
        });
        await db.SaveChangesAsync();

        var from = DateTime.UtcNow.AddDays(-1).ToString("o");
        var to = DateTime.UtcNow.AddHours(1).ToString("o");

        var query = $$"""
            query {
                checkIns(studentId: "{{studentId}}", from: "{{from}}", to: "{{to}}") {
                    id timestamp
                }
            }
            """;
        var result = await GqlHelper.ExecuteQueryAsync(_client, query, token: token);

        result.RootElement.TryGetProperty("errors", out _).Should().BeFalse();
        var checkIns = result.RootElement.GetProperty("data").GetProperty("checkIns");
        checkIns.GetArrayLength().Should().Be(1);
    }

    [Fact]
    public async Task GetCheckIns_TenantIsolation_OtherAcademy_ReturnsForbidden()
    {
        var (_, studentIdA, _) = await SeedStudentAsync("tenant.ci.a@test.com");
        var (_, _, tokenB) = await SeedStudentAsync("tenant.ci.b@test.com");

        var query = $$"""
            query { checkIns(studentId: "{{studentIdA}}") { id } }
            """;
        var result = await GqlHelper.ExecuteQueryAsync(_client, query, token: tokenB);

        result.RootElement.TryGetProperty("errors", out var errors).Should().BeTrue();
        var code = errors[0].GetProperty("extensions").GetProperty("code").GetString();
        code.Should().BeOneOf("FORBIDDEN", "AUTH_ERROR", "UNAUTHORIZED", "NOT_FOUND");
    }
}
