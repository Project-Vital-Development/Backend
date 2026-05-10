namespace ProjectVital.Domain.Entities;

/// <summary>
/// Aluno matriculado em uma academia.
/// Sujeito principal da análise preditiva de churn.
/// </summary>
public class Student
{
    public int Id { get; set; }

    /// <summary>FK para o User correspondente (Role = Student).</summary>
    public int UserId { get; set; }

    /// <summary>FK para a academia em que o aluno está matriculado.</summary>
    public int AcademyId { get; set; }

    /// <summary>FK para o plano contratado.</summary>
    public int PlanId { get; set; }

    /// <summary>Data de matrícula do aluno.</summary>
    public DateTime EnrollmentDate { get; set; }

    /// <summary>Indica se o aluno está ativo (false = cancelou ou foi desativado).</summary>
    public bool Active { get; set; } = true;

    // ── Navigation Properties ────────────────────────────────────────────────
    public User User { get; set; } = null!;
    public Academy Academy { get; set; } = null!;
    public Plan Plan { get; set; } = null!;

    public ICollection<CheckIn> CheckIns { get; set; } = new List<CheckIn>();
    public ICollection<Prediction> Predictions { get; set; } = new List<Prediction>();
}
