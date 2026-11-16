using Microsoft.Extensions.Caching.Memory;
using ProjectVital.Domain.Exceptions;

namespace ProjectVital.API.Security;

/// <summary>
/// Intervalo mínimo entre operações caras POR ACADEMIA (ex.: retreino, predição em lote),
/// para que um gestor — ou um token roubado — não esgote a CPU do ML Service.
/// Em memória: adequado a uma réplica.
/// </summary>
public sealed class OperationThrottle(IMemoryCache cache, IConfiguration config)
{
    public void Enforce(string operation, int academyId, TimeSpan defaultInterval)
    {
        var seconds = int.TryParse(config[$"THROTTLE_{operation.ToUpperInvariant()}_SECONDS"], out var s)
            ? s : (int)defaultInterval.TotalSeconds;
        if (seconds <= 0) return;

        var key = $"op:{operation}:{academyId}";
        if (cache.TryGetValue(key, out _))
            throw new TooManyAttemptsException("Operação executada recentemente. Aguarde alguns instantes e tente de novo.");
        cache.Set(key, true, TimeSpan.FromSeconds(seconds));
    }
}
