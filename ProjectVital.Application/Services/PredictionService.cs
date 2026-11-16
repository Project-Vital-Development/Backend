using ProjectVital.Application.DTOs;
using ProjectVital.Application.Interfaces;
using ProjectVital.Domain.Entities;
using ProjectVital.Domain.Enums;
using ProjectVital.Domain.Exceptions;
using ProjectVital.Domain.Interfaces;

namespace ProjectVital.Application.Services;

/// <summary>
/// Orquestra o fluxo completo de predição de churn:
///   Backend .NET chama ML Service Python → recebe probabilidade → salva no banco.
///
/// ARQUITETURA:
///   Este service é o único responsável por CHAMAR o ML Service.
///   O ML Service nunca é chamado diretamente pelos resolvers — sempre via este service.
///   Frontend → Resolver → PredictionService → IMLServiceClient → FastAPI Python
///
/// DEPENDÊNCIAS:
///   - IPredictionRepository → salvar e buscar predições no MySQL
///   - IStudentRepository    → verificar existência do aluno antes de predizer
///   - IMLServiceClient      → cliente HTTP para o FastAPI Python (implementado em MLServiceClient.cs)
///
/// SE MUDAR AQUI, IMPACTA:
///   - PredictionMutationResolver / PredictionQueryResolver → chamam este service
///   - IMLServiceClient.cs → contrato HTTP (mudar aqui exige mudar o contrato)
///   - MLServiceClient.cs  → implementação HTTP real do IMLServiceClient
///   - predictor.py (Python) → formato da resposta { probability, risk_level, model_version }
/// </summary>
public class PredictionService : IPredictionService
{
    private readonly IPredictionRepository _predictions;
    private readonly IStudentRepository _students;
    private readonly IMLServiceClient _mlClient;

    public PredictionService(
        IPredictionRepository predictions,
        IStudentRepository students,
        IMLServiceClient mlClient)
    {
        _predictions = predictions;
        _students = students;
        _mlClient = mlClient;
    }

    /// <summary>
    /// Gera uma predição individual de churn para um aluno.
    ///
    /// FLUXO COMPLETO:
    ///   1. Verifica que o Student existe no banco
    ///   2. Chama MLServiceClient.PredictAsync(studentId, academyId)
    ///      → HTTP POST /predict ao FastAPI Python com { student_id, academy_id }
    ///   3. Python busca check-ins do aluno no MySQL, calcula 5 features e aplica o modelo
    ///   4. Python retorna { probability, risk_level, model_version }
    ///   5. Backend salva Prediction no MySQL (histórico imutável)
    ///   6. Retorna PredictionDto para o frontend
    ///
    /// PODE FALHAR SE:
    ///   - ML Service offline → HttpRequestException (timeout configurado em Program.cs)
    ///   - Aluno sem check-ins suficientes → ValueError do Python → HTTP 422 → HttpRequestException
    ///   - Modelo não treinado ainda → RuntimeError do Python → HTTP 503 → HttpRequestException
    ///
    /// CHAMADO POR: PredictionMutationResolver.RunPredictionAsync()
    /// </summary>
    public async Task<PredictionDto> RunAsync(int studentId, CancellationToken cancellationToken = default)
    {
        // Verifica existência antes de chamar o ML Service — evita call HTTP desnecessário
        var student = await _students.GetByIdAsync(studentId, cancellationToken)
            ?? throw new NotFoundException("Student", studentId);

        // Chama o ML Service Python via HTTP — pode levar até ML_SERVICE_TIMEOUT_SECONDS
        var mlResult = await _mlClient.PredictAsync(studentId, student.AcademyId, cancellationToken);

        // Classifica a probabilidade em Low/Medium/High usando os thresholds do RiskLevelExtensions
        var prediction = new Prediction
        {
            StudentId = studentId,
            Probability = mlResult.Probability,
            RiskLevel = RiskLevelExtensions.FromModel(mlResult.RiskLevel, mlResult.Probability),
            ModelVersion = mlResult.ModelVersion,
            PredictedAt = DateTime.UtcNow,
        };

        await _predictions.AddAsync(prediction, cancellationToken);
        await _predictions.SaveChangesAsync(cancellationToken);

        return MapToDto(prediction);
    }

