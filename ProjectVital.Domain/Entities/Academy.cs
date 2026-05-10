namespace ProjectVital.Domain.Entities;

/// <summary>
/// Academia cadastrada na plataforma (tenant root).
/// Cada academia tem seu próprio conjunto isolado de alunos, planos e predições.
/// </summary>
public class Academy
{
    public int Id { get; set; }

    /// <summary>Nome comercial da academia.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Endereço completo (opcional).</summary>
    public string? Address { get; set; }

    /// <summary>FK para o User que gerencia esta academia (Role = Manager).</summary>
    public int ManagerId { get; set; }

    /// <summary>Data de criação do registro (UTC).</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // ── Navigation Properties ────────────────────────────────────────────────
    public User Manager { get; set; } = null!;

    public ICollection<Plan> Plans { get; set; } = new List<Plan>();

    public ICollection<Student> Students { get; set; } = new List<Student>();
}
