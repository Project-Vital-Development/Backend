using HotChocolate;
using HotChocolate.Authorization;
using HotChocolate.Types;
using ProjectVital.Application.DTOs;
using ProjectVital.Application.Interfaces;

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
        CancellationToken cancellationToken)
    {
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
        CancellationToken cancellationToken)
    {
        return await checkInService.RegisterAsync(studentId, cancellationToken);
    }
}
