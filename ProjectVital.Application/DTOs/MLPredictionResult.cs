namespace ProjectVital.Application.DTOs;

/// <summary>
/// Resposta do ML Service para POST /predict (predição individual).
///
/// CRIADO POR: MLServiceClient.PredictAsync() — mapeia o JSON do Python para este record.
/// CONSUMIDO POR: PredictionService.RunAsync() — usa os campos para criar uma Prediction no banco.
///
/// RISCO COMO STRING:
///   RiskLevel aqui é string ("low", "medium", "high" — snake_case do Python).
///   PredictionService converte para RiskLevel enum via Enum.Parse antes de salvar.
///   SE O PYTHON MUDAR OS VALORES: atualizar o Enum.Parse em PredictionService.
///
/// MODELVERSION:
///   Formato: "xgb_v{N}" (XGBoost; N = versão do treino da academia).
///   Indica qual versão do modelo gerou esta predição — rastreabilidade.
/// </summary>
public record MLPredictionResult(
    // 0.0 a 1.0 — probabilidade de churn calculada pelo modelo
    double Probability,
    // "low" | "medium" | "high" — string do Python, convertido para RiskLevel enum no service
    string RiskLevel,
    // Ex: "xgb_v3" — versão do modelo que gerou esta predição
    string ModelVersion
);

/// <summary>
/// Um item da lista retornada pelo ML Service em POST /predict/batch.
///
/// CRIADO POR: MLServiceClient.PredictBatchAsync() — mapeia cada item da lista JSON.
/// CONSUMIDO POR: PredictionService.RunBatchAsync() — cria uma Prediction por item.
///
/// DIFERENÇA EM RELAÇÃO A MLPredictionResult:
///   - Inclui StudentId (necessário para saber a qual aluno pertence cada predição)
///   - NÃO inclui ModelVersion (limitação do contrato atual com o Python)
///     → PredictionService usa string.Empty como ModelVersion neste caso
///
/// SE ADICIONAR MODELVERSION AO BATCH:
///   1. Adicionar "model_version" à resposta de /predict/batch no Python (main.py)
///   2. Adicionar ModelVersion ao record MLBatchItemResponse em MLServiceClient.cs
///   3. Adicionar ModelVersion aqui
///   4. Atualizar PredictionService.RunBatchAsync() para usar o campo
/// </summary>
public record MLBatchPredictionItem(
    // Id do aluno — necessário para vincular a predição ao Student correto
    int StudentId,
    // 0.0 a 1.0 — probabilidade de churn
    double Probability,
    // "low" | "medium" | "high" — string do Python, convertido para RiskLevel enum no service
    string RiskLevel
)
{
    /// <summary>Versão do modelo da academia que gerou a predição.</summary>
    public string ModelVersion { get; init; } = string.Empty;
}
