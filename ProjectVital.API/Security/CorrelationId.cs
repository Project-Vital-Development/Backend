using System.Text.RegularExpressions;

namespace ProjectVital.API.Security;

/// <summary>
/// Identificador de correlação: liga a requisição GraphQL, os logs do backend e a chamada
/// ao ML Service. Aceita X-Correlation-ID do cliente (validado) ou gera um novo; devolve no
/// cabeçalho da resposta e o inclui no escopo de log (logs JSON em produção).
/// </summary>
public sealed partial class CorrelationIdMiddleware(RequestDelegate next, ILogger<CorrelationIdMiddleware> logger)
{
    public const string Header = "X-Correlation-ID";

    [GeneratedRegex("^[A-Za-z0-9-]{8,64}$")]
    private static partial Regex Valid();

    public async Task InvokeAsync(HttpContext context)
    {
        var incoming = context.Request.Headers[Header].ToString();
        var id = Valid().IsMatch(incoming) ? incoming : Guid.NewGuid().ToString("N");
        context.TraceIdentifier = id;
        context.Response.Headers[Header] = id;

        using (logger.BeginScope(new Dictionary<string, object> { ["CorrelationId"] = id }))
        {
            var started = System.Diagnostics.Stopwatch.GetTimestamp();
            await next(context);
            // Sem corpo, sem cabeçalhos e sem query string: nada de token ou dado pessoal no log
            logger.LogInformation("{Method} {Path} → {Status} em {ElapsedMs:F0} ms",
                context.Request.Method, context.Request.Path.Value, context.Response.StatusCode,
                System.Diagnostics.Stopwatch.GetElapsedTime(started).TotalMilliseconds);
        }
    }
}

/// <summary>Repassa o identificador de correlação nas chamadas HTTP ao ML Service.</summary>
public sealed class CorrelationIdHandler(IHttpContextAccessor accessor) : DelegatingHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var id = accessor.HttpContext?.TraceIdentifier;
        if (!string.IsNullOrEmpty(id) && !request.Headers.Contains(CorrelationIdMiddleware.Header))
            request.Headers.Add(CorrelationIdMiddleware.Header, id);
        return base.SendAsync(request, cancellationToken);
    }
}
