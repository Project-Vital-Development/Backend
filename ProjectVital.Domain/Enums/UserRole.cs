namespace ProjectVital.Domain.Enums;

/// <summary>
/// Papéis (roles) de usuário do sistema. Define o nível de acesso no JWT e nos resolvers.
///
/// ONDE É USADO:
///   - AuthService.LoginAsync()              → inclui academyId no token apenas para Manager
///   - AuthService.GenerateToken()           → escreve o claim "role" no JWT
///   - ResolverAuthHelper.EnforceAcademyAccess() → string "Admin" dá acesso irrestrito
///   - StudentService.RegisterAsync()        → força Role=Student ao criar aluno
///
/// COMO FLUI NO JWT:
///   UserRole enum → .ToString() → claim "role" no token → lido por ClaimTypes.Role
///
/// SE ADICIONAR UM NOVO ROLE:
///   1. Adicionar o valor aqui
///   2. Atualizar EnforceAcademyAccess() se o novo role tem acesso especial
///   3. Adicionar lógica em LoginAsync() se o novo role precisa de academyId no token
///   4. Atualizar UserConfiguration se o MySQL ENUM precisa mudar
/// </summary>
public enum UserRole
{
    /// <summary>
    /// Acesso total — pode gerenciar qualquer academia, plano e usuário.
    /// ResolverAuthHelper.EnforceAcademyAccess() retorna sem checar academia para Admin.
    /// Admin NÃO tem academyId no JWT (não está vinculado a uma academia específica).
    /// </summary>
    Admin = 0,

    /// <summary>
    /// Gestor de uma única academia. Escopo limitado à sua AcademyId.
    /// O JWT de um Manager contém o claim "academyId" com o Id da academia gerenciada.
    /// EnforceAcademyAccess() compara este claim com o academyId da operação solicitada.
    /// </summary>
    Manager = 1,

    /// <summary>
    /// Aluno cadastrado. Placeholder para funcionalidade de self-service futuro.
    /// Atualmente alunos não fazem login direto — apenas Managers operam o sistema.
    /// </summary>
    Student = 2
}
