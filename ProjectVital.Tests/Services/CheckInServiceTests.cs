using Moq;
using ProjectVital.Application.Services;
using ProjectVital.Domain.Entities;
using ProjectVital.Domain.Exceptions;
using ProjectVital.Domain.Interfaces;
using Xunit;

namespace ProjectVital.Tests.Services;

/// <summary>
/// Regras de entrada/saída: aluno ativo, entrada duplicada, concorrência e permanência.
/// Os repositórios são simulados (Moq) — sem banco.
/// </summary>
public class CheckInServiceTests
{
    private readonly Mock<ICheckInRepository> _checkInRepo = new();
    private readonly Mock<IStudentRepository> _studentRepo = new();
    private readonly List<CheckIn> _stored = [];
    private readonly CheckInService _sut;

    public CheckInServiceTests()
    {
        _checkInRepo
            .Setup(r => r.GetByStudentAsync(It.IsAny<int>(), It.IsAny<DateTime?>(), It.IsAny<DateTime?>(), It.IsAny<CancellationToken>(), It.IsAny<bool>()))
            .ReturnsAsync((int id, DateTime? from, DateTime? to, CancellationToken _, bool _) =>
                _stored.Where(c => c.StudentId == id && (from == null || c.Timestamp >= from) && (to == null || c.Timestamp <= to)).ToList());
        _checkInRepo
            .Setup(r => r.AddAsync(It.IsAny<CheckIn>(), It.IsAny<CancellationToken>()))
            .Callback((CheckIn c, CancellationToken _) => { lock (_stored) _stored.Add(c); })
            .ReturnsAsync((CheckIn c, CancellationToken _) => c);
        _sut = new CheckInService(_checkInRepo.Object, _studentRepo.Object);
    }

    private void GivenStudent(int id, bool active = true) =>
        _studentRepo.Setup(r => r.GetByIdAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Student { Id = id, AcademyId = 1, Active = active });

    [Fact]
    public async Task RegisterAsync_ActiveStudent_CreatesCheckInInUtc()
    {
        GivenStudent(10);
        var result = await _sut.RegisterAsync(10);

        Assert.Equal(10, result.StudentId);
        Assert.Null(result.DurationMinutes);
        Assert.Equal(DateTimeKind.Utc, _stored.Single().Timestamp.Kind);
        _checkInRepo.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RegisterAsync_InactiveStudent_IsBusinessRuleViolation()
    {
        GivenStudent(20, active: false);
        await Assert.ThrowsAsync<BusinessRuleException>(() => _sut.RegisterAsync(20));
        Assert.Empty(_stored);
    }

    [Fact]
    public async Task RegisterAsync_UnknownStudent_IsNotFound()
    {
        await Assert.ThrowsAsync<NotFoundException>(() => _sut.RegisterAsync(999));
    }

    [Fact]
    public async Task RegisterAsync_WithOpenVisit_RejectsDuplicate()
    {
        GivenStudent(30);
        await _sut.RegisterAsync(30);
        await Assert.ThrowsAsync<BusinessRuleException>(() => _sut.RegisterAsync(30));
        Assert.Single(_stored);
    }

    [Fact]
    public async Task RegisterAsync_ConcurrentRequests_CreateOnlyOneCheckIn()
    {
        GivenStudent(40);
        var attempts = Enumerable.Range(0, 10).Select(async _ =>
        {
            try { await _sut.RegisterAsync(40); return true; }
            catch (BusinessRuleException) { return false; }
        });
        var results = await Task.WhenAll(attempts);

        Assert.Equal(1, results.Count(ok => ok));
        Assert.Single(_stored, c => c.StudentId == 40);
    }

    [Fact]
    public async Task CheckOutAsync_SetsDurationOfOpenVisit()
    {
        GivenStudent(50);
        _stored.Add(new CheckIn { StudentId = 50, Timestamp = DateTime.UtcNow.AddMinutes(-75) });

        var result = await _sut.CheckOutAsync(50);

        Assert.InRange(result.DurationMinutes!.Value, 74, 76);
        // Depois da saída, uma nova entrada é permitida
        await _sut.RegisterAsync(50);
        Assert.Equal(2, _stored.Count(c => c.StudentId == 50));
    }

    [Fact]
    public async Task CheckOutAsync_WithoutOpenVisit_IsBusinessRuleViolation()
    {
        GivenStudent(60);
        _stored.Add(new CheckIn { StudentId = 60, Timestamp = DateTime.UtcNow.AddHours(-6) });   // fora da janela
        await Assert.ThrowsAsync<BusinessRuleException>(() => _sut.CheckOutAsync(60));
    }

    [Fact]
    public async Task ListByStudentAsync_InvalidRange_IsRejected()
    {
        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            _sut.ListByStudentAsync(1, DateTime.UtcNow, DateTime.UtcNow.AddDays(-1)));
    }
}
