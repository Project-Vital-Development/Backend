using System.ComponentModel.DataAnnotations;

namespace ProjectVital.Application.Inputs;

/// <summary>
/// Input da mutation "updateStudent" — campos opcionais para atualização parcial (PATCH semântico).
/// Apenas os campos não-null serão atualizados; os null mantêm o valor atual do banco.
///
/// VALIDADO POR: Hot Chocolate automaticamente (DataAnnotations).
///   Se fornecido, deve satisfazer as restrições de tamanho/formato.
///   null é sempre permitido (campo não será atualizado).
///
/// USADO EM: StudentMutationResolver.UpdateStudentAsync() → StudentService.UpdateAsync()
///
/// PADRÃO PARTIAL UPDATE:
///   Se Name=null → mantém Name atual no banco
///   Se Name="João" → atualiza Name para "João"
///   O StudentService aplica cada campo individualmente (if input.Name is not null).
///
/// ACTIVE FLAG:
///   true → reativar aluno (ex: retornou após cancelamento)
///   false → desativar aluno (marca como churned para fins de ML)
///   IMPACTO NO ML: aluno com Active=false vira label=churn nos dados de treino.
///     Desativar muitos alunos de uma vez pode distorcer o próximo retreino.
///
/// EMAIL (nullable):
///   Se fornecido e diferente do atual, o StudentService verifica a unicidade
///   (BusinessRuleException "E-mail ... já está em uso.").
///
/// PLANID (nullable):
///   Se fornecido, deve pertencer à academia do aluno — validado em StudentService.
///
/// O QUE NÃO PODE SER ATUALIZADO AQUI:
///   AcademyId: alunos não trocam de academia (seria migração de tenant)
///   EnrollmentDate: imutável após criação (histórico)
///   Password: fora do escopo (congelado) — alunos não fazem login; só gestores (AuthService)
/// </summary>
public record UpdateStudentInput(
    // null = manter nome atual; não-null = atualizar com valor fornecido
    [StringLength(255, MinimumLength = 2)] string? Name,
    // null = manter email atual; não-null = atualizar (verificar unicidade no service)
    [EmailAddress][StringLength(255)] string? Email,
    // null = manter plano atual; não-null = trocar para plano da mesma academia
    [Range(1, int.MaxValue)] int? PlanId,
    // null = manter status; true = reativar; false = desativar (impacta ML)
    bool? Active
);
