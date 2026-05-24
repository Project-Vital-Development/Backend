using HotChocolate.Types;
using ProjectVital.Application.DTOs;

namespace ProjectVital.API.GraphQL.Types;

/// <summary>
/// Define o tipo GraphQL "Prediction" a partir de PredictionDto.
///
/// SCHEMA GERADO:
///   type Prediction {
///     id: ID!
///     studentId: ID!
///     probability: Float!     # 0.0 a 1.0
///     riskLevel: RiskLevel!   # enum: LOW | MEDIUM | HIGH
///     modelVersion: String!   # ex: "rf_v1.2"
///     predictedAt: DateTime!  # UTC
///   }
///
///   enum RiskLevel { LOW MEDIUM HIGH }
///
/// FONTE DE DADOS: PredictionDto (Application/DTOs/PredictionDto.cs)
///   Criado por: PredictionService.RunAsync() e RunBatchAsync()
///   Baseado em: resposta do ML Service Python (MLServiceClient)
///
/// THRESHOLDS DE RISCO (sincronizados em 4 lugares):
///   probability < 0.3  → LOW
///   probability 0.3–0.6 → MEDIUM
///   probability > 0.6  → HIGH
///   SE MUDAR: atualizar RiskLevel.cs (.NET), _classify_risk() (Python),
///   riskConfig (Badge.tsx) e getRiskColor (RiskBar.tsx) no frontend.
///
/// MODELVERSION PODE SER string.Empty:
///   Predições batch (RunBatchAsync) atualmente não recebem a versão do modelo
///   do Python — limitação conhecida. Exibir "desconhecida" no frontend se vazio.
///
/// IMUTABILIDADE:
///   Predições são registros históricos — nunca são atualizadas, apenas criadas.
///   Para "atualizar" o risco de um aluno, cria-se uma nova predição.
///   O histórico completo fica na tabela Predictions; o dashboard mostra a mais recente.
///
/// USADO EM:
///   - StudentType.LatestPrediction (campo aninhado)
///   - PredictionQueryResolver.PredictionsAsync (lista por academia)
///   - PredictionQueryResolver.PredictionAsync (última por aluno)
///   - PredictionMutationResolver.RunPredictionAsync (retorno da mutation)
/// </summary>
public class PredictionType : ObjectType<PredictionDto>
{
    protected override void Configure(IObjectTypeDescriptor<PredictionDto> descriptor)
    {
        descriptor.Name("Prediction");

        descriptor.Field(p => p.Id).Type<NonNullType<IdType>>();
        descriptor.Field(p => p.StudentId).Type<NonNullType<IdType>>();

        // Probabilidade contínua 0.0–1.0 do Python. Maior = maior risco de churn.
        descriptor.Field(p => p.Probability).Type<NonNullType<FloatType>>();

        // Enum derivado de Probability via RiskLevelExtensions.FromProbability().
        // Mudar thresholds em RiskLevel.cs requer atualizar Python e frontend também.
        descriptor.Field(p => p.RiskLevel).Type<NonNullType<EnumType<Domain.Enums.RiskLevel>>>();

        // Formato: "rf_v1.2" ou "xgb_v2.0". Permite rastrear qual modelo gerou a predição.
        // Pode ser "" em predições batch — limitação conhecida.
        descriptor.Field(p => p.ModelVersion).Type<NonNullType<StringType>>();

        // UTC — quando o ML Service processou a predição (não quando foi chamado)
        descriptor.Field(p => p.PredictedAt).Type<NonNullType<DateTimeType>>();
    }
}
