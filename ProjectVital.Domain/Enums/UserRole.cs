namespace ProjectVital.Domain.Enums;

/// <summary>
/// Papéis (roles) de usuário do sistema.
/// Corresponde ao ENUM('Admin', 'Manager', 'Student') no MySQL.
/// </summary>
public enum UserRole
{
    /// <summary>Acesso total — gerencia academias, planos e usuários.</summary>
    Admin = 0,

    /// <summary>Gestor de uma única academia — escopo limitado à sua AcademyId.</summary>
    Manager = 1,

    /// <summary>Aluno — acesso restrito (placeholder para self-service futuro).</summary>
    Student = 2
}
