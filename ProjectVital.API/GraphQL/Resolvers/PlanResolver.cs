using HotChocolate;
using HotChocolate.Authorization;
using HotChocolate.Types;
using Microsoft.AspNetCore.Http;
using ProjectVital.API.GraphQL.Helpers;
using ProjectVital.Domain.Entities;
using ProjectVital.Domain.Interfaces;

namespace ProjectVital.API.GraphQL.Resolvers;

/// <summary>
/// Resolver GraphQL de queries de planos.
/// Expõe "plans(academyId: ID!)" — lista planos disponíveis de uma academia.
///
/// USO PRINCIPAL: formulário de cadastro/edição de alunos (select de plano).
/// </summary>
[ExtendObjectType(OperationTypeNames.Query)]
public class PlanQueryResolver
{
    [Authorize]
    public async Task<IReadOnlyList<Plan>> PlansAsync(
        [ID] int academyId,
        [Service] IPlanRepository planRepository,
        [Service] IHttpContextAccessor httpContext,
        CancellationToken cancellationToken)
    {
        ResolverAuthHelper.EnforceAcademyAccess(httpContext, academyId);
        return await planRepository.GetByAcademyIdAsync(academyId, cancellationToken);
    }
}
