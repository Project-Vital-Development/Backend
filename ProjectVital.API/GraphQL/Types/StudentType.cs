using HotChocolate.Types;
using ProjectVital.Application.DTOs;

namespace ProjectVital.API.GraphQL.Types;

public class StudentType : ObjectType<StudentDto>
{
    protected override void Configure(IObjectTypeDescriptor<StudentDto> descriptor)
    {
        descriptor.Name("Student");

        descriptor.Field(s => s.Id).Type<NonNullType<IdType>>();
        descriptor.Field(s => s.UserId).Type<NonNullType<IdType>>();
        descriptor.Field(s => s.AcademyId).Type<NonNullType<IdType>>();
        descriptor.Field(s => s.PlanId).Type<NonNullType<IdType>>();
        descriptor.Field(s => s.Name).Type<NonNullType<StringType>>();
        descriptor.Field(s => s.Email).Type<NonNullType<StringType>>();
        descriptor.Field(s => s.EnrollmentDate).Type<NonNullType<DateTimeType>>();
        descriptor.Field(s => s.Active).Type<NonNullType<BooleanType>>();
        descriptor.Field(s => s.PlanName).Type<StringType>();
        descriptor.Field(s => s.LatestPrediction).Type<PredictionType>();
    }
}
