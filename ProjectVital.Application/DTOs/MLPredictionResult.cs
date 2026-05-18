namespace ProjectVital.Application.DTOs;

/// <summary>Resposta da predição individual do ML Service (/predict).</summary>
public record MLPredictionResult(
    double Probability,
    string RiskLevel,
    string ModelVersion
);

/// <summary>Item individual no resultado de predição em batch (/predict/batch).</summary>
public record MLBatchPredictionItem(
    int StudentId,
    double Probability,
    string RiskLevel
);
