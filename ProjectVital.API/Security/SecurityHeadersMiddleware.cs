namespace ProjectVital.API.Security;

/// <summary>
/// Cabeçalhos de segurança em todas as respostas da API (que só devolve JSON).
/// O frontend (Vercel/nginx) define a sua própria CSP para as páginas.
/// </summary>
public sealed class SecurityHeadersMiddleware(RequestDelegate next, IWebHostEnvironment env)
{
    public Task InvokeAsync(HttpContext context)
    {
        var h = context.Response.Headers;
        h["X-Content-Type-Options"] = "nosniff";
        h["X-Frame-Options"] = "DENY";
        h["Referrer-Policy"] = "no-referrer";
        h["Permissions-Policy"] = "camera=(), microphone=(), geolocation=(), payment=()";
        h["Cross-Origin-Opener-Policy"] = "same-origin";
        h["Cache-Control"] = "no-store";

        // Banana Cake Pop (só em Development) precisa carregar scripts próprios.
        if (!env.IsDevelopment())
            h["Content-Security-Policy"] = "default-src 'none'; frame-ancestors 'none'; base-uri 'none'";

        // HSTS só faz sentido sob HTTPS (o proxy do Railway informa via X-Forwarded-Proto).
        if (context.Request.IsHttps && !env.IsDevelopment())
            h["Strict-Transport-Security"] = "max-age=31536000; includeSubDomains";

        return next(context);
    }
}
