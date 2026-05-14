using HotChocolate;
using HotChocolate.Authorization;
using HotChocolate.Types;
using Microsoft.AspNetCore.Http;
using ProjectVital.API.GraphQL.Helpers;
using ProjectVital.Application.DTOs;
using ProjectVital.Application.Interfaces;
using ProjectVital.Domain.Exceptions;

namespace ProjectVital.API.GraphQL.Resolvers;

[ExtendObjectType(OperationTypeNames.Query)]
public class CheckInQueryResolver
{
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
        // Verifica que o aluno pertence à academia do token (CRIT-02)
        var student = await studentService.GetByIdAsync(studentId, cancellationToken)
            ?? throw new NotFoundException("Student", studentId);

        ResolverAuthHelper.EnforceAcademyAccess(httpContext, student.AcademyId);
        return await checkInService.ListByStudentAsync(studentId, from, to, cancellationToken);
    }
}

[ExtendObjectType(OperationTypeNames.Mutation)]
public class CheckInMutationResolver
{
    [Authorize]
    public async Task<CheckInDto> CheckInAsync(
        [ID] int studentId,
        [Service] ICheckInService checkInService,
        [Service] IStudentService studentService,
        [Service] IHttpContextAccessor httpContext,
        CancellationToken cancellationToken)
    {
        var student = await studentService.GetByIdAsync(studentId, cancellationToken)
            ?? throw new NotFoundException("Student", studentId);

        ResolverAuthHelper.EnforceAcademyAccess(httpContext, student.AcademyId);
        return await checkInService.RegisterAsync(studentId, cancellationToken);
    }
}
