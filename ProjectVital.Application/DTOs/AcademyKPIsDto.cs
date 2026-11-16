namespace ProjectVital.Application.DTOs;

/// <summary>
/// KPIs operacionais de uma academia — snapshot em tempo real para o dashboard.
///
/// CRIADO POR: AcademyService.GetKPIsAsync()
/// EXPOSTO COMO TIPO GRAPHQL: AcademyKPIsType.cs
/// CONSUMIDO POR: KPIResolver.AcademyKPIsAsync() → frontend Dashboard
///
/// COMO SÃO CALCULADOS (AcademyService):
///   TotalStudents, ActiveStudents: StudentRepository.CountByAcademyAsync()
///   HighRiskCount, MediumRiskCount, LowRiskCount:
///     PredictionRepository.CountByRiskLevelAsync() — conta última predição de cada aluno
///   CheckInsThisWeek: CheckInRepository.CountByAcademyInPeriodAsync (UtcNow-7d, UtcNow)
///   AverageWeeklyFrequency: CheckInsThisWeek / ActiveStudents (0.0 se ActiveStudents=0)
///   GeneratedAt: DateTime.UtcNow no momento do cálculo
///
/// ALUNOS SEM PREDIÇÃO:
///   Não entram nos contadores de risco (HighRiskCount/Medium/Low).
///   O frontend pode calcular "sem predição" como:
///     semPredicao = ActiveStudents - HighRiskCount - MediumRiskCount - LowRiskCount
///
/// PERFORMANCE: o AcademyService usa 2 queries ao banco (não N+1).
///   Query 1: COUNT de alunos (StudentRepository)
///   Query 2: COUNT de predições por nível + check-ins da semana (PredictionRepository + CheckInRepository)
///
/// SE ADICIONAR NOVO KPI:
///   1. Adicionar parâmetro aqui (ao final do record para manter posições existentes)
///   2. Calcular em AcademyService.GetKPIsAsync()
///   3. Adicionar em AcademyKPIsType.cs
///   4. Adicionar no frontend (Apollo query + card UI)
/// </summary>
public record AcademyKPIsDto(
    int AcademyId,
    int TotalStudents,
    int ActiveStudents,
    // Alunos ativos cuja última predição é High (probabilidade ≥ limiar validado do modelo)
    int HighRiskCount,
    // Alunos ativos cuja última predição é Medium (≥ metade do limiar do modelo)
    int MediumRiskCount,
    // Alunos ativos cuja última predição é Low
    int LowRiskCount,
    // Total de check-ins nos últimos 7 dias em toda a academia
    int CheckInsThisWeek,
    // CheckInsThisWeek / ActiveStudents — frequência média semanal por aluno
    double AverageWeeklyFrequency,
    // UTC — quando este snapshot foi calculado; exibir como "atualizado às HH:mm"
    DateTime GeneratedAt
);
