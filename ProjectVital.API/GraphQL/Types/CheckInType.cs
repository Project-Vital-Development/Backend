using HotChocolate.Types;
using ProjectVital.Application.DTOs;

namespace ProjectVital.API.GraphQL.Types;

public class CheckInType : ObjectType<CheckInDto>
{
    protected override void Configure(IObjectTypeDescriptor<CheckInDto> descriptor)
    {
        descriptor.Name("CheckIn");

        descriptor.Field(c => c.Id).Type<NonNullType<IdType>>();
        descriptor.Field(c => c.StudentId).Type<NonNullType<IdType>>();
        descriptor.Field(c => c.Timestamp).Type<NonNullType<DateTimeType>>();
        descriptor.Field(c => c.DurationMinutes).Type<IntType>();
    }
}
