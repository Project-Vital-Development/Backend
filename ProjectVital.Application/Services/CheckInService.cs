using System.Collections.Concurrent;
using ProjectVital.Application.DTOs;
using ProjectVital.Application.Interfaces;
using ProjectVital.Domain.Entities;
using ProjectVital.Domain.Exceptions;
using ProjectVital.Domain.Interfaces;

namespace ProjectVital.Application.Services;

/// <summary>
/// Entradas (check-in) e saídas (check-out) de alunos.
///
/// REGRAS
///   - Só alunos ATIVOS registram entrada.
///   - Entrada duplicada: se o aluno já tem uma entrada SEM saída nas últimas
///     <see cref="OpenVisitWindow"/>, a nova entrada é recusada (clique duplo, catraca
///     repetida ou requisições simultâneas). Um lock por aluno serializa as requisições
///     concorrentes nesta instância.
///   - Saída: completa a entrada aberta mais recente com o tempo de permanência
///     (DurationMinutes), atributo usado pelo modelo de evasão. Sem saída registrada, a
///     permanência fica nula e o modelo a trata como ausente (imputação), nunca como zero.
///   - Horários sempre em UTC (o ML converte para o fuso da academia).
/// </summary>
public class CheckInService : ICheckInService
{
    public static readonly TimeSpan OpenVisitWindow = TimeSpan.FromHours(4);
    private const int MaxVisitMinutes = 6 * 60;
    private static readonly ConcurrentDictionary<int, SemaphoreSlim> StudentLocks = new();

    private readonly ICheckInRepository _checkIns;
    private readonly IStudentRepository _students;

    public CheckInService(ICheckInRepository checkIns, IStudentRepository students)
    {
        _checkIns = checkIns;
        _students = students;
    }

    public async Task<CheckInDto> RegisterAsync(int studentId, CancellationToken cancellationToken = default)
    {
        var gate = StudentLocks.GetOrAdd(studentId, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(cancellationToken);
        try
        {
            var student = await _students.GetByIdAsync(studentId, cancellationToken)
                ?? throw new NotFoundException("Aluno não encontrado.");
            if (!student.Active)
                throw new BusinessRuleException("Matrícula cancelada: não é possível registrar entrada.");

            var now = DateTime.UtcNow;
            if (await FindOpenVisitAsync(studentId, now, cancellationToken) is not null)
                throw new BusinessRuleException("O aluno já está com uma entrada aberta. Registre a saída antes de uma nova entrada.");

            var checkIn = new CheckIn { StudentId = studentId, Timestamp = now };
            await _checkIns.AddAsync(checkIn, cancellationToken);
            await _checkIns.SaveChangesAsync(cancellationToken);
            return ToDto(checkIn);
        }
        finally
        {
            gate.Release();
        }
    }

    public async Task<CheckInDto> CheckOutAsync(int studentId, CancellationToken cancellationToken = default)
    {
        var gate = StudentLocks.GetOrAdd(studentId, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(cancellationToken);
        try
        {
            _ = await _students.GetByIdAsync(studentId, cancellationToken)
                ?? throw new NotFoundException("Aluno não encontrado.");

            var now = DateTime.UtcNow;
            var open = await FindOpenVisitAsync(studentId, now, cancellationToken)
                ?? throw new BusinessRuleException("Não há entrada aberta para este aluno nas últimas 4 horas.");

            open.DurationMinutes = (int)Math.Clamp(Math.Round((now - open.Timestamp).TotalMinutes), 1, MaxVisitMinutes);
            await _checkIns.SaveChangesAsync(cancellationToken);
            return ToDto(open);
        }
        finally
        {
            gate.Release();
        }
    }

    public async Task<IReadOnlyList<CheckInDto>> ListByStudentAsync(
        int studentId,
        DateTime? from = null,
        DateTime? to = null,
        CancellationToken cancellationToken = default)
    {
        if (from is not null && to is not null && from > to)
            throw new BusinessRuleException("Período inválido: a data inicial é posterior à final.");
        var checkIns = await _checkIns.GetByStudentAsync(studentId, from, to, cancellationToken);
        return checkIns.Select(ToDto).ToList().AsReadOnly();
    }

    private async Task<CheckIn?> FindOpenVisitAsync(int studentId, DateTime now, CancellationToken ct)
    {
        var recent = await _checkIns.GetByStudentAsync(studentId, now - OpenVisitWindow, now, ct, track: true);
        return recent.Where(c => c.DurationMinutes is null).OrderByDescending(c => c.Timestamp).FirstOrDefault();
    }

    private static CheckInDto ToDto(CheckIn c) => new(c.Id, c.StudentId, c.Timestamp, c.DurationMinutes);
}
