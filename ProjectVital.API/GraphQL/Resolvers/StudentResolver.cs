using HotChocolate;
using HotChocolate.Authorization;
using HotChocolate.Types;
using Microsoft.AspNetCore.Http;
using ProjectVital.API.GraphQL.Helpers;
using ProjectVital.Application.DTOs;
using ProjectVital.Application.Inputs;
using ProjectVital.Application.Interfaces;
using ProjectVital.Domain.Exceptions;

namespace ProjectVital.API.GraphQL.Resolvers;

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
        ResolverAuthHelper.EnforceAcademyAccess(httpContext, academyId);
        return await studentService.ListByAcademyAsync(academyId, null, cancellationToken);
    }

    [Authorize]
    public async Task<StudentDto?> StudentAsync(
        [ID] int id,
        [Service] IStudentService studentService,
        [Service] IHttpContextAccessor httpContext,
        CancellationToken cancellationToken)
    {
        var student = await studentService.GetByIdAsync(id, cancellationToken);
        if (student is null) return null;

        // Verifica que o aluno pertence à academia do token (CRIT-01)
        ResolverAuthHelper.EnforceAcademyAccess(httpContext, student.AcademyId);
        return student;
    }
}

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
        ResolverAuthHelper.EnforceAcademyAccess(httpContext, input.AcademyId);
        return await studentService.RegisterAsync(input, cancellationToken);
    }

    [Authorize]
    public async Task<StudentDto> UpdateStudentAsync(
        [ID] int id,
        UpdateStudentInput input,
        [Service] IStudentService studentService,
        [Service] IHttpContextAccessor httpContext,
        CancellationToken cancellationToken)
    {
        // Busca o aluno primeiro para verificar academia (CRIT-03)
        var existing = await studentService.GetByIdAsync(id, cancellationToken)
            ?? throw new NotFoundException("Student", id);

        ResolverAuthHelper.EnforceAcademyAccess(httpContext, existing.AcademyId);
        return await studentService.UpdateAsync(id, input, cancellationToken);
    }

    [Authorize]
    public async Task<bool> DeleteStudentAsync(
        [ID] int id,
        [Service] IStudentService studentService,
        [Service] IHttpContextAccessor httpContext,
        CancellationToken cancellationToken)
    {
        var existing = await studentService.GetByIdAsync(id, cancellationToken)
            ?? throw new NotFoundException("Student", id);

        ResolverAuthHelper.EnforceAcademyAccess(httpContext, existing.AcademyId);
        await studentService.DeleteAsync(id, cancellationToken);
        return true;
    }
}
