using HotChocolate.Types;
using ProjectVital.Application.DTOs;

namespace ProjectVital.API.GraphQL.Types;

/// <summary>
/// Define o tipo GraphQL "User" a partir de UserDto.
/// Usado como campo aninhado dentro de AuthPayload — não exposto diretamente
/// como query (não há query "user(id)" por ora).
///
/// SCHEMA GERADO:
///   type User {
///     id: ID!
///     name: String!
///     email: String!
///     role: UserRole!   # enum: ADMIN | MANAGER | STUDENT
///     createdAt: DateTime!
///   }
///
/// FONTE DE DADOS: UserDto (Application/DTOs/UserDto.cs)
///   Populado por: AuthService.LoginAsync() como parte do AuthPayloadDto
///
/// NOTA: PasswordHash NUNCA aparece aqui (nem em UserDto) — por design,
///   a senha é excluída no mapeamento User → UserDto no AuthService.
///
/// SE ADICIONAR CAMPO AO USER:
///   1. Adicionar em User.cs (Domain)
///   2. Adicionar em UserDto.cs (Application)
///   3. Adicionar o mapeamento em AuthService (User → UserDto)
///   4. Adicionar descriptor.Field aqui
/// </summary>
public class UserType : ObjectType<UserDto>
{
    protected override void Configure(IObjectTypeDescriptor<UserDto> descriptor)
    {
        descriptor.Name("User");

        descriptor.Field(u => u.Id).Type<NonNullType<IdType>>();
        descriptor.Field(u => u.Name).Type<NonNullType<StringType>>();
        descriptor.Field(u => u.Email).Type<NonNullType<StringType>>();

        // Determina o que o frontend pode ou não fazer (Admin vs Manager)
        descriptor.Field(u => u.Role).Type<NonNullType<EnumType<Domain.Enums.UserRole>>>();

        descriptor.Field(u => u.CreatedAt).Type<NonNullType<DateTimeType>>();
    }
}

/// <summary>
/// Define o tipo GraphQL "AuthPayload" a partir de AuthPayloadDto.
/// Retornado pela mutation "login" — contém tudo que o frontend precisa após autenticar.
///
/// SCHEMA GERADO:
///   type AuthPayload {
///     token: String!         # JWT Bearer — incluir em todo request futuro
///     expiresAt: DateTime!   # quando o token expira (token + 24h UTC)
///     user: User!            # dados do usuário logado
///     academyId: ID          # nullable — null para Admin, preenchido para Manager
///   }
///
/// FLUXO DE USO NO FRONTEND:
///   1. Chamar mutation login(email, password) → recebe AuthPayload
///   2. Armazenar token em localStorage (ou cookie httpOnly — ver TD-01)
///   3. Armazenar academyId no AuthContext para usar em queries subsequentes
///   4. Apollo Link injeta "Authorization: Bearer {token}" em todo request
///   5. Checar expiresAt para logout preventivo antes de expirar
///
/// POR QUE academyId É NULLABLE:
///   Admins gerenciam múltiplas academias e não têm academyId fixo no token.
///   O frontend deve tratar null exibindo seleção de academia (se implementado).
///   Managers sempre têm academyId preenchido — é o que EnforceAcademyAccess usa.
///
/// FONTE DE DADOS: AuthPayloadDto (Application/DTOs/AuthPayloadDto.cs)
///   Populado por: AuthService.LoginAsync()
/// </summary>
public class AuthPayloadType : ObjectType<AuthPayloadDto>
{
    protected override void Configure(IObjectTypeDescriptor<AuthPayloadDto> descriptor)
    {
        descriptor.Name("AuthPayload");

        // JWT completo — o frontend não deve decodificar ou inspecionar, só enviar
        descriptor.Field(a => a.Token).Type<NonNullType<StringType>>();

        // UTC — frontend pode calcular tempo restante para mostrar aviso de expiração
        descriptor.Field(a => a.ExpiresAt).Type<NonNullType<DateTimeType>>();

        // Dados do usuário logado — evita uma query extra após o login
        descriptor.Field(a => a.User).Type<NonNullType<UserType>>();

        // Nullable: null para Admin, preenchido (int como string GraphQL ID) para Manager
        descriptor.Field(a => a.AcademyId).Type<IdType>();
    }
}
