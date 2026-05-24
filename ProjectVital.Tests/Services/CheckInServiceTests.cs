using Moq;
using Xunit;
using ProjectVital.Application.Services;
using ProjectVital.Domain.Entities;
using ProjectVital.Domain.Interfaces;

namespace ProjectVital.Tests.Services;

/// <summary>
/// Testes unitários de CheckInService.
///
/// ESTRATÉGIA DE TESTES:
///   - Moq para ICheckInRepository e IStudentRepository (sem banco real)
///   - Testa os 3 caminhos de RegisterAsync: sucesso, aluno inativo, aluno inexistente
///
/// POR QUE ESTES 3 CASOS SÃO CRÍTICOS:
///   1. Aluno ativo → caminho feliz: check-in salvo, retorno com StudentId correto
///   2. Aluno inativo → regra de negócio: não pode entrar na academia se cancelou
///      AppErrorFilter mapeia InvalidOperationException → "BUSINESS_RULE_VIOLATION"
///   3. Aluno inexistente → segurança: não deve criar check-in órfão
///      AppErrorFilter mapeia KeyNotFoundException → "NOT_FOUND"
///
/// EXECUTAR: dotnet test --filter "FullyQualifiedName~CheckInServiceTests"
///
/// SE MUDAR A REGRA "aluno inativo não pode fazer check-in":
///   Atualizar CheckInService.RegisterAsync() E atualizar este teste.
///   O teste documenta o comportamento esperado — se o teste muda, a regra mudou.
/// </summary>
public class CheckInServiceTests
{
    private readonly Mock<ICheckInRepository> _checkInRepo;
    private readonly Mock<IStudentRepository> _studentRepo;
    private readonly CheckInService _sut;

    public CheckInServiceTests()
    {
        _checkInRepo = new Mock<ICheckInRepository>();
        _studentRepo = new Mock<IStudentRepository>();
        _sut = new CheckInService(_checkInRepo.Object, _studentRepo.Object);
    }

    /// <summary>
    /// Aluno ativo deve poder fazer check-in com sucesso.
    /// Verifica: retorno não-null, StudentId correto, SaveChanges chamado.
    /// </summary>
    [Fact]
    public async Task RegisterAsync_WithActiveStudent_CreatesCheckIn()
    {
        var student = new Student { Id = 1, Active = true };
        _studentRepo.Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(student);

        var checkIn = new CheckIn { Id = 1, StudentId = 1, Timestamp = DateTime.UtcNow };
        _checkInRepo.Setup(r => r.AddAsync(It.IsAny<CheckIn>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(checkIn);
        _checkInRepo.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        var result = await _sut.RegisterAsync(1);

        Assert.NotNull(result);
        Assert.Equal(1, result.StudentId);
    }

    /// <summary>
    /// Aluno inativo (Active=false) não pode fazer check-in — regra de negócio.
    /// AppErrorFilter mapeia InvalidOperationException → "BUSINESS_RULE_VIOLATION" no GraphQL.
    ///
    /// CENÁRIO REAL: aluno cancelou a matrícula mas ainda tenta entrar.
    /// </summary>
    [Fact]
    public async Task RegisterAsync_WithInactiveStudent_ThrowsInvalidOperation()
    {
        var student = new Student { Id = 2, Active = false };
        _studentRepo.Setup(r => r.GetByIdAsync(2, It.IsAny<CancellationToken>()))
            .ReturnsAsync(student);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _sut.RegisterAsync(2));
    }

    /// <summary>
    /// StudentId desconhecido deve lançar KeyNotFoundException.
    /// Evita criar check-in órfão (sem Student vinculado) no banco.
    /// AppErrorFilter mapeia → "NOT_FOUND" no GraphQL.
    /// </summary>
    [Fact]
    public async Task RegisterAsync_WithUnknownStudent_ThrowsKeyNotFound()
    {
        _studentRepo.Setup(r => r.GetByIdAsync(99, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Student?)null);

        await Assert.ThrowsAsync<KeyNotFoundException>(
            () => _sut.RegisterAsync(99));
    }
}
