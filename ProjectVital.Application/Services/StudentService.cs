using ProjectVital.Application.DTOs;
using ProjectVital.Application.Inputs;
using ProjectVital.Application.Interfaces;
using ProjectVital.Domain.Entities;
using ProjectVital.Domain.Enums;
using ProjectVital.Domain.Exceptions;
using ProjectVital.Domain.Interfaces;

namespace ProjectVital.Application.Services;

/// <summary>
/// Centraliza toda a lógica de negócio relacionada a alunos: cadastro, atualização,
/// listagem e exclusão (soft delete LGPD).
///
/// RESPONSABILIDADES:
///   - Validar unicidade de email antes de criar/atualizar
///   - Criar User + Student atomicamente (mesmo SaveChanges)
///   - Mapear entidades de domínio para DTOs de saída
///   - Executar soft delete em User e Student juntos
///
/// DEPENDÊNCIAS:
///   - IStudentRepository  → CRUD de Student
///   - IUserRepository     → CRUD de User (criado junto com Student)
///   - IPredictionRepository → busca última predição para incluir no StudentDto
///   - IAuthService        → HashPassword() para criar a senha do aluno
///
/// SE MUDAR AQUI, IMPACTA:
///   - StudentMutationResolver / StudentQueryResolver → chamam este service
///   - IStudentService.cs → interface que este service implementa (manter sincronizado)
///   - StudentDto.cs      → formato retornado ao frontend
/// </summary>
public class StudentService : IStudentService
{
    private readonly IStudentRepository _students;
    private readonly IUserRepository _users;
    private readonly IPredictionRepository _predictions;
    private readonly IAuthService _auth;

    public StudentService(
        IStudentRepository students,
        IUserRepository users,
        IPredictionRepository predictions,
        IAuthService auth)
    {
        _students = students;
        _users = users;
        _predictions = predictions;
        _auth = auth;
    }

    /// <summary>
    /// Lista todos os alunos de uma academia com seus dados de usuário e plano.
    /// Opcionalmente filtra apenas os ativos (activeOnly=true) ou inativos (false).
    ///
    /// NOTA: não inclui predições — chama GetByIdAsync() para lista individual
    /// com predição. Manter assim para não gerar N+1: a listagem é otimizada
    /// sem joins desnecessários.
    ///
    /// CHAMADO POR: StudentQueryResolver.StudentsAsync()
    /// </summary>
    public async Task<IReadOnlyList<StudentDto>> ListByAcademyAsync(
        int academyId,
        bool? activeOnly = null,
        CancellationToken cancellationToken = default)
    {
        var students = await _students.GetByAcademyIdAsync(academyId, activeOnly, cancellationToken);
        // MapToDto sem predição — predição não é carregada na listagem (performance)
        return students.Select(s => MapToDto(s)).ToList().AsReadOnly();
    }

    /// <summary>
    /// Retorna um aluno com todos os dados relacionados (User, Plan, Academy)
    /// e sua última predição de churn.
    ///
    /// FAZ 2 QUERIES:
    ///   1. StudentRepository.GetWithRelationsAsync() → Student + User + Plan + Academy
    ///   2. PredictionRepository.GetLatestByStudentAsync() → última predição
    ///
    /// Se o aluno não existe, retorna null (o resolver converte em null no GraphQL).
    /// CHAMADO POR: StudentQueryResolver.StudentAsync()
    /// </summary>
    public async Task<StudentDto?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        var student = await _students.GetWithRelationsAsync(id, cancellationToken);
        if (student is null) return null;

        var latestPrediction = await _predictions.GetLatestByStudentAsync(id, cancellationToken);
        var predDto = latestPrediction is null ? null : MapPredictionToDto(latestPrediction);

