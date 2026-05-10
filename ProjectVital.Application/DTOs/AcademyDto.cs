namespace ProjectVital.Application.DTOs;

public record AcademyDto(
    int Id,
    string Name,
    string? Address,
    int ManagerId,
    DateTime CreatedAt
);
