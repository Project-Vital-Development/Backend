namespace ProjectVital.Application.DTOs;

/// <summary>
/// Resultado da operação de retreinamento do modelo ML.
/// Retornado pela mutation "retrainModel" e criado por MLServiceClient.RetrainAsync().
///
/// CRIADO POR:
///   - MLServiceClient.RetrainAsync() — mapeia resposta JSON do Python
///   - PredictionMutationResolver.RetrainModelAsync() — placeholder atual retorna
///     RetrainResultDto(false, "será implementado na Fase 4", null, null, null)
///
/// EXPOSTO COMO TIPO GRAPHQL: RetrainResultType.cs
///
/// CAMPOS NULLABLE:
///   ModelVersion, F1Score e AucRoc são null quando Success=false.
///   O Python não retorna esses campos em caso de falha no retreino.
///
/// COMO INTERPRETAR:
///   Success=true  → modelo retreinado e salvo. ModelVersion é a nova versão.
///                   F1Score e AucRoc indicam a qualidade do modelo.
///   Success=false → Message explica o motivo da falha.
///                   Causas comuns: dados insuficientes, erro no Python, academia vazia.
///
/// F1SCORE (métrica principal para churn):
///   Harmônica entre precisão e recall — mais relevante que accuracy para classes desbalanceadas.
///   Valor ideal > 0.80. Abaixo de 0.70 indica dados insuficientes ou features inadequadas.
///
/// AUCROC:
///   Área sob a curva ROC — capacidade do modelo de separar churners de não-churners.
///   0.5 = aleatório, 1.0 = perfeito. Ideal > 0.85 para produção.
/// </summary>
public record RetrainResultDto(
    // true = modelo treinado com sucesso; false = falhou (ver Message)
    bool Success,
    // Mensagem humana — motivo do sucesso ou do erro
    string Message,
    // "rf_v2.0" ou "xgb_v1.3" — null se Success=false
    string? ModelVersion,
    // F1-score do melhor modelo (0.0–1.0) — null se Success=false
    double? F1Score,
    // AUC-ROC do melhor modelo (0.0–1.0) — null se Success=false
    double? AucRoc
);
