using HotChocolate.Types;
using ProjectVital.Application.DTOs;

namespace ProjectVital.API.GraphQL.Types;

/// <summary>
/// Tipo GraphQL "RetrainResult" — relatório de treino de UMA academia
/// (mutation retrainModel e query modelReport). Campos explícitos: nada além do
/// declarado aqui é exposto no schema.
/// </summary>
public class RetrainResultType : ObjectType<RetrainResultDto>
{
    protected override void Configure(IObjectTypeDescriptor<RetrainResultDto> d)
    {
        d.Name("RetrainResult");
        d.BindFieldsExplicitly();
        d.Field(r => r.Success).Type<NonNullType<BooleanType>>();
        d.Field(r => r.Message).Type<NonNullType<StringType>>();
        d.Field(r => r.ModelVersion);
        d.Field(r => r.BestAlgorithm);
        d.Field(r => r.SelectionMetric);
        d.Field(r => r.TrainedAt);
        d.Field(r => r.HorizonDays);
        d.Field(r => r.Threshold);
        d.Field(r => r.F1Score);
        d.Field(r => r.AucRoc);
        d.Field(r => r.Accuracy);
        d.Field(r => r.Precision);
        d.Field(r => r.Recall);
        d.Field(r => r.PrAuc);
        d.Field(r => r.Dataset).Type<TrainingDatasetType>();
        d.Field(r => r.Comparison).Type<NonNullType<ListType<NonNullType<ModelMetricsType>>>>();
        d.Field(r => r.Baselines).Type<NonNullType<ListType<NonNullType<ModelMetricsType>>>>();
        d.Field(r => r.FeatureImportance).Type<NonNullType<ListType<NonNullType<FeatureImportanceType>>>>();
        d.Field(r => r.ThresholdAnalysis).Type<NonNullType<ListType<NonNullType<ThresholdPointType>>>>();
        d.Field(r => r.Calibration).Type<NonNullType<ListType<NonNullType<CalibrationBinType>>>>();
        d.Field(r => r.Versions).Type<NonNullType<ListType<NonNullType<KeyValueType>>>>();
    }
}

public class ModelMetricsType : ObjectType<ModelMetricsDto>
{
    protected override void Configure(IObjectTypeDescriptor<ModelMetricsDto> d)
    {
        d.Name("ModelMetrics");
        d.BindFieldsExplicitly();
        d.Field(m => m.Algorithm).Type<NonNullType<StringType>>();
        d.Field(m => m.Accuracy);
        d.Field(m => m.Precision);
        d.Field(m => m.Recall);
        d.Field(m => m.F1Score);
        d.Field(m => m.AucRoc);
        d.Field(m => m.PrAuc);
        d.Field(m => m.Brier);
        d.Field(m => m.Threshold);
        d.Field(m => m.Confusion).Type<ConfusionMatrixType>();
        d.Field(m => m.CvF1Mean);
        d.Field(m => m.CvF1Std);
        d.Field(m => m.CvPrAucMean);
        d.Field(m => m.CvPrAucStd);
        d.Field(m => m.BestParams);
    }
}

public class ConfusionMatrixType : ObjectType<ConfusionMatrixDto>
{
    protected override void Configure(IObjectTypeDescriptor<ConfusionMatrixDto> d)
    {
        d.Name("ConfusionMatrix");
        d.BindFieldsExplicitly();
        d.Field(c => c.TrueNegatives);
        d.Field(c => c.FalsePositives);
        d.Field(c => c.FalseNegatives);
        d.Field(c => c.TruePositives);
    }
}

public class FeatureImportanceType : ObjectType<FeatureImportanceDto>
{
    protected override void Configure(IObjectTypeDescriptor<FeatureImportanceDto> d)
    {
        d.Name("FeatureImportance");
        d.BindFieldsExplicitly();
        d.Field(f => f.Algorithm).Type<NonNullType<StringType>>();
        d.Field(f => f.Feature).Type<NonNullType<StringType>>();
        d.Field(f => f.Importance);
    }
}

public class ThresholdPointType : ObjectType<ThresholdPointDto>
{
    protected override void Configure(IObjectTypeDescriptor<ThresholdPointDto> d)
    {
        d.Name("ThresholdPoint");
        d.BindFieldsExplicitly();
        d.Field(t => t.Algorithm).Type<NonNullType<StringType>>();
        d.Field(t => t.Threshold);
        d.Field(t => t.Precision);
        d.Field(t => t.Recall);
        d.Field(t => t.F1Score);
        d.Field(t => t.Flagged);
    }
}

public class CalibrationBinType : ObjectType<CalibrationBinDto>
{
    protected override void Configure(IObjectTypeDescriptor<CalibrationBinDto> d)
    {
        d.Name("CalibrationBin");
        d.BindFieldsExplicitly();
        d.Field(c => c.Algorithm).Type<NonNullType<StringType>>();
        d.Field(c => c.BinLow);
        d.Field(c => c.BinHigh);
        d.Field(c => c.Count);
        d.Field(c => c.MeanPredicted);
        d.Field(c => c.ObservedRate);
    }
}

public class KeyValueType : ObjectType<KeyValueDto>
{
    protected override void Configure(IObjectTypeDescriptor<KeyValueDto> d)
    {
        d.Name("KeyValue");
        d.BindFieldsExplicitly();
        d.Field(k => k.Key).Type<NonNullType<StringType>>();
        d.Field(k => k.Value).Type<NonNullType<StringType>>();
    }
}

public class TrainingDatasetType : ObjectType<TrainingDatasetDto>
{
    protected override void Configure(IObjectTypeDescriptor<TrainingDatasetDto> d)
    {
        d.Name("TrainingDataset");
        d.BindFieldsExplicitly();
        d.Field(x => x.Students);
        d.Field(x => x.Churned);
        d.Field(x => x.ChurnRate);
        d.Field(x => x.TrainSize);
        d.Field(x => x.TestSize);
        d.Field(x => x.Excluded);
        d.Field(x => x.Samples);
        d.Field(x => x.TrainStudents);
        d.Field(x => x.TestStudents);
    }
}
