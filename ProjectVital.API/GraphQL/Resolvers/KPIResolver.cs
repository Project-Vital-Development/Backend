using HotChocolate;
using HotChocolate.Authorization;
using HotChocolate.Types;
using Microsoft.AspNetCore.Http;
using ProjectVital.API.GraphQL.Helpers;
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
        ResolverAuthHelper.EnforceAcademyAccess(httpContext, academyId);
        return await academyService.GetKPIsAsync(academyId, cancellationToken);
    }
}
