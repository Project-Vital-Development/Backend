using Microsoft.Extensions.Caching.Memory;
using ProjectVital.Domain.Exceptions;

namespace ProjectVital.API.Security;

/// <summary>
/// Proteção contra força bruta no login (em memória — adequado a uma réplica; com várias
/// réplicas, trocar por um cache distribuído).
///   - por e-mail: 5 falhas em 15 min bloqueiam o e-mail por 15 min;
///   - por IP:    20 tentativas em 10 min bloqueiam o IP por 10 min.
/// A resposta de bloqueio é a mesma para e-mails existentes e inexistentes.
/// </summary>
public sealed class LoginThrottle(IMemoryCache cache, IConfiguration config)
{
    private int MaxFailuresPerEmail => int.TryParse(config["LOGIN_MAX_FAILURES_PER_EMAIL"], out var v) ? v : 5;
    private int MaxAttemptsPerIp => int.TryParse(config["LOGIN_MAX_ATTEMPTS_PER_IP"], out var v) ? v : 20;
    private static readonly TimeSpan EmailWindow = TimeSpan.FromMinutes(15);
    private static readonly TimeSpan IpWindow = TimeSpan.FromMinutes(10);

    private sealed class Counter { public int Count; }

    private static string EmailKey(string email) => "login:email:" + email.Trim().ToLowerInvariant();
    private static string IpKey(string ip) => "login:ip:" + ip;

    public void EnsureAllowed(string email, string ip)
    {
        var ipCounter = cache.GetOrCreate(IpKey(ip), e => { e.AbsoluteExpirationRelativeToNow = IpWindow; return new Counter(); })!;
        if (Interlocked.Increment(ref ipCounter.Count) > MaxAttemptsPerIp)
            throw new TooManyAttemptsException("Muitas tentativas de login. Aguarde alguns minutos.");

        if (cache.TryGetValue(EmailKey(email), out Counter? emailCounter) && emailCounter!.Count >= MaxFailuresPerEmail)
            throw new TooManyAttemptsException("Muitas tentativas de login. Aguarde alguns minutos.");
    }

    public void RegisterFailure(string email)
    {
        var counter = cache.GetOrCreate(EmailKey(email), e => { e.AbsoluteExpirationRelativeToNow = EmailWindow; return new Counter(); })!;
        Interlocked.Increment(ref counter.Count);
    }

    public void RegisterSuccess(string email) => cache.Remove(EmailKey(email));
}
