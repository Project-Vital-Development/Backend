using HotChocolate;
using HotChocolate.Authorization;
using HotChocolate.Types;
using Microsoft.AspNetCore.Http;
using ProjectVital.API.GraphQL.Helpers;
using ProjectVital.Application.DTOs;
using ProjectVital.Domain.Interfaces;

namespace ProjectVital.API.GraphQL.Resolvers;

[ExtendObjectType(OperationTypeNames.Query)]
public class PlanQueryResolver
{
    [Authorize]
    public async Task<IReadOnlyList<PlanDto>> PlansAsync(
        [ID] int academyId,
        // ServiceKind.Resolver: scope/DbContext isolado por resolver (ver KPIResolver).
        [Service(ServiceKind.Resolver)] IPlanRepository planRepository,
        [Service] IHttpContextAccessor httpContext,
        CancellationToken cancellationToken)
    {
        ResolverAuthHelper.EnforceAcademyAccess(httpContext, academyId);
        var plans = await planRepository.GetByAcademyIdAsync(academyId, cancellationToken);
        return plans.Select(p => new PlanDto(p.Id, p.AcademyId, p.Name, p.Price, p.DurationDays))
                    .ToList();
    }
}
