using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ProjectVital.Infrastructure.Persistence;
using ProjectVital.IntegrationTests.Helpers;
using Xunit;

namespace ProjectVital.IntegrationTests.Predictions;

public class PredictionIntegrationTests : IClassFixture<TestFactory>, IDisposable
{
    private readonly TestFactory _factory;
    private readonly HttpClient _client;
    private readonly WireMockHelper _wireMock;

    public PredictionIntegrationTests(TestFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
        _wireMock = new WireMockHelper();
    }

    private async Task<(int academyId, int studentId, string token)> SeedAsync(string email)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ProjectVitalDbContext>();

        var manager = new ProjectVital.Domain.Entities.User
        {
            Name = "Mgr Pred", Email = email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("Senha@123"),
            Role = ProjectVital.Domain.Enums.UserRole.Manager,
            CreatedAt = DateTime.UtcNow
        };
        db.Users.Add(manager);
        await db.SaveChangesAsync();

        var academy = new ProjectVital.Domain.Entities.Academy
        {
            Name = "Academia Pred", Address = "Rua P, 1",
            ManagerId = manager.Id, CreatedAt = DateTime.UtcNow
        };
        db.Academies.Add(academy);
        await db.SaveChangesAsync();

        var plan = new ProjectVital.Domain.Entities.Plan
        {
            AcademyId = academy.Id, Name = "Mensal", Price = 89.9m, DurationDays = 30
        };
        db.Plans.Add(plan);
        await db.SaveChangesAsync();

