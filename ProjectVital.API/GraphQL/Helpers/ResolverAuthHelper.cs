using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using ProjectVital.Domain.Exceptions;

namespace ProjectVital.API.GraphQL.Helpers;

public static class ResolverAuthHelper
{
    public static int GetAcademyIdFromToken(IHttpContextAccessor accessor)
    {
        var value = accessor.HttpContext?.User.FindFirst("academyId")?.Value;
        if (value is null || !int.TryParse(value, out var id))
            throw new ForbiddenException("Token não contém academyId válido.");
        return id;
    }

    /// <summary>
    /// Admin tem acesso irrestrito. Manager só pode acessar a própria academia.
    /// Lança ForbiddenException se o acesso for negado.
    /// </summary>
    public static void EnforceAcademyAccess(IHttpContextAccessor accessor, int requestedAcademyId)
    {
        var ctx = accessor.HttpContext
            ?? throw new ForbiddenException("Contexto HTTP não disponível.");

        var role = ctx.User.FindFirst(ClaimTypes.Role)?.Value;
        if (role == "Admin") return;

        var academyIdClaim = ctx.User.FindFirst("academyId")?.Value;
        if (academyIdClaim != requestedAcademyId.ToString())
            throw new ForbiddenException(
                "Acesso negado: você não tem permissão para acessar esta academia.");
    }
}
