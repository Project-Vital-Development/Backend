using HotChocolate.Types;
using ProjectVital.Application.DTOs;

namespace ProjectVital.API.GraphQL.Types;

/// <summary>
/// Define o tipo GraphQL "RetrainResult" a partir de RetrainResultDto.
/// Retornado pela mutation "retrainModel" — indica se o retreino do modelo ML teve sucesso.
///
/// SCHEMA GERADO:
///   type RetrainResult {
///     success: Boolean!
///     message: String!
///     modelVersion: String    # nullable — null se o retreino falhou
///     f1Score: Float          # nullable — null se o retreino falhou
///     aucRoc: Float           # nullable — null se o retreino falhou
///   }
///
/// STATUS ATUAL:
///   RetrainModelAsync em PredictionMutationResolver é um PLACEHOLDER.
///   Atualmente retorna Success=false com uma mensagem de "será implementado na Fase 4".
///   Quando implementado, chamará MLServiceClient.RetrainAsync() que chama POST /retrain.
///
/// CAMPOS NULLABLE:
///   ModelVersion, F1Score e AucRoc são nullable porque em caso de falha o Python
///   não retorna essas informações — apenas status="error" e message com o motivo.
///
/// COMO INTERPRETAR NO FRONTEND:
///   if (retrainModel.success) {
///     // mostrar nova versão do modelo e métricas
///     // ex: "Modelo rf_v2 treinado com F1=0.84"
///   } else {
///     // mostrar retrainModel.message como erro
///   }
///
/// MÉTRICAS DISPONÍVEIS:
///   F1Score: balanceamento entre precisão e recall (mais importante para churn)
///   AucRoc: área sob a curva ROC — capacidade de separação das classes
///   Precision e Recall: calculados pelo Python mas não expostos aqui (expandir se necessário)
///
/// FONTE DE DADOS: RetrainResultDto (Application/DTOs/RetrainResultDto.cs)
///   Populado por: MLServiceClient.RetrainAsync() → quando implementado
/// </summary>
public class RetrainResultType : ObjectType<RetrainResultDto>
{
    protected override void Configure(IObjectTypeDescriptor<RetrainResultDto> descriptor)
    {
        descriptor.Name("RetrainResult");

        // true = modelo treinado com sucesso e salvo; false = falhou (ver message)
        descriptor.Field(r => r.Success).Type<NonNullType<BooleanType>>();

        // Mensagem humana: motivo do sucesso ou do erro
        descriptor.Field(r => r.Message).Type<NonNullType<StringType>>();

        // Ex: "rf_v2.0" — null se Success=false
        descriptor.Field(r => r.ModelVersion).Type<StringType>();

        // F1-score do melhor modelo selecionado — null se Success=false
        descriptor.Field(r => r.F1Score).Type<FloatType>();

        // AUC-ROC do melhor modelo — null se Success=false
        descriptor.Field(r => r.AucRoc).Type<FloatType>();
    }
}
