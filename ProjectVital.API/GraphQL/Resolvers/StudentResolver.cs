using HotChocolate;
using HotChocolate.Authorization;
using HotChocolate.Types;
using Microsoft.AspNetCore.Http;
using ProjectVital.API.GraphQL.Helpers;
using ProjectVital.Application.DTOs;
using ProjectVital.Application.Inputs;
using ProjectVital.Application.Interfaces;
using ProjectVital.Domain.Exceptions;

namespace ProjectVital.API.GraphQL.Resolvers;

/// <summary>
/// Resolver GraphQL de queries de alunos.
/// Expõe os fields "students" e "student" no tipo Query.
///
/// SEGURANÇA — PADRÃO DE TODAS AS QUERIES:
///   1. [Authorize] → verifica token JWT válido (Hot Chocolate)
///   2. EnforceAcademyAccess() → verifica que o Manager é dono da academia
///   3. Chama o service → que executa a lógica de negócio
///   Esta ordem é OBRIGATÓRIA — nunca chame o service antes de verificar acesso.
///
/// QUERY GraphQL GERADA:
///   query {
///     students(academyId: 1) { id name email active latestPrediction { probability riskLevel } }
///     student(id: 42) { id name email plan { name } latestPrediction { ... } }
///   }
/// </summary>
[ExtendObjectType(OperationTypeNames.Query)]
public class StudentQueryResolver
{
    /// <summary>
    /// Lista todos os alunos de uma academia (ativos e inativos).
    ///
    /// VERIFICAÇÃO DE ACESSO (CRIT-01):
    ///   EnforceAcademyAccess verifica que o academyId do parâmetro corresponde
    ///   ao academyId do token JWT. Um Manager da Academia A não pode listar
    ///   alunos da Academia B.
    ///
    /// [ID] int academyId:
    ///   O tipo GraphQL ID serializa como string — [ID] faz a conversão de/para int.
    /// </summary>
    [Authorize]
    public async Task<IReadOnlyList<StudentDto>> StudentsAsync(
        [ID] int academyId,
        // ServiceKind.Resolver: scope/DbContext isolado por resolver (ver KPIResolver).
        [Service(ServiceKind.Resolver)] IStudentService studentService,
        [Service] IHttpContextAccessor httpContext,
        CancellationToken cancellationToken)
    {
        ResolverAuthHelper.EnforceAcademyAccess(httpContext, academyId);
        return await studentService.ListByAcademyAsync(academyId, null, cancellationToken);
    }

    /// <summary>
    /// Busca um aluno pelo Id com verificação de tenant isolation.
    ///
    /// PADRÃO "buscar primeiro para verificar":
    ///   O aluno é buscado (1 query) → AcademyId verificado → retornado ao cliente.
    ///   Isso evita retornar dados de aluno de outra academia antes de verificar acesso.
    ///   Mesmo que o cliente saiba o Id do aluno, não recebe os dados se for de outra academia.
    /// </summary>
    [Authorize]
    public async Task<StudentDto?> StudentAsync(
        [ID] int id,
        // ServiceKind.Resolver: scope/DbContext isolado por resolver (ver KPIResolver).
        [Service(ServiceKind.Resolver)] IStudentService studentService,
        [Service] IHttpContextAccessor httpContext,
        CancellationToken cancellationToken)
    {
        var student = await studentService.GetByIdAsync(id, cancellationToken);
        if (student is null) return null;

        // Verificar APÓS buscar — precisa do AcademyId do aluno para comparar com o token
        ResolverAuthHelper.EnforceAcademyAccess(httpContext, student.AcademyId);
        return student;
    }
}

/// <summary>
/// Resolver GraphQL de mutations de alunos.
/// Expõe "registerStudent", "updateStudent" e "deleteStudent" no tipo Mutation.
///
/// PATTERN PADRÃO DE SEGURANÇA (igual ao QueryResolver):
///   [Authorize] → EnforceAcademyAccess() → service call
/// </summary>
[ExtendObjectType(OperationTypeNames.Mutation)]
public class StudentMutationResolver
{
    /// <summary>
    /// Registra um novo aluno na academia.
    ///
    /// MUTATION GraphQL:
    ///   mutation {
    ///     registerStudent(input: { name: "João", email: "joao@ex.com", password: "123456789",
    ///                              academyId: 1, planId: 2 }) { id name active }
    ///   }
    ///
    /// VALIDAÇÃO DE INPUT:
    ///   RegisterStudentInput tem DataAnnotations — Hot Chocolate as valida automaticamente.
    ///   Se inválido, retorna erro de validação ANTES de chegar ao service.
    ///
    /// ACESSO: Manager só pode criar alunos na sua própria academia (input.AcademyId).
    /// </summary>
    [Authorize]
    public async Task<StudentDto> RegisterStudentAsync(
        RegisterStudentInput input,
        [Service] IStudentService studentService,
        [Service] IHttpContextAccessor httpContext,
        CancellationToken cancellationToken)
    {
        // A academia gravada é a do TOKEN; o campo do input só é aceito se coincidir.
        var academyId = ResolverAuthHelper.EnforceAcademyAccess(httpContext, input.AcademyId);
        return await studentService.RegisterAsync(input with { AcademyId = academyId }, cancellationToken);
    }

    /// <summary>
    /// Atualiza dados de um aluno existente.
    ///
    /// PATTERN "verificar antes de operar" (CRIT-03):
    ///   1. Busca o aluno → se não existir lança NotFoundException
    ///   2. Verifica que pertence à academia do token → ForbiddenException se não
    ///   3. Chama o service para atualizar
    ///   Sem o passo 1+2, um Manager poderia atualizar alunos de outras academias.
    /// </summary>
    [Authorize]
    public async Task<StudentDto> UpdateStudentAsync(
        [ID] int id,
        UpdateStudentInput input,
        [Service] IStudentService studentService,
        [Service] IHttpContextAccessor httpContext,
        CancellationToken cancellationToken)
    {
        // Busca o aluno primeiro para verificar a qual academia pertence
        var existing = await studentService.GetByIdAsync(id, cancellationToken)
            ?? throw new NotFoundException("Student", id);

        ResolverAuthHelper.EnforceAcademyAccess(httpContext, existing.AcademyId);
        return await studentService.UpdateAsync(id, input, cancellationToken);
    }

    /// <summary>
    /// Exclui logicamente um aluno (soft delete — LGPD Art.18).
    /// Retorna true se bem-sucedido.
    ///
    /// O aluno E o usuário vinculado recebem DeletedAt = agora.
    /// Após isso, ambos desaparecem das queries (Global Query Filter).
    /// CheckIns e Predictions do aluno permanecem no banco (histórico preservado).
    /// </summary>
    [Authorize]
    public async Task<bool> DeleteStudentAsync(
        [ID] int id,
        [Service] IStudentService studentService,
        [Service] IHttpContextAccessor httpContext,
        CancellationToken cancellationToken)
    {
        var existing = await studentService.GetByIdAsync(id, cancellationToken)
            ?? throw new NotFoundException("Student", id);

        ResolverAuthHelper.EnforceAcademyAccess(httpContext, existing.AcademyId);
        await studentService.DeleteAsync(id, cancellationToken);
        return true;
    }
}
