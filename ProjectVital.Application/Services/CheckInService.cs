using ProjectVital.Application.DTOs;
using ProjectVital.Application.Interfaces;
using ProjectVital.Domain.Entities;
using ProjectVital.Domain.Interfaces;

namespace ProjectVital.Application.Services;

/// <summary>
/// Implementação do serviço de check-ins.
/// Valida que o aluno existe e está ativo antes de registrar a entrada.
/// </summary>
public class CheckInService : ICheckInService
{
    private readonly ICheckInRepository _checkIns;
    private readonly IStudentRepository _students;

    public CheckInService(ICheckInRepository checkIns, IStudentRepository students)
    {
        _checkIns = checkIns;
        _students = students;
    }

    /// <summary>
    /// Registra check-in com timestamp UTC atual.
    /// DurationMinutes fica null — o check-out não é suportado nesta versão.
    /// </summary>
    public async Task<CheckInDto> RegisterAsync(int studentId, CancellationToken cancellationToken = default)
    {
        var student = await _students.GetByIdAsync(studentId, cancellationToken)
            ?? throw new KeyNotFoundException($"Aluno com Id={studentId} não encontrado.");

        if (!student.Active)
            throw new InvalidOperationException(
                $"Não é possível registrar check-in para aluno inativo (Id={studentId}).");

        var checkIn = new CheckIn
        {
            StudentId = studentId,
            Timestamp = DateTime.UtcNow
        };

        await _checkIns.AddAsync(checkIn, cancellationToken);
        await _checkIns.SaveChangesAsync(cancellationToken);

        return new CheckInDto(checkIn.Id, checkIn.StudentId, checkIn.Timestamp, checkIn.DurationMinutes);
    }

    public async Task<IReadOnlyList<CheckInDto>> ListByStudentAsync(
        int studentId,
        DateTime? from = null,
        DateTime? to = null,
        CancellationToken cancellationToken = default)
    {
        var checkIns = await _checkIns.GetByStudentAsync(studentId, from, to, cancellationToken);
        return checkIns
            .Select(c => new CheckInDto(c.Id, c.StudentId, c.Timestamp, c.DurationMinutes))
            .ToList()
            .AsReadOnly();
    }
}