    /// <summary>
    /// Gera predições para TODOS os alunos ativos de uma academia em lote.
    ///
    /// FLUXO:
    ///   1. Chama MLServiceClient.PredictBatchAsync(academyId)
    ///      → HTTP POST /predict/batch ao FastAPI Python
    ///   2. Python carrega check-ins de todos os alunos ativos da academia
    ///   3. Calcula features e aplica o modelo para cada aluno em paralelo (numpy/pandas)
    ///   4. Retorna lista de { student_id, probability, risk_level }
    ///   5. Backend salva uma Prediction para cada aluno (loop sequencial)
    ///   6. Único SaveChanges ao final para performance
    ///
    /// NOTA: ModelVersion fica vazio (string.Empty) pois o batch endpoint do Python
    /// retorna risk_level mas não model_version por item. Se precisar, atualizar
    /// o response schema de /predict/batch em main.py e MLBatchItemResponse em MLServiceClient.cs.
    ///
    /// CHAMADO POR: PredictionMutationResolver.RunBatchPredictionAsync()
    /// </summary>
    public async Task<IReadOnlyList<PredictionDto>> RunBatchAsync(int academyId, CancellationToken cancellationToken = default)
    {
        var batchItems = await _mlClient.PredictBatchAsync(academyId, cancellationToken);
        if (batchItems.Count == 0)
            return [];

        // Defesa em profundidade: só grava predições de alunos ATIVOS desta academia,
        // mesmo que o ML devolva algum id inesperado.
        var allowed = (await _students.GetByAcademyIdAsync(academyId, true, cancellationToken))
            .Select(s => s.Id).ToHashSet();
        batchItems = batchItems.Where(i => allowed.Contains(i.StudentId)).ToList();

        var saved = new List<PredictionDto>(batchItems.Count);
        foreach (var item in batchItems)
        {
            var prediction = new Prediction
            {
                StudentId = item.StudentId,
                Probability = item.Probability,
                RiskLevel = RiskLevelExtensions.FromModel(item.RiskLevel, item.Probability),
                ModelVersion = item.ModelVersion,
                PredictedAt = DateTime.UtcNow,
            };

            await _predictions.AddAsync(prediction, cancellationToken);
            saved.Add(MapToDto(prediction));
        }

        // SaveChanges único ao final — mais eficiente que um por aluno
        if (saved.Count > 0)
            await _predictions.SaveChangesAsync(cancellationToken);

        return saved.AsReadOnly();
    }

    /// <summary>
    /// Retorna a predição mais recente de cada aluno de uma academia.
    /// Usada pelo dashboard para exibir o risco atual de todos os alunos.
    /// Uma query SQL eficiente com subquery MAX (veja PredictionRepository).
    ///
    /// CHAMADO POR: PredictionQueryResolver.PredictionsAsync()
    /// </summary>
    public async Task<IReadOnlyList<PredictionDto>> ListLatestByAcademyAsync(
        int academyId,
        CancellationToken cancellationToken = default)
    {
        var predictions = await _predictions.GetLatestByAcademyAsync(academyId, cancellationToken);
        return predictions.Select(p => MapToDto(p)).ToList().AsReadOnly();
    }

    /// <summary>
    /// Retorna a última predição de um aluno específico.
    /// Retorna null se o aluno ainda não foi avaliado pelo ML.
    ///
    /// CHAMADO POR:
    ///   - PredictionQueryResolver.PredictionAsync()
    ///   - StudentService.GetByIdAsync() (inclui no StudentDto)
    /// </summary>
    public async Task<PredictionDto?> GetLatestByStudentAsync(
        int studentId,
        CancellationToken cancellationToken = default)
    {
        var p = await _predictions.GetLatestByStudentAsync(studentId, cancellationToken);
        return p is null ? null : MapToDto(p);
    }

    /// <summary>Converte Prediction em PredictionDto para retorno ao frontend.</summary>
    private static PredictionDto MapToDto(Prediction p) =>
        new(p.Id, p.StudentId, p.Probability, p.RiskLevel, p.ModelVersion, p.PredictedAt);
}
