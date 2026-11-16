using ProjectVital.Application.DTOs;
using ProjectVital.Application.Inputs;

namespace ProjectVital.Application.Interfaces;

/// <summary>
/// Contrato do serviço de autenticação.
/// Define os comportamentos públicos de autenticação e hash de senhas.
///
/// IMPLEMENTAÇÃO: AuthService.cs (Application/Services/AuthService.cs)
/// REGISTRADO EM: Program.cs → services.AddScoped&lt;IAuthService, AuthService&gt;()
///
/// FLUXO DE AUTENTICAÇÃO COMPLETO:
///   Cliente → mutation login(email, password)
///   → AuthResolver.LoginAsync()
///   → IAuthService.LoginAsync()
///   → AuthService: busca usuário + BCrypt.Verify + GenerateToken
///   → Retorna AuthPayloadDto com JWT
///   → Cliente armazena token e inclui em headers futuros
///
/// ONDE É USADO:
///   - AuthResolver.LoginAsync() — único consumer de LoginAsync
///   - StudentService.RegisterAsync() — usa HashPassword para hash da senha do aluno
///
/// SE ADICIONAR AUTENTICAÇÃO VIA GOOGLE / OAUTH:
///   Adicionar método nesta interface (ex: LoginWithGoogleAsync)
///   e implementar em AuthService — o resolver chama via interface.
///
/// SE ADICIONAR REFRESH TOKEN:
///   Adicionar RefreshAsync(string refreshToken) aqui.
///   Ver TD-02 no CLAUDE.md.
/// </summary>
public interface IAuthService
{
    /// <summary>
    /// Autentica um usuário pelo email/senha e retorna JWT + dados do usuário.
    ///
    /// LANÇA: UnauthorizedAccessException se email não existir ou senha incorreta.
    ///   A mensagem é genérica ("Credenciais inválidas.") para evitar user enumeration:
    ///   um atacante não consegue saber se o email existe ou se só a senha está errada.
    ///
    /// RETORNA: AuthPayloadDto com:
    ///   - Token: JWT assinado (válido por JWT_EXPIRY_HOURS horas, padrão 24h)
    ///   - ExpiresAt: quando o token expira (UTC)
    ///   - User: dados do usuário sem PasswordHash
    ///   - AcademyId: null para Admin, preenchido para Manager
    ///
    /// SE MUDAR O FORMATO DO TOKEN:
    ///   Atualizar GenerateToken() em AuthService e ResolverAuthHelper
    ///   que lê os claims "academyId" e ClaimTypes.Role.
    /// </summary>
    Task<AuthPayloadDto> LoginAsync(LoginInput input, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gera hash BCrypt de uma senha em texto puro com work factor >= 12.
    ///
    /// CHAMADO POR: StudentService.RegisterAsync() ao criar a conta do aluno.
    ///
    /// NUNCA armazenar senhas sem hash. BCrypt com work factor 12 é resistente
    /// a ataques de força bruta — aumentar work factor aumenta o tempo de hash
    /// mas também o custo computacional por tentativa de ataque.
    /// </summary>
    string HashPassword(string plainPassword);

    /// <summary>
    /// Verifica se uma senha em texto puro corresponde ao hash BCrypt armazenado.
    ///
    /// CHAMADO POR: AuthService.LoginAsync() — compara senha fornecida com hash no banco.
    ///
    /// TEMPO CONSTANTE: BCrypt.Verify é resistente a timing attacks — sempre demora
    /// aproximadamente o mesmo tempo, independente de quantos caracteres batem.
    /// </summary>
    bool VerifyPassword(string plainPassword, string hash);
}
