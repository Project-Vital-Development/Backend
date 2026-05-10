namespace ProjectVital.Domain.Entities;

/// <summary>
/// Registro de entrada de um aluno na academia.
/// É a fonte primária de dados para o modelo de churn prediction.
/// O índice composto (StudentId, Timestamp) é CRÍTICO para feature engineering performática.
/// </summary>
public class CheckIn
{
    public int Id { get; set; }

    /// <summary>FK para o aluno que fez o check-in.</summary>
    public int StudentId { get; set; }

    /// <summary>Momento exato da entrada (UTC).</summary>
    public DateTime Timestamp { get; set; }

    /// <summary>Duração da visita em minutos. Null = check-out não registrado.</summary>
    public int? DurationMinutes { get; set; }

    // ── Navigation Properties ────────────────────────────────────────────────
    public Student Student { get; set; } = null!;
}
