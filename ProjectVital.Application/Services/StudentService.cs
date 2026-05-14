using ProjectVital.Application.DTOs;
using ProjectVital.Application.Inputs;
using ProjectVital.Application.Interfaces;
using ProjectVital.Domain.Entities;
using ProjectVital.Domain.Enums;
using ProjectVital.Domain.Interfaces;

namespace ProjectVital.Application.Services;

/// <summary>
/// Implementação do serviço de alunos.
/// Aplica tenant isolation: Manager só pode operar alunos da sua própria academy.
/// A validação do token (academyId claim) é feita no resolver — este serviço recebe entrada já validada.
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

    public async Task<IReadOnlyList<StudentDto>> ListByAcademyAsync(
        int academyId,
        bool? activeOnly = null,
        CancellationToken cancellationToken = default)
    {
        var students = await _students.GetByAcademyIdAsync(academyId, activeOnly, cancellationToken);
        return students.Select(s => MapToDto(s)).ToList().AsReadOnly();
    }

    public async Task<StudentDto?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        var student = await _students.GetWithRelationsAsync(id, cancellationToken);
        if (student is null) return null;

        var latestPrediction = await _predictions.GetLatestByStudentAsync(id, cancellationToken);
        var predDto = latestPrediction is null ? null : MapPredictionToDto(latestPrediction);

        return MapToDto(student, predDto);
    }

    /// <summary>
    /// Cria um User (Role=Student) e o Student vinculado na mesma transação.
    /// Email único é validado antes para retornar erro legível.
    /// </summary>
    public async Task<StudentDto> RegisterAsync(RegisterStudentInput input, CancellationToken cancellationToken = default)
    {
        if (await _users.EmailExistsAsync(input.Email, cancellationToken))
            throw new InvalidOperationException($"E-mail '{input.Email}' já está em uso.");

        var user = new User
        {
            Name = input.Name,
            Email = input.Email,
            PasswordHash = _auth.HashPassword(input.Password),
            Role = UserRole.Student,
            CreatedAt = DateTime.UtcNow
        };

        await _users.AddAsync(user, cancellationToken);
        await _users.SaveChangesAsync(cancellationToken);

        var student = new Student
        {
            UserId = user.Id,
            AcademyId = input.AcademyId,
            PlanId = input.PlanId,
            EnrollmentDate = input.EnrollmentDate ?? DateTime.UtcNow,
            Active = true
        };

        await _students.AddAsync(student, cancellationToken);
        await _students.SaveChangesAsync(cancellationToken);

        // Recarrega com relações para montar o DTO completo
        var created = await _students.GetWithRelationsAsync(student.Id, cancellationToken)
            ?? throw new InvalidOperationException("Falha ao carregar aluno após criação.");

        return MapToDto(created);
    }

    /// <summary>Partial update — somente campos não-nulos são aplicados.</summary>
    public async Task<StudentDto> UpdateAsync(int id, UpdateStudentInput input, CancellationToken cancellationToken = default)
    {
        var student = await _students.GetByIdAsync(id, cancellationToken)
            ?? throw new KeyNotFoundException($"Aluno com Id={id} não encontrado.");

        var user = await _users.GetByIdAsync(student.UserId, cancellationToken)
            ?? throw new KeyNotFoundException($"Usuário do aluno Id={id} não encontrado.");

        if (input.Name is not null) user.Name = input.Name;

        if (input.Email is not null && input.Email != user.Email)
        {
            if (await _users.EmailExistsAsync(input.Email, cancellationToken))
                throw new InvalidOperationException($"E-mail '{input.Email}' já está em uso.");
            user.Email = input.Email;
        }

        if (input.PlanId.HasValue) student.PlanId = input.PlanId.Value;
        if (input.Active.HasValue) student.Active = input.Active.Value;

        await _users.UpdateAsync(user, cancellationToken);
        await _students.UpdateAsync(student, cancellationToken);
        await _students.SaveChangesAsync(cancellationToken);

        var updated = await _students.GetWithRelationsAsync(id, cancellationToken)
            ?? throw new InvalidOperationException("Falha ao carregar aluno após atualização.");

        return MapToDto(updated);
    }

    // ── Mapeamento ────────────────────────────────────────────────────────────

    private static StudentDto MapToDto(
        Domain.Entities.Student s,
        PredictionDto? latestPrediction = null)
        => new(
            s.Id,
            s.UserId,
            s.AcademyId,
            s.PlanId,
            s.User?.Name ?? string.Empty,
            s.User?.Email ?? string.Empty,
            s.EnrollmentDate,
            s.Active,
            s.Plan?.Name,
            latestPrediction);

    private static PredictionDto MapPredictionToDto(Domain.Entities.Prediction p)
        => new(p.Id, p.StudentId, p.Probability, p.RiskLevel, p.ModelVersion, p.PredictedAt);
}
