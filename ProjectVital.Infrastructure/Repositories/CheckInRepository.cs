using Microsoft.EntityFrameworkCore;
using ProjectVital.Domain.Entities;
using ProjectVital.Domain.Interfaces;
using ProjectVital.Infrastructure.Persistence;

namespace ProjectVital.Infrastructure.Repositories;

/// <summary>
/// Repositório de CheckIn com queries de filtro por data e contagens.
/// Todas as queries aproveitam o índice composto (StudentId, Timestamp) do MySQL.
///
/// PERFORMANCE:
///   O índice composto em CheckIns(StudentId, Timestamp) é CRÍTICO aqui.
///   Sem ele, cada query com filtro de data faria full table scan na tabela
///   que pode ter centenas de milhares de linhas.
///   O índice é criado em CheckInConfiguration.cs via HasIndex().
///
/// SE MUDAR O ÍNDICE:
///   Atualizar CheckInConfiguration.cs e criar nova migration EF Core.
/// </summary>
public class CheckInRepository : Repository<CheckIn>, ICheckInRepository
{
    public CheckInRepository(ProjectVitalDbContext db) : base(db) { }

    /// <summary>
    /// Lista check-ins de um aluno com filtro opcional de data.
    /// Resultado sempre ordenado por Timestamp descendente (mais recente primeiro).
    ///
    /// QUERY GERADA (Pomelo/MySQL):
    ///   SELECT * FROM CheckIns WHERE StudentId = @id
    ///   [AND Timestamp >= @from] [AND Timestamp <= @to]
    ///   ORDER BY Timestamp DESC
    ///   — usa índice composto (StudentId, Timestamp)
    ///
    /// PARÂMETROS from e to são INCLUSIVOS em ambos os lados.
    /// Passar ambos null retorna todo o histórico do aluno.
    ///
    /// CHAMADO POR: CheckInService.ListByStudentAsync()
    /// </summary>
    public async Task<IReadOnlyList<CheckIn>> GetByStudentAsync(
        int studentId,
        DateTime? from = null,
        DateTime? to = null,
        CancellationToken cancellationToken = default)
    {
        var query = Set
            .AsNoTracking()
            .Where(c => c.StudentId == studentId);

        if (from.HasValue)
            query = query.Where(c => c.Timestamp >= from.Value);

        if (to.HasValue)
            query = query.Where(c => c.Timestamp <= to.Value);

        return await query
            .OrderByDescending(c => c.Timestamp) // mais recente primeiro
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Conta check-ins de um aluno específico em um intervalo de datas.
    /// Usa COUNT(*) no banco — não carrega os registros em memória.
    ///
    /// CHAMADO POR: uso futuro em relatórios individuais de frequência.
    /// </summary>
    public async Task<int> CountByStudentAsync(
        int studentId,
        DateTime? from = null,
        DateTime? to = null,
        CancellationToken cancellationToken = default)
    {
        var query = Set.Where(c => c.StudentId == studentId);

        if (from.HasValue) query = query.Where(c => c.Timestamp >= from.Value);
        if (to.HasValue)   query = query.Where(c => c.Timestamp <= to.Value);

        return await query.CountAsync(cancellationToken);
    }

    /// <summary>
    /// Conta check-ins de TODOS os alunos de uma academia em um intervalo de datas.
    /// Usado em GetKPIsAsync() para calcular check-ins da última semana.
    ///
    /// QUERY GERADA (Pomelo/MySQL):
    ///   SELECT COUNT(*) FROM CheckIns c
    ///   INNER JOIN Students s ON c.StudentId = s.Id
    ///   WHERE s.AcademyId = @academyId
    ///   [AND c.Timestamp >= @from] [AND c.Timestamp <= @to]
    ///   — o JOIN implícito via c.Student.AcademyId é gerado pelo Pomelo
    ///
    /// O POMELO gera um JOIN eficiente usando as navigation properties.
    /// O índice (StudentId, Timestamp) ainda é usado para o filtro de data.
    ///
    /// CHAMADO POR: AcademyService.GetKPIsAsync()
    /// </summary>
    public async Task<int> CountByAcademyAsync(
        int academyId,
        DateTime? from = null,
        DateTime? to = null,
        CancellationToken cancellationToken = default)
    {
        // Db.CheckIns em vez de Set para poder navegar via Student.AcademyId no WHERE
        var query = Db.CheckIns
            .Where(c => c.Student.AcademyId == academyId); // JOIN implícito via navigation

        if (from.HasValue) query = query.Where(c => c.Timestamp >= from.Value);
        if (to.HasValue)   query = query.Where(c => c.Timestamp <= to.Value);

        return await query.CountAsync(cancellationToken);
    }
}
