namespace ProjectVital.Application.DTOs;

/// <summary>
/// Resultado da operação de retreinamento do modelo ML.
/// Retornado pela mutation `retrainModel`.
/// </summary>
public record RetrainResultDto(
    bool Success,
    string Message,
    string? ModelVersion,
    double? F1Score,
    double? AucRoc
);
