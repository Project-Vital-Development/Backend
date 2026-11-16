namespace ProjectVital.Domain.Entities;

/// <summary>
/// Registro de entrada de um aluno na academia.
/// É a FONTE PRIMÁRIA DE DADOS para o modelo de churn prediction.
/// Sem check-ins, o ML Service não consegue calcular features e lança ValueError.
///
/// VOLUME: academias ativas geram centenas/milhares de check-ins por mês.
/// O índice composto (StudentId, Timestamp) no MySQL é CRÍTICO para que as
/// queries de feature engineering sejam rápidas. Sem ele, o ML vira lento.
///
/// RELAÇÕES:
///   - CheckIn (N) → (1) Student : cada entrada pertence a um aluno
///
/// IMPACTO EM CASCATA: se você alterar campos aqui, também precisa atualizar:
///   - CheckInConfiguration.cs      → mapeamento EF Core + índices
///   - CheckInDto.cs                → DTO de saída para o frontend
///   - CheckInService.RegisterAsync → cria o check-in
///   - queries.py (ML)              → SELECT usa StudentId e Timestamp diretamente
///   - engineering.py (ML)          → usa coluna "Timestamp" por nome string
/// </summary>
public class CheckIn
{
    public int Id { get; set; }

    /// <summary>
    /// FK para o aluno que fez o check-in. Parte do índice composto (StudentId, Timestamp).
    /// Se mudar o nome desta propriedade, atualizar CheckInConfiguration.cs (HasIndex).
    /// </summary>
    public int StudentId { get; set; }

    /// <summary>
    /// Momento exato da entrada (UTC). Parte do índice composto (StudentId, Timestamp).
    /// O ML Service usa este campo para calcular todas as 5 features comportamentais.
    /// SEMPRE usar UTC — misturar UTC e horário local corrompe feature engineering.
    /// CheckInService.RegisterAsync() sempre usa DateTime.UtcNow.
    /// </summary>
    public DateTime Timestamp { get; set; }

    /// <summary>
    /// Duração da visita em minutos. Null = check-out não foi registrado.
    /// Não é usado como feature ML na versão atual — apenas para exibição histórica.
    /// Se futuramente usar como feature, precisa retreinar o modelo.
    /// </summary>
    public int? DurationMinutes { get; set; }

    // ── Navigation Property ──────────────────────────────────────────────────────
    /// <summary>
    /// Aluno vinculado. Carregado via .Include(c => c.Student) quando necessário.
    /// O ML Service NÃO usa esta navegação — acessa o banco via SQL direto.
    /// </summary>
    public Student Student { get; set; } = null!;
}
