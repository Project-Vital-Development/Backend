using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ProjectVital.Infrastructure.Persistence;
using ProjectVital.IntegrationTests.Helpers;
using Xunit;

namespace ProjectVital.IntegrationTests.Students;

public class StudentIntegrationTests : IClassFixture<TestFactory>
{
    private readonly TestFactory _factory;
    private readonly HttpClient _client;

    public StudentIntegrationTests(TestFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    private async Task<(int academyId, int planId, string token)> SeedAcademyWithPlanAsync(
        string email = "manager@test.com")
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ProjectVitalDbContext>();

        var manager = new ProjectVital.Domain.Entities.User
        {
            Name = "Manager",
            Email = email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("Senha@123"),
            Role = ProjectVital.Domain.Enums.UserRole.Manager,
            CreatedAt = DateTime.UtcNow
        };
        db.Users.Add(manager);
        await db.SaveChangesAsync();

        var academy = new ProjectVital.Domain.Entities.Academy
        {
            Name = $"Academia {email}",
            Address = "Rua A, 1",
            ManagerId = manager.Id,
            CreatedAt = DateTime.UtcNow
        };
        db.Academies.Add(academy);
        await db.SaveChangesAsync();

        var plan = new ProjectVital.Domain.Entities.Plan
        {
            AcademyId = academy.Id,
            Name = "Mensal",
            Price = 89.90m,
            DurationDays = 30
        };
        db.Plans.Add(plan);
        await db.SaveChangesAsync();

        var token = await GqlHelper.LoginAsync(_client, email, "Senha@123");
        return (academy.Id, plan.Id, token!);
    }

    [Fact]
    public async Task RegisterStudent_ValidInput_PersistsAndReturnsId()
    {
        var (academyId, planId, token) = await SeedAcademyWithPlanAsync("reg1@test.com");

        var mutation = $$"""
            mutation {
                registerStudent(input: {
                    academyId: "{{academyId}}"
                    planId: "{{planId}}"
                    name: "Aluno Teste"
                    email: "aluno1@test.com"
                    password: "Senha@123"
                    enrollmentDate: "2025-01-01T00:00:00Z"
                }) {
                    id name email
                }
            }
            """;

        var result = await GqlHelper.ExecuteQueryAsync(_client, mutation, token: token);

        result.RootElement.TryGetProperty("errors", out _).Should().BeFalse();
        var student = result.RootElement.GetProperty("data").GetProperty("registerStudent");
        var id = int.Parse(student.GetProperty("id").GetString()!);
        id.Should().BeGreaterThan(0);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ProjectVitalDbContext>();
        var exists = await db.Students.AnyAsync(s => s.Id == id);
        exists.Should().BeTrue();
    }

