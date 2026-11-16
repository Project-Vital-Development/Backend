namespace ProjectVital.Application.DTOs;

/// <summary>
/// DTO de check-in — registro de entrada de um aluno na academia.
///
/// CRIADO POR:
///   - CheckInService.RegisterAsync() → check-in recém-criado
///   - CheckInService.ListByStudentAsync() → histórico de check-ins
///
/// EXPOSTO COMO TIPO GRAPHQL: CheckInType.cs
///
/// TIMESTAMP:
///   Sempre UTC (DateTime.UtcNow no momento do registro).
///   O frontend deve converter para o timezone local do usuário antes de exibir.
///
/// DURATIONMINUTES (nullable):
///   Opcional — nem todos os sistemas capturam duração do treino.
///   Quando null, o registro representa apenas a entrada, sem saída registrada.
///   NÃO USADO nas features do ML atualmente, mas disponível para expansão futura.
///   SE ADICIONAR AO ML: atualizar engineering.py E retreinar o modelo.
///
/// IMPORTÂNCIA PARA O ML:
///   Cada CheckIn é um ponto de dado para o modelo de churn.
///   O Python lê CheckIns via SQLAlchemy (read-only) e calcula:
///   - Frequência semanal média (avg_weekly_frequency)
///   - Dias desde o último check-in (days_since_last_checkin)
///   - Intervalo médio entre treinos (avg_interval_days)
///   - Tendência de frequência (frequency_trend)
///   - Total histórico (total_checkins)
///   Alunos sem check-ins nos últimos 30 dias têm alto risco de churn.
/// </summary>
public record CheckInDto(
    int Id,
    int StudentId,
    // UTC — frontend converte para timezone local antes de exibir
    DateTime Timestamp,
    // Nullable: registrado apenas quando o aluno faz check-out também
    int? DurationMinutes
);
