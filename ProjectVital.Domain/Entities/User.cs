using ProjectVital.Domain.Enums;

namespace ProjectVital.Domain.Entities;

/// <summary>
/// Conta de acesso ao sistema. Pode representar um Admin, Manager (gestor de academia)
/// ou Student (aluno — placeholder para self-service futuro).
/// </summary>
public class User
{
    public int Id { get; set; }

    /// <summary>Nome completo do usuário.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Email único — usado como credencial de login.</summary>
    public string Email { get; set; } = string.Empty;

    /// <summary>Hash BCrypt da senha (work factor &gt;= 12). Nunca armazenar texto plano.</summary>
    public string PasswordHash { get; set; } = string.Empty;

    /// <summary>Papel do usuário no sistema.</summary>
    public UserRole Role { get; set; }

    /// <summary>Data de criação do registro (UTC).</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // ── Navigation Properties ────────────────────────────────────────────────
    /// <summary>Academia gerenciada (válido apenas quando Role = Manager).</summary>
    public Academy? ManagedAcademy { get; set; }

    /// <summary>Aluno associado (válido apenas quando Role = Student).</summary>
    public Student? Student { get; set; }
}
