using ProjectVital.Application.DTOs;

namespace ProjectVital.Application.Interfaces;

/// <summary>
/// Contrato de comunicação HTTP com o ML Service (FastAPI Python).
/// Implementação concreta em ProjectVital.ML/MLServiceClient.cs.
/// </summary>
public interface IMLServiceClient
{
    /// <summary>Chama POST /predict — predição individual.</summary>
    Task<MLPredictionResult> PredictAsync(int studentId, int academyId, CancellationToken cancellationToken = default);

    /// <summary>Chama POST /predict/batch — predição em lote por academia.</summary>
    Task<IReadOnlyList<MLBatchPredictionItem>> PredictBatchAsync(int academyId, CancellationToken cancellationToken = default);

    /// <summary>Chama POST /retrain — retreina modelo para uma academia.</summary>
    Task<RetrainResultDto> RetrainAsync(int academyId, CancellationToken cancellationToken = default);
}
