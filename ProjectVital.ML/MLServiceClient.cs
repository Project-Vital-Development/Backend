using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using ProjectVital.Application.DTOs;
using ProjectVital.Application.Interfaces;

namespace ProjectVital.ML;

/// <summary>
/// Cliente HTTP typed para o ML Service (FastAPI Python em http://ml-service:8000).
/// É a única classe no sistema .NET que faz chamadas HTTP ao Python.
///
/// REGISTRADO EM: Program.cs via AddHttpClient&lt;IMLServiceClient, MLServiceClient&gt;()
///   - BaseAddress: ML_SERVICE_URL (variável de ambiente)
///   - Timeout: ML_SERVICE_TIMEOUT_SECONDS (padrão 30s)
///   - Retry: Polly policy configurada no Program.cs (2 tentativas + backoff)
///   - Injetado via DI em PredictionService
///
/// CONTRATO COM O PYTHON (cross-service contract):
///   POST /predict      → MLPredictResponse   (individual)
///   POST /predict/batch → MLBatchItemResponse[] (lote)
///   POST /retrain      → MLRetrainResponse   (retreino)
///
/// SE MUDAR O CONTRATO DO PYTHON:
///   - Renomear um campo JSON (ex: "risk_level" → "riskLevel") → atualizar o
///     [JsonPropertyName] no record correspondente aqui E no Python
///   - Adicionar campo opcional → adicionar como `string? NovoField = null` no record
///   - Mudar estrutura do batch → atualizar MLBatchItemResponse e PredictBatchAsync
///   - Mudar /retrain response → atualizar MLRetrainResponse e RetrainAsync
///
/// SE O ML SERVICE ESTIVER OFFLINE:
///   HttpRequestException é lançada com StatusCode null (timeout/conexão recusada).
///   PredictionService não captura — sobe até AppErrorFilter → "BUSINESS_RULE_VIOLATION".
///   O frontend deve tratar esse código exibindo mensagem de serviço indisponível.
///
/// THREADING: HttpClient é thread-safe e reutilizável — não instanciar por request.
///   IHttpClientFactory (usado pelo DI) gerencia o ciclo de vida dos sockets corretamente.
/// </summary>
public sealed class MLServiceClient : IMLServiceClient
{
    private readonly HttpClient _http;

