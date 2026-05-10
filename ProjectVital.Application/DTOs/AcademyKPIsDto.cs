namespace ProjectVital.Application.DTOs;

/// <summary>
/// Indicadores agregados de uma academia para o dashboard.
/// </summary>
public record AcademyKPIsDto(
    int AcademyId,
    int TotalStudents,
    int ActiveStudents,
    int HighRiskCount,
    int MediumRiskCount,
    int LowRiskCount,
    int CheckInsThisWeek,
    double AverageWeeklyFrequency,
    DateTime GeneratedAt
);
