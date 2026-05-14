using System.ComponentModel.DataAnnotations;

namespace ProjectVital.Application.Inputs;

/// <summary>
/// Entrada da mutation `updateStudent`. Todos os campos são opcionais —
/// apenas os fornecidos serão atualizados (partial update).
/// </summary>
public record UpdateStudentInput(
    [StringLength(255, MinimumLength = 2)] string? Name,
    [EmailAddress][StringLength(255)] string? Email,
    [Range(1, int.MaxValue)] int? PlanId,
    bool? Active
);
