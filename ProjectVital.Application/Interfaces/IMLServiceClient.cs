using ProjectVital.Application.DTOs;

namespace ProjectVital.Application.Interfaces;

/// <summary>
/// Contrato de comunicação HTTP com o ML Service (FastAPI Python em http://ml-service:8000).
///
/// IMPORTANTE — ARQUITETURA DE DEPENDÊNCIAS:
///   Esta interface está em ProjectVital.Application (camada de negócio).
///   A implementação concreta (MLServiceClient) está em ProjectVital.ML.
///   Isso segue a Dependency Inversion Principle: Application não depende de ML,
///   mas ambas dependem desta interface.
///
///   Application (define interface) ← ML (implementa)
///   Application (usa interface) → injeta MLServiceClient via DI em runtime
///
/// IMPLEMENTAÇÃO: MLServiceClient.cs (ProjectVital.ML/MLServiceClient.cs)
/// REGISTRADO EM: Program.cs → AddHttpClient&lt;IMLServiceClient, MLServiceClient&gt;()
///   com BaseAddress = ML_SERVICE_URL, timeout e retry configurados via Polly
///
/// CONSUMERS DESTA INTERFACE:
///   - PredictionService.RunAsync() → usa PredictAsync
///   - PredictionService.RunBatchAsync() → usa PredictBatchAsync
///   - PredictionMutationResolver.RetrainModelAsync() → usará RetrainAsync (quando implementado)
///
/// SE O PYTHON MUDAR O CONTRATO (ex: adicionar campo na resposta):
///   1. Atualizar os records privados em MLServiceClient.cs (JsonPropertyName)
///   2. Atualizar os DTOs de retorno (MLPredictionResult, MLBatchPredictionItem, RetrainResultDto)
///   3. Esta interface só muda se a assinatura do método mudar (parâmetros ou tipo de retorno)
///
/// PARA TESTES UNITÁRIOS:
///   Mockar esta interface (ex: com Moq) em vez de depender do ML Service real.
///   Ver PredictionServiceTests.cs para exemplos.
/// </summary>
public interface IMLServiceClient
{
    /// <summary>
    /// Chama POST /predict ao ML Service para predição individual de churn.
    ///
    /// INPUT: studentId + academyId (Python precisa de ambos para buscar check-ins no MySQL)
    /// OUTPUT: MLPredictionResult com Probability (0.0–1.0), RiskLevel string, ModelVersion
    ///
    /// PODE LANÇAR HttpRequestException SE:
    ///   - ML Service offline (ConnectionRefused)
    ///   - Timeout (ML_SERVICE_TIMEOUT_SECONDS)
    ///   - Python retorna HTTP 4xx/5xx (aluno sem dados, modelo não carregado)
    ///
    /// CHAMADO POR: PredictionService.RunAsync()
    /// </summary>
    Task<MLPredictionResult> PredictAsync(int studentId, int academyId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Chama POST /predict/batch para predição de TODOS os alunos ativos da academia.
    ///
    /// Uma chamada HTTP → lista de resultados (mais eficiente que N chamadas individuais).
    /// Python processa todos os alunos com numpy vetorizado internamente.
    ///
    /// RETORNA: lista vazia se a academia não tiver alunos ativos com dados suficientes.
    ///
    /// CHAMADO POR: PredictionService.RunBatchAsync()
    /// </summary>
    Task<IReadOnlyList<MLBatchPredictionItem>> PredictBatchAsync(int academyId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Chama POST /retrain para retreinar o modelo ML para uma academia.
    ///
    /// Python treina Random Forest + XGBoost com dados históricos da academia,
    /// seleciona o melhor por F1-score e salva como novo arquivo versionado.
    /// NUNCA sobrescreve o modelo anterior.
    ///
    /// PODE DEMORAR: retreino é computacionalmente intenso — não chamar em loops.
    /// Recomendado: agendar periodicamente (semanal/mensal).
    ///
    /// NOTA: ainda não integrado ao resolver (ver retrainModel placeholder).
    ///
    /// CHAMADO POR: PredictionMutationResolver.RetrainModelAsync() — quando implementado
    /// </summary>
    Task<RetrainResultDto> RetrainAsync(int academyId, CancellationToken cancellationToken = default);
}
