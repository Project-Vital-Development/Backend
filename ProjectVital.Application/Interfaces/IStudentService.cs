using ProjectVital.Application.DTOs;
using ProjectVital.Application.Inputs;

namespace ProjectVital.Application.Interfaces;

/// <summary>
/// Contrato do serviço de alunos.
/// Define CRUD de alunos com tenant isolation e regras de negócio.
///
/// IMPLEMENTAÇÃO: StudentService.cs (Application/Services/StudentService.cs)
/// REGISTRADO EM: Program.cs → services.AddScoped&lt;IStudentService, StudentService&gt;()
///
/// CONSUMERS DESTA INTERFACE:
///   - StudentQueryResolver: usa ListByAcademyAsync e GetByIdAsync
///   - StudentMutationResolver: usa RegisterAsync, UpdateAsync, DeleteAsync
///   - CheckInQueryResolver: usa GetByIdAsync (para verificar AcademyId do aluno)
///   - CheckInMutationResolver: usa GetByIdAsync (para verificar AcademyId)
///   - PredictionQueryResolver: usa GetByIdAsync (para verificar AcademyId)
///   - PredictionMutationResolver: usa GetByIdAsync (para verificar AcademyId)
///
/// PADRÃO DE SEGURANÇA:
///   Os resolvers chamam GetByIdAsync ANTES de qualquer operação para obter o AcademyId
///   do aluno e verificar com EnforceAcademyAccess(). Isso garante tenant isolation mesmo
///   quando o cliente conhece o studentId de outro tenant.
///
/// SE ADICIONAR MÉTODO:
///   1. Adicionar aqui (interface)
///   2. Implementar em StudentService.cs
///   3. Adicionar ao resolver que vai usá-lo
/// </summary>
public interface IStudentService
{
    /// <summary>
    /// Lista todos os alunos de uma academia com filtro opcional de status.
    ///
    /// activeOnly=null → retorna ativos E inativos
    /// activeOnly=true → apenas Active=true
    /// activeOnly=false → apenas Active=false (inativos)
    ///
    /// CHAMADO POR: StudentQueryResolver.StudentsAsync()
    /// </summary>
    Task<IReadOnlyList<StudentDto>> ListByAcademyAsync(
        int academyId,
        bool? activeOnly = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Busca um aluno pelo Id incluindo Plan e LatestPrediction.
    /// Retorna null se não existir (resolver lança NotFoundException em seguida).
    ///
    /// DOIS USOS DISTINTOS:
    ///   1. Pelos resolvers de Student (retornar ao cliente)
    ///   2. Pelos resolvers de CheckIn e Prediction (só para obter AcademyId e verificar acesso)
    ///      → neste segundo caso, o StudentDto retornado é usado apenas para student.AcademyId
    ///
    /// RETORNA NULL: quando o aluno não existe ou foi soft-deleted (Global Query Filter).
    /// </summary>
    Task<StudentDto?> GetByIdAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Cria um novo aluno com sua conta de usuário de forma atômica.
    ///
    /// ATOMICIDADE: cria User + Student em um único SaveChanges usando
    /// navigation property (EF Core resolve a FK automaticamente).
    ///
    /// VALIDA: email único — lança InvalidOperationException se o email já existe.
    /// LANÇA: InvalidOperationException se planId não pertence à academia.
    ///
    /// CHAMADO POR: StudentMutationResolver.RegisterStudentAsync()
    /// </summary>
    Task<StudentDto> RegisterAsync(RegisterStudentInput input, CancellationToken cancellationToken = default);

    /// <summary>
    /// Atualiza dados mutáveis de um aluno existente (partial update).
    /// Campos null no input são ignorados (mantém o valor atual).
    ///
    /// CAMPOS ATUALIZÁVEIS: Name, PlanId, Active (via UpdateStudentInput)
    /// Email não é atualizável por ora (requer verificação de unicidade adicional).
    ///
    /// CHAMADO POR: StudentMutationResolver.UpdateStudentAsync()
    /// </summary>
    Task<StudentDto> UpdateAsync(int id, UpdateStudentInput input, CancellationToken cancellationToken = default);

    /// <summary>
    /// Soft delete de um aluno (LGPD Art.18 — direito ao esquecimento).
    /// Marca Student.DeletedAt e User.DeletedAt com o timestamp atual.
    /// Após isso, o aluno não aparece mais em nenhuma query (Global Query Filter).
    /// CheckIns e Predictions do aluno permanecem no banco (histórico preservado).
    ///
    /// NÃO É HARD DELETE: os dados físicos continuam no banco para auditoria.
    /// Para hard delete LGPD real, seria necessário uma migration de purga periódica.
    ///
    /// CHAMADO POR: StudentMutationResolver.DeleteStudentAsync()
    /// </summary>
    Task DeleteAsync(int id, CancellationToken cancellationToken = default);
}
