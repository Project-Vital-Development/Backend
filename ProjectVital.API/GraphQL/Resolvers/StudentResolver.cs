using System.Security.Claims;
using HotChocolate;
using HotChocolate.Authorization;
using HotChocolate.Types;
using Microsoft.AspNetCore.Http;
using ProjectVital.Application.DTOs;
using ProjectVital.Application.Inputs;
using ProjectVital.Application.Interfaces;

namespace ProjectVital.API.GraphQL.Resolvers;

/// <summary>
/// Queries de alunos. Manager só pode acessar alunos da sua própria academia.
/// A validação do academyId do token é feita em cada método.
/// </summary>
[ExtendObjectType(OperationTypeNames.Query)]
public class StudentQueryResolver
{
    [Authorize]
    public async Task<IReadOnlyList<StudentDto>> StudentsAsync(
        [ID] int academyId,
        [Service] IStudentService studentService,
        [Service] IHttpContextAccessor httpContext,
        CancellationToken cancellationToken)
    {
        EnforceAcademyAccess(httpContext.HttpContext!, academyId);
        return await studentService.ListByAcademyAsync(academyId, null, cancellationToken);
    }

    [Authorize]
    public async Task<StudentDto?> StudentAsync(
        [ID] int id,
        [Service] IStudentService studentService,
        CancellationToken cancellationToken)
    {
        return await studentService.GetByIdAsync(id, cancellationToken);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Manager só pode acessar a própria academia.
    /// Admin tem acesso irrestrito.
    /// </summary>
    private static void EnforceAcademyAccess(HttpContext context, int requestedAcademyId)
    {
        var role = context.User.FindFirst(ClaimTypes.Role)?.Value;
        if (role == "Admin") return;

        var academyIdClaim = context.User.FindFirst("academyId")?.Value;
        if (academyIdClaim != requestedAcademyId.ToString())
            throw new UnauthorizedAccessException(
                "Acesso negado: você não tem permissão para acessar esta academia.");
    }
}

/// <summary>
/// Mutations de alunos. Manager só pode registrar alunos na própria academia.
/// </summary>
[ExtendObjectType(OperationTypeNames.Mutation)]
public class StudentMutationResolver
{
    [Authorize]
    public async Task<StudentDto> RegisterStudentAsync(
        RegisterStudentInput input,
        [Service] IStudentService studentService,
        [Service] IHttpContextAccessor httpContext,
        CancellationToken cancellationToken)
    {
        EnforceAcademyAccess(httpContext.HttpContext!, input.AcademyId);
        return await studentService.RegisterAsync(input, cancellationToken);
    }

    [Authorize]
    public async Task<StudentDto> UpdateStudentAsync(
        [ID] int id,
        UpdateStudentInput input,
        [Service] IStudentService studentService,
        CancellationToken cancellationToken)
    {
        return await studentService.UpdateAsync(id, input, cancellationToken);
    }

    private static void EnforceAcademyAccess(HttpContext context, int requestedAcademyId)
    {
        var role = context.User.FindFirst(ClaimTypes.Role)?.Value;
        if (role == "Admin") return;

        var academyIdClaim = context.User.FindFirst("academyId")?.Value;
        if (academyIdClaim != requestedAcademyId.ToString())
            throw new UnauthorizedAccessException(
                "Acesso negado: você não tem permissão para acessar esta academia.");
    }
}
