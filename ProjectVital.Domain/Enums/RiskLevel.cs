namespace ProjectVital.Domain.Enums;

/// <summary>
/// Nível de risco de churn calculado a partir da probabilidade do modelo ML.
/// Threshold:  &lt; 0.3 = Low | 0.3–0.6 = Medium | &gt; 0.6 = High
/// Corresponde ao ENUM('low', 'medium', 'high') no MySQL.
/// </summary>
public enum RiskLevel
{
    /// <summary>Baixo risco — probabilidade &lt; 0.3.</summary>
    Low = 0,

    /// <summary>Risco médio — probabilidade entre 0.3 e 0.6.</summary>
    Medium = 1,

    /// <summary>Alto risco — probabilidade &gt; 0.6.</summary>
    High = 2
}

/// <summary>
/// Helper estático para classificar uma probabilidade contínua em RiskLevel
/// segundo o threshold do CLAUDE.md.
/// </summary>
public static class RiskLevelExtensions
{
    public static RiskLevel FromProbability(double probability)
    {
        if (probability < 0.3) return RiskLevel.Low;
        if (probability <= 0.6) return RiskLevel.Medium;
        return RiskLevel.High;
    }
}
