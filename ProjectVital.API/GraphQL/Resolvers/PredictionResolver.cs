using HotChocolate;
using HotChocolate.Authorization;
using HotChocolate.Types;
using Microsoft.AspNetCore.Http;
using ProjectVital.API.GraphQL.Helpers;
using ProjectVital.Application.DTOs;
using ProjectVital.Application.Interfaces;
using ProjectVital.Domain.Exceptions;

namespace ProjectVital.API.GraphQL.Resolvers;

/// <summary>
/// Resolver GraphQL de queries de predições.
/// Expõe "predictions" (lista por academia) e "prediction" (última por aluno).
///
/// QUERIES GraphQL GERADAS:
///   query {
///     predictions(academyId: 1) { studentId probability riskLevel modelVersion predictedAt }
///     prediction(studentId: 42) { probability riskLevel }
///   }
/// </summary>
[ExtendObjectType(OperationTypeNames.Query)]
public class PredictionQueryResolver
{
    /// <summary>
    /// Lista a predição mais recente de CADA aluno da academia.
    /// Usada pelo dashboard para exibir o risco atual de toda a academia.
    ///
    /// UMA QUERY OTIMIZADA: PredictionRepository.GetLatestByAcademyAsync()
    /// usa subquery MAX para evitar N+1. Retorna apenas a predição mais recente
    /// por aluno, ordenada por Probability DESC (maior risco primeiro).
    ///
    /// ALUNOS SEM PREDIÇÃO: não aparecem na lista (JOIN inner na subquery).
    /// Para incluí-los, precisaria de LEFT JOIN + flag "sem predição ainda".
    /// </summary>
    [Authorize]
    public async Task<IReadOnlyList<PredictionDto>> PredictionsAsync(
        [ID] int academyId,
        // ServiceKind.Resolver: scope/DbContext isolado por resolver (ver KPIResolver).
        [Service(ServiceKind.Resolver)] IPredictionService predictionService,
        [Service] IHttpContextAccessor httpContext,
        CancellationToken cancellationToken)
    {
        ResolverAuthHelper.EnforceAcademyAccess(httpContext, academyId);
        return await predictionService.ListLatestByAcademyAsync(academyId, cancellationToken);
    }

    /// <summary>
    /// Retorna a última predição de um aluno específico.
    /// Usado na tela de detalhe do aluno para exibir o risco atual.
    ///
    /// RETORNA NULL se o aluno ainda não foi avaliado pelo ML.
    /// O frontend deve tratar null exibindo "Sem predição disponível".
    ///
    /// VERIFICAÇÃO DE TENANT:
    ///   Busca o aluno → obtém AcademyId → EnforceAcademyAccess.
    ///   Mesmo padrão usado em CheckInsAsync e StudentAsync.
    /// </summary>
    [Authorize]
    public async Task<PredictionDto?> PredictionAsync(
        [ID] int studentId,
        // ServiceKind.Resolver: scope/DbContext isolado por resolver (ver KPIResolver).
        [Service(ServiceKind.Resolver)] IPredictionService predictionService,
        [Service(ServiceKind.Resolver)] IStudentService studentService,
        [Service] IHttpContextAccessor httpContext,
        CancellationToken cancellationToken)
    {
        var student = await studentService.GetByIdAsync(studentId, cancellationToken)
            ?? throw new NotFoundException("Student", studentId);

        ResolverAuthHelper.EnforceAcademyAccess(httpContext, student.AcademyId);
        return await predictionService.GetLatestByStudentAsync(studentId, cancellationToken);
    }

    /// <summary>
    /// Relatório do último treino do modelo XGBoost da academia: métricas no conjunto de teste,
    /// regras de referência e importância das variáveis. Null se nenhum treino foi feito ainda.
    ///
    /// EXEMPLO:
    ///   query { modelReport(academyId: 1) { bestAlgorithm comparison { algorithm f1Score } } }
    /// </summary>
    [Authorize]
    public Task<RetrainResultDto?> ModelReportAsync(
        [ID] int academyId,
        [Service] IMLServiceClient mlServiceClient,
        [Service] IHttpContextAccessor httpContext,
        CancellationToken cancellationToken)
    {
        var tenant = ResolverAuthHelper.EnforceAcademyAccess(httpContext, academyId);
        return mlServiceClient.GetModelReportAsync(tenant, cancellationToken);
    }
}

