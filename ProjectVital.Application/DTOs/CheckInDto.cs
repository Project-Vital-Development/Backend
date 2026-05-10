namespace ProjectVital.Application.DTOs;

public record CheckInDto(
    int Id,
    int StudentId,
    DateTime Timestamp,
    int? DurationMinutes
);
