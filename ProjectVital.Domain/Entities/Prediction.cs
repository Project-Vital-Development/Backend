using ProjectVital.Domain.Enums;

namespace ProjectVital.Domain.Entities;

/// <summary>
/// Resultado de uma predição de churn gerada pelo modelo ML para um aluno específico.
/// Cada execução do modelo cria um novo registro — histórico imutável.
/// </summary>
public class Prediction
{
    public int Id { get; set; }

    /// <summary>FK para o aluno avaliado.</summary>
    public int StudentId { get; set; }

    /// <summary>Probabilidade contínua de churn (0.0 a 1.0) retornada pelo modelo.</summary>
    public double Probability { get; set; }

    /// <summary>Classificação categórica derivada da probabilidade (Low/Medium/High).</summary>
    public RiskLevel RiskLevel { get; set; }

    /// <summary>Identificador da versão do modelo que gerou a predição (ex: "rf_v1.2").</summary>
    public string ModelVersion { get; set; } = string.Empty;

    /// <summary>Momento em que a predição foi gerada (UTC).</summary>
    public DateTime PredictedAt { get; set; } = DateTime.UtcNow;

    // ── Navigation Properties ────────────────────────────────────────────────
    public Student Student { get; set; } = null!;
}
