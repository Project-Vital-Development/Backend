using ProjectVital.Application.DTOs;

namespace ProjectVital.Application.Interfaces;

/// <summary>
/// Serviço de predições. Orquestra: chama o ML Service via HTTP → persiste no MySQL → retorna resultado.
/// O ML Service é o ÚNICO local onde inferência ocorre — esta camada apenas integra.
/// </summary>
public interface IPredictionService
{
    /// <summary>Executa uma predição individual para um aluno.</summary>
    Task<PredictionDto> RunAsync(int studentId, CancellationToken cancellationToken = default);

    /// <summary>Executa predições em lote para todos os alunos ativos de uma academia.</summary>
    Task<IReadOnlyList<PredictionDto>> RunBatchAsync(int academyId, CancellationToken cancellationToken = default);

    /// <summary>Lista as últimas predições de cada aluno de uma academia.</summary>
    Task<IReadOnlyList<PredictionDto>> ListLatestByAcademyAsync(int academyId, CancellationToken cancellationToken = default);

    /// <summary>Última predição de um aluno específico.</summary>
    Task<PredictionDto?> GetLatestByStudentAsync(int studentId, CancellationToken cancellationToken = default);
}