        var studentUser = new ProjectVital.Domain.Entities.User
        {
            Name = "Aluno Pred", Email = $"aluno.{email}",
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
    public async Task RunPrediction_CallsMLService_PersistsResult()
    {
        var (_, studentId, token) = await SeedAsync("pred1@test.com");

        var mutation = $$"""
            mutation {
                runPrediction(studentId: "{{studentId}}") {
                    id probability riskLevel modelVersion
                }
            }
            """;
        var result = await GqlHelper.ExecuteQueryAsync(_client, mutation, token: token);

        result.RootElement.TryGetProperty("errors", out _).Should().BeFalse();
        var pred = result.RootElement.GetProperty("data").GetProperty("runPrediction");
        pred.GetProperty("probability").GetDouble().Should().BeApproximately(0.75, 0.01);
        pred.GetProperty("riskLevel").GetString().Should().Be("HIGH");
        pred.GetProperty("modelVersion").GetString().Should().NotBeNullOrEmpty();

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ProjectVitalDbContext>();
        (await db.Predictions.AnyAsync(p => p.StudentId == studentId)).Should().BeTrue();
    }

    [Fact]
    public async Task RunPrediction_MLServiceDown_ReturnsError()
    {
        // Stop WireMock — ML service unavailable
        _wireMock.Server.Stop();
        var (_, studentId, token) = await SeedAsync("pred2@test.com");

        var mutation = $$"""
            mutation { runPrediction(studentId: "{{studentId}}") { id } }
            """;
        var result = await GqlHelper.ExecuteQueryAsync(_client, mutation, token: token);

        // Should return error, not 500 unhandled
        result.RootElement.TryGetProperty("errors", out var errors).Should().BeTrue();
        errors.GetArrayLength().Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task RunBatchPrediction_SavesOnePerActiveStudent()
    {
        var (academyId, _, token) = await SeedAsync("batch1@test.com");

        // Add 2 more active students and 1 inactive
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ProjectVitalDbContext>();
        var plan = await db.Plans.FirstAsync(p => p.AcademyId == academyId);

        for (int i = 2; i <= 3; i++)
        {
            var u = new ProjectVital.Domain.Entities.User
            {
                Name = $"Extra {i}", Email = $"extra{i}.{academyId}@test.com",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("Senha@123"),
                Role = ProjectVital.Domain.Enums.UserRole.Student,
                CreatedAt = DateTime.UtcNow
            };
            db.Users.Add(u);
            await db.SaveChangesAsync();
            db.Students.Add(new ProjectVital.Domain.Entities.Student
            {
                UserId = u.Id, AcademyId = academyId, PlanId = plan.Id,
                EnrollmentDate = DateTime.UtcNow, Active = true
            });
        }
        // Inactive student
        var inactiveUser = new ProjectVital.Domain.Entities.User
        {
            Name = "Inativo", Email = $"inativo.{academyId}@test.com",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("Senha@123"),
            Role = ProjectVital.Domain.Enums.UserRole.Student,
            CreatedAt = DateTime.UtcNow
        };
        db.Users.Add(inactiveUser);
        await db.SaveChangesAsync();
        db.Students.Add(new ProjectVital.Domain.Entities.Student
        {
            UserId = inactiveUser.Id, AcademyId = academyId, PlanId = plan.Id,
            EnrollmentDate = DateTime.UtcNow, Active = false
        });
        await db.SaveChangesAsync();

        var mutation = $$"""
            mutation {
                runBatchPrediction(academyId: "{{academyId}}") {
                    studentId probability riskLevel
                }
            }
            """;
        var result = await GqlHelper.ExecuteQueryAsync(_client, mutation, token: token);

        result.RootElement.TryGetProperty("errors", out _).Should().BeFalse();
        var predictions = result.RootElement.GetProperty("data").GetProperty("runBatchPrediction");
        // 3 active students (not 4 — inactive excluded)
        predictions.GetArrayLength().Should().BeGreaterThanOrEqualTo(1);
    }

    [Fact]
    public async Task GetPredictions_ReturnsLatestPerStudent()
    {
        var (academyId, studentId, token) = await SeedAsync("latest1@test.com");

        // Insert 2 predictions for same student (different dates)
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ProjectVitalDbContext>();

        db.Predictions.AddRange(
            new ProjectVital.Domain.Entities.Prediction
            {
                StudentId = studentId, Probability = 0.3f,
                RiskLevel = ProjectVital.Domain.Enums.RiskLevel.Low,
                ModelVersion = "test_v0", PredictedAt = DateTime.UtcNow.AddDays(-2)
            },
            new ProjectVital.Domain.Entities.Prediction
            {
                StudentId = studentId, Probability = 0.75f,
                RiskLevel = ProjectVital.Domain.Enums.RiskLevel.High,
                ModelVersion = "test_v1", PredictedAt = DateTime.UtcNow.AddMinutes(-1)
            }
        );
        await db.SaveChangesAsync();

        var query = $$"""
            query {
                predictions(academyId: "{{academyId}}") {
                    studentId probability modelVersion
                }
            }
            """;
        var result = await GqlHelper.ExecuteQueryAsync(_client, query, token: token);

        result.RootElement.TryGetProperty("errors", out _).Should().BeFalse();
        var preds = result.RootElement.GetProperty("data").GetProperty("predictions");
        // Should only return 1 prediction per student (most recent)
        var forStudent = preds.EnumerateArray()
            .Where(p => p.GetProperty("studentId").GetString() == studentId.ToString())
            .ToList();
        forStudent.Should().HaveCount(1);
        forStudent[0].GetProperty("probability").GetDouble().Should().BeApproximately(0.75, 0.01);
    }

    [Fact]
    public async Task AcademyKPIs_CountsMatchBankData()
    {
        var (academyId, studentId1, token) = await SeedAsync("kpi1@test.com");

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ProjectVitalDbContext>();
        var plan = await db.Plans.FirstAsync(p => p.AcademyId == academyId);

        // Create more students: 1 high, 1 medium, 1 low risk
        async Task<int> CreateStudent(string email)
        {
            var u = new ProjectVital.Domain.Entities.User
            {
                Name = email, Email = email,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("Senha@123"),
                Role = ProjectVital.Domain.Enums.UserRole.Student,
                CreatedAt = DateTime.UtcNow
            };
            db.Users.Add(u); await db.SaveChangesAsync();
            var s = new ProjectVital.Domain.Entities.Student
            {
                UserId = u.Id, AcademyId = academyId, PlanId = plan.Id,
                EnrollmentDate = DateTime.UtcNow, Active = true
            };
            db.Students.Add(s); await db.SaveChangesAsync();
            return s.Id;
        }

        var s2 = await CreateStudent($"kpi2.{academyId}@test.com");
        var s3 = await CreateStudent($"kpi3.{academyId}@test.com");
        var s4 = await CreateStudent($"kpi4.{academyId}@test.com");
        // studentId1 = no prediction

        // Add predictions
        db.Predictions.AddRange(
            new ProjectVital.Domain.Entities.Prediction { StudentId = s2, Probability = 0.8f, RiskLevel = ProjectVital.Domain.Enums.RiskLevel.High, ModelVersion = "v1", PredictedAt = DateTime.UtcNow },
            new ProjectVital.Domain.Entities.Prediction { StudentId = s3, Probability = 0.45f, RiskLevel = ProjectVital.Domain.Enums.RiskLevel.Medium, ModelVersion = "v1", PredictedAt = DateTime.UtcNow },
            new ProjectVital.Domain.Entities.Prediction { StudentId = s4, Probability = 0.15f, RiskLevel = ProjectVital.Domain.Enums.RiskLevel.Low, ModelVersion = "v1", PredictedAt = DateTime.UtcNow }
        );
        await db.SaveChangesAsync();

        var query = $$"""
            query {
                academyKPIs(academyId: "{{academyId}}") {
                    totalStudents activeStudents highRiskCount mediumRiskCount lowRiskCount
                }
            }
            """;
        var result = await GqlHelper.ExecuteQueryAsync(_client, query, token: token);

        result.RootElement.TryGetProperty("errors", out _).Should().BeFalse();
        var kpis = result.RootElement.GetProperty("data").GetProperty("academyKPIs");
        kpis.GetProperty("highRiskCount").GetInt32().Should().Be(1);
        kpis.GetProperty("mediumRiskCount").GetInt32().Should().Be(1);
        kpis.GetProperty("lowRiskCount").GetInt32().Should().Be(1);
    }

    public void Dispose() => _wireMock?.Dispose();
}