        return MapToDto(student, predDto);
    }

    /// <summary>
    /// Cria um novo aluno criando User (Role=Student) e Student atomicamente.
    ///
    /// ATOMICIDADE via EF Core:
    ///   Ao atribuir o objeto User à navegação Student.User (em vez de só StudentId),
    ///   o EF Core resolve a FK automaticamente: INSERT User → obtém Id gerado →
    ///   INSERT Student com o UserId correto — tudo em uma única transação.
    ///   Se qualquer INSERT falhar, ambos são revertidos.
    ///
    /// VALIDAÇÕES:
    ///   - Email único: verifica antes de criar → lança BusinessRuleException se duplicado
    ///   - EnrollmentDate: usa DateTime.UtcNow se não fornecido
    ///
    /// SE MUDAR A CRIAÇÃO DE USER:
    ///   - O Role é fixado em UserRole.Student — não exposto como parâmetro.
    ///   - A senha é hasheada por IAuthService.HashPassword() (BCrypt work factor 12).
    ///   - Para alterar o work factor, mudar em AuthService.cs.
    ///
    /// CHAMADO POR: StudentMutationResolver.RegisterStudentAsync()
    /// </summary>
    public async Task<StudentDto> RegisterAsync(RegisterStudentInput input, CancellationToken cancellationToken = default)
    {
        // Validação de email único — verificar ANTES de criar para evitar INSERT parcial
        if (await _users.EmailExistsAsync(input.Email, cancellationToken))
            throw new BusinessRuleException($"E-mail '{input.Email}' já está em uso.");

        var user = new User
        {
            Name = input.Name,
            Email = input.Email,
            PasswordHash = _auth.HashPassword(input.Password),
            Role = UserRole.Student,
            CreatedAt = DateTime.UtcNow
        };

        // Usa propriedade de navegação (User = user) em vez de UserId para que o EF Core
        // resolva o FK automaticamente após o INSERT do User gerar o Id.
        var student = new Student
        {
            User = user,          // ← EF Core extrai UserId desta navegação
            AcademyId = input.AcademyId,
            PlanId = input.PlanId,
            EnrollmentDate = input.EnrollmentDate ?? DateTime.UtcNow,
            Active = true
        };

        await _users.AddAsync(user, cancellationToken);
        await _students.AddAsync(student, cancellationToken);
        // Único SaveChanges: INSERT User → Id gerado → INSERT Student (transação atômica)
        await _students.SaveChangesAsync(cancellationToken);

        // Recarregar com todas as relações para montar o DTO de resposta completo
        var created = await _students.GetWithRelationsAsync(student.Id, cancellationToken)
            ?? throw new BusinessRuleException("Falha ao carregar aluno após criação.");

        return MapToDto(created);
    }

    /// <summary>
    /// Atualiza campos do aluno e/ou seu usuário. Todos os campos são opcionais
    /// (UpdateStudentInput usa tipos nullable) — só atualiza o que foi fornecido.
    ///
    /// CAMPOS ATUALIZÁVEIS:
    ///   - input.Name   → User.Name
    ///   - input.Email  → User.Email (com validação de unicidade)
    ///   - input.PlanId → Student.PlanId
    ///   - input.Active → Student.Active (desativar impede novos check-ins)
    ///
    /// CAMPO NÃO ATUALIZADO: Student.AcademyId — um aluno não muda de academia.
    ///
    /// FAZ 2 SaveChanges SEPARADOS (User e Student em repositórios diferentes).
    /// Se a segunda falhar, a primeira já foi commitada — inconsistência rara mas possível.
    /// Para atomicidade total, precisaria de uma Unit of Work ou transação explícita.
    ///
    /// CHAMADO POR: StudentMutationResolver.UpdateStudentAsync()
    /// </summary>
    public async Task<StudentDto> UpdateAsync(int id, UpdateStudentInput input, CancellationToken cancellationToken = default)
    {
        // Busca o Student base (sem relações — só precisamos dos IDs)
        var student = await _students.GetByIdAsync(id, cancellationToken)
            ?? throw new NotFoundException("Student", id);

        // Busca o User vinculado para atualizar Nome/Email
        var user = await _users.GetByIdAsync(student.UserId, cancellationToken)
            ?? throw new NotFoundException($"Usuário do aluno Id={id} não encontrado.");

        if (input.Name is not null) user.Name = input.Name;

        // Email só atualiza se fornecido E diferente do atual — evita query de unicidade desnecessária
        if (input.Email is not null && input.Email != user.Email)
        {
            if (await _users.EmailExistsAsync(input.Email, cancellationToken))
                throw new BusinessRuleException($"E-mail '{input.Email}' já está em uso.");
            user.Email = input.Email;
        }

        if (input.PlanId.HasValue) student.PlanId = input.PlanId.Value;
        if (input.Active.HasValue) student.Active = input.Active.Value;

        await _users.UpdateAsync(user, cancellationToken);
        await _students.UpdateAsync(student, cancellationToken);
        await _students.SaveChangesAsync(cancellationToken); // salva User e Student (mesmo DbContext)

        // Recarregar com relações para DTO de resposta
        var updated = await _students.GetWithRelationsAsync(id, cancellationToken)
            ?? throw new BusinessRuleException("Falha ao carregar aluno após atualização.");

        return MapToDto(updated);
    }

    /// <summary>
    /// Exclui logicamente o aluno E seu usuário (LGPD Art.18 — direito de exclusão).
    ///
    /// SOFT DELETE:
    ///   Define DeletedAt = DateTime.UtcNow em Student e User.
    ///   O Global Query Filter em ProjectVitalDbContext.OnModelCreating() filtra
    ///   automaticamente registros com DeletedAt != null em todas as queries futuras.
    ///
    /// O QUE PERMANECE NO BANCO:
    ///   - Student e User com DeletedAt preenchido (invisíveis nas queries normais)
    ///   - CheckIns e Predictions do aluno (histórico preservado para auditoria)
    ///
    /// CHAMADO POR: StudentMutationResolver.DeleteStudentAsync()
    /// </summary>
    public async Task DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        // GetWithRelationsAsync carrega Student + User via Include — necessário para chamar SoftDelete() em ambos
        var student = await _students.GetWithRelationsAsync(id, cancellationToken)
            ?? throw new NotFoundException("Student", id);

        student.SoftDelete();       // Student.DeletedAt = DateTime.UtcNow
        student.User.SoftDelete();  // User.DeletedAt = DateTime.UtcNow

        await _students.UpdateAsync(student, cancellationToken);
        await _users.UpdateAsync(student.User, cancellationToken);
        await _students.SaveChangesAsync(cancellationToken); // salva ambos (mesmo DbContext)
    }

    // ── Mapeamento Entidade → DTO ─────────────────────────────────────────────────
    // Funções privadas puras — transformam entidades de domínio em DTOs de saída.
    // Se adicionar novos campos ao StudentDto ou PredictionDto, atualizar aqui.

    /// <summary>
    /// Converte Student (com User e Plan carregados via Include) em StudentDto.
    /// O operador ?. protege contra Student sem User carregado (quando vem de GetByIdAsync básico).
    /// latestPrediction é opcional — null quando não há predições para o aluno.
    /// </summary>
    private static StudentDto MapToDto(
        Domain.Entities.Student s,
        PredictionDto? latestPrediction = null)
        => new(
            s.Id,
            s.UserId,
            s.AcademyId,
            s.PlanId,
            s.User?.Name ?? string.Empty,    // protege se User não foi incluído no Include
            s.User?.Email ?? string.Empty,
            s.EnrollmentDate,
            s.Active,
            s.Plan?.Name,                    // null se Plan não foi incluído no Include
            latestPrediction);

    /// <summary>Converte Prediction em PredictionDto sem dependências externas.</summary>
    private static PredictionDto MapPredictionToDto(Domain.Entities.Prediction p)
        => new(p.Id, p.StudentId, p.Probability, p.RiskLevel, p.ModelVersion, p.PredictedAt);
}
