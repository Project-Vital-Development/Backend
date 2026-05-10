namespace ProjectVital.Application.DTOs;

/// <summary>
/// Payload retornado pela mutation de login GraphQL.
/// Contém o token JWT e os dados básicos do usuário autenticado.
/// </summary>
public record AuthPayloadDto(
    string Token,
    DateTime ExpiresAt,
    UserDto User,
    int? AcademyId
);
