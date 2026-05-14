using HotChocolate.Types;
using ProjectVital.Application.DTOs;

namespace ProjectVital.API.GraphQL.Types;

public class PredictionType : ObjectType<PredictionDto>
{
    protected override void Configure(IObjectTypeDescriptor<PredictionDto> descriptor)
    {
        descriptor.Name("Prediction");

        descriptor.Field(p => p.Id).Type<NonNullType<IdType>>();
        descriptor.Field(p => p.StudentId).Type<NonNullType<IdType>>();
        descriptor.Field(p => p.Probability).Type<NonNullType<FloatType>>();
        descriptor.Field(p => p.RiskLevel).Type<NonNullType<EnumType<Domain.Enums.RiskLevel>>>();
        descriptor.Field(p => p.ModelVersion).Type<NonNullType<StringType>>();
        descriptor.Field(p => p.PredictedAt).Type<NonNullType<DateTimeType>>();
    }
}
