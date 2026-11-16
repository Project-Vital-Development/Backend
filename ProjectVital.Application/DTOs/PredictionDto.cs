using ProjectVital.Domain.Enums;

namespace ProjectVital.Application.DTOs;

/// <summary>
/// DTO de predição de churn — resultado imutável de uma avaliação do ML Service.
///
/// CRIADO POR:
///   - PredictionService.RunAsync() → predição individual
///   - PredictionService.RunBatchAsync() → predição em lote
///   - PredictionService.GetLatestByStudentAsync() → última predição salva
///   - PredictionService.ListLatestByAcademyAsync() → última de cada aluno
///
/// EXPOSTO COMO TIPO GRAPHQL: PredictionType.cs
/// TAMBÉM USADO COMO CAMPO ANINHADO em StudentDto.LatestPrediction
///
/// PROBABILITY:
///   Valor contínuo 0.0–1.0 calculado pelo modelo ML (XGBoost).
///   Representa a probabilidade de o aluno cancelar a matrícula nos próximos N dias.
///   Quanto maior, maior o risco — exibir como porcentagem no frontend.
///
/// RISKLEVEL:
///   Derivado de Probability via RiskLevelExtensions.FromProbability() em RiskLevel.cs.
///   Nível calculado pelo ML com o limiar validado do modelo da academia
///   (alto ≥ limiar, atenção ≥ limiar/2). Faixas fixas só como reserva (FromProbability).
///   SE MUDAR OS THRESHOLDS: atualizar também _classify_risk() no Python, Badge.tsx e RiskBar.tsx.
///
/// MODELVERSION:
///   Formato: "xgb_v{N}" (XGBoost; N = versão do treino da academia).
///   Permite rastrear qual versão do modelo gerou cada predição.
///   PODE SER string.Empty em predições batch — bug conhecido (ver MLServiceClient.PredictBatchAsync).
///
/// IMUTABILIDADE:
///   Predições são registros históricos — nunca são atualizadas, apenas acumuladas.
///   O dashboard sempre exibe a mais recente (MaxBy PredictedAt).
///
/// PREDATEDAT: sempre UTC — o frontend converte para timezone local.
/// </summary>
public record PredictionDto(
    int Id,
    int StudentId,
    // 0.0 a 1.0 — exibir como "73%" no frontend (Probability * 100)
    double Probability,
    // Enum calculado a partir de Probability via RiskLevelExtensions.FromProbability()
    RiskLevel RiskLevel,
    // "xgb_v3" ou similar. string.Empty em predições batch (limitação conhecida).
    string ModelVersion,
    // UTC — quando o ML Service processou (não quando foi solicitado)
    DateTime PredictedAt
);
