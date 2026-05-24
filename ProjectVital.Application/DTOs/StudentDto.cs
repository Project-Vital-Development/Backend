namespace ProjectVital.Application.DTOs;

/// <summary>
/// DTO de aluno — versão imutável e segura de Student para trafegar entre camadas.
///
/// CRIADO POR:
///   - StudentService.ListByAcademyAsync() → lista de alunos da academia
///   - StudentService.GetByIdAsync() → aluno individual com Plan e Prediction
///   - StudentService.RegisterAsync() → aluno recém-criado
///   - StudentService.UpdateAsync() → aluno atualizado
///
/// EXPOSTO COMO TIPO GRAPHQL: StudentType.cs
///
/// CAMPOS NULLABLE (podem ser null dependendo do contexto):
///   PlanName: preenchido apenas quando StudentRepository.GetWithRelationsAsync() inclui Plan.
///     - Na listagem (ListByAcademyAsync), inclui Plan → PlanName preenchido
///     - Em uso interno (GetByIdAsync para verificar AcademyId), pode não incluir Plan
///   LatestPrediction: null se o aluno nunca foi avaliado pelo ML.
///     - O frontend deve exibir "Sem predição" quando null
///     - Preenchido via PredictionRepository.GetLatestByStudentAsync()
///
/// CAMPOS DE SEGURANÇA:
///   PasswordHash NÃO está aqui — é excluído no mapeamento Student → StudentDto.
///   Email está incluído (necessário para exibir no dashboard), mas é dado LGPD pessoal.
///
/// ACTIVE FLAG E ML:
///   Active=false indica churn ocorreu — usado como label de treinamento no Python.
///   Alunos inativos AINDA aparecem neste DTO (para exibir histórico no dashboard).
///   O filtro de alunos ativos ocorre no service/repository, não aqui.
///
/// SE ADICIONAR CAMPO:
///   1. Adicionar parâmetro ao record aqui
///   2. Popular no StudentService correspondente
///   3. Adicionar em StudentType.cs para expor via GraphQL
/// </summary>
public record StudentDto(
    int Id,
    int UserId,
    int AcademyId,
    int PlanId,
    string Name,
    string Email,
    DateTime EnrollmentDate,
    // false = aluno cancelou (churn). Impacta feature "label" no treinamento ML.
    bool Active,
    // Nullable: nome do plano; incluso quando repository carrega Plan via Include()
    string? PlanName,
    // Nullable: null quando aluno ainda não passou pelo ML Service
    PredictionDto? LatestPrediction
);
