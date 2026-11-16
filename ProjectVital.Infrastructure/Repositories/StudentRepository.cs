using Microsoft.EntityFrameworkCore;
using ProjectVital.Domain.Entities;
using ProjectVital.Domain.Interfaces;
using ProjectVital.Infrastructure.Persistence;

namespace ProjectVital.Infrastructure.Repositories;

/// <summary>
/// Repositório de Student com queries otimizadas para o contexto multi-tenant.
/// Herda operações CRUD base de Repository{Student}.
///
/// PATTERN DE INCLUDE:
///   EF Core não carrega navigation properties automaticamente (lazy loading desativado).
///   Cada método decide quais relações carregar via .Include() conforme necessidade:
///   - GetByAcademyIdAsync → inclui User + Plan (para exibição na listagem)
///   - GetWithRelationsAsync → inclui User + Academy + Plan (para detalhes completos)
///   - GetByUserIdAsync → sem includes (apenas verificação de existência)
///
/// SE ADICIONAR UMA NOVA QUERY:
///   1. Adicionar na interface IStudentRepository em Domain/Interfaces/
///   2. Implementar aqui seguindo o padrão AsNoTracking() + Include()
/// </summary>
public class StudentRepository : Repository<Student>, IStudentRepository
{
    public StudentRepository(ProjectVitalDbContext db) : base(db) { }

    /// <summary>
    /// Lista alunos de uma academia com filtragem opcional por status ativo.
    /// Usada pelo dashboard e pela listagem de alunos do gestor.
    ///
    /// INCLUDES: User (nome/email) + Plan (nome do plano) — necessários para o StudentDto.
    /// NAO INCLUI Academy e CheckIns — desnecessários para listagem.
    ///
    /// FILTRO activeOnly:
    ///   null  → retorna todos (ativos e inativos)
    ///   true  → apenas alunos com Active = true
    ///   false → apenas alunos com Active = false
    ///
    /// ORDENAÇÃO: por nome do usuário (User.Name) para exibição alfabética.
    ///
    /// GLOBAL QUERY FILTER: alunos soft-deletados (DeletedAt != null) são excluídos
    /// automaticamente pelo filtro do DbContext — não precisa filtrar manualmente.
    ///
    /// SE ADICIONAR NOVOS CAMPOS AO StudentDto:
    ///   Adicionar o .Include() correspondente aqui para evitar N+1.
    ///
    /// CHAMADO POR:
    ///   - StudentService.ListByAcademyAsync()
    ///   - AcademyService.GetKPIsAsync() (contagem de ativos/inativos)
    /// </summary>
    public async Task<IReadOnlyList<Student>> GetByAcademyIdAsync(
        int academyId,
        bool? activeOnly = null,
        CancellationToken cancellationToken = default)
    {
        var query = Set
            .AsNoTracking()               // leitura — não precisa de tracking
            .Include(s => s.User)         // carrega Nome e Email do aluno
            .Include(s => s.Plan)         // carrega o Nome do plano
            .Where(s => s.AcademyId == academyId);

        // Aplica filtro de status apenas se fornecido
        if (activeOnly.HasValue)
            query = query.Where(s => s.Active == activeOnly.Value);

        return await query
            .OrderBy(s => s.User.Name)    // alfabético por nome
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Carrega um aluno com TODAS as relações necessárias para operações de detalhes.
    /// Mais completo (e mais lento) que GetByIdAsync() base.
    ///
    /// INCLUDES: User + Academy + Plan
    ///   - User → para nome, email e soft delete em StudentService.DeleteAsync()
    ///   - Academy → para verificação de tenant e exibição
    ///   - Plan → para nome do plano no StudentDto
    ///
    /// UsAndo AsNoTracking() porque o resultado vai para DTO — não precisa de tracking.
    /// EXCEÇÃO: StudentService.DeleteAsync() chama SoftDelete() neste resultado —
    /// em seguida chama UpdateAsync() que faz Set.Update() re-anexando a entidade.
    ///
    /// CHAMADO POR:
    ///   - StudentService.GetByIdAsync()   → detalhe de um aluno
    ///   - StudentService.RegisterAsync()  → carrega após criação para DTO completo
    ///   - StudentService.UpdateAsync()    → carrega após update para DTO completo
    ///   - StudentService.DeleteAsync()    → precisa de Student.User para SoftDelete()
    ///   - StudentMutationResolver.UpdateStudentAsync() → verifica AcademyId antes de operar
    ///   - StudentMutationResolver.DeleteStudentAsync() → verifica AcademyId antes de operar
    /// </summary>
    public async Task<Student?> GetWithRelationsAsync(int id, CancellationToken cancellationToken = default)
    {
        return await Set
            .AsNoTracking()
            .Include(s => s.User)
            .Include(s => s.Academy)
            .Include(s => s.Plan)
            .FirstOrDefaultAsync(s => s.Id == id, cancellationToken);
    }

    /// <summary>
    /// Busca um aluno pelo UserId da conta de login vinculada (relação 1:1).
    /// Sem includes — usado apenas para verificar existência da vinculação.
    ///
    /// CHAMADO POR: uso futuro (self-service de alunos logados).
    /// </summary>
    public async Task<Student?> GetByUserIdAsync(int userId, CancellationToken cancellationToken = default)
    {
        return await Set
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.UserId == userId, cancellationToken);
    }
}
