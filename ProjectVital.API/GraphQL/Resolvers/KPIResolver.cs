using HotChocolate;
using HotChocolate.Authorization;
using HotChocolate.Types;
using Microsoft.AspNetCore.Http;
using ProjectVital.API.GraphQL.Helpers;
using ProjectVital.Application.DTOs;
using ProjectVital.Application.Interfaces;

namespace ProjectVital.API.GraphQL.Resolvers;

/// <summary>
/// Resolver GraphQL para a query "academyKPIs".
/// Retorna indicadores operacionais em tempo real de uma academia.
///
/// QUERY GraphQL GERADA:
///   query {
///     academyKPIs(academyId: 1) {
///       totalStudents
///       activeStudents
///       highRiskCount
///       mediumRiskCount
///       lowRiskCount
///       noRiskDataCount
///     }
///   }
///
/// QUANDO USAR NO FRONTEND:
///   - Dashboard principal: carregar ao montar a tela, refrescar a cada X min
///   - Use fetchPolicy: 'cache-and-network' para mostrar dados imediatos do
///     Apollo cache mas ainda buscar atualizações no fundo
///
/// DEPENDÊNCIAS:
///   - AcademyService.GetKPIsAsync() → AcademyRepository + PredictionRepository
///   - ResolverAuthHelper.EnforceAcademyAccess() → verifica JWT claims
///
/// SE MUDAR OS CAMPOS DE AcademyKPIsDto:
///   Atualizar também AcademyKPIsType.cs (o Hot Chocolate precisa saber os campos)
///   e o frontend que lê esses campos na query Apollo.
/// </summary>
[ExtendObjectType(OperationTypeNames.Query)]
public class KPIResolver
{
    /// <summary>
    /// Retorna KPIs agregados da academia em uma única chamada.
    ///
    /// SEGURANÇA: mesmo padrão de todos os resolvers —
    ///   [Authorize] garante token válido, EnforceAcademyAccess garante que
    ///   o Manager só acessa a academia que pertence ao seu token JWT.
    ///
    /// O QUE É CALCULADO (dentro de AcademyService.GetKPIsAsync):
    ///   - totalStudents: COUNT total de alunos (ativos + inativos, sem deleted)
    ///   - activeStudents: COUNT de Active = true
    ///   - highRiskCount / mediumRiskCount / lowRiskCount: contagem por nível
    ///     de risco baseado na última predição de cada aluno
    ///   - noRiskDataCount: ativos sem predição (ainda não passaram pelo ML)
    ///
    /// PERFORMANCE:
    ///   AcademyService usa 2 queries otimizadas, não N queries por aluno.
    ///   Ver AcademyService.GetKPIsAsync() para detalhes.
    ///
    /// SE ADICIONAR NOVOS KPIs:
    ///   1. Adicionar campo em AcademyKPIsDto.cs
    ///   2. Calcular em AcademyService.GetKPIsAsync()
    ///   3. Expor em AcademyKPIsType.cs (Hot Chocolate)
    ///   4. Adicionar ao frontend (Apollo query + UI)
    /// </summary>
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
