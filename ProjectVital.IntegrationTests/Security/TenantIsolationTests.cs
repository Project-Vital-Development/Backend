using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using ProjectVital.Domain.Entities;
using ProjectVital.Domain.Enums;
using ProjectVital.Infrastructure.Persistence;
using ProjectVital.IntegrationTests.Helpers;
using Xunit;

namespace ProjectVital.IntegrationTests.Security;

/// <summary>
/// Demonstração do isolamento entre academias (multi-tenant) e da autenticação.
///
/// Cenário: academias A e B, cada uma com gestor, plano, aluno ativo, check-in e predição.
/// O gestor de A tenta LER, ALTERAR, EXCLUIR e PREVER dados de B manipulando os IDs
/// enviados nas operações GraphQL. Toda tentativa deve falhar (FORBIDDEN ou NOT_FOUND)
/// e os dados de B devem permanecer intactos.
/// </summary>
public class TenantIsolationTests : IClassFixture<TestFactory>
{
    private readonly TestFactory _factory;
    private readonly HttpClient _client;

    public TenantIsolationTests(TestFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    private sealed record Tenant(int AcademyId, int PlanId, int StudentId, string ManagerEmail, string StudentEmail, string Token);

    private async Task<Tenant> SeedTenantAsync(string tag)
    {
        using var scope = _factory.Services.CreateScope();   // sem HttpContext → escopo de sistema
        var db = scope.ServiceProvider.GetRequiredService<ProjectVitalDbContext>();
        var hash = BCrypt.Net.BCrypt.HashPassword("Senha@123", 4);

        var manager = new User { Name = $"Gestor {tag}", Email = $"gestor.{tag}@iso.test", PasswordHash = hash, Role = UserRole.Manager, CreatedAt = DateTime.UtcNow };
        db.Users.Add(manager);
        await db.SaveChangesAsync();
        var academy = new Academy { Name = $"Academia {tag}", Address = "Rua X", ManagerId = manager.Id, CreatedAt = DateTime.UtcNow };
        db.Academies.Add(academy);
        await db.SaveChangesAsync();
        var plan = new Plan { AcademyId = academy.Id, Name = "Mensal", Price = 100m, DurationDays = 30 };
        db.Plans.Add(plan);
        await db.SaveChangesAsync();
        var student = new Student
        {
            User = new User { Name = $"Aluno {tag}", Email = $"aluno.{tag}@iso.test", PasswordHash = hash, Role = UserRole.Student, CreatedAt = DateTime.UtcNow },
            AcademyId = academy.Id, PlanId = plan.Id, EnrollmentDate = DateTime.UtcNow.AddDays(-60), Active = true,
        };
        student.CheckIns.Add(new CheckIn { Timestamp = DateTime.UtcNow.AddDays(-1), DurationMinutes = 60 });
        student.Predictions.Add(new Prediction { Probability = 0.42f, RiskLevel = RiskLevel.Medium, ModelVersion = "xgb_v1", PredictedAt = DateTime.UtcNow });
        db.Students.Add(student);
        await db.SaveChangesAsync();

        var token = await GqlHelper.LoginAsync(_client, manager.Email, "Senha@123");
        return new Tenant(academy.Id, plan.Id, student.Id, manager.Email, student.User.Email, token!);
    }

    private async Task<(Tenant a, Tenant b)> SeedPairAsync()
    {
        var id = Guid.NewGuid().ToString("N")[..8];
        return (await SeedTenantAsync($"a{id}"), await SeedTenantAsync($"b{id}"));
    }

    private Task<JsonDocument> Gql(string query, string? token) => GqlHelper.ExecuteQueryAsync(_client, query, token: token);

    private static string? ErrorCode(JsonDocument doc) =>
        doc.RootElement.TryGetProperty("errors", out var e) && e.GetArrayLength() > 0
        && e[0].TryGetProperty("extensions", out var x) && x.TryGetProperty("code", out var c) ? c.GetString() : null;

    // ── Leitura de dados de outra academia ───────────────────────────────────

    [Theory]
    [InlineData("query { students(academyId: \"{B}\") { id } }")]
    [InlineData("query { predictions(academyId: \"{B}\") { id } }")]
    [InlineData("query { plans(academyId: \"{B}\") { id } }")]
    [InlineData("query { academyKPIs(academyId: \"{B}\") { totalStudents } }")]
    [InlineData("query { modelReport(academyId: \"{B}\") { modelVersion } }")]
    [InlineData("query { checkIns(studentId: \"{SB}\") { id } }")]
    [InlineData("query { prediction(studentId: \"{SB}\") { id } }")]
    public async Task Manager_cannot_read_other_academy(string template)
    {
        var (a, b) = await SeedPairAsync();
        var doc = await Gql(template.Replace("{B}", b.AcademyId.ToString()).Replace("{SB}", b.StudentId.ToString()), a.Token);
        ErrorCode(doc).Should().BeOneOf("FORBIDDEN", "NOT_FOUND");
        doc.RootElement.GetRawText().Should().NotContain(b.StudentEmail);
    }

    [Fact]
    public async Task Student_of_other_academy_is_invisible()
    {
        var (a, b) = await SeedPairAsync();
        var doc = await Gql($"query {{ student(id: \"{b.StudentId}\") {{ id email }} }}", a.Token);
        doc.RootElement.GetProperty("data").GetProperty("student").ValueKind.Should().Be(JsonValueKind.Null);

        var own = await Gql($"query {{ students(academyId: \"{a.AcademyId}\") {{ id email academyId }} }}", a.Token);
        var list = own.RootElement.GetProperty("data").GetProperty("students").EnumerateArray().ToList();
        list.Should().OnlyContain(s => s.GetProperty("academyId").GetString() == a.AcademyId.ToString());
        own.RootElement.GetRawText().Should().NotContain(b.StudentEmail);
    }

    // ── Escrita / exclusão / predição em outra academia ──────────────────────

    [Theory]
    [InlineData("mutation { updateStudent(id: \"{SB}\", input: { name: \"Invadido\" }) { id } }")]
    [InlineData("mutation { deleteStudent(id: \"{SB}\") }")]
    [InlineData("mutation { checkIn(studentId: \"{SB}\") { id } }")]
    [InlineData("mutation { runPrediction(studentId: \"{SB}\") { id } }")]
    [InlineData("mutation { runBatchPrediction(academyId: \"{B}\") { id } }")]
    [InlineData("mutation { retrainModel(academyId: \"{B}\") { success } }")]
    [InlineData("mutation { registerStudent(input: { academyId: {B}, planId: {PB}, name: \"Intruso\", email: \"intruso@iso.test\", password: \"Senha@123\" }) { id } }")]
    public async Task Manager_cannot_modify_other_academy(string template)
    {
        var (a, b) = await SeedPairAsync();
        var query = template.Replace("{B}", b.AcademyId.ToString()).Replace("{SB}", b.StudentId.ToString())
            .Replace("{PB}", b.PlanId.ToString());
        var doc = await Gql(query, a.Token);
        ErrorCode(doc).Should().BeOneOf("FORBIDDEN", "NOT_FOUND");

        // Os dados de B continuam intactos
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ProjectVitalDbContext>();
        var student = await db.Students.IgnoreQueryFilters().Include(s => s.User).SingleAsync(s => s.Id == b.StudentId);
        student.DeletedAt.Should().BeNull();
        student.User.Name.Should().NotBe("Invadido");
        (await db.CheckIns.IgnoreQueryFilters().CountAsync(c => c.StudentId == b.StudentId)).Should().Be(1);
        (await db.Predictions.IgnoreQueryFilters().CountAsync(p => p.StudentId == b.StudentId)).Should().Be(1);
        (await db.Students.IgnoreQueryFilters().AnyAsync(s => s.User.Email == "intruso@iso.test")).Should().BeFalse();
    }

    [Fact]
    public async Task Plan_of_other_academy_cannot_be_assigned()
    {
        var (a, b) = await SeedPairAsync();
        var register = await Gql($$"""
            mutation { registerStudent(input: { academyId: {{a.AcademyId}}, planId: {{b.PlanId}},
              name: "Plano Alheio", email: "plano.alheio.{{a.AcademyId}}@iso.test", password: "Senha@123" }) { id } }
            """, a.Token);
        ErrorCode(register).Should().Be("BUSINESS_RULE_VIOLATION");

        var update = await Gql($"mutation {{ updateStudent(id: \"{a.StudentId}\", input: {{ planId: {b.PlanId} }}) {{ id }} }}", a.Token);
        ErrorCode(update).Should().Be("BUSINESS_RULE_VIOLATION");
    }

    // ── Autenticação ─────────────────────────────────────────────────────────

    [Fact]
    public async Task Student_account_cannot_log_in()
    {
        var (_, b) = await SeedPairAsync();
        var doc = await Gql($"mutation {{ login(email: \"{b.StudentEmail}\", password: \"Senha@123\") {{ token }} }}", null);
        ErrorCode(doc).Should().Be("AUTH_FAILED");
    }

    [Theory]
    [InlineData("query { students(academyId: \"1\") { id } }")]
    [InlineData("query { academyKPIs(academyId: \"1\") { totalStudents } }")]
    [InlineData("mutation { retrainModel(academyId: \"1\") { success } }")]
    public async Task Anonymous_requests_are_rejected(string query)
    {
        var doc = await Gql(query, null);
        ErrorCode(doc).Should().Be("AUTH_NOT_AUTHORIZED");
    }

    private string ForgeToken(int academyId, string? secret = null, string? issuer = null, string? audience = null,
        string role = "Manager", DateTime? expires = null)
    {
        var config = _factory.Services.GetRequiredService<IConfiguration>();
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret ?? config["JWT_SECRET"]!));
        var token = new JwtSecurityToken(
            issuer: issuer ?? "project-vital-api", audience: audience ?? "project-vital-web",
            claims: [new Claim("sub", "1"), new Claim(ClaimTypes.Role, role), new Claim("academyId", academyId.ToString())],
            notBefore: (expires ?? DateTime.UtcNow.AddHours(1)).AddHours(-2), expires: expires ?? DateTime.UtcNow.AddHours(1),
            signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256));
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    [Fact]
    public async Task Forged_or_invalid_tokens_are_rejected()
    {
        var (_, b) = await SeedPairAsync();
        var query = $"query {{ students(academyId: \"{b.AcademyId}\") {{ id }} }}";

        var wrongKey = ForgeToken(b.AcademyId, secret: "outra-chave-qualquer-com-mais-de-32-caracteres!!");
        var wrongIssuer = ForgeToken(b.AcademyId, issuer: "atacante");
        var wrongAudience = ForgeToken(b.AcademyId, audience: "outro-sistema");
        var expired = ForgeToken(b.AcademyId, expires: DateTime.UtcNow.AddMinutes(-5));
        var studentRole = ForgeToken(b.AcademyId, role: "Student");
        var header = Convert.ToBase64String(Encoding.UTF8.GetBytes("""{"alg":"none","typ":"JWT"}""")).TrimEnd('=');
        var payload = Convert.ToBase64String(Encoding.UTF8.GetBytes(
            $$"""{"sub":"1","http://schemas.microsoft.com/ws/2008/06/identity/claims/role":"Manager","academyId":"{{b.AcademyId}}","iss":"project-vital-api","aud":"project-vital-web","exp":{{DateTimeOffset.UtcNow.AddHours(1).ToUnixTimeSeconds()}}}""")).TrimEnd('=');
        var algNone = $"{header}.{payload}.";

        foreach (var token in new[] { wrongKey, wrongIssuer, wrongAudience, expired, algNone })
            ErrorCode(await Gql(query, token)).Should().Be("AUTH_NOT_AUTHORIZED");

        ErrorCode(await Gql(query, studentRole)).Should().Be("FORBIDDEN");
    }

    [Fact]
    public async Task Login_is_locked_after_repeated_failures()
    {
        var (a, _) = await SeedPairAsync();
        for (var i = 0; i < 5; i++)
            ErrorCode(await Gql($"mutation {{ login(email: \"{a.ManagerEmail}\", password: \"errada-{i}\") {{ token }} }}", null))
                .Should().Be("AUTH_FAILED");
        // Mesmo com a senha certa, o e-mail fica bloqueado temporariamente
        ErrorCode(await Gql($"mutation {{ login(email: \"{a.ManagerEmail}\", password: \"Senha@123\") {{ token }} }}", null))
            .Should().Be("TOO_MANY_ATTEMPTS");
    }

    [Fact]
    public async Task Unknown_email_gets_same_error_as_wrong_password()
    {
        var doc = await Gql("mutation { login(email: \"naoexiste@iso.test\", password: \"Senha@123\") { token } }", null);
        ErrorCode(doc).Should().Be("AUTH_FAILED");
        doc.RootElement.GetProperty("errors")[0].GetProperty("message").GetString().Should().Be("Credenciais inválidas.");
    }

    // ── Superfície GraphQL ───────────────────────────────────────────────────

    [Fact]
    public async Task Introspection_is_disabled_outside_development()
    {
        var (a, _) = await SeedPairAsync();
        var doc = await Gql("query { __schema { types { name } } }", a.Token);
        doc.RootElement.TryGetProperty("errors", out _).Should().BeTrue();
    }

    [Fact]
    public async Task Get_requests_are_not_accepted()
    {
        var response = await _client.GetAsync("/graphql?query=%7B__typename%7D");
        response.StatusCode.Should().NotBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Excessively_complex_queries_are_rejected()
    {
        var (a, _) = await SeedPairAsync();
        var aliases = string.Join(" ", Enumerable.Range(0, 300).Select(i =>
            $"s{i}: students(academyId: \"{a.AcademyId}\") {{ id name email latestPrediction {{ probability }} }}"));
        var doc = await Gql($"query {{ {aliases} }}", a.Token);
        doc.RootElement.TryGetProperty("errors", out _).Should().BeTrue();
        doc.RootElement.GetRawText().Should().NotContain("\"s0\"");   // nada foi executado
    }

    [Fact]
    public async Task Responses_carry_security_headers()
    {
        var response = await _client.PostAsync("/graphql",
            new StringContent("""{"query":"{ __typename }"}""", Encoding.UTF8, "application/json"));
        response.Headers.GetValues("X-Content-Type-Options").Should().Contain("nosniff");
        response.Headers.GetValues("X-Frame-Options").Should().Contain("DENY");
        response.Headers.Contains("Server").Should().BeFalse();
    }
}
