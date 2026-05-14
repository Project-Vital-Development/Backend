using ProjectVital.Application.DTOs;
using ProjectVital.Application.Interfaces;
using ProjectVital.Domain.Interfaces;

namespace ProjectVital.Application.Services;

/// <summary>
/// Implementação do serviço de predições.
/// RunAsync e RunBatchAsync são stubs — a integração HTTP com o ML Service será
/// implementada na Fase 4 via ProjectVital.ML/MLServiceClient.cs.
/// Leitura de predições existentes já é funcional (consulta ao MySQL).
/// </summary>
public class PredictionService : IPredictionService
{
    private readonly IPredictionRepository _predictions;

    public PredictionService(IPredictionRepository predictions)
    {
        _predictions = predictions;
    }

    /// <summary>Stub — disponível na Fase 4 com integração ao ML Service.</summary>
    public Task<PredictionDto> RunAsync(int studentId, CancellationToken cancellationToken = default)
        => throw new NotImplementedException(
            "Integração com o ML Service será implementada na Fase 4. " +
            "Para testar, insira predições diretamente no banco de dados.");

    /// <summary>Stub — disponível na Fase 4 com integração ao ML Service.</summary>
    public Task<IReadOnlyList<PredictionDto>> RunBatchAsync(int academyId, CancellationToken cancellationToken = default)
        => throw new NotImplementedException(
            "Integração com o ML Service será implementada na Fase 4. " +
            "Para testar, insira predições diretamente no banco de dados.");

    public async Task<IReadOnlyList<PredictionDto>> ListLatestByAcademyAsync(
        int academyId,
        CancellationToken cancellationToken = default)
    {
        var predictions = await _predictions.GetLatestByAcademyAsync(academyId, cancellationToken);
        return predictions
            .Select(p => new PredictionDto(p.Id, p.StudentId, p.Probability, p.RiskLevel, p.ModelVersion, p.PredictedAt))
            .ToList()
            .AsReadOnly();
    }

    public async Task<PredictionDto?> GetLatestByStudentAsync(
        int studentId,
        CancellationToken cancellationToken = default)
    {
        var p = await _predictions.GetLatestByStudentAsync(studentId, cancellationToken);
        return p is null
            ? null
            : new PredictionDto(p.Id, p.StudentId, p.Probability, p.RiskLevel, p.ModelVersion, p.PredictedAt);
    }
}
