using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using ProjectVital.API.Security;
using ProjectVital.Domain.Exceptions;

namespace ProjectVital.API.GraphQL.Helpers;

/// <summary>
/// Autorização por academia (tenant) nos resolvers GraphQL.
///
/// REGRA: a academia de trabalho é SEMPRE a do JWT (assinado pelo servidor no login).
/// Argumentos "academyId" enviados pelo cliente só são aceitos se forem iguais a ela —
/// servem apenas como confirmação, nunca como fonte da informação.
///
/// Apenas gestores (role Manager com claim academyId) acessam dados de academias.
/// Alunos e requisições anônimas são recusados. Não há bypass por papel.
///
/// Os filtros globais do EF Core (ProjectVitalDbContext) repetem a mesma regra no
/// banco — se um resolver esquecer esta checagem, os dados de outra academia
/// continuam invisíveis.
/// </summary>
public static class ResolverAuthHelper
{
    /// <summary>Academia do gestor autenticado. Lança FORBIDDEN para qualquer outro caso.</summary>
    public static int GetAcademyIdFromToken(IHttpContextAccessor accessor)
    {
        var user = accessor.HttpContext?.User
            ?? throw new ForbiddenException("Contexto HTTP não disponível.");

        if (user.FindFirstValue(ClaimTypes.Role) != "Manager")
            throw new ForbiddenException("Acesso restrito a gestores de academia.");

        var value = user.FindFirstValue(HttpTenantContext.AcademyClaim);
        if (!int.TryParse(value, out var id) || id <= 0)
            throw new ForbiddenException("Token não contém academia válida.");

        return id;
    }

    /// <summary>
    /// Confirma que o academyId informado é o do token e devolve o valor do TOKEN.
    /// </summary>
    public static int EnforceAcademyAccess(IHttpContextAccessor accessor, int requestedAcademyId)
    {
        var academyId = GetAcademyIdFromToken(accessor);
        if (requestedAcademyId != academyId)
            throw new ForbiddenException("Acesso negado: você não tem permissão para acessar esta academia.");
        return academyId;
    }
}
