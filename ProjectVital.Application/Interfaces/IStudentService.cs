using ProjectVital.Application.DTOs;
using ProjectVital.Application.Inputs;

namespace ProjectVital.Application.Interfaces;

/// <summary>
/// Serviço de gestão de alunos. Aplica regras de tenant isolation
/// (Manager só pode atuar sobre alunos da sua própria Academy).
/// </summary>
public interface IStudentService
{
    Task<IReadOnlyList<StudentDto>> ListByAcademyAsync(
        int academyId,
        bool? activeOnly = null,
        CancellationToken cancellationToken = default);

    Task<StudentDto?> GetByIdAsync(int id, CancellationToken cancellationToken = default);

    Task<StudentDto> RegisterAsync(RegisterStudentInput input, CancellationToken cancellationToken = default);

    Task<StudentDto> UpdateAsync(int id, UpdateStudentInput input, CancellationToken cancellationToken = default);
}