    /// <summary>
    /// Opções de desserialização JSON compartilhadas por todos os métodos.
    /// PropertyNameCaseInsensitive: aceita "Probability" e "probability" (Python snake_case).
    /// AllowReadingFromString: aceita probabilidade como "0.73" (string) além de 0.73 (number).
    /// </summary>
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        NumberHandling = JsonNumberHandling.AllowReadingFromString,
    };

    /// <summary>
    /// HttpClient injetado pelo IHttpClientFactory com BaseAddress e timeout pré-configurados.
    /// Não instanciar manualmente — sempre usar DI para garantir gerenciamento correto de sockets.
    /// </summary>
    public MLServiceClient(HttpClient httpClient)
    {
        _http = httpClient;
    }

    /// <summary>
    /// Solicita predição de churn para um único aluno ao ML Service.
    ///
    /// FLUXO:
    ///   1. POST /predict { student_id, academy_id }
    ///   2. Python busca check-ins do aluno no MySQL (read-only via SQLAlchemy)
    ///   3. Calcula 5 features comportamentais (engineering.py)
    ///   4. Aplica modelo treinado (predictor.py) → probabilidade 0.0–1.0
    ///   5. Classifica risco: <0.3=low, 0.3–0.6=medium, >0.6=high
    ///   6. Retorna { probability, risk_level, model_version }
    ///   7. .NET mapeia para MLPredictionResult e salva como Prediction no MySQL
    ///
    /// ERROS DO PYTHON (mapeados para HttpRequestException):
    ///   HTTP 422: aluno sem check-ins suficientes → Python lança ValueError
    ///   HTTP 503: modelo não carregado (primeira execução sem /retrain)
    ///   HTTP 500: erro interno do Python (ver logs do ml-service)
    ///
    /// CHAMADO POR: PredictionService.RunAsync()
    ///
    /// SE MUDAR: atualizar MLPredictResponse abaixo e IMLServiceClient.cs
    /// </summary>
    public async Task<MLPredictionResult> PredictAsync(
        int studentId,
        int academyId,
        CancellationToken cancellationToken = default)
    {
        var body = new { student_id = studentId, academy_id = academyId };
        using var response = await _http.PostAsJsonAsync("/predict", body, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var detail = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new HttpRequestException(
                $"ML Service /predict retornou {(int)response.StatusCode}: {detail}",
                null,
                response.StatusCode);
        }

        var result = await response.Content.ReadFromJsonAsync<MLPredictResponse>(JsonOptions, cancellationToken)
            ?? throw new InvalidOperationException("ML Service retornou resposta vazia.");

        return new MLPredictionResult(result.Probability, result.RiskLevel, result.ModelVersion);
    }

    /// <summary>
    /// Solicita predição de churn para TODOS os alunos ativos de uma academia em lote.
    /// Uma única chamada HTTP — Python processa todos os alunos internamente com numpy.
    ///
    /// FLUXO:
    ///   1. POST /predict/batch { academy_id }
    ///   2. Python busca todos os alunos ativos da academia + seus check-ins
    ///   3. Calcula features para cada aluno em vetores numpy (eficiente)
    ///   4. Aplica modelo em batch (sklearn/XGBoost suporta predict_proba em array)
    ///   5. Retorna lista [{ student_id, probability, risk_level }]
    ///
    /// RETORNO VAZIO (?? []):
    ///   Se o Python retornar null ou lista vazia (academia sem alunos ativos),
    ///   retornamos lista vazia — não é erro, é estado válido.
    ///
    /// NOTA: ModelVersion não está no item individual do batch — PredictionService
    ///   usa string.Empty para ModelVersion nas predições de batch. Bug conhecido.
    ///   Para corrigir: adicionar "model_version" ao item do batch no Python E aqui.
    ///
    /// CHAMADO POR: PredictionService.RunBatchAsync()
    /// </summary>
    public async Task<IReadOnlyList<MLBatchPredictionItem>> PredictBatchAsync(
        int academyId,
        CancellationToken cancellationToken = default)
    {
        var body = new { academy_id = academyId };
        using var response = await _http.PostAsJsonAsync("/predict/batch", body, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var detail = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new HttpRequestException(
                $"ML Service /predict/batch retornou {(int)response.StatusCode}: {detail}",
                null,
                response.StatusCode);
        }

        var items = await response.Content.ReadFromJsonAsync<List<MLBatchItemResponse>>(JsonOptions, cancellationToken)
            ?? [];

        return items
            .Select(i => new MLBatchPredictionItem(i.StudentId, i.Probability, i.RiskLevel))
            .ToList()
            .AsReadOnly();
    }

    /// <summary>
    /// Solicita retreino do modelo ML para uma academia ao Python.
    ///
    /// FLUXO NO PYTHON (trainer.py):
    ///   1. Carrega check-ins históricos da academia (SQLAlchemy)
    ///   2. Calcula features para cada aluno
    ///   3. Define label: aluno inativo → churn=1, ativo → churn=0
    ///   4. SMOTE para balancear classes
    ///   5. Treina Random Forest + XGBoost com grid search
    ///   6. Seleciona melhor modelo por F1-score
    ///   7. Salva como rf_v{N}.joblib ou xgb_v{N}.joblib (NUNCA sobrescreve)
    ///   8. Retorna { status, model_version, metrics { f1_score, auc_roc, ... } }
    ///
    /// MAPEAMENTO DE MÉTRICAS:
    ///   result.Metrics?.F1Score → RetrainResultDto.F1Score (nullable se Python não retornou)
    ///   result.Metrics?.AucRoc → RetrainResultDto.AucRoc
    ///   Precision e Recall são recebidos mas não propagados para o DTO (expandir se necessário)
    ///
    /// NOTA: RetrainModelAsync no PredictionMutationResolver é um PLACEHOLDER — não chama
    ///   este método ainda. Para ativar: ver o TODO em PredictionResolver.cs.
    ///
    /// CHAMADO POR: PredictionMutationResolver.RetrainModelAsync() — quando implementado
    /// </summary>
    public async Task<RetrainResultDto> RetrainAsync(
        int academyId,
        CancellationToken cancellationToken = default)
    {
        var body = new { academy_id = academyId };
        using var response = await _http.PostAsJsonAsync("/retrain", body, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var detail = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new HttpRequestException(
                $"ML Service /retrain retornou {(int)response.StatusCode}: {detail}",
                null,
                response.StatusCode);
        }

        var result = await response.Content.ReadFromJsonAsync<MLRetrainResponse>(JsonOptions, cancellationToken)
            ?? throw new InvalidOperationException("ML Service retornou resposta vazia para /retrain.");

        return new RetrainResultDto(
            Success: result.Status == "success",
            Message: result.Message ?? string.Empty,
            ModelVersion: result.ModelVersion,
            F1Score: result.Metrics?.F1Score,
            AucRoc: result.Metrics?.AucRoc
        );
    }

    // ── Records privados de desserialização JSON ──────────────────────────────
    // Usados apenas internamente para mapear a resposta raw do Python para os
    // tipos da Application layer. São privados porque o contrato público é
    // IMLServiceClient (Application/Interfaces) + MLPredictionResult/MLBatchPredictionItem (Application/DTOs).
    //
    // SE O PYTHON MUDAR O NOME DE UM CAMPO:
    //   Atualizar o [JsonPropertyName] do record correspondente abaixo.
    //   O nome da propriedade C# pode ser diferente do JSON — o atributo faz o mapeamento.

    /// <summary>Resposta do Python para POST /predict (individual).</summary>
    private sealed record MLPredictResponse(
        [property: JsonPropertyName("probability")] double Probability,
        [property: JsonPropertyName("risk_level")] string RiskLevel,
        [property: JsonPropertyName("model_version")] string ModelVersion
    );

    /// <summary>
    /// Um item da lista retornada pelo Python em POST /predict/batch.
    /// Não inclui ModelVersion — bug conhecido, ver PredictBatchAsync.
    /// </summary>
    private sealed record MLBatchItemResponse(
        [property: JsonPropertyName("student_id")] int StudentId,
        [property: JsonPropertyName("probability")] double Probability,
        [property: JsonPropertyName("risk_level")] string RiskLevel
    );

    /// <summary>Métricas de avaliação retornadas pelo Python após retreino.</summary>
    private sealed record MLRetrainMetrics(
        [property: JsonPropertyName("f1_score")] double F1Score,
        [property: JsonPropertyName("auc_roc")] double AucRoc,
        [property: JsonPropertyName("precision")] double Precision,
        [property: JsonPropertyName("recall")] double Recall
    );

    /// <summary>Resposta completa do Python para POST /retrain.</summary>
    private sealed record MLRetrainResponse(
        [property: JsonPropertyName("status")] string Status,
        [property: JsonPropertyName("model_version")] string? ModelVersion,
        [property: JsonPropertyName("metrics")] MLRetrainMetrics? Metrics,
        [property: JsonPropertyName("message")] string? Message
    );
}
