namespace ProjectVital.Application.DTOs;

/// <summary>
/// Resultado de um treino do modelo de evasão DE UMA ACADEMIA.
/// Retornado pela mutation "retrainModel" e pela query "modelReport".
///
/// Métricas do holdout (25% dos alunos; o mesmo conjunto para o XGBoost e para as regras de
/// referência), calculadas com probabilidades calibradas e com o limiar sugerido (escolhido no
/// treino, nunca no teste). SelectionMetric: métrica que fixou os hiperparâmetros no estudo offline.
/// </summary>
public record RetrainResultDto(
    bool Success,
    string Message,
    string? ModelVersion,
    double? F1Score,
    double? AucRoc
)
{
    public double? Accuracy { get; init; }
    public double? Precision { get; init; }
    public double? Recall { get; init; }
    public double? PrAuc { get; init; }

    /// <summary>Algoritmo do modelo em uso: sempre "xgboost".</summary>
    public string? BestAlgorithm { get; init; }
    public string? SelectionMetric { get; init; }
    public string? TrainedAt { get; init; }

    /// <summary>Horizonte da previsão: probabilidade de cancelar nos próximos N dias.</summary>
    public int? HorizonDays { get; init; }

    /// <summary>Limiar de classificação sugerido para o modelo em uso.</summary>
    public double? Threshold { get; init; }

    public TrainingDatasetDto? Dataset { get; init; }
    public IReadOnlyList<ModelMetricsDto> Comparison { get; init; } = [];
    public IReadOnlyList<ModelMetricsDto> Baselines { get; init; } = [];
    public IReadOnlyList<FeatureImportanceDto> FeatureImportance { get; init; } = [];
    public IReadOnlyList<ThresholdPointDto> ThresholdAnalysis { get; init; } = [];
    public IReadOnlyList<CalibrationBinDto> Calibration { get; init; } = [];
    public IReadOnlyList<KeyValueDto> Versions { get; init; } = [];
}

/// <summary>Métricas de um algoritmo (ou baseline) no holdout + estatísticas da validação cruzada.</summary>
public record ModelMetricsDto(
    string Algorithm,
    double Accuracy,
    double Precision,
    double Recall,
    double F1Score,
    double AucRoc
)
{
    public double PrAuc { get; init; }
    public double Brier { get; init; }
    public double Threshold { get; init; }
    public ConfusionMatrixDto? Confusion { get; init; }
    public double? CvF1Mean { get; init; }
    public double? CvF1Std { get; init; }
    public double? CvPrAucMean { get; init; }
    public double? CvPrAucStd { get; init; }

    /// <summary>Hiperparâmetros escolhidos na validação cruzada (JSON compacto).</summary>
    public string? BestParams { get; init; }
}

public record ConfusionMatrixDto(int TrueNegatives, int FalsePositives, int FalseNegatives, int TruePositives);

/// <summary>Importância de uma variável (queda média da PR-AUC ao embaralhá-la). Associação, não causalidade.</summary>
public record FeatureImportanceDto(string Algorithm, string Feature, double Importance);

/// <summary>Desempenho de um algoritmo em um limiar de classificação.</summary>
public record ThresholdPointDto(string Algorithm, double Threshold, double Precision, double Recall, double F1Score, int Flagged);

/// <summary>Faixa de calibração: probabilidade média prevista × taxa real de cancelamento.</summary>
public record CalibrationBinDto(string Algorithm, double BinLow, double BinHigh, int Count, double MeanPredicted, double ObservedRate);

public record KeyValueDto(string Key, string Value);

/// <summary>
/// Tamanho e balanceamento da base. Students = alunos distintos; TrainSize/TestSize e
/// Churned contam AMOSTRAS (fotografias). Treino e teste são separados por aluno.
/// </summary>
public record TrainingDatasetDto(
    int Students,
    int Churned,
    double ChurnRate,
    int TrainSize,
    int TestSize
)
{
    /// <summary>Alunos fora do treino (pouco tempo de matrícula ou cancelamento sem data).</summary>
    public int Excluded { get; init; }

    /// <summary>Amostras ("fotografias" aluno × data); um aluno gera várias.</summary>
    public int Samples { get; init; }
    public int TrainStudents { get; init; }
    public int TestStudents { get; init; }
}
