using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using ProjectVital.Application.DTOs;
using ProjectVital.Application.Interfaces;

namespace ProjectVital.ML;

/// <summary>
/// Cliente HTTP para o ML Service (FastAPI Python).
/// Registrado no DI como IMLServiceClient com HttpClient nomeado.
/// Retry e timeout configurados via IHttpClientFactory no Program.cs.
/// </summary>
public sealed class MLServiceClient : IMLServiceClient
{
    private readonly HttpClient _http;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        NumberHandling = JsonNumberHandling.AllowReadingFromString,
    };

    public MLServiceClient(HttpClient httpClient)
    {
        _http = httpClient;
    }

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

    // ── Internal response DTOs (JSON deserialization) ─────────────────────────

    private sealed record MLPredictResponse(
        [property: JsonPropertyName("probability")] double Probability,
        [property: JsonPropertyName("risk_level")] string RiskLevel,
        [property: JsonPropertyName("model_version")] string ModelVersion
    );

    private sealed record MLBatchItemResponse(
        [property: JsonPropertyName("student_id")] int StudentId,
        [property: JsonPropertyName("probability")] double Probability,
        [property: JsonPropertyName("risk_level")] string RiskLevel
    );

    private sealed record MLRetrainMetrics(
        [property: JsonPropertyName("f1_score")] double F1Score,
        [property: JsonPropertyName("auc_roc")] double AucRoc,
        [property: JsonPropertyName("precision")] double Precision,
        [property: JsonPropertyName("recall")] double Recall
    );

    private sealed record MLRetrainResponse(
        [property: JsonPropertyName("status")] string Status,
        [property: JsonPropertyName("model_version")] string? ModelVersion,
        [property: JsonPropertyName("metrics")] MLRetrainMetrics? Metrics,
        [property: JsonPropertyName("message")] string? Message
    );
}
