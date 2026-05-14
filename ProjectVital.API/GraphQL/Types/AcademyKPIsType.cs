using HotChocolate.Types;
using ProjectVital.Application.DTOs;

namespace ProjectVital.API.GraphQL.Types;

public class AcademyKPIsType : ObjectType<AcademyKPIsDto>
{
    protected override void Configure(IObjectTypeDescriptor<AcademyKPIsDto> descriptor)
    {
        descriptor.Name("AcademyKPIs");

        descriptor.Field(k => k.AcademyId).Type<NonNullType<IdType>>();
        descriptor.Field(k => k.TotalStudents).Type<NonNullType<IntType>>();
        descriptor.Field(k => k.ActiveStudents).Type<NonNullType<IntType>>();
        descriptor.Field(k => k.HighRiskCount).Type<NonNullType<IntType>>();
        descriptor.Field(k => k.MediumRiskCount).Type<NonNullType<IntType>>();
        descriptor.Field(k => k.LowRiskCount).Type<NonNullType<IntType>>();
        descriptor.Field(k => k.CheckInsThisWeek).Type<NonNullType<IntType>>();
        descriptor.Field(k => k.AverageWeeklyFrequency).Type<NonNullType<FloatType>>();
        descriptor.Field(k => k.GeneratedAt).Type<NonNullType<DateTimeType>>();
    }
}
