namespace ProjectVital.Domain.Enums;

/// <summary>
/// Nível de risco de churn calculado a partir da probabilidade retornada pelo modelo ML.
///
/// THRESHOLDS (definidos em CLAUDE.md e espelhados em 3 lugares — manter sincronizados):
///   probability &lt; 0.3   → Low    (baixo risco)
///   0.3 ≤ probability ≤ 0.6 → Medium (risco médio)
///   probability &gt; 0.6   → High   (alto risco)
///
/// SE MUDAR OS THRESHOLDS, atualizar também:
///   1. RiskLevelExtensions.FromProbability() abaixo (backend .NET)
///   2. _classify_risk() em ml-service/prediction/predictor.py (Python)
///   3. riskConfig em frontend/src/components/ui/Badge.tsx
///   4. getRiskColor() em frontend/src/components/ui/RiskBar.tsx
///
/// Corresponde ao ENUM('low', 'medium', 'high') no MySQL (via PredictionConfiguration).
/// </summary>
public enum RiskLevel
{
    /// <summary>Baixo risco — probabilidade &lt; 0.3. Aluno provavelmente não vai cancelar.</summary>
    Low = 0,

    /// <summary>Risco médio — probabilidade 0.3–0.6. Atenção recomendada.</summary>
    Medium = 1,

    /// <summary>Alto risco — probabilidade &gt; 0.6. Ação de retenção urgente.</summary>
    High = 2
}

/// <summary>
/// Converte uma probabilidade contínua (0.0 a 1.0) no enum RiskLevel correspondente.
/// Centraliza a lógica de classificação no backend — evita duplicação em vários resolvers.
///
/// USADO POR:
///   - PredictionService.RunAsync()      → ao salvar predição individual
///   - PredictionService.RunBatchAsync() → ao salvar predições em lote
///
/// SINCRONIZAÇÃO CRÍTICA: este método DEVE usar os mesmos thresholds que
/// _classify_risk() em predictor.py. Se mudar aqui, mudar lá também.
/// </summary>
public static class RiskLevelExtensions
{
    /// <summary>
    /// Classifica uma probabilidade em Low / Medium / High.
    /// Probability vem diretamente da resposta do ML Service (campo "probability").
    /// </summary>
    /// <param name="probability">Valor de 0.0 a 1.0 retornado pelo modelo ML.</param>
    /// <summary>
    /// Faixas fixas — usadas apenas como reserva quando o ML não informa o nível.
    /// O nível oficial vem do ML (FromModel), calculado com o limiar validado do modelo
    /// de cada academia: alto ≥ limiar; médio ≥ metade do limiar.
    /// </summary>
    public static RiskLevel FromProbability(double probability)
    {
        if (probability < 0.3) return RiskLevel.Low;
        if (probability <= 0.6) return RiskLevel.Medium;
        return RiskLevel.High;
    }

    /// <summary>Converte o risk_level do ML ("low" | "medium" | "high").</summary>
    public static RiskLevel FromModel(string? riskLevel, double probability) => riskLevel?.ToLowerInvariant() switch
    {
        "low" => RiskLevel.Low,
        "medium" => RiskLevel.Medium,
        "high" => RiskLevel.High,
        _ => FromProbability(probability),
    };
}
