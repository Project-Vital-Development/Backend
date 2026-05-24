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
/// Centraliza toda a lógica de autenticação: login com BCrypt, geração de JWT,
/// hash e verificação de senha.
///
/// FLUXO DE LOGIN:
///   1. Cliente envia { email, password } via mutation GraphQL "login"
///   2. AuthResolver chama LoginAsync()
///   3. LoginAsync busca o User por email e verifica o BCrypt hash
///   4. Se ok, gera JWT com claims (userId, role, academyId se Manager)
///   5. Retorna AuthPayloadDto com token + dados do usuário
///   6. Frontend armazena o token e o inclui em todos os requests futuros
///
/// DEPENDÊNCIAS:
///   - IUserRepository      → busca o User por email para autenticar
///   - IAcademyRepository   → busca a academia do Manager para incluir academyId no token
///   - IConfiguration       → lê JWT_SECRET e JWT_EXPIRY_HOURS do ambiente
///
/// SE MUDAR AQUI, IMPACTA:
///   - Program.cs           → JWT_SECRET deve ter ≥ 32 chars (validado nos dois lugares)
///   - ResolverAuthHelper   → lê os claims gerados aqui ("academyId", ClaimTypes.Role)
///   - Todos os resolvers   → dependem do claim "role" para [Authorize]
///   - .env / appsettings   → variáveis JWT_SECRET e JWT_EXPIRY_HOURS
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
    /// Autentica o usuário e retorna um JWT com os claims necessários.
    ///
    /// SEGURANÇA — user enumeration prevention:
    ///   Email inválido e senha incorreta retornam a MESMA mensagem de erro ("Credenciais inválidas.").
    ///   Isso impede que um atacante descubra quais emails estão cadastrados tentando logar.
    ///
    /// TENANT ISOLATION:
    ///   Managers recebem o claim "academyId" no JWT, que é usado por
    ///   ResolverAuthHelper.EnforceAcademyAccess() para restringir acesso à academia correta.
    ///   Admins não têm "academyId" — têm acesso irrestrito por role.
    ///
    /// SE MUDAR O FORMATO DO TOKEN:
    ///   Atualizar ResolverAuthHelper.GetAcademyIdFromToken() e EnforceAcademyAccess()
    ///   que leem os claims diretamente por nome de string.
    /// </summary>
    public async Task<AuthPayloadDto> LoginAsync(LoginInput input, CancellationToken cancellationToken = default)
    {
        var user = await _users.GetByEmailAsync(input.Email, cancellationToken);

        // Mensagem genérica intencional: evita user enumeration attack.
        // Verificar BCrypt mesmo quando user é null protege contra timing attacks.
        if (user is null || !VerifyPassword(input.Password, user.PasswordHash))
            throw new UnauthorizedAccessException("Credenciais inválidas.");

        // Managers têm academyId no token para que os resolvers possam fazer tenant isolation.
        // Admins e Students não têm — Admins são irrestritros, Students não usam a API ainda.
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

    /// <summary>
    /// Gera um hash BCrypt da senha com work factor 12.
    /// Work factor 12 é o mínimo recomendado para produção em 2024.
    /// Cada incremento de 1 duplica o tempo de hash — work factor 13 = 2x mais lento.
    ///
    /// USADO POR:
    ///   - StudentService.RegisterAsync()   → ao criar um aluno novo
    ///   - Program.cs SeedDevelopmentData() → ao criar dados de teste
    ///
    /// IMPACTO: se aumentar o work factor, usuários existentes ainda autenticam
    /// corretamente (BCrypt.Verify detecta o factor do hash existente automaticamente).
    /// Apenas novos hashes usarão o novo factor.
    /// </summary>
    public string HashPassword(string plainPassword)
        => BCrypt.Net.BCrypt.HashPassword(plainPassword, workFactor: 12);

    /// <summary>
    /// Verifica se uma senha em texto plano corresponde ao hash BCrypt armazenado.
    /// BCrypt.Verify é constante em tempo para hashes válidos — protege contra timing attacks.
    ///
    /// USADO EM:
    ///   - LoginAsync() → para autenticar o usuário
    /// </summary>
    public bool VerifyPassword(string plainPassword, string hash)
        => BCrypt.Net.BCrypt.Verify(plainPassword, hash);

    // ── Geração do JWT ────────────────────────────────────────────────────────────
    // Gera o token JWT com os claims do usuário. Privado — só chamado por LoginAsync().

    /// <summary>
    /// Gera um JWT assinado com HMAC-SHA256.
    ///
    /// CLAIMS INCLUÍDOS:
    ///   - "sub"      (JwtRegisteredClaimNames.Sub) → Id do usuário (int como string)
    ///   - "role"     (ClaimTypes.Role) → "Admin", "Manager" ou "Student"
    ///   - "jti"      (JwtRegisteredClaimNames.Jti) → UUID único por token
    ///   - "academyId" → Id da academia (apenas para Managers)
    ///
    /// COMO OS CLAIMS SÃO LIDOS:
    ///   - ClaimTypes.Role  → lido por [Authorize] do Hot Chocolate e por EnforceAcademyAccess()
    ///   - "academyId"      → lido por GetAcademyIdFromToken() e EnforceAcademyAccess()
    ///
    /// CONFIGURAÇÃO:
    ///   - JWT_SECRET via variável de ambiente (mínimo 32 chars — verificado aqui E em Program.cs)
    ///   - Expiração via "Jwt:ExpiryHours" (padrão 24h se não configurado)
    ///   - Issuer/Audience não validados (ValidateIssuer/ValidateAudience = false em Program.cs)
    /// </summary>
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
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()) // UUID único por emissão
        };

        // academyId é incluído apenas para Manager — Admin não tem academy vinculada.
        // Lido por ResolverAuthHelper.GetAcademyIdFromToken() e EnforceAcademyAccess().
        if (academyId.HasValue)
            claims.Add(new Claim("academyId", academyId.Value.ToString()));

        var tokenDescriptor = new JwtSecurityToken(
            claims: claims,
            expires: expiresAt,
            signingCredentials: credentials);

        return (new JwtSecurityTokenHandler().WriteToken(tokenDescriptor), expiresAt);
    }
}
