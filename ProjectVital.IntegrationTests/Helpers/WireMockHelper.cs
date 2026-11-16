using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;
using WireMock.Settings;

namespace ProjectVital.IntegrationTests.Helpers;

public class WireMockHelper : IDisposable
{
    public WireMockServer Server { get; }

    public WireMockHelper()
    {
        Server = WireMockServer.Start(new WireMockServerSettings { Port = 9999 });

        // Stub: POST /predict
        Server
            .Given(Request.Create().WithPath("/predict").UsingPost())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "application/json")
                .WithBody("""{"probability": 0.75, "risk_level": "high", "model_version": "test_v1"}"""));

        // Stub: POST /predict/batch
        Server
            .Given(Request.Create().WithPath("/predict/batch").UsingPost())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "application/json")
                .WithBody("""[{"student_id": 1, "probability": 0.75, "risk_level": "high", "model_version": "test_v1"}]"""));

        // Stub: POST /retrain
        Server
            .Given(Request.Create().WithPath("/retrain").UsingPost())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "application/json")
                .WithBody("""{"status": "success", "model_version": "test_v1", "f1_score": 0.85, "auc_roc": 0.91}"""));

        // Stub: GET /health
        Server
            .Given(Request.Create().WithPath("/health").UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "application/json")
                .WithBody("""{"status": "ok", "model_loaded": true, "model_version": "test_v1"}"""));
    }

    public void Dispose() => Server.Stop();
}
