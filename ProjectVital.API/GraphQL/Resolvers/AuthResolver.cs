using HotChocolate;
using HotChocolate.Types;
using ProjectVital.API.Security;
using ProjectVital.Application.DTOs;
using ProjectVital.Application.Inputs;
using ProjectVital.Application.Interfaces;

namespace ProjectVital.API.GraphQL.Resolvers;

/// <summary>
/// Mutation pública "login". Única operação sem [Authorize].
/// Protegida por LoginThrottle (força bruta) e por respostas genéricas
/// ("Credenciais inválidas.") que não revelam se o e-mail existe.
/// </summary>
[ExtendObjectType(OperationTypeNames.Mutation)]
public class AuthResolver
{
    public async Task<AuthPayloadDto> LoginAsync(
        string email,
        string password,
        [Service] IAuthService authService,
        [Service] LoginThrottle throttle,
        [Service] IHttpContextAccessor httpContext,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(email) || email.Length > 255 || string.IsNullOrEmpty(password) || password.Length > 128)
            throw new UnauthorizedAccessException("Credenciais inválidas.");

        var ip = httpContext.HttpContext?.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        throttle.EnsureAllowed(email, ip);

        try
        {
            var result = await authService.LoginAsync(new LoginInput(email, password), cancellationToken);
            throttle.RegisterSuccess(email);
            return result;
        }
        catch (UnauthorizedAccessException)
        {
            throttle.RegisterFailure(email);
            throw;
        }
    }
}
