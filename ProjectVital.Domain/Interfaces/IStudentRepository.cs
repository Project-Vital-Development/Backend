using ProjectVital.Domain.Entities;

namespace ProjectVital.Domain.Interfaces;

/// <summary>
/// Repositório de alunos. Sempre escopado por academia (multi-tenant).
/// </summary>
public interface IStudentRepository : IRepository<Student>
{
    /// <summary>Lista alunos de uma academia, opcionalmente filtrando por status ativo.</summary>
    Task<IReadOnlyList<Student>> GetByAcademyIdAsync(
        int academyId,
        bool? activeOnly = null,
        CancellationToken cancellationToken = default);

    /// <summary>Carrega um aluno com User, Plan e Academy associados.</summary>
    Task<Student?> GetWithRelationsAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>Busca aluno pelo UserId (relação 1:1).</summary>
    Task<Student?> GetByUserIdAsync(int userId, CancellationToken cancellationToken = default);
}
