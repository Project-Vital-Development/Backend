using System.Security.Claims;
using HotChocolate;
using HotChocolate.Authorization;
using HotChocolate.Types;
using Microsoft.AspNetCore.Http;
using ProjectVital.Application.DTOs;
using ProjectVital.Application.Interfaces;

namespace ProjectVital.API.GraphQL.Resolvers;

[ExtendObjectType(OperationTypeNames.Query)]
public class KPIResolver
{
    [Authorize]
    public async Task<AcademyKPIsDto> AcademyKPIsAsync(
        [ID] int academyId,
        [Service] IAcademyService academyService,
        [Service] IHttpContextAccessor httpContext,
        CancellationToken cancellationToken)
    {
        EnforceAcademyAccess(httpContext.HttpContext!, academyId);
        return await academyService.GetKPIsAsync(academyId, cancellationToken);
    }

    private static void EnforceAcademyAccess(HttpContext context, int requestedAcademyId)
    {
        var role = context.User.FindFirst(ClaimTypes.Role)?.Value;
        if (role == "Admin") return;

        var academyIdClaim = context.User.FindFirst("academyId")?.Value;
        if (academyIdClaim != requestedAcademyId.ToString())
            throw new UnauthorizedAccessException(
                "Acesso negado: você não tem permissão para acessar esta academia.");
    }
}
