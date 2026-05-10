namespace ProjectVital.Application.Inputs;

/// <summary>Entrada da mutation `registerStudent`.</summary>
public record RegisterStudentInput(
    string Name,
    string Email,
    string Password,
    int AcademyId,
    int PlanId,
    DateTime? EnrollmentDate
);
