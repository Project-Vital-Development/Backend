namespace ProjectVital.Domain.Entities;

public class Student
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public int AcademyId { get; set; }
    public int PlanId { get; set; }
    public DateTime EnrollmentDate { get; set; }
    public bool Active { get; set; } = true;

    /// <summary>LGPD Art.18 — soft delete. Null = ativo; preenchido = excluído logicamente.</summary>
    public DateTime? DeletedAt { get; set; }

    public void SoftDelete() => DeletedAt = DateTime.UtcNow;

    // ── Navigation Properties ────────────────────────────────────────────────
    public User User { get; set; } = null!;
    public Academy Academy { get; set; } = null!;
    public Plan Plan { get; set; } = null!;

    public ICollection<CheckIn> CheckIns { get; set; } = new List<CheckIn>();
    public ICollection<Prediction> Predictions { get; set; } = new List<Prediction>();
}
