using ProjectVital.Application.DTOs;
using ProjectVital.Application.Inputs;

namespace ProjectVital.Application.Interfaces;

public interface IStudentService
{
    Task<IReadOnlyList<StudentDto>> ListByAcademyAsync(
        int academyId,
        bool? activeOnly = null,
        CancellationToken cancellationToken = default);

    Task<StudentDto?> GetByIdAsync(int id, CancellationToken cancellationToken = default);

    Task<StudentDto> RegisterAsync(RegisterStudentInput input, CancellationToken cancellationToken = default);

    Task<StudentDto> UpdateAsync(int id, UpdateStudentInput input, CancellationToken cancellationToken = default);

    Task DeleteAsync(int id, CancellationToken cancellationToken = default);
}
