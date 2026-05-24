using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using ProjectVital.Domain.Exceptions;

namespace ProjectVital.API.GraphQL.Helpers;

/// <summary>
/// Helper estático de autorização para os resolvers GraphQL.
/// Centraliza a lógica de tenant isolation em um único lugar para evitar duplicação.
///
/// PROBLEMA QUE RESOLVE:
///   Sem este helper, cada resolver teria 10+ linhas de código para verificar
///   se o Manager pode acessar a academia solicitada. Com o helper, é 1 linha.
///
/// QUANDO USAR:
///   - Em QUALQUER resolver que receba um academyId como parâmetro
///   - Em resolvers que acessam dados de um aluno (para verificar AcademyId do aluno)
///
/// FLUXO TÍPICO NOS RESOLVERS:
///   1. [Authorize] garante que há um token JWT válido
///   2. EnforceAcademyAccess() verifica que o Manager é dono da academia solicitada
///   3. Se tudo ok, chama o service correspondente
///
/// SE MUDAR A ESTRUTURA DO JWT (claims):
///   Atualizar GetAcademyIdFromToken() e EnforceAcademyAccess() que leem por nome de string.
///   Os nomes "academyId" e ClaimTypes.Role devem corresponder aos gerados em AuthService.GenerateToken().
/// </summary>
public static class ResolverAuthHelper
{
    /// <summary>
    /// Extrai o AcademyId do claim JWT do usuário logado.
    /// Lança ForbiddenException se o claim não existir ou for inválido.
    ///
    /// QUANDO USAR: quando o resolver precisa saber QUAL academia o usuário logado gerencia,
    /// sem que o cliente precise enviar o academyId explicitamente.
    ///
    /// EXEMPLO DE USO:
    ///   var myAcademyId = ResolverAuthHelper.GetAcademyIdFromToken(httpContext);
    ///   // myAcademyId == o Id da academia do Manager logado
    ///
    /// CUIDADO: Admins não têm o claim "academyId" — chamar este método com um token
    /// de Admin lançará ForbiddenException. Para Admins, o academyId deve vir como parâmetro.
    /// </summary>
    public static int GetAcademyIdFromToken(IHttpContextAccessor accessor)
    {
        var value = accessor.HttpContext?.User.FindFirst("academyId")?.Value;
        if (value is null || !int.TryParse(value, out var id))
            throw new ForbiddenException("Token não contém academyId válido.");
        return id;
    }

    /// <summary>
    /// Verifica se o usuário logado tem permissão para acessar a academia solicitada.
    /// Lança ForbiddenException se o acesso for negado.
    ///
    /// REGRAS:
    ///   - Admin → acesso irrestrito (retorna imediatamente sem verificar academia)
    ///   - Manager → só pode acessar a academia cujo Id está no claim "academyId" do token
    ///   - Qualquer outro caso → ForbiddenException
    ///
    /// POR QUE ADMIN É IRRESTRITO:
    ///   Admins gerenciam múltiplas academias — eles não têm academyId fixo no token.
    ///   A string "Admin" deve corresponder EXATAMENTE ao UserRole.Admin.ToString().
    ///
    /// CHAMADO POR TODOS OS RESOLVERS QUE RECEBEM academyId:
    ///   - StudentQueryResolver.StudentsAsync()
    ///   - StudentQueryResolver.StudentAsync() (verifica AcademyId do aluno retornado)
    ///   - StudentMutationResolver.RegisterStudentAsync()
    ///   - StudentMutationResolver.UpdateStudentAsync()
    ///   - StudentMutationResolver.DeleteStudentAsync()
    ///   - CheckInQueryResolver.CheckInsAsync()
    ///   - CheckInMutationResolver.CheckInAsync()
    ///   - PredictionQueryResolver.PredictionsAsync()
    ///   - PredictionQueryResolver.PredictionAsync()
    ///   - KPIResolver.AcademyKPIsAsync()
    ///
    /// SE MUDAR O NOME DO CLAIM:
    ///   "academyId" aqui deve ser igual ao usado em AuthService.GenerateToken().
    ///   ClaimTypes.Role aqui deve ser igual ao usado em AuthService.GenerateToken().
    /// </summary>
    public static void EnforceAcademyAccess(IHttpContextAccessor accessor, int requestedAcademyId)
    {
        var ctx = accessor.HttpContext
            ?? throw new ForbiddenException("Contexto HTTP não disponível.");

        var role = ctx.User.FindFirst(ClaimTypes.Role)?.Value;

        // Admin tem acesso irrestrito — não verifica academyId
        if (role == "Admin") return;

        // Manager: o claim "academyId" no token deve corresponder ao academyId solicitado
        var academyIdClaim = ctx.User.FindFirst("academyId")?.Value;
        if (academyIdClaim != requestedAcademyId.ToString())
            throw new ForbiddenException(
                "Acesso negado: você não tem permissão para acessar esta academia.");
    }
}
