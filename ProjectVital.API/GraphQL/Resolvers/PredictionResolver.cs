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
        [Service] IPredictionService predictionService,
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
        [Service] IPredictionService predictionService,
        [Service] IStudentService studentService,
        [Service] IHttpContextAccessor httpContext,
        CancellationToken cancellationToken)
    {
        var student = await studentService.GetByIdAsync(studentId, cancellationToken)
            ?? throw new NotFoundException("Student", studentId);

        ResolverAuthHelper.EnforceAcademyAccess(httpContext, student.AcademyId);
        return await predictionService.GetLatestByStudentAsync(studentId, cancellationToken);
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
    /// NOTA: não verifica EnforceAcademyAccess aqui — o Service verifica internamente
    /// que o aluno existe (NotFoundException se não existir).
    /// TODO: adicionar verificação de tenant por completude.
    /// </summary>
    [Authorize]
    public Task<PredictionDto> RunPredictionAsync(
        [ID] int studentId,
        [Service] IPredictionService predictionService,
        CancellationToken cancellationToken)
        => predictionService.RunAsync(studentId, cancellationToken);

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
        CancellationToken cancellationToken)
        => predictionService.RunBatchAsync(academyId, cancellationToken);

    /// <summary>
    /// PLACEHOLDER — retreino do modelo será integrado ao MLServiceClient na Fase 4.
    ///
    /// Quando implementado, o fluxo será:
    ///   → MLServiceClient.RetrainAsync(academyId) → HTTP POST /retrain ao Python
    ///   → Python carrega check-ins, treina RF + XGBoost, salva melhor modelo
    ///   → Retorna métricas (F1, AUC-ROC) e nova versão do modelo
    ///   → Backend atualiza dashboards automaticamente
    ///
    /// PARA IMPLEMENTAR:
    ///   1. Descomentar a lógica de retrain em MLServiceClient.RetrainAsync()
    ///   2. Injetar IMLServiceClient neste resolver
    ///   3. Remover o Task.FromResult placeholder abaixo
    /// </summary>
    [Authorize]
    public Task<RetrainResultDto> RetrainModelAsync(
        [ID] int academyId,
        CancellationToken cancellationToken)
        => Task.FromResult(new RetrainResultDto(
            false,
            "Retreinamento do modelo será implementado na Fase 4 com integração ao ML Service Python.",
            null, null, null));
}
