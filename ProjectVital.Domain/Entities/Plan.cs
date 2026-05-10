namespace ProjectVital.Domain.Entities;

/// <summary>
/// Plano de mensalidade oferecido por uma academia (ex: Mensal, Trimestral, Anual).
/// </summary>
public class Plan
{
    public int Id { get; set; }

    /// <summary>FK para a academia proprietária do plano.</summary>
    public int AcademyId { get; set; }

    /// <summary>Nome comercial do plano.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Preço mensal/total do plano em moeda local.</summary>
    public decimal Price { get; set; }

    /// <summary>Duração total do plano em dias.</summary>
    public int DurationDays { get; set; }

    // ── Navigation Properties ────────────────────────────────────────────────
    public Academy Academy { get; set; } = null!;

    public ICollection<Student> Students { get; set; } = new List<Student>();
}
