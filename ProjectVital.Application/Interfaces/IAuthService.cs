using ProjectVital.Application.DTOs;
using ProjectVital.Application.Inputs;

namespace ProjectVital.Application.Interfaces;

/// <summary>
/// Serviço de autenticação. Centraliza login, geração de JWT e verificação de credenciais.
/// </summary>
public interface IAuthService
{
    /// <summary>
    /// Autentica um usuário pelo email/senha. Retorna o JWT + dados do usuário em caso de sucesso.
    /// Lança UnauthorizedAccessException se as credenciais forem inválidas.
    /// </summary>
    Task<AuthPayloadDto> LoginAsync(LoginInput input, CancellationToken cancellationToken = default);

    /// <summary>Gera o hash BCrypt de uma senha em texto puro (work factor &gt;= 12).</summary>
    string HashPassword(string plainPassword);

    /// <summary>Verifica se uma senha em texto puro corresponde a um hash BCrypt.</summary>
    bool VerifyPassword(string plainPassword, string hash);
}
