using HotChocolate.Types;
using ProjectVital.Application.DTOs;

namespace ProjectVital.API.GraphQL.Types;

public class PlanType : ObjectType<PlanDto>
{
    protected override void Configure(IObjectTypeDescriptor<PlanDto> descriptor)
    {
        descriptor.Name("Plan");
        descriptor.Field(p => p.Id).Type<NonNullType<IdType>>();
        descriptor.Field(p => p.AcademyId).Type<NonNullType<IdType>>();
        descriptor.Field(p => p.Name).Type<NonNullType<StringType>>();
        descriptor.Field(p => p.Price).Type<NonNullType<DecimalType>>();
        descriptor.Field(p => p.DurationDays).Type<NonNullType<IntType>>();
    }
}
