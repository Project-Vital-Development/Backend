namespace ProjectVital.Application.DTOs;

public record PlanDto(
    int Id,
    int AcademyId,
    string Name,
    decimal Price,
    int DurationDays
);
