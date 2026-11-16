using ProjectVital.Application.DTOs;

namespace ProjectVital.Application.Interfaces;

/// <summary>
/// Contrato do serviço de predições de churn.
/// Orquestra a comunicação com o ML Service Python e a persistência das predições.
///
/// IMPLEMENTAÇÃO: PredictionService.cs (Application/Services/PredictionService.cs)
/// REGISTRADO EM: Program.cs → services.AddScoped&lt;IPredictionService, PredictionService&gt;()
///
/// RESPONSABILIDADE DESTA CAMADA:
///   1. Chamar IMLServiceClient (HTTP → Python)
///   2. Mapear resposta Python → entidade Prediction
///   3. Persistir via IPredictionRepository
///   4. Retornar PredictionDto ao resolver
///
/// O PYTHON É O ÚNICO QUE FAZ ML:
///   Este serviço não implementa nenhum algoritmo de ML — apenas orquestra.
///   A lógica de feature engineering e inferência está em ml-service/prediction/predictor.py.
///
/// CONSUMERS DESTA INTERFACE:
///   - PredictionQueryResolver: usa ListLatestByAcademyAsync e GetLatestByStudentAsync
///   - PredictionMutationResolver: usa RunAsync e RunBatchAsync
///
/// SE O ML SERVICE ESTIVER OFFLINE:
///   RunAsync e RunBatchAsync lançam HttpRequestException.
///   AppErrorFilter mapeia para "BUSINESS_RULE_VIOLATION".
///   ListLatestByAcademyAsync e GetLatestByStudentAsync não dependem do Python
///   — leem predições já salvas no banco.
/// </summary>
public interface IPredictionService
{
    /// <summary>
    /// Executa predição de churn para um único aluno.
    ///
    /// FLUXO:
    ///   1. Busca AcademyId do aluno (para enviar ao Python)
    ///   2. Chama IMLServiceClient.PredictAsync(studentId, academyId)
    ///   3. Python calcula features + aplica modelo → probabilidade 0.0–1.0
    ///   4. Salva nova Prediction no banco com PredictedAt = UtcNow
    ///   5. Retorna PredictionDto
    ///
    /// PODE FALHAR COM HttpRequestException SE:
    ///   - ML Service offline ou timeout
    ///   - Aluno sem check-ins (Python retorna HTTP 422)
    ///   - Modelo não treinado (Python retorna HTTP 503)
    ///
    /// CHAMADO POR: PredictionMutationResolver.RunPredictionAsync()
    /// </summary>
    Task<PredictionDto> RunAsync(int studentId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Executa predição de churn para TODOS os alunos ativos de uma academia.
    ///
    /// MAIS EFICIENTE QUE CHAMAR RunAsync N VEZES:
    ///   Python processa em batch com numpy — uma chamada HTTP, múltiplas predições.
    ///   Ideal para atualização periódica do dashboard (diária/semanal).
    ///
    /// LIMITAÇÃO: ModelVersion fica como string.Empty nas predições batch.
    ///   Ver MLServiceClient.PredictBatchAsync() para detalhes.
    ///
    /// CHAMADO POR: PredictionMutationResolver.RunBatchPredictionAsync()
    /// </summary>
    Task<IReadOnlyList<PredictionDto>> RunBatchAsync(int academyId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lista a predição mais recente de cada aluno ativo da academia.
    /// NÃO chama o Python — apenas lê predições já salvas no banco.
    ///
    /// ANTI-N+1: usa query com MAX subquery + JOIN no PredictionRepository.
    ///   Ver PredictionRepository.GetLatestByAcademyAsync() para o SQL gerado.
    ///
    /// ORDENAÇÃO: por Probability DESC (alunos de maior risco primeiro).
    ///
    /// CHAMADO POR: PredictionQueryResolver.PredictionsAsync()
    /// </summary>
    Task<IReadOnlyList<PredictionDto>> ListLatestByAcademyAsync(int academyId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retorna a predição mais recente de um aluno específico.
    /// NÃO chama o Python — apenas lê do banco.
    /// Retorna null se o aluno nunca foi avaliado.
    ///
    /// CHAMADO POR:
    ///   - PredictionQueryResolver.PredictionAsync()
    ///   - StudentRepository.GetWithRelationsAsync() (via navigation → LatestPrediction no StudentDto)
    /// </summary>
    Task<PredictionDto?> GetLatestByStudentAsync(int studentId, CancellationToken cancellationToken = default);
}
