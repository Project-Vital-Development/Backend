namespace ProjectVital.Application.DTOs;

/// <summary>
/// Payload retornado pela mutation "login" — tudo que o frontend precisa após autenticar.
///
/// CRIADO POR: AuthService.LoginAsync()
/// EXPOSTO COMO TIPO GRAPHQL: AuthPayloadType.cs
///
/// FLUXO DE USO NO FRONTEND:
///   1. Chamar mutation login(email, password)
///   2. Armazenar Token em localStorage (ver TD-01: risco XSS, avaliar httpOnly cookie)
///   3. Armazenar AcademyId no AuthContext (usado em todas as queries como parâmetro)
///   4. Armazenar User no AuthContext (exibir nome, verificar role)
///   5. Apollo Link injeta "Authorization: Bearer {Token}" em todo request
///   6. Ao expirar (ExpiresAt), redirecionar para /login
///
/// TOKEN:
///   JWT assinado com JWT_SECRET. Contém claims: sub (userId), role, academyId, jti, exp.
///   O frontend NÃO deve decodificar o token para obter dados — usar User e AcademyId aqui.
///   Se o backend mudar claims internos do JWT, este DTO não muda (a estrutura pública permanece).
///
/// EXPIRESA:
///   UTC. Calculado como UtcNow + JWT_EXPIRY_HOURS (padrão 24h).
///   O frontend pode usar para mostrar aviso "sessão expira em X min".
///
/// ACADEMYID (nullable):
///   null para Admin (gerencia múltiplas academias, sem academyId fixo no token).
///   Preenchido para Manager (o academyId é fixado no JWT claim e neste DTO).
///   SE NULL: frontend pode exibir seleção de academia (funcionalidade futura).
/// </summary>
public record AuthPayloadDto(
    // JWT completo — incluir em Authorization: Bearer {Token} em todo request GraphQL
    string Token,
    // UTC — quando o token expira; após isso todos os requests retornam AUTH_FAILED
    DateTime ExpiresAt,
    // Dados do usuário sem PasswordHash — evita query extra após o login
    UserDto User,
    // null para Admin; preenchido para Manager (Id da academia que gerencia)
    int? AcademyId
);
