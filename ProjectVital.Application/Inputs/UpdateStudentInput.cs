namespace ProjectVital.Application.Inputs;

/// <summary>
/// Entrada da mutation `updateStudent`. Todos os campos são opcionais —
/// apenas os fornecidos serão atualizados (partial update).
/// </summary>
public record UpdateStudentInput(
    string? Name,
    string? Email,
    int? PlanId,
    bool? Active
);
