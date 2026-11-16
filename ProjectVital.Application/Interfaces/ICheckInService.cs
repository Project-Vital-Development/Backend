using ProjectVital.Application.DTOs;

namespace ProjectVital.Application.Interfaces;

/// <summary>
/// Contrato do serviço de check-ins.
/// Gerencia o registro de entradas de alunos na academia — a principal fonte de dados do ML.
///
/// IMPLEMENTAÇÃO: CheckInService.cs (Application/Services/CheckInService.cs)
/// REGISTRADO EM: Program.cs → services.AddScoped&lt;ICheckInService, CheckInService&gt;()
///
/// POR QUE CHECK-INS SÃO CRÍTICOS PARA O ML:
///   O modelo de churn usa exclusivamente dados de frequência extraídos dos check-ins:
///   - avg_weekly_frequency: média de check-ins por semana
///   - days_since_last_checkin: dias desde o último registro
///   - avg_interval_days: intervalo médio entre treinos
///   - frequency_trend: variação de frequência nas últimas semanas
///   - total_checkins: histórico acumulado
///   Sem check-ins, o Python não consegue calcular features → lança ValueError → HTTP 422.
///
/// CONSUMERS DESTA INTERFACE:
///   - CheckInMutationResolver.CheckInAsync() — registra entrada
///   - CheckInQueryResolver.CheckInsAsync() — lista histórico
///   - AcademyService.GetKPIsAsync() — usa CheckInRepository diretamente (não este serviço)
///
/// TODOS OS TIMESTAMPS SÃO UTC:
///   O servidor registra DateTime.UtcNow. O frontend converte para timezone local.
/// </summary>
public interface ICheckInService
{
    /// <summary>
    /// Registra a entrada de um aluno na academia com o timestamp atual (UTC).
    ///
    /// VALIDA: aluno deve estar Active=true — lança InvalidOperationException caso contrário.
    ///   (Mapeado para "BUSINESS_RULE_VIOLATION" pelo AppErrorFilter)
    ///
    /// IMPACTO NO ML: cada check-in melhora a precisão da próxima predição.
    ///   Alunos com mais check-ins recentes têm menor probabilidade de churn.
    ///
    /// NOTA: não verifica se o aluno já fez check-in hoje — múltiplos check-ins no mesmo dia
    ///   são permitidos (academias podem registrar entrada e saída separadamente).
    ///
    /// CHAMADO POR: CheckInMutationResolver.CheckInAsync()
    /// </summary>
    Task<CheckInDto> RegisterAsync(int studentId, CancellationToken cancellationToken = default);

    /// <summary>Registra a SAÍDA: completa a entrada aberta com o tempo de permanência.</summary>
    Task<CheckInDto> CheckOutAsync(int studentId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lista check-ins de um aluno em um intervalo de datas opcional.
    ///
    /// from=null → sem limite inferior (todos os check-ins até "to")
    /// to=null → sem limite superior (todos os check-ins desde "from")
    /// Ambos null → histórico completo do aluno
    ///
    /// FILTRO COMUM NO FRONTEND:
    ///   from = 30 dias atrás, to = hoje → frequência do último mês
    ///   from = início do mês, to = fim do mês → relatório mensal
    ///
    /// ORDENAÇÃO: por Timestamp DESC (mais recente primeiro) — ver CheckInRepository.
    ///
    /// CHAMADO POR: CheckInQueryResolver.CheckInsAsync()
    /// </summary>
    Task<IReadOnlyList<CheckInDto>> ListByStudentAsync(
        int studentId,
        DateTime? from = null,
        DateTime? to = null,
        CancellationToken cancellationToken = default);
}
