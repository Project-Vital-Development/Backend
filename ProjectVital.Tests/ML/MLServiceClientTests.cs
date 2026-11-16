using System.Net;
using System.Text;
using ProjectVital.ML;
using Xunit;

namespace ProjectVital.Tests.ML;

/// <summary>
/// Testa o mapeamento JSON (Python → DTO .NET) do MLServiceClient sem rede:
/// um HttpMessageHandler falso devolve respostas fixas.
/// </summary>
public class MLServiceClientTests
{
    private const string ReportJson = """
    {
      "status": "success",
      "model_version": "xgb_v4",
      "best_algorithm": "xgboost",
      "trained_at": "2026-10-28T12:00:00+00:00",
      "metrics": {"accuracy": 0.81, "f1_score": 0.74, "auc_roc": 0.88, "precision": 0.7, "recall": 0.79},
      "comparison": {
        "xgboost": {"accuracy": 0.81, "f1_score": 0.74, "auc_roc": 0.88, "precision": 0.7, "recall": 0.79}
      },
      "baselines": {
        "regra_inatividade": {"accuracy": 0.77, "f1_score": 0.68, "auc_roc": 0.84, "precision": 0.65, "recall": 0.71},
        "proporcao":         {"accuracy": 0.9,  "f1_score": 0.0,  "auc_roc": 0.5,  "precision": 0.0,  "recall": 0.0}
      },
      "feature_importance": {
        "xgboost": [
          {"feature": "days_since_last_checkin", "importance": 0.21},
          {"feature": "avg_weekly_frequency", "importance": 0.12}
        ]
      },
      "dataset": {"n_students": 240, "n_churn": 70, "churn_rate": 0.2917, "n_train": 192, "n_test": 48},
      "message": "ok"
    }
    """;

    private static MLServiceClient ClientReturning(HttpStatusCode status, string body)
    {
        var http = new HttpClient(new StubHandler(status, body)) { BaseAddress = new Uri("http://ml") };
        return new MLServiceClient(http);
    }

    [Fact]
    public async Task GetModelReportAsync_MapsXGBoostAndReferenceRules()
    {
        var report = await ClientReturning(HttpStatusCode.OK, ReportJson).GetModelReportAsync(1);

        Assert.NotNull(report);
        Assert.True(report!.Success);
        Assert.Equal("xgboost", report.BestAlgorithm);
        Assert.Equal(0.81, report.Accuracy);
        var model = Assert.Single(report.Comparison);
        Assert.Equal("xgboost", model.Algorithm);
        Assert.Equal(0.74, model.F1Score);
        Assert.Equal(2, report.Baselines.Count);
        var rule = Assert.Single(report.Baselines, b => b.Algorithm == "regra_inatividade");
        Assert.Equal(0.68, rule.F1Score);
        Assert.Equal(0.77, rule.Accuracy);
    }

    [Fact]
    public async Task GetModelReportAsync_MapsFeatureImportanceAndDataset()
    {
        var report = await ClientReturning(HttpStatusCode.OK, ReportJson).GetModelReportAsync(1);

        Assert.Equal(2, report!.FeatureImportance.Count);
        Assert.All(report.FeatureImportance, f => Assert.Equal("xgboost", f.Algorithm));
        Assert.Equal(240, report.Dataset!.Students);
        Assert.Equal(48, report.Dataset.TestSize);
    }

    [Fact]
    public async Task GetModelReportAsync_Returns_Null_When_NoTrainingYet()
    {
        var report = await ClientReturning(HttpStatusCode.NotFound, """{"detail":"none"}""").GetModelReportAsync(1);
        Assert.Null(report);
    }

    [Fact]
    public async Task RetrainAsync_ErrorStatus_MapsToUnsuccessful()
    {
        var result = await ClientReturning(HttpStatusCode.OK, """{"status":"error","message":"Dados insuficientes"}""")
            .RetrainAsync(1);

        Assert.False(result.Success);
        Assert.Equal("Dados insuficientes", result.Message);
        Assert.Empty(result.Comparison);
    }

    private sealed class StubHandler(HttpStatusCode status, string body) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) =>
            Task.FromResult(new HttpResponseMessage(status)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json")
            });
    }
}
