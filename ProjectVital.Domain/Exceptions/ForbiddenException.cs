namespace ProjectVital.Domain.Exceptions;

/// <summary>
/// Lançada quando um usuário autenticado tenta acessar um recurso que não pertence
/// à sua academia (violação de tenant isolation).
/// AppErrorFilter mapeia para código GraphQL "FORBIDDEN".
///
/// ONDE É LANÇADA (via ResolverAuthHelper):
///   - ResolverAuthHelper.GetAcademyIdFromToken() → token sem claim "academyId"
///   - ResolverAuthHelper.EnforceAcademyAccess() → Manager tentando acessar outra academia
///
/// CENÁRIO TÍPICO:
///   Manager da Academia A tenta listar alunos da Academia B.
///   EnforceAcademyAccess(httpContext, academyIdB) detecta que o claim "academyId"
///   do token é A, não B → lança ForbiddenException.
///
/// RESPOSTA GRAPHQL GERADA:
///   { "errors": [{ "message": "Acesso negado: ...", "extensions": { "code": "FORBIDDEN" } }] }
///
/// DIFERENÇA DE UnauthorizedAccessException:
///   - UnauthorizedAccessException → usuário NÃO autenticado (não tem token válido) → "AUTH_FAILED"
///   - ForbiddenException          → usuário autenticado mas SEM PERMISSÃO → "FORBIDDEN"
/// </summary>
public sealed class ForbiddenException : DomainException
{
    public ForbiddenException(string message = "Acesso negado: você não tem permissão para este recurso.")
        : base(message) { }
}