/// <summary>
/// Resolver GraphQL de mutations de predições.
/// Expõe "runPrediction", "runBatchPrediction" e "retrainModel" no tipo Mutation.
///
/// MUTATIONS GraphQL GERADAS:
///   mutation { runPrediction(studentId: 42) { probability riskLevel modelVersion } }
///   mutation { runBatchPrediction(academyId: 1) { studentId probability riskLevel } }
///   mutation { retrainModel(academyId: 1) { status modelVersion metrics { f1Score } } }
/// </summary>
[ExtendObjectType(OperationTypeNames.Mutation)]
public class PredictionMutationResolver
{
    /// <summary>
    /// Dispara predição de churn para um único aluno.
    ///
    /// FLUXO COMPLETO:
    ///   Este resolver → PredictionService.RunAsync()
    ///   → MLServiceClient.PredictAsync() (HTTP POST /predict ao Python)
    ///   → Python calcula features + aplica modelo
    ///   → Probabilidade retornada → salva Prediction no banco
    ///
    /// PODE DEMORAR: o ML Service pode levar até ML_SERVICE_TIMEOUT_SECONDS (padrão 30s).
    /// O frontend deve exibir loading state enquanto aguarda.
    ///
    /// PODE FALHAR SE:
    ///   - ML Service offline → HttpRequestException
    ///   - Aluno sem check-ins → ValueError do Python → HTTP 422
    ///   - Modelo não treinado → RuntimeError do Python → HTTP 503
    ///
    /// TENANT ISOLATION: busca o aluno → verifica que pertence à academia do token
    /// (EnforceAcademyAccess) ANTES de chamar o ML. Sem isso, um Manager poderia
    /// disparar predição para aluno de outra academia conhecendo o studentId.
    /// </summary>
    [Authorize]
    public async Task<PredictionDto> RunPredictionAsync(
        [ID] int studentId,
        [Service] IPredictionService predictionService,
        [Service] IStudentService studentService,
        [Service] IHttpContextAccessor httpContext,
        CancellationToken cancellationToken)
    {
        var student = await studentService.GetByIdAsync(studentId, cancellationToken)
            ?? throw new NotFoundException("Student", studentId);
        ResolverAuthHelper.EnforceAcademyAccess(httpContext, student.AcademyId);

        return await predictionService.RunAsync(studentId, cancellationToken);
    }

    /// <summary>
    /// Dispara predição de churn para TODOS os alunos ativos de uma academia.
    /// Uma chamada só → uma request HTTP ao Python → múltiplas predições salvas.
    ///
    /// PERFORMANCE:
    ///   O Python processa todos os alunos em batch com numpy — mais eficiente
    ///   que chamar /predict N vezes individualmente.
    ///   Tempo proporcional ao número de alunos, mas sempre uma única chamada HTTP.
    ///
    /// USO RECOMENDADO: acionar periodicamente (diário/semanal) para manter
    /// o dashboard de risco atualizado sem precisar predizer aluno por aluno.
    /// </summary>
    [Authorize]
    public Task<IReadOnlyList<PredictionDto>> RunBatchPredictionAsync(
        [ID] int academyId,
        [Service] IPredictionService predictionService,
        [Service] IHttpContextAccessor httpContext,
        [Service] ProjectVital.API.Security.OperationThrottle throttle,
        CancellationToken cancellationToken)
    {
        // Tenant isolation: Manager só dispara predição em lote da própria academia.
        var tenant = ResolverAuthHelper.EnforceAcademyAccess(httpContext, academyId);
        throttle.Enforce("batch", tenant, TimeSpan.FromSeconds(20));
        return predictionService.RunBatchAsync(tenant, cancellationToken);
    }

    /// <summary>
    /// Dispara o retreino do modelo de churn da academia no ML Service.
    ///
    /// FLUXO:
    ///   → MLServiceClient.RetrainAsync(academyId) → HTTP POST /retrain ao Python
    ///   → Python carrega os dados DA ACADEMIA, treina o XGBoost e salva uma nova versão
    ///     (versionado, nunca sobrescreve) e recarrega em memória (main.py)
    ///   → Retorna métricas (F1, AUC-ROC) e a nova versão do modelo
    ///
    /// PODE DEMORAR: o treino é computacionalmente intenso. O frontend deve exibir
    /// loading state. Recomendado acionar periodicamente, não em loop.
    ///
    /// VERIFICAÇÃO DE TENANT: EnforceAcademyAccess garante que o gestor só retreina
    /// o modelo da própria academia.
    /// </summary>
    [Authorize]
    public Task<RetrainResultDto> RetrainModelAsync(
        [ID] int academyId,
        [Service] IMLServiceClient mlServiceClient,
        [Service] IHttpContextAccessor httpContext,
        [Service] ProjectVital.API.Security.OperationThrottle throttle,
        CancellationToken cancellationToken)
    {
        var tenant = ResolverAuthHelper.EnforceAcademyAccess(httpContext, academyId);
        throttle.Enforce("retrain", tenant, TimeSpan.FromMinutes(2));   // treino é caro em CPU
        return mlServiceClient.RetrainAsync(tenant, cancellationToken);
    }
}
