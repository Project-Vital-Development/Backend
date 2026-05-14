using ProjectVital.Domain.Enums;

namespace ProjectVital.Domain.Entities;

public class User
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public UserRole Role { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>LGPD Art.18 — soft delete. Null = ativo; preenchido = excluído logicamente.</summary>
    public DateTime? DeletedAt { get; set; }

    public void SoftDelete() => DeletedAt = DateTime.UtcNow;

    // ── Navigation Properties ────────────────────────────────────────────────
    public Academy? ManagedAcademy { get; set; }
    public Student? Student { get; set; }
}
