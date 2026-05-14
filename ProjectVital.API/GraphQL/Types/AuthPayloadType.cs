using HotChocolate.Types;
using ProjectVital.Application.DTOs;

namespace ProjectVital.API.GraphQL.Types;

public class UserType : ObjectType<UserDto>
{
    protected override void Configure(IObjectTypeDescriptor<UserDto> descriptor)
    {
        descriptor.Name("User");

        descriptor.Field(u => u.Id).Type<NonNullType<IdType>>();
        descriptor.Field(u => u.Name).Type<NonNullType<StringType>>();
        descriptor.Field(u => u.Email).Type<NonNullType<StringType>>();
        descriptor.Field(u => u.Role).Type<NonNullType<EnumType<Domain.Enums.UserRole>>>();
        descriptor.Field(u => u.CreatedAt).Type<NonNullType<DateTimeType>>();
    }
}

public class AuthPayloadType : ObjectType<AuthPayloadDto>
{
    protected override void Configure(IObjectTypeDescriptor<AuthPayloadDto> descriptor)
    {
        descriptor.Name("AuthPayload");

        descriptor.Field(a => a.Token).Type<NonNullType<StringType>>();
        descriptor.Field(a => a.ExpiresAt).Type<NonNullType<DateTimeType>>();
        descriptor.Field(a => a.User).Type<NonNullType<UserType>>();
        descriptor.Field(a => a.AcademyId).Type<IdType>();
    }
}
