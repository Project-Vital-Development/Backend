using ProjectVital.Domain.Enums;

namespace ProjectVital.Domain.Entities;

/// <summary>
/// Resultado de uma predição de churn gerada pelo modelo ML para um aluno específico.
/// Cada execução do modelo cria um NOVO registro — o histórico é imutável.
/// NUNCA atualizar uma predição existente, apenas inserir novas.
///
/// FLUXO DE CRIAÇÃO:
///   Frontend → GraphQL mutation runPrediction(studentId)
///   → PredictionMutationResolver
///   → PredictionService.RunAsync()
///   → MLServiceClient.PredictAsync()   (HTTP POST /predict ao Python)
///   → Python calcula features + aplica modelo
///   → retorna { probability, risk_level, model_version }
///   → PredictionService salva este registro no MySQL
///   → Frontend exibe no dashboard
///
/// RELAÇÕES:
///   - Prediction (N) → (1) Student : predição pertence a um aluno
///
/// IMPACTO EM CASCATA: se você alterar campos aqui, também precisa atualizar:
///   - PredictionConfiguration.cs   → mapeamento EF Core
///   - PredictionDto.cs             → DTO para o frontend
///   - PredictionRepository.cs      → queries de última predição
///   - PredictionService.cs         → criação do objeto Prediction
///   - MLServiceClient.cs           → mapeamento da resposta Python → Prediction
///   - predictor.py (Python)        → formato da resposta { probability, risk_level, model_version }
/// </summary>
public class Prediction
{
    public int Id { get; set; }

    /// <summary>FK para o aluno avaliado.</summary>
    public int StudentId { get; set; }

    /// <summary>
    /// Probabilidade contínua de churn retornada pelo modelo ML (0.0 a 1.0).
    /// Quanto maior, maior a chance de o aluno cancelar a matrícula.
    /// Convertida em RiskLevel por RiskLevelExtensions.FromProbability().
    /// Se mudar os thresholds de classificação, atualizar:
    ///   - RiskLevelExtensions.FromProbability() (backend .NET)
    ///   - _classify_risk() em predictor.py (Python)
    ///   - riskConfig em Badge.tsx e getRiskColor em RiskBar.tsx (frontend)
    ///   — o nível vem do ML (limiar validado do modelo da academia); FromProbability é só reserva
    /// </summary>
    public double Probability { get; set; }

    /// <summary>
    /// Classificação categórica derivada de Probability via FromProbability().
    /// Armazenada no banco como ENUM('low','medium','high') pelo Pomelo provider.
    /// Usada no dashboard para filtrar e colorir indicadores de risco.
    /// </summary>
    public RiskLevel RiskLevel { get; set; }

    /// <summary>
    /// Identificador da versão do modelo que gerou a predição (ex: "xgb_v2").
    /// Formato: {prefix}_{version}; hoje o prefixo é sempre "xgb" (registros anteriores a
    /// 2026-11 podem ter "rf", do modelo que foi removido).
    /// Permite rastrear qual modelo gerou cada predição — útil para auditoria
    /// e para saber se predições antigas usaram um modelo obsoleto.
    /// Gerado por trainer.py e retornado via MLServiceClient.PredictAsync().
    /// </summary>
    public string ModelVersion { get; set; } = string.Empty;

    /// <summary>Momento em que a predição foi gerada (UTC). Imutável após criação.</summary>
    public DateTime PredictedAt { get; set; } = DateTime.UtcNow;

    // ── Navigation Property ──────────────────────────────────────────────────────
    /// <summary>Aluno avaliado. Carregado via .Include() quando necessário.</summary>
    public Student Student { get; set; } = null!;
}
