using Moq;
using Xunit;
using ProjectVital.Application.DTOs;
using ProjectVital.Application.Interfaces;
using ProjectVital.Application.Services;
using ProjectVital.Domain.Entities;
using ProjectVital.Domain.Enums;
using ProjectVital.Domain.Exceptions;
using ProjectVital.Domain.Interfaces;

namespace ProjectVital.Tests.Services;

/// <summary>
/// Testes unitários de PredictionService.
///
/// ESTRATÉGIA DE TESTES:
///   - Moq para IPredictionRepository, IStudentRepository e IMLServiceClient
///   - IMLServiceClient mockado evita chamadas HTTP reais ao Python
///   - Testa o fluxo de orquestração: service coordena repositórios + cliente ML
///
/// POR QUE MOCKAR IMLServiceClient:
///   Testes unitários não devem depender de serviços externos (Python/HTTP).
///   O mock permite testar o comportamento do service em isolamento:
///   - Quando o ML retorna com sucesso → predição salva corretamente?
///   - Quando o aluno não existe → NotFoundException lançada antes de chamar ML?
///   - Quando batch retorna vazio → SaveChanges não é chamado?
///
/// EXECUTAR: dotnet test --filter "FullyQualifiedName~PredictionServiceTests"
///
/// SE MUDAR PredictionService (ex: adicionar retreino automático após predição):
///   Adicionar teste verificando o novo comportamento com _predRepo.Verify().
///
/// SE MUDAR O CONTRATO DO ML (ex: remover ModelVersion do retorno):
///   Atualizar o mock de _mlClient.Setup() e os asserts de result.ModelVersion.
/// </summary>
public class PredictionServiceTests
{
    private readonly Mock<IPredictionRepository> _predRepo;
    private readonly Mock<IStudentRepository> _studentRepo;
    private readonly Mock<IMLServiceClient> _mlClient;
    private readonly PredictionService _sut;

    public PredictionServiceTests()
    {
        _predRepo = new Mock<IPredictionRepository>();
        _studentRepo = new Mock<IStudentRepository>();
        _mlClient = new Mock<IMLServiceClient>();
        _sut = new PredictionService(_predRepo.Object, _studentRepo.Object, _mlClient.Object);
    }

    /// <summary>
    /// GetLatestByStudentAsync deve mapear corretamente Prediction → PredictionDto.
    /// Verifica: Probability e RiskLevel chegam iguais ao que o repositório retornou.
    /// </summary>
    [Fact]
    public async Task GetLatestByStudentAsync_ReturnsMappedDto()
    {
        var prediction = new Prediction
        {
            Id = 1,
            StudentId = 5,
            Probability = 0.75,
            RiskLevel = RiskLevel.High,
            ModelVersion = "xgb_v1",
            PredictedAt = DateTime.UtcNow
        };

        _predRepo.Setup(r => r.GetLatestByStudentAsync(5, It.IsAny<CancellationToken>()))
            .ReturnsAsync(prediction);

        var result = await _sut.GetLatestByStudentAsync(5);

        Assert.NotNull(result);
        Assert.Equal(0.75, result.Probability);
        Assert.Equal(RiskLevel.High, result.RiskLevel);
    }

    /// <summary>
    /// Aluno sem predições deve retornar null (não lançar exceção).
    /// O resolver trata null exibindo "Sem predição disponível" no frontend.
    /// </summary>
    [Fact]
    public async Task GetLatestByStudentAsync_WhenNoPrediction_ReturnsNull()
    {
        _predRepo.Setup(r => r.GetLatestByStudentAsync(99, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Prediction?)null);

        var result = await _sut.GetLatestByStudentAsync(99);

        Assert.Null(result);
    }

    /// <summary>
    /// RunAsync com aluno válido deve:
    ///   1. Chamar IMLServiceClient.PredictAsync com studentId + academyId corretos
    ///   2. Salvar a predição no banco (SaveChanges chamado exatamente uma vez)
    ///   3. Retornar PredictionDto com os valores do ML (Probability, RiskLevel, ModelVersion)
    ///
    /// O _predRepo.Verify garante que SaveChanges foi chamado — sem isso, a predição
    /// seria perdida entre requests.
    /// </summary>
    [Fact]
    public async Task RunAsync_CallsMLClientAndSavesPrediction()
    {
        var student = new Student { Id = 1, AcademyId = 10, Active = true };
        _studentRepo.Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(student);

        // Mock do Python retornando probability=0.8, risk="high", version="xgb_v1"
        _mlClient.Setup(c => c.PredictAsync(1, 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MLPredictionResult(0.8, "high", "xgb_v1"));

        // AddAsync retorna o mesmo objeto que recebeu (simula EF Core Add + Return)
        _predRepo.Setup(r => r.AddAsync(It.IsAny<Prediction>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Prediction p, CancellationToken _) => p);
        _predRepo.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        var result = await _sut.RunAsync(1);

        Assert.NotNull(result);
        Assert.Equal(0.8, result.Probability);
        Assert.Equal(RiskLevel.High, result.RiskLevel);
        Assert.Equal("xgb_v1", result.ModelVersion);
        // CRÍTICO: verifica que a predição foi persistida — não apenas retornada
        _predRepo.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    /// StudentId inexistente deve lançar NotFoundException ANTES de chamar o ML Service.
    /// Evita chamada HTTP desnecessária ao Python para um aluno que não existe.
    /// AppErrorFilter mapeia → "NOT_FOUND" no GraphQL.
    /// </summary>
    [Fact]
    public async Task RunAsync_WithUnknownStudent_ThrowsNotFoundException()
    {
        _studentRepo.Setup(r => r.GetByIdAsync(99, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Student?)null);

        await Assert.ThrowsAsync<NotFoundException>(() => _sut.RunAsync(99));
    }

    /// <summary>
    /// RunBatchAsync com lista vazia do ML deve:
    ///   - Retornar lista vazia (não erro)
    ///   - NÃO chamar SaveChanges (nada para salvar)
    ///
    /// Cenário: academia sem alunos ativos ou Python não encontrou dados suficientes.
    /// </summary>
    [Fact]
    public async Task RunBatchAsync_ReturnsEmptyList_WhenMLReturnsEmpty()
    {
        _mlClient.Setup(c => c.PredictBatchAsync(10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<MLBatchPredictionItem>());

        var result = await _sut.RunBatchAsync(10);

        Assert.Empty(result);
        // Verifica que não tentou persistir nada (lista vazia → sem SaveChanges)
        _predRepo.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RunBatchAsync_IgnoresStudentsOutsideAcademy()
    {
        _mlClient.Setup(c => c.PredictBatchAsync(10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<MLBatchPredictionItem>
            {
                new(1, 0.8, "high") { ModelVersion = "xgb_v3" },
                new(2, 0.1, "low") { ModelVersion = "xgb_v3" },
                new(999, 0.9, "high") { ModelVersion = "xgb_v3" },   // aluno de outra academia
            });
        _studentRepo.Setup(r => r.GetByAcademyIdAsync(10, true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Student>
            {
                new() { Id = 1, AcademyId = 10, Active = true },
                new() { Id = 2, AcademyId = 10, Active = true },
            });

        var result = await _sut.RunBatchAsync(10);

        Assert.Equal(new[] { 1, 2 }, result.Select(p => p.StudentId).OrderBy(i => i));
        Assert.All(result, p => Assert.Equal("xgb_v3", p.ModelVersion));
        _predRepo.Verify(r => r.AddAsync(It.Is<Prediction>(p => p.StudentId == 999), It.IsAny<CancellationToken>()), Times.Never);
    }
}
