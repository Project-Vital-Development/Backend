using HotChocolate;
using HotChocolate.Authorization;
using HotChocolate.Types;
using Microsoft.AspNetCore.Http;
using ProjectVital.API.GraphQL.Helpers;
using ProjectVital.Application.DTOs;
using ProjectVital.Application.Interfaces;
using ProjectVital.Domain.Exceptions;

namespace ProjectVital.API.GraphQL.Resolvers;

[ExtendObjectType(OperationTypeNames.Query)]
public class PredictionQueryResolver
{
    [Authorize]
    public async Task<IReadOnlyList<PredictionDto>> PredictionsAsync(
        [ID] int academyId,
        [Service] IPredictionService predictionService,
        [Service] IHttpContextAccessor httpContext,
        CancellationToken cancellationToken)
    {
        ResolverAuthHelper.EnforceAcademyAccess(httpContext, academyId);
        return await predictionService.ListLatestByAcademyAsync(academyId, cancellationToken);
    }

    [Authorize]
    public async Task<PredictionDto?> PredictionAsync(
        [ID] int studentId,
        [Service] IPredictionService predictionService,
        [Service] IStudentService studentService,
        [Service] IHttpContextAccessor httpContext,
        CancellationToken cancellationToken)
    {
        // Verifica que o aluno pertence à academia do token
        var student = await studentService.GetByIdAsync(studentId, cancellationToken)
            ?? throw new NotFoundException("Student", studentId);

        ResolverAuthHelper.EnforceAcademyAccess(httpContext, student.AcademyId);
        return await predictionService.GetLatestByStudentAsync(studentId, cancellationToken);
    }
}

[ExtendObjectType(OperationTypeNames.Mutation)]
public class PredictionMutationResolver
{
    [Authorize]
    public Task<PredictionDto> RunPredictionAsync(
        [ID] int studentId,
        [Service] IPredictionService predictionService,
        CancellationToken cancellationToken)
        => predictionService.RunAsync(studentId, cancellationToken);

    [Authorize]
    public Task<IReadOnlyList<PredictionDto>> RunBatchPredictionAsync(
        [ID] int academyId,
        [Service] IPredictionService predictionService,
        CancellationToken cancellationToken)
        => predictionService.RunBatchAsync(academyId, cancellationToken);

    [Authorize]
    public Task<RetrainResultDto> RetrainModelAsync(
        [ID] int academyId,
        CancellationToken cancellationToken)
        => Task.FromResult(new RetrainResultDto(
            false,
            "Retreinamento do modelo será implementado na Fase 4 com integração ao ML Service Python.",
            null, null, null));
}
