using HotChocolate.Types;
using ProjectVital.Application.DTOs;

namespace ProjectVital.API.GraphQL.Types;

public class RetrainResultType : ObjectType<RetrainResultDto>
{
    protected override void Configure(IObjectTypeDescriptor<RetrainResultDto> descriptor)
    {
        descriptor.Name("RetrainResult");

        descriptor.Field(r => r.Success).Type<NonNullType<BooleanType>>();
        descriptor.Field(r => r.Message).Type<NonNullType<StringType>>();
        descriptor.Field(r => r.ModelVersion).Type<StringType>();
        descriptor.Field(r => r.F1Score).Type<FloatType>();
        descriptor.Field(r => r.AucRoc).Type<FloatType>();
    }
}
