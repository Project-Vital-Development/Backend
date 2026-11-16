using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using ProjectVital.Application.DTOs;
using ProjectVital.Application.Interfaces;
using ProjectVital.Domain.Exceptions;

namespace ProjectVital.ML;

/// <summary>
/// Cliente HTTP do ML Service (FastAPI). Única classe .NET que chama o Python.
///
/// SEGURANÇA: o HttpClient é registrado em Program.cs com o cabeçalho X-Internal-Key
/// (ML_API_KEY). Toda chamada informa a academia; o Python filtra os dados por ela.
///
/// ERROS (convertidos em exceções de domínio, sem repassar detalhes internos):
///   404 → NotFoundException       (aluno não pertence à academia)
///   422 → BusinessRuleException   (histórico insuficiente / dados inválidos)
///   503 → BusinessRuleException   (academia ainda sem modelo treinado)
///   409 → BusinessRuleException   (retreino já em andamento)
///   demais / sem resposta → ServiceUnavailableException
/// </summary>
public sealed class MLServiceClient(HttpClient http) : IMLServiceClient
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        NumberHandling = JsonNumberHandling.AllowReadingFromString,
    };

    public async Task<MLPredictionResult> PredictAsync(int studentId, int academyId, CancellationToken ct = default)
    {
        using var response = await SendAsync(() => http.PostAsJsonAsync("/predict",
            new { student_id = studentId, academy_id = academyId }, ct));
        await EnsureSuccessAsync(response, "/predict", ct);
        var r = await ReadAsync<MLPredictResponse>(response, ct);
        return new MLPredictionResult(r.Probability, r.RiskLevel, r.ModelVersion);
    }

    public async Task<IReadOnlyList<MLBatchPredictionItem>> PredictBatchAsync(int academyId, CancellationToken ct = default)
    {
        using var response = await SendAsync(() => http.PostAsJsonAsync("/predict/batch",
            new { academy_id = academyId }, ct));
        await EnsureSuccessAsync(response, "/predict/batch", ct);
        var items = await ReadAsync<List<MLBatchItemResponse>>(response, ct);
        return items.Select(i => new MLBatchPredictionItem(i.StudentId, i.Probability, i.RiskLevel)
            { ModelVersion = i.ModelVersion ?? string.Empty }).ToList();
    }

    public async Task<RetrainResultDto> RetrainAsync(int academyId, CancellationToken ct = default)
    {
        using var response = await SendAsync(() => http.PostAsJsonAsync("/retrain",
            new { academy_id = academyId }, ct));
        await EnsureSuccessAsync(response, "/retrain", ct);
        return ToDto(await ReadAsync<MLRetrainResponse>(response, ct));
    }

    public async Task<RetrainResultDto?> GetModelReportAsync(int academyId, CancellationToken ct = default)
    {
        using var response = await SendAsync(() => http.GetAsync($"/model/report?academy_id={academyId}", ct));
        if (response.StatusCode == HttpStatusCode.NotFound)
            return null;
        await EnsureSuccessAsync(response, "/model/report", ct);
        return ToDto(await ReadAsync<MLRetrainResponse>(response, ct));
    }

    // ── Infra ────────────────────────────────────────────────────────────────

    private static async Task<HttpResponseMessage> SendAsync(Func<Task<HttpResponseMessage>> send)
    {
        try
        {
            return await send();
        }
        catch (HttpRequestException ex)
        {
            throw new ServiceUnavailableException("Serviço de predição indisponível.", ex);
        }
        catch (TaskCanceledException ex) when (!ex.CancellationToken.IsCancellationRequested)
        {
            throw new ServiceUnavailableException("Serviço de predição não respondeu a tempo.", ex);
        }
    }

    private static async Task EnsureSuccessAsync(HttpResponseMessage response, string route, CancellationToken ct)
    {
        if (response.IsSuccessStatusCode) return;
        var detail = await ReadDetailAsync(response, ct);
        throw response.StatusCode switch
        {
            HttpStatusCode.NotFound => new NotFoundException("Aluno não encontrado nesta academia."),
            HttpStatusCode.UnprocessableEntity => new BusinessRuleException(detail ?? "Dados insuficientes para a predição."),
            HttpStatusCode.ServiceUnavailable => new BusinessRuleException(
                "Esta academia ainda não tem modelo treinado. Treine em Modelos → Retreinar modelos."),
            HttpStatusCode.Conflict => new BusinessRuleException("Já existe um retreino em andamento. Aguarde."),
            _ => new ServiceUnavailableException(
                $"ML Service {route} retornou {(int)response.StatusCode}.", null),
        };
    }

    /// <summary>Lê o campo "detail" do FastAPI (mensagens próprias do serviço, sem dados internos).</summary>
    private static async Task<string?> ReadDetailAsync(HttpResponseMessage response, CancellationToken ct)
    {
        try
        {
            var body = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions, ct);
            return body.TryGetProperty("detail", out var d) && d.ValueKind == JsonValueKind.String ? d.GetString() : null;
        }
        catch (Exception ex) when (ex is JsonException or NotSupportedException or InvalidOperationException)
        {
            return null;
        }
    }

    private static async Task<T> ReadAsync<T>(HttpResponseMessage response, CancellationToken ct)
    {
        try
        {
            return await response.Content.ReadFromJsonAsync<T>(JsonOptions, ct)
                ?? throw new ServiceUnavailableException("ML Service retornou resposta vazia.", null);
        }
        catch (JsonException ex)
        {
            throw new ServiceUnavailableException("ML Service retornou resposta inválida.", ex);
        }
    }

    // ── Mapeamento ───────────────────────────────────────────────────────────

    private static ModelMetricsDto ToMetrics(string name, MLMetrics m) =>
        new(name, m.Accuracy, m.Precision, m.Recall, m.F1Score, m.AucRoc)
        {
            PrAuc = m.PrAuc,
            Brier = m.Brier,
            Threshold = m.Threshold,
            Confusion = m.Confusion is null ? null
                : new ConfusionMatrixDto(m.Confusion.Tn, m.Confusion.Fp, m.Confusion.Fn, m.Confusion.Tp),
            CvF1Mean = m.CvF1Mean,
            CvF1Std = m.CvF1Std,
            CvPrAucMean = m.CvPrAucMean,
            CvPrAucStd = m.CvPrAucStd,
            BestParams = m.BestParams is null ? null : JsonSerializer.Serialize(m.BestParams),
        };

    private static RetrainResultDto ToDto(MLRetrainResponse r)
    {
        var d = r.Dataset;
        return new RetrainResultDto(r.Status == "success", r.Message ?? string.Empty, r.ModelVersion,
            r.Metrics?.F1Score, r.Metrics?.AucRoc)
        {
            Accuracy = r.Metrics?.Accuracy,
            Precision = r.Metrics?.Precision,
            Recall = r.Metrics?.Recall,
            PrAuc = r.Metrics?.PrAuc,
            BestAlgorithm = r.BestAlgorithm,
            SelectionMetric = r.SelectionMetric,
            TrainedAt = r.TrainedAt,
            HorizonDays = r.HorizonDays,
            Threshold = r.Threshold,
            Dataset = d is null ? null
                : new TrainingDatasetDto(d.Students, d.Churned, d.ChurnRate, d.TrainSize, d.TestSize) { Excluded = d.Excluded, Samples = d.Samples, TrainStudents = d.TrainStudents, TestStudents = d.TestStudents },
            Comparison = (r.Comparison ?? []).Select(kv => ToMetrics(kv.Key, kv.Value)).ToList(),
            Baselines = (r.Baselines ?? []).Select(kv => ToMetrics(kv.Key, kv.Value)).ToList(),
            FeatureImportance = (r.FeatureImportance ?? [])
                .SelectMany(kv => kv.Value.Select(i => new FeatureImportanceDto(kv.Key, i.Feature, i.Importance))).ToList(),
            ThresholdAnalysis = (r.ThresholdAnalysis ?? [])
                .SelectMany(kv => kv.Value.Select(t => new ThresholdPointDto(kv.Key, t.Threshold, t.Precision, t.Recall, t.F1Score, t.Flagged))).ToList(),
            Calibration = (r.Calibration ?? [])
                .SelectMany(kv => kv.Value.Select(c => new CalibrationBinDto(kv.Key, c.BinLow, c.BinHigh, c.Count, c.MeanPredicted, c.ObservedRate))).ToList(),
            Versions = (r.Versions ?? []).Select(kv => new KeyValueDto(kv.Key, kv.Value)).ToList(),
        };
    }

    // ── Contratos JSON do Python ─────────────────────────────────────────────

    private sealed record MLPredictResponse(
        [property: JsonPropertyName("probability")] double Probability,
        [property: JsonPropertyName("risk_level")] string RiskLevel,
        [property: JsonPropertyName("model_version")] string ModelVersion);

    private sealed record MLBatchItemResponse(
        [property: JsonPropertyName("student_id")] int StudentId,
        [property: JsonPropertyName("probability")] double Probability,
        [property: JsonPropertyName("risk_level")] string RiskLevel,
        [property: JsonPropertyName("model_version")] string? ModelVersion = null);

    private sealed record MLConfusion(
        [property: JsonPropertyName("tn")] int Tn, [property: JsonPropertyName("fp")] int Fp,
        [property: JsonPropertyName("fn")] int Fn, [property: JsonPropertyName("tp")] int Tp);

    private sealed record MLMetrics(
        [property: JsonPropertyName("f1_score")] double F1Score,
        [property: JsonPropertyName("auc_roc")] double AucRoc,
        [property: JsonPropertyName("precision")] double Precision,
        [property: JsonPropertyName("recall")] double Recall,
        [property: JsonPropertyName("accuracy")] double Accuracy = 0,
        [property: JsonPropertyName("pr_auc")] double PrAuc = 0,
        [property: JsonPropertyName("brier")] double Brier = 0,
        [property: JsonPropertyName("threshold")] double Threshold = 0.5,
        [property: JsonPropertyName("confusion")] MLConfusion? Confusion = null,
        [property: JsonPropertyName("cv_f1_mean")] double? CvF1Mean = null,
        [property: JsonPropertyName("cv_f1_std")] double? CvF1Std = null,
        [property: JsonPropertyName("cv_pr_auc_mean")] double? CvPrAucMean = null,
        [property: JsonPropertyName("cv_pr_auc_std")] double? CvPrAucStd = null,
        [property: JsonPropertyName("best_params")] JsonElement? BestParams = null);

    private sealed record MLFeatureImportance(
        [property: JsonPropertyName("feature")] string Feature,
        [property: JsonPropertyName("importance")] double Importance);

    private sealed record MLThresholdRow(
        [property: JsonPropertyName("threshold")] double Threshold,
        [property: JsonPropertyName("precision")] double Precision,
        [property: JsonPropertyName("recall")] double Recall,
        [property: JsonPropertyName("f1_score")] double F1Score,
        [property: JsonPropertyName("flagged")] int Flagged);

    private sealed record MLCalibrationRow(
        [property: JsonPropertyName("bin_low")] double BinLow,
        [property: JsonPropertyName("bin_high")] double BinHigh,
        [property: JsonPropertyName("count")] int Count,
        [property: JsonPropertyName("mean_predicted")] double MeanPredicted,
        [property: JsonPropertyName("observed_rate")] double ObservedRate);

    private sealed record MLDatasetInfo(
        [property: JsonPropertyName("n_students")] int Students,
        [property: JsonPropertyName("n_churn")] int Churned,
        [property: JsonPropertyName("churn_rate")] double ChurnRate,
        [property: JsonPropertyName("n_train")] int TrainSize,
        [property: JsonPropertyName("n_test")] int TestSize,
        [property: JsonPropertyName("n_excluded")] int Excluded = 0,
        [property: JsonPropertyName("n_samples")] int Samples = 0,
        [property: JsonPropertyName("n_train_students")] int TrainStudents = 0,
        [property: JsonPropertyName("n_test_students")] int TestStudents = 0);

    private sealed record MLRetrainResponse(
        [property: JsonPropertyName("status")] string Status,
        [property: JsonPropertyName("model_version")] string? ModelVersion = null,
        [property: JsonPropertyName("metrics")] MLMetrics? Metrics = null,
        [property: JsonPropertyName("message")] string? Message = null,
        [property: JsonPropertyName("best_algorithm")] string? BestAlgorithm = null,
        [property: JsonPropertyName("selection_metric")] string? SelectionMetric = null,
        [property: JsonPropertyName("trained_at")] string? TrainedAt = null,
        [property: JsonPropertyName("horizon_days")] int? HorizonDays = null,
        [property: JsonPropertyName("threshold")] double? Threshold = null,
        [property: JsonPropertyName("comparison")] Dictionary<string, MLMetrics>? Comparison = null,
        [property: JsonPropertyName("baselines")] Dictionary<string, MLMetrics>? Baselines = null,
        [property: JsonPropertyName("feature_importance")] Dictionary<string, List<MLFeatureImportance>>? FeatureImportance = null,
        [property: JsonPropertyName("threshold_analysis")] Dictionary<string, List<MLThresholdRow>>? ThresholdAnalysis = null,
        [property: JsonPropertyName("calibration")] Dictionary<string, List<MLCalibrationRow>>? Calibration = null,
        [property: JsonPropertyName("dataset")] MLDatasetInfo? Dataset = null,
        [property: JsonPropertyName("versions")] Dictionary<string, string>? Versions = null);
}
