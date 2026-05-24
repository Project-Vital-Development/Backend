using HotChocolate;
using HotChocolate.Authorization;
using HotChocolate.Types;
using Microsoft.AspNetCore.Http;
using ProjectVital.API.GraphQL.Helpers;
using ProjectVital.Application.DTOs;
using ProjectVital.Application.Interfaces;
using ProjectVital.Domain.Exceptions;

namespace ProjectVital.API.GraphQL.Resolvers;

/// <summary>
/// Resolver GraphQL de queries de check-ins.
/// Expõe o field "checkIns" no tipo Query.
///
/// QUERY GraphQL GERADA:
///   query {
///     checkIns(studentId: 42, from: "2024-01-01", to: "2024-12-31") {
///       id timestamp durationMinutes
///     }
///   }
/// </summary>
[ExtendObjectType(OperationTypeNames.Query)]
public class CheckInQueryResolver
{
    /// <summary>
    /// Lista check-ins de um aluno com filtro opcional de período.
    ///
    /// SEGURANÇA (CRIT-02) — verificação de tenant isolation:
    ///   Busca o aluno pelo studentId → obtém AcademyId → verifica acesso.
    ///   Sem isso, um Manager poderia ver check-ins de alunos de outras academias
    ///   se soubesse o studentId.
    ///
    /// PARÂMETROS:
    ///   from, to → nullable DateTime: ambos opcionais.
    ///   Exemplo: from = 30 dias atrás, to = agora → frequência do último mês.
    ///
    /// PERFORMANCE:
    ///   O índice composto (StudentId, Timestamp) garante query rápida mesmo
    ///   com grandes volumes de check-ins.
    /// </summary>
    [Authorize]
    public async Task<IReadOnlyList<CheckInDto>> CheckInsAsync(
        [ID] int studentId,
        DateTime? from,
        DateTime? to,
        [Service] ICheckInService checkInService,
        [Service] IStudentService studentService,
        [Service] IHttpContextAccessor httpContext,
        CancellationToken cancellationToken)
    {
        // Busca aluno para obter AcademyId — necessário para EnforceAcademyAccess
        var student = await studentService.GetByIdAsync(studentId, cancellationToken)
            ?? throw new NotFoundException("Student", studentId);

        // Verifica que o aluno pertence à academia do token logado
        ResolverAuthHelper.EnforceAcademyAccess(httpContext, student.AcademyId);
        return await checkInService.ListByStudentAsync(studentId, from, to, cancellationToken);
    }
}

/// <summary>
/// Resolver GraphQL de mutations de check-ins.
/// Expõe a mutation "checkIn" no tipo Mutation.
///
/// MUTATION GraphQL GERADA:
///   mutation {
///     checkIn(studentId: 42) { id studentId timestamp durationMinutes }
///   }
/// </summary>
[ExtendObjectType(OperationTypeNames.Mutation)]
public class CheckInMutationResolver
{
    /// <summary>
    /// Registra a entrada de um aluno na academia.
    ///
    /// FLUXO:
    ///   1. Busca o aluno para verificar AcademyId (tenant isolation)
    ///   2. EnforceAcademyAccess verifica que o Manager é dono da academia
    ///   3. CheckInService.RegisterAsync() verifica que o aluno está ativo
    ///   4. Insere CheckIn com Timestamp = DateTime.UtcNow
    ///
    /// ERRO ESPERADO:
    ///   Aluno inativo → InvalidOperationException → AppErrorFilter → "BUSINESS_RULE_VIOLATION"
    ///
    /// IMPACTO NO ML:
    ///   Este check-in vai alimentar o modelo de churn na próxima predição.
    ///   Alunos com mais check-ins recentes têm menor probabilidade de churn.
    /// </summary>
    [Authorize]
    public async Task<CheckInDto> CheckInAsync(
        [ID] int studentId,
        [Service] ICheckInService checkInService,
        [Service] IStudentService studentService,
        [Service] IHttpContextAccessor httpContext,
        CancellationToken cancellationToken)
    {
        // Busca primeiro para verificar AcademyId
        var student = await studentService.GetByIdAsync(studentId, cancellationToken)
            ?? throw new NotFoundException("Student", studentId);

        ResolverAuthHelper.EnforceAcademyAccess(httpContext, student.AcademyId);
        return await checkInService.RegisterAsync(studentId, cancellationToken);
    }
}
