using ProjectVital.Application.DTOs;
using ProjectVital.Application.Interfaces;
using ProjectVital.Domain.Entities;
using ProjectVital.Domain.Interfaces;

namespace ProjectVital.Application.Services;

/// <summary>
/// Centraliza a lógica de negócio de check-ins.
/// Um check-in é o evento de entrada do aluno na academia —
/// é a PRINCIPAL fonte de dados para o modelo de churn prediction.
///
/// REGRA DE NEGÓCIO PRINCIPAL:
///   Somente alunos ativos (Student.Active = true) podem fazer check-in.
///   Alunos inativos foram desativados pelo gestor (inadimplência, cancelamento, etc.)
///   e não devem acumular registros de presença.
///
/// DEPENDÊNCIAS:
///   - ICheckInRepository → INSERT e queries de check-ins
///   - IStudentRepository → verificar se o aluno existe e está ativo
///
/// SE MUDAR AQUI, IMPACTA:
///   - CheckInMutationResolver / CheckInQueryResolver → chamam este service
///   - ICheckInService.cs → interface (manter sincronizado)
///   - CheckInDto.cs      → formato retornado ao frontend
///   - O modelo ML lê check-ins DIRETAMENTE via SQL — esta camada não é usada pelo Python
/// </summary>
public class CheckInService : ICheckInService
{
    private readonly ICheckInRepository _checkIns;
    private readonly IStudentRepository _students;

    public CheckInService(ICheckInRepository checkIns, IStudentRepository students)
    {
        _checkIns = checkIns;
        _students = students;
    }

    /// <summary>
    /// Registra a entrada de um aluno na academia com timestamp UTC atual.
    ///
    /// VALIDAÇÕES:
    ///   1. Aluno deve existir → KeyNotFoundException se não encontrado
    ///   2. Aluno deve estar ativo → InvalidOperationException se inativo
    ///      (ambas mapeadas para "BUSINESS_RULE_VIOLATION" pelo AppErrorFilter)
    ///
    /// DurationMinutes fica null — check-out não é suportado nesta versão.
    /// Se implementar check-out no futuro, adicionar a lógica aqui e atualizar
    /// o CheckIn.DurationMinutes na entidade.
    ///
    /// IMPACTO NO ML:
    ///   Cada check-in registrado aqui alimenta o feature engineering do ML Service.
    ///   Mais check-ins = features mais precisas = predições mais confiáveis.
    ///   Alunos com poucos check-ins sofrem de "cold start" (veja engineering.py).
    ///
    /// CHAMADO POR: CheckInMutationResolver.CheckInAsync()
    /// </summary>
    public async Task<CheckInDto> RegisterAsync(int studentId, CancellationToken cancellationToken = default)
    {
        // Verificar existência e status ativo antes de inserir
        var student = await _students.GetByIdAsync(studentId, cancellationToken)
            ?? throw new KeyNotFoundException($"Aluno com Id={studentId} não encontrado.");

        if (!student.Active)
            throw new InvalidOperationException(
                $"Não é possível registrar check-in para aluno inativo (Id={studentId}).");

        var checkIn = new CheckIn
        {
            StudentId = studentId,
            Timestamp = DateTime.UtcNow, // sempre UTC — misturar fusos corromperia feature engineering
            // DurationMinutes = null (check-out não implementado ainda)
        };

        await _checkIns.AddAsync(checkIn, cancellationToken);
        await _checkIns.SaveChangesAsync(cancellationToken);

        return new CheckInDto(checkIn.Id, checkIn.StudentId, checkIn.Timestamp, checkIn.DurationMinutes);
    }

    /// <summary>
    /// Lista check-ins de um aluno com filtro opcional de intervalo de datas.
    /// Resultados ordenados por Timestamp descendente (mais recente primeiro).
    ///
    /// PARÂMETROS:
    ///   - from: data inicial inclusive (null = sem limite inferior)
    ///   - to:   data final inclusive (null = sem limite superior)
    ///
    /// USO TÍPICO:
    ///   - Frontend exibe histórico de presença do aluno
    ///   - Relatórios de frequência por período
    ///
    /// IMPORTANTE: o filtro de data é aplicado no banco (WHERE Timestamp >= from AND Timestamp <= to)
    /// usando o índice composto (StudentId, Timestamp) — eficiente mesmo com muitos check-ins.
    ///
    /// CHAMADO POR: CheckInQueryResolver.CheckInsAsync()
    /// </summary>
    public async Task<IReadOnlyList<CheckInDto>> ListByStudentAsync(
        int studentId,
        DateTime? from = null,
        DateTime? to = null,
        CancellationToken cancellationToken = default)
    {
        var checkIns = await _checkIns.GetByStudentAsync(studentId, from, to, cancellationToken);
        return checkIns
            .Select(c => new CheckInDto(c.Id, c.StudentId, c.Timestamp, c.DurationMinutes))
            .ToList()
            .AsReadOnly();
    }
}
