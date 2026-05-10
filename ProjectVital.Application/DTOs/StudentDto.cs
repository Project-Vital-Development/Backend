namespace ProjectVital.Application.DTOs;

public record StudentDto(
    int Id,
    int UserId,
    int AcademyId,
    int PlanId,
    string Name,
    string Email,
    DateTime EnrollmentDate,
    bool Active,
    string? PlanName,
    PredictionDto? LatestPrediction
);
