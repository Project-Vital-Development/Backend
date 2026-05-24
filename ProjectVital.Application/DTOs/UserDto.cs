using ProjectVital.Domain.Enums;

namespace ProjectVital.Application.DTOs;

/// <summary>
/// DTO de usuário — representação segura de User para trafegar entre camadas.
/// PasswordHash é EXCLUÍDO por design — nunca exposto fora da camada de autenticação.
///
/// CRIADO POR: AuthService.LoginAsync() — ao mapear User → AuthPayloadDto.User
/// EXPOSTO COMO TIPO GRAPHQL: UserType (em AuthPayloadType.cs)
/// USADO EM: AuthPayloadDto.User (campo aninhado no payload do login)
///
/// ROLE DETERMINA PERMISSÕES:
///   Admin → acesso irrestrito a todas as academias (ResolverAuthHelper retorna sem verificar)
///   Manager → acesso apenas à academia do seu token JWT (EnforceAcademyAccess verifica)
///   Student → sem acesso à API por enquanto (possível self-service futuro)
///   SE ADICIONAR NOVO ROLE: atualizar UserRole enum, ResolverAuthHelper e as regras de acesso.
///
/// SE ADICIONAR CAMPO AO USER:
///   1. Adicionar em User.cs (Domain entity)
///   2. Adicionar parâmetro aqui
///   3. Popular no mapeamento em AuthService.LoginAsync()
///   4. Adicionar em UserType (dentro de AuthPayloadType.cs) para expor via GraphQL
///   ATENÇÃO: nunca adicionar PasswordHash ou campos sensíveis aqui
/// </summary>
public record UserDto(
    int Id,
    string Name,
    string Email,
    // Determina o que o usuário pode acessar (Admin=tudo, Manager=sua academia)
    UserRole Role,
    DateTime CreatedAt
);
