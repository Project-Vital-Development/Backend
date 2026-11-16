using System.ComponentModel.DataAnnotations;

namespace ProjectVital.Application.Inputs;

/// <summary>
/// Input da mutation "registerStudent" — dados para criar um novo aluno.
///
/// VALIDADO POR: Hot Chocolate automaticamente antes de chegar ao resolver.
///   Campos inválidos retornam erro de validação antes de qualquer lógica de negócio.
///
/// USADO EM: StudentMutationResolver.RegisterStudentAsync() → StudentService.RegisterAsync()
///
/// CAMPOS E REGRAS:
///   Name: 2–255 caracteres (evita nomes vazios ou absurdamente longos)
///   Email: formato válido + max 255 chars (chave única na tabela Users)
///   Password: 8–255 chars (hash BCrypt gerado em StudentService)
///   AcademyId: >= 1 (IDs no MySQL começam em 1). Verificado também pelo resolver
///     via EnforceAcademyAccess — Manager só pode criar alunos na sua academia.
///   PlanId: >= 1. Validado no StudentService (plano deve pertencer à academia).
///   EnrollmentDate: opcional — se null, StudentService usa DateTime.UtcNow.
///
/// SEGURANÇA (TENANT ISOLATION):
///   StudentMutationResolver verifica EnforceAcademyAccess(httpContext, input.AcademyId)
///   ANTES de chamar o service. Um Manager não pode criar alunos em outra academia
///   mesmo que passe um AcademyId diferente no input.
///
/// DADO PESSOAL (LGPD):
///   Email e Name são dados pessoais. São armazenados em texto puro na tabela Users.
///   Pseudonimização ou criptografia em repouso não estão implementadas (ver TD-05).
///
/// PASSWORD:
///   Nunca armazenada em texto puro — StudentService.RegisterAsync() chama
///   IAuthService.HashPassword() antes de salvar.
/// </summary>
public record RegisterStudentInput(
    [Required][StringLength(255, MinimumLength = 2)] string Name,
    [Required][EmailAddress][StringLength(255)] string Email,
    [Required][StringLength(255, MinimumLength = 8)] string Password,
    // Verificado pelo resolver (EnforceAcademyAccess) E pelo service (PlanId pertence à academia)
    [Required][Range(1, int.MaxValue)] int AcademyId,
    // Deve pertencer à academia especificada — validado em StudentService
    [Required][Range(1, int.MaxValue)] int PlanId,
    // Opcional: null → StudentService usa DateTime.UtcNow como data de matrícula
    DateTime? EnrollmentDate
);
