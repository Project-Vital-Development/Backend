using HotChocolate;
using HotChocolate.Types;
using ProjectVital.Application.DTOs;
using ProjectVital.Application.Inputs;
using ProjectVital.Application.Interfaces;

namespace ProjectVital.API.GraphQL.Resolvers;

/// <summary>
/// Resolver de autenticação. A mutation `login` é a única operação pública —
/// não carrega [Authorize] pois é o ponto de entrada para obtenção do token.
/// </summary>
[ExtendObjectType(OperationTypeNames.Mutation)]
public class AuthResolver
{
    public async Task<AuthPayloadDto> LoginAsync(
        string email,
        string password,
        [Service] IAuthService authService,
        CancellationToken cancellationToken)
    {
        return await authService.LoginAsync(new LoginInput(email, password), cancellationToken);
    }
}
