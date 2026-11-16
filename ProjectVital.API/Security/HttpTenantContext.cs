using System.Security.Claims;
using ProjectVital.Application.Interfaces;

namespace ProjectVital.API.Security;

/// <summary>
/// Resolve a academia a partir do usuário autenticado da requisição atual.
/// O academyId vem do JWT assinado pelo servidor — nunca de argumentos GraphQL.
///
///   sem HttpContext (seed, BackgroundService)  → sistema (sem filtro)
///   HttpContext + claim academyId              → somente essa academia
///   HttpContext sem claim (anônimo, aluno)     → nenhuma academia (-1)
///
/// Não existe bypass por papel: nenhum fluxo cria usuários Admin, e um papel
/// alterado manualmente no banco não deve abrir todas as academias.
/// </summary>
public sealed class HttpTenantContext(IHttpContextAccessor accessor) : ITenantContext
{
    public const string AcademyClaim = "academyId";

    public bool IsSystem => accessor.HttpContext is null;

    public int AcademyId
    {
        get
        {
            var value = accessor.HttpContext?.User.FindFirstValue(AcademyClaim);
            return int.TryParse(value, out var id) && id > 0 ? id : -1;
        }
    }
}