    [Fact]
    public async Task RegisterStudent_DuplicateEmail_ReturnsError()
    {
        var (academyId, planId, token) = await SeedAcademyWithPlanAsync("reg2@test.com");

        var mutation = $$"""
            mutation {
                registerStudent(input: {
                    academyId: "{{academyId}}"
                    planId: "{{planId}}"
                    name: "Aluno A"
                    email: "duplicate@test.com"
                    password: "Senha@123"
                    enrollmentDate: "2025-01-01T00:00:00Z"
                }) { id }
            }
            """;

        await GqlHelper.ExecuteQueryAsync(_client, mutation, token: token);
        var result = await GqlHelper.ExecuteQueryAsync(_client, mutation, token: token);

        result.RootElement.TryGetProperty("errors", out var errors).Should().BeTrue();
        errors.GetArrayLength().Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task GetStudents_ReturnsTenantIsolation()
    {
        var (academyId1, planId1, token1) = await SeedAcademyWithPlanAsync("tenant1@test.com");
        var (academyId2, planId2, token2) = await SeedAcademyWithPlanAsync("tenant2@test.com");

        // Create student in academy 1
        var mut1 = $$"""
            mutation {
                registerStudent(input: {
                    academyId: "{{academyId1}}" planId: "{{planId1}}"
                    name: "Aluno Academia 1" email: "aluno.ac1@test.com"
                    password: "Senha@123" enrollmentDate: "2025-01-01T00:00:00Z"
                }) { id }
            }
            """;
        await GqlHelper.ExecuteQueryAsync(_client, mut1, token: token1);

        // Create student in academy 2
        var mut2 = $$"""
            mutation {
                registerStudent(input: {
                    academyId: "{{academyId2}}" planId: "{{planId2}}"
                    name: "Aluno Academia 2" email: "aluno.ac2@test.com"
                    password: "Senha@123" enrollmentDate: "2025-01-01T00:00:00Z"
                }) { id }
            }
            """;
        await GqlHelper.ExecuteQueryAsync(_client, mut2, token: token2);

        // Query with token1 — should only see academy1 students
        var query1 = $$"""
            query { students(academyId: "{{academyId1}}") { id name } }
            """;
        var result1 = await GqlHelper.ExecuteQueryAsync(_client, query1, token: token1);

        result1.RootElement.TryGetProperty("errors", out _).Should().BeFalse();
        var students1 = result1.RootElement.GetProperty("data").GetProperty("students");
        students1.EnumerateArray().Should().OnlyContain(s =>
            s.GetProperty("name").GetString()!.Contains("Academia 1"));
    }

    [Fact]
    public async Task GetStudents_ExcludesDeletedStudents()
    {
        var (academyId, planId, token) = await SeedAcademyWithPlanAsync("excl@test.com");

        // Create and delete a student
        var createMut = $$"""
            mutation {
                registerStudent(input: {
                    academyId: "{{academyId}}" planId: "{{planId}}"
                    name: "Aluno Deletado" email: "deletado@test.com"
                    password: "Senha@123" enrollmentDate: "2025-01-01T00:00:00Z"
                }) { id }
            }
            """;
        var created = await GqlHelper.ExecuteQueryAsync(_client, createMut, token: token);
        var studentId = created.RootElement.GetProperty("data").GetProperty("registerStudent")
            .GetProperty("id").GetString();

        var deleteMut = $$"""
            mutation { deleteStudent(id: "{{studentId}}") }
            """;
        await GqlHelper.ExecuteQueryAsync(_client, deleteMut, token: token);

        var query = $$"""
            query { students(academyId: "{{academyId}}") { id name } }
            """;
        var result = await GqlHelper.ExecuteQueryAsync(_client, query, token: token);

        var students = result.RootElement.GetProperty("data").GetProperty("students");
        students.EnumerateArray().Should().NotContain(s =>
            s.GetProperty("id").GetString() == studentId);
    }

    [Fact]
    public async Task UpdateStudent_OwnAcademy_Succeeds()
    {
        var (academyId, planId, token) = await SeedAcademyWithPlanAsync("upd@test.com");

        // Create a second plan
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ProjectVitalDbContext>();
        var plan2 = new ProjectVital.Domain.Entities.Plan
        {
            AcademyId = academyId, Name = "Anual", Price = 799m, DurationDays = 365
        };
        db.Plans.Add(plan2);
        await db.SaveChangesAsync();

        var createMut = $$"""
            mutation {
                registerStudent(input: {
                    academyId: "{{academyId}}" planId: "{{planId}}"
                    name: "Aluno Upd" email: "upd.aluno@test.com"
                    password: "Senha@123" enrollmentDate: "2025-01-01T00:00:00Z"
                }) { id }
            }
            """;
        var created = await GqlHelper.ExecuteQueryAsync(_client, createMut, token: token);
        var studentId = created.RootElement.GetProperty("data").GetProperty("registerStudent")
            .GetProperty("id").GetString();

        var updateMut = $$"""
            mutation {
                updateStudent(id: "{{studentId}}", input: {
                    planId: "{{plan2.Id}}"
                }) { id planName }
            }
            """;
        var result = await GqlHelper.ExecuteQueryAsync(_client, updateMut, token: token);

        result.RootElement.TryGetProperty("errors", out _).Should().BeFalse();
    }

    [Fact]
    public async Task UpdateStudent_OtherAcademy_ReturnsForbidden()
    {
        var (_, planId1, token1) = await SeedAcademyWithPlanAsync("other1@test.com");
        var (academyId2, planId2, token2) = await SeedAcademyWithPlanAsync("other2@test.com");

        var createMut = $$"""
            mutation {
                registerStudent(input: {
                    academyId: "{{academyId2}}" planId: "{{planId2}}"
                    name: "Aluno Other2" email: "other2.aluno@test.com"
                    password: "Senha@123" enrollmentDate: "2025-01-01T00:00:00Z"
                }) { id }
            }
            """;
        var created = await GqlHelper.ExecuteQueryAsync(_client, createMut, token: token2);
        var studentId = created.RootElement.GetProperty("data").GetProperty("registerStudent")
            .GetProperty("id").GetString();

        var updateMut = $$"""
            mutation {
                updateStudent(id: "{{studentId}}", input: {
                    planId: "{{planId1}}"
                }) { id }
            }
            """;
        var result = await GqlHelper.ExecuteQueryAsync(_client, updateMut, token: token1);

        result.RootElement.TryGetProperty("errors", out var errors).Should().BeTrue();
        var code = errors[0].GetProperty("extensions").GetProperty("code").GetString();
        code.Should().BeOneOf("FORBIDDEN", "AUTH_ERROR", "UNAUTHORIZED");
    }

    [Fact]
    public async Task DeleteStudent_SetsSoftDeletedAt()
    {
        var (academyId, planId, token) = await SeedAcademyWithPlanAsync("del2@test.com");

        var createMut = $$"""
            mutation {
                registerStudent(input: {
                    academyId: "{{academyId}}" planId: "{{planId}}"
                    name: "Aluno Del2" email: "del2.aluno@test.com"
                    password: "Senha@123" enrollmentDate: "2025-01-01T00:00:00Z"
                }) { id }
            }
            """;
        var created = await GqlHelper.ExecuteQueryAsync(_client, createMut, token: token);
        var studentId = int.Parse(
            created.RootElement.GetProperty("data").GetProperty("registerStudent")
                .GetProperty("id").GetString()!);

        var deleteMut = $$"""
            mutation { deleteStudent(id: "{{studentId}}") }
            """;
        var deleteResult = await GqlHelper.ExecuteQueryAsync(_client, deleteMut, token: token);
        deleteResult.RootElement.TryGetProperty("errors", out _).Should().BeFalse();

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ProjectVitalDbContext>();
        var student = await db.Students.IgnoreQueryFilters()
            .FirstOrDefaultAsync(s => s.Id == studentId);
        student.Should().NotBeNull();
        student!.DeletedAt.Should().NotBeNull();
    }
}
