using ProjectVital.Domain.Enums;

namespace ProjectVital.Application.DTOs;

public record PredictionDto(
    int Id,
    int StudentId,
    double Probability,
    RiskLevel RiskLevel,
    string ModelVersion,
    DateTime PredictedAt
);
