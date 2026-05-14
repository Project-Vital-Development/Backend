using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using ProjectVital.Application.DTOs;
using ProjectVital.Application.Inputs;
using ProjectVital.Application.Interfaces;
using ProjectVital.Domain.Entities;
using ProjectVital.Domain.Enums;
using ProjectVital.Domain.Interfaces;

namespace ProjectVital.Application.Services;

/// <summary>
/// Implementação do serviço de autenticação.
/// Centraliza login com BCrypt, geração de JWT e verificação de credenciais.
/// </summary>
public class AuthService : IAuthService
{
    private readonly IUserRepository _users;
    private readonly IAcademyRepository _academies;
    private readonly IConfiguration _config;

    public AuthService(
        IUserRepository users,
        IAcademyRepository academies,
        IConfiguration config)
    {
        _users = users;
        _academies = academies;
        _config = config;
    }

    /// <summary>
    /// Autentica o usuário. Usa a mesma mensagem de erro para email inexistente
    /// e senha incorreta para prevenir enumeração de usuários.
    /// </summary>
    public async Task<AuthPayloadDto> LoginAsync(LoginInput input, CancellationToken cancellationToken = default)
    {
        var user = await _users.GetByEmailAsync(input.Email, cancellationToken);

        // Mensagem genérica intencional: evita user enumeration attack
        if (user is null || !VerifyPassword(input.Password, user.PasswordHash))
            throw new UnauthorizedAccessException("Credenciais inválidas.");

        // Managers têm academyId no token para tenant isolation
        int? academyId = null;
        if (user.Role == UserRole.Manager)
        {
            var academy = await _academies.GetByManagerIdAsync(user.Id, cancellationToken);
            academyId = academy?.Id;
        }

        var (token, expiresAt) = GenerateToken(user, academyId);
        var userDto = new UserDto(user.Id, user.Name, user.Email, user.Role, user.CreatedAt);

        return new AuthPayloadDto(token, expiresAt, userDto, academyId);
    }

    /// <summary>Work factor fixado em 12 — mínimo seguro para produção.</summary>
    public string HashPassword(string plainPassword)
        => BCrypt.Net.BCrypt.HashPassword(plainPassword, workFactor: 12);

    public bool VerifyPassword(string plainPassword, string hash)
        => BCrypt.Net.BCrypt.Verify(plainPassword, hash);

    // ── Geração do JWT ────────────────────────────────────────────────────────

    private (string token, DateTime expiresAt) GenerateToken(User user, int? academyId)
    {
        var secret = _config["JWT_SECRET"]
            ?? throw new InvalidOperationException(
                "JWT_SECRET não configurado. Defina a variável de ambiente JWT_SECRET.");

        if (secret.Length < 32)
            throw new InvalidOperationException(
                "JWT_SECRET deve ter no mínimo 32 caracteres.");

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var expiryHours = int.TryParse(_config["Jwt:ExpiryHours"], out var h) ? h : 24;
        var expiresAt = DateTime.UtcNow.AddHours(expiryHours);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(ClaimTypes.Role, user.Role.ToString()),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        // academyId é incluído apenas para Manager — Admin não tem academy vinculada
        if (academyId.HasValue)
            claims.Add(new Claim("academyId", academyId.Value.ToString()));

        var tokenDescriptor = new JwtSecurityToken(
            claims: claims,
            expires: expiresAt,
            signingCredentials: credentials);

        return (new JwtSecurityTokenHandler().WriteToken(tokenDescriptor), expiresAt);
    }
}
