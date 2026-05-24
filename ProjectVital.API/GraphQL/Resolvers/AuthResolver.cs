using HotChocolate;
using HotChocolate.Types;
using ProjectVital.Application.DTOs;
using ProjectVital.Application.Inputs;
using ProjectVital.Application.Interfaces;

namespace ProjectVital.API.GraphQL.Resolvers;

/// <summary>
/// Resolver GraphQL de autenticação.
/// Expõe a mutation "login" — único endpoint público do sistema (sem [Authorize]).
///
/// POR QUE SEM [Authorize]:
///   O login é o PONTO DE ENTRADA para obter um token. Colocar [Authorize] aqui
///   criaria um paradoxo: precisaria de token para obter um token.
///
/// MUTATION GraphQL GERADA:
///   mutation {
///     login(email: "admin@example.com", password: "Senha@123") {
///       token
///       expiresAt
///       user { id name email role }
///       academyId
///     }
///   }
///
/// FLUXO:
///   Cliente → login(email, password)
///   → AuthService.LoginAsync()
///   → BCrypt.Verify() + JWT gerado
///   → AuthPayloadDto { token, expiresAt, user, academyId }
///   → Cliente armazena token e inclui em headers futuros
///
/// [ExtendObjectType(Mutation)]:
///   Hot Chocolate usa "type merging" — cada resolver pode adicionar fields ao tipo
///   Mutation sem conflito. Isso evita uma classe gigante com todos os mutations.
///   Configurado em Program.cs via .AddMutationType() + .AddTypeExtension{AuthResolver}().
/// </summary>
[ExtendObjectType(OperationTypeNames.Mutation)]
public class AuthResolver
{
    /// <summary>
    /// Autentica o usuário e retorna um JWT com claims de autorização.
    ///
    /// PARÂMETROS VIA GRAPHQL:
    ///   email    → string (validado: Required, EmailAddress, max 255 chars em LoginInput)
    ///   password → string (validado: Required, min 8, max 255 chars em LoginInput)
    ///
    /// EM CASO DE ERRO:
    ///   UnauthorizedAccessException → AppErrorFilter mapeia para "AUTH_FAILED"
    ///   com mensagem genérica "Credenciais inválidas." (não revela se email existe).
    ///
    /// [Service] IAuthService:
    ///   Hot Chocolate injeta o serviço via [Service] — diferente do constructor DI.
    ///   Isso é necessário porque resolvers são instanciados pelo Hot Chocolate,
    ///   não pelo container de DI padrão.
    /// </summary>
    public async Task<AuthPayloadDto> LoginAsync(
        string email,
        string password,
        [Service] IAuthService authService,
        CancellationToken cancellationToken)
    {
        return await authService.LoginAsync(new LoginInput(email, password), cancellationToken);
    }
}
