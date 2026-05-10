using ProjectVital.Domain.Enums;

namespace ProjectVital.Application.DTOs;

/// <summary>DTO de usuário — não expõe PasswordHash.</summary>
public record UserDto(
    int Id,
    string Name,
    string Email,
    UserRole Role,
    DateTime CreatedAt
);
