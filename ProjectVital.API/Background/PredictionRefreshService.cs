using Microsoft.EntityFrameworkCore;
using ProjectVital.Application.Interfaces;
using ProjectVital.Infrastructure.Persistence;

namespace ProjectVital.API.Background;

/// <summary>
/// Alertas automáticos (objetivo específico "f" do TCC): recalcula periodicamente o
/// risco de evasão de todos os alunos ativos de cada academia, para que o painel
/// do gestor reflita os check-ins mais recentes sem ação manual.
///
/// CONFIGURAÇÃO: PREDICTION_REFRESH_HOURS (padrão 24; 0 desativa).
/// A primeira execução acontece após um intervalo completo — no startup, o seed
/// de demonstração já gera as predições iniciais.
/// </summary>
public sealed class PredictionRefreshService(
    IServiceScopeFactory scopeFactory,
    IConfiguration configuration,
    ILogger<PredictionRefreshService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var hours = double.TryParse(configuration["PREDICTION_REFRESH_HOURS"], out var h) ? h : 24;
        if (hours <= 0)
        {
            logger.LogInformation("Atualização periódica de predições desativada (PREDICTION_REFRESH_HOURS=0).");
            return;
        }

        using var timer = new PeriodicTimer(TimeSpan.FromHours(hours));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            await RefreshAllAcademiesAsync(stoppingToken);
        }
    }

    private async Task RefreshAllAcademiesAsync(CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ProjectVitalDbContext>();
        var predictions = scope.ServiceProvider.GetRequiredService<IPredictionService>();

        var academyIds = await db.Academies.Select(a => a.Id).ToListAsync(ct);
        foreach (var academyId in academyIds)
        {
            try
            {
                var result = await predictions.RunBatchAsync(academyId, ct);
                logger.LogInformation(
                    "Predições atualizadas automaticamente: academia {AcademyId}, {Count} alunos.",
                    academyId, result.Count);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogWarning(
                    "Falha na atualização automática da academia {AcademyId}: {Msg}", academyId, ex.Message);
            }
        }
    }
}
