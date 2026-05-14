using System.Security.Claims;
using HotChocolate;
using HotChocolate.Authorization;
using HotChocolate.Types;
using Microsoft.AspNetCore.Http;
using ProjectVital.Application.DTOs;
using ProjectVital.Application.Interfaces;

namespace ProjectVital.API.GraphQL.Resolvers;

[ExtendObjectType(OperationTypeNames.Query)]
public class PredictionQueryResolver
{
    /// <summary>Lista a última predição de cada aluno da academia.</summary>
    [Authorize]
    public async Task<IReadOnlyList<PredictionDto>> PredictionsAsync(
        [ID] int academyId,
        [Service] IPredictionService predictionService,
        [Service] IHttpContextAccessor httpContext,
        CancellationToken cancellationToken)
    {
        EnforceAcademyAccess(httpContext.HttpContext!, academyId);
        return await predictionService.ListLatestByAcademyAsync(academyId, cancellationToken);
    }

    /// <summary>Última predição de um aluno específico.</summary>
    [Authorize]
    public async Task<PredictionDto?> PredictionAsync(
        [ID] int studentId,
        [Service] IPredictionService predictionService,
        CancellationToken cancellationToken)
    {
        return await predictionService.GetLatestByStudentAsync(studentId, cancellationToken);
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

[ExtendObjectType(OperationTypeNames.Mutation)]
public class PredictionMutationResolver
{
    /// <summary>
    /// Stub — integração com ML Service disponível na Fase 4.
    /// Retorna erro legível ao invés de crash.
    /// </summary>
    [Authorize]
    public Task<PredictionDto> RunPredictionAsync(
        [ID] int studentId,
        [Service] IPredictionService predictionService,
        CancellationToken cancellationToken)
    {
        return predictionService.RunAsync(studentId, cancellationToken);
    }

    /// <summary>Stub — disponível na Fase 4.</summary>
    [Authorize]
    public Task<IReadOnlyList<PredictionDto>> RunBatchPredictionAsync(
        [ID] int academyId,
        [Service] IPredictionService predictionService,
        CancellationToken cancellationToken)
    {
        return predictionService.RunBatchAsync(academyId, cancellationToken);
    }

    /// <summary>Stub — disponível na Fase 4.</summary>
    [Authorize]
    public Task<RetrainResultDto> RetrainModelAsync(
        [ID] int academyId,
        CancellationToken cancellationToken)
    {
        return Task.FromResult(new RetrainResultDto(
            false,
            "Retreinamento do modelo será implementado na Fase 4 com integração ao ML Service Python.",
            null, null, null));
    }
}
