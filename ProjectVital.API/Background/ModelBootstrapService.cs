using Microsoft.EntityFrameworkCore;
using ProjectVital.Application.Interfaces;
using ProjectVital.Infrastructure.Persistence;

namespace ProjectVital.API.Background;

/// <summary>
/// Depois que a API sobe, garante para CADA academia um modelo próprio e predições:
///   - sem relatório de treino (ML novo, volume vazio) ou relatório de versão antiga → retreina;
///   - academia sem predições (ou recém-treinada) → predição em lote.
/// Roda fora de requisições HTTP (escopo de sistema). Falhas são registradas e não derrubam a API.
/// </summary>
public sealed class ModelBootstrapService(
    IServiceScopeFactory scopeFactory,
    ILogger<ModelBootstrapService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);

        List<int> academyIds;
        using (var scope = scopeFactory.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ProjectVitalDbContext>();
            academyIds = await db.Academies.OrderBy(a => a.Id).Select(a => a.Id).ToListAsync(stoppingToken);
        }

        foreach (var academyId in academyIds)
        {
            try
            {
                await EnsureAcademyAsync(academyId, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                logger.LogWarning("Bootstrap do modelo da academia {AcademyId} falhou: {Type}", academyId, ex.GetType().Name);
            }
        }
    }

    private async Task EnsureAcademyAsync(int academyId, CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var ml = scope.ServiceProvider.GetRequiredService<IMLServiceClient>();
        var predictions = scope.ServiceProvider.GetRequiredService<IPredictionService>();
        var db = scope.ServiceProvider.GetRequiredService<ProjectVitalDbContext>();

        // O ML pode levar alguns minutos para subir na plataforma (~5 min de espera).
        Application.DTOs.RetrainResultDto? report = null;
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                report = await ml.GetModelReportAsync(academyId, ct);
                break;
            }
            catch (Domain.Exceptions.ServiceUnavailableException) when (attempt < 60)
            {
                if (attempt % 6 == 1) logger.LogInformation("Aguardando ML Service ({Attempt}/60)...", attempt);
                await Task.Delay(TimeSpan.FromSeconds(5), ct);
            }
        }

        var needsTraining = report is null || report.HorizonDays is null;   // HorizonDays: formato atual
        if (needsTraining)
        {
            var result = await ml.RetrainAsync(academyId, ct);
            if (!result.Success)
            {
                logger.LogInformation("Academia {AcademyId}: treino não realizado — {Message}", academyId, result.Message);
                return;
            }
            logger.LogInformation("Academia {AcademyId}: modelo {Version} ({Algorithm}) F1={F1:F3} PR-AUC={PrAuc:F3}",
                academyId, result.ModelVersion, result.BestAlgorithm, result.F1Score, result.PrAuc);
        }

        var hasPredictions = await db.Predictions.AnyAsync(p => p.Student.AcademyId == academyId, ct);
        if (needsTraining || !hasPredictions)
        {
            var saved = await predictions.RunBatchAsync(academyId, ct);
            logger.LogInformation("Academia {AcademyId}: {Count} predições geradas.", academyId, saved.Count);
        }
    }
}
