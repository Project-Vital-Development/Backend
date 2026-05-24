namespace ProjectVital.Application.DTOs;

/// <summary>
/// DTO de academia — representação da entidade tenant root.
///
/// CRIADO POR: AcademyService.GetByIdAsync()
/// USADO EM: IAcademyService.GetByIdAsync() — não exposto via GraphQL atualmente
///   (não há query "academy(id)" no schema — funcionalidade futura)
///
/// ACADEMIA É O TENANT ROOT:
///   Todos os dados do sistema são isolados por AcademyId.
///   Alunos, planos, check-ins e predições pertencem a uma academia específica.
///
/// SE ADICIONAR QUERY DE ACADEMIA AO GRAPHQL:
///   1. Criar AcademyType.cs em GraphQL/Types/
///   2. Criar AcademyQueryResolver.cs em GraphQL/Resolvers/
///   3. Registrar no Program.cs
///
/// ADDRESS (nullable): endereço físico da academia — pode não ser cadastrado.
/// MANAGERID: FK para User com Role=Manager. Um Manager por academia (modelo atual).
///   SE MUDAR PARA MÚLTIPLOS MANAGERS: alterar o relacionamento na entidade Academy.
/// </summary>
public record AcademyDto(
    int Id,
    string Name,
    // Nullable: endereço físico, opcional no cadastro
    string? Address,
    // Id do User (Role=Manager) que gerencia esta academia
    int ManagerId,
    DateTime CreatedAt
);
