using System.ComponentModel.DataAnnotations;

namespace ProjectVital.Application.Inputs;

public record RegisterStudentInput(
    [Required][StringLength(255, MinimumLength = 2)] string Name,
    [Required][EmailAddress][StringLength(255)] string Email,
    [Required][StringLength(255, MinimumLength = 8)] string Password,
    [Required][Range(1, int.MaxValue)] int AcademyId,
    [Required][Range(1, int.MaxValue)] int PlanId,
    DateTime? EnrollmentDate
);
