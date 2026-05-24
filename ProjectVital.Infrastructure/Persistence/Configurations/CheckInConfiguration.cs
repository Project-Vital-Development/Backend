using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ProjectVital.Domain.Entities;

namespace ProjectVital.Infrastructure.Persistence.Configurations;

/// <summary>
/// Configura o mapeamento da entidade CheckIn para a tabela "CheckIns" no MySQL.
/// Chamado automaticamente pelo EF Core ao inicializar ProjectVitalDbContext.
///
/// RESPONSABILIDADE:
///   Define colunas, chave estrangeira e o índice crítico de performance ML
///   para a tabela de check-ins (entradas do aluno na academia).
///
/// POR QUE CHECK-INS SÃO CRÍTICOS:
///   Cada registro de check-in é a matéria-prima do sistema de predição de churn.
///   O serviço Python lê TODOS os check-ins de um aluno para calcular as 5 features
///   comportamentais (avg_weekly_frequency, days_since_last_checkin, avg_interval_days,
///   frequency_trend, total_checkins). Sem check-ins, o ML não consegue fazer predições.
///
/// FK BEHAVIOR — CASCADE (diferente das outras entidades):
///   DeleteBehavior.Cascade = se um Student for deletado no banco, todos os seus
///   CheckIns são automaticamente deletados em cascata pelo MySQL.
///   MOTIVO DA DIFERENÇA vs. Student→User (Restrict):
///     CheckIns sem Student são dados sem sentido — não têm valor histórico isolados.
///     Student→User usa Restrict porque o User pode existir independentemente.
///   ATENÇÃO: o soft delete (DeletedAt em Student) impede que Students sejam
///   realmente deletados via EF Core, tornando o cascade raramente acionado na prática.
///   SE IMPLEMENTAR HARD DELETE DE STUDENTS: cascade vai apagar todos os check-ins.
///
/// ÍNDICE idx_student_timestamp — CRÍTICO PARA O ML:
///   Índice composto em (StudentId, Timestamp). Esta é a query mais executada do sistema:
///
///     SELECT StudentId, Timestamp, DurationMinutes
///     FROM CheckIns
///     WHERE StudentId IN (SELECT Id FROM Students WHERE AcademyId = X)
///     ORDER BY StudentId, Timestamp
///
///   Essa query é executada toda vez que:
///     - POST /predict é chamado (predição individual)
///     - POST /predict/batch é chamado (predição de toda academia)
///     - POST /retrain é chamado (treinamento do modelo)
///
///   SEM ESTE ÍNDICE: O pipeline ML faz full table scan em CheckIns. Com 100k check-ins
///   e 10 academias, cada predição varre 100k linhas desnecessariamente → timeout.
///   COM ESTE ÍNDICE: MySQL usa index range scan → acesso direto por StudentId.
///
///   SE REMOVER ESTE ÍNDICE: predições e treinamento ficam O(N) sobre toda a tabela.
///   SE MUDAR A QUERY ML em database/queries.py: verificar se o índice ainda cobre.
///   SE ADICIONAR FILTRO POR DATA NO ML: o índice já cobre (StudentId + Timestamp).
///
/// QUANDO MUDAR ESTE ARQUIVO:
///   1. Qualquer mudança → rodar: dotnet ef migrations add <Nome>
///   2. Se alterar FK behavior para Restrict: CheckIns órfãos viram problema de LGPD
///   3. Se adicionar coluna (ex: LocationId): revisar feature engineering Python
///   4. Se remover DurationMinutes: verificar se ML usa (atualmente não usa — feature futura)
/// </summary>
public class CheckInConfiguration : IEntityTypeConfiguration<CheckIn>
{
    public void Configure(EntityTypeBuilder<CheckIn> builder)
    {
        // Nome exato da tabela no MySQL
        builder.ToTable("CheckIns");

        // PK autoincrement
        builder.HasKey(c => c.Id);

        builder.Property(c => c.Id)
            .ValueGeneratedOnAdd();

        // NOT NULL — todo check-in pertence a um aluno
        builder.Property(c => c.StudentId)
            .IsRequired();

        // NOT NULL — UTC obrigatório (ver CheckInService.CheckInAsync)
        // O Python lê Timestamp para calcular days_since_last_checkin e frequency_trend.
        // SE ALTERAR PARA LOCAL TIME: o feature engineering Python quebraria silenciosamente
        //   (cálculos de intervalo de dias ficariam errados com horário de verão).
        builder.Property(c => c.Timestamp)
            .IsRequired();

        // NULLABLE — duração é opcional; aluno pode entrar sem registrar saída
        // Atualmente NÃO usado pelo ML (feature futura potencial: avg_duration_minutes).
        // SE O ML PASSAR A USAR: atualizar FEATURE_COLUMNS em features/engineering.py
        //   e retreinar o modelo antes de alterar o endpoint /predict.
        builder.Property(c => c.DurationMinutes);

        // FK CheckIns.StudentId → Students.Id
        // CASCADE: delete de Student apaga todos os seus CheckIns automaticamente.
        // Impacto LGPD: ao excluir aluno (hard delete), histórico de frequência é perdido.
        // O soft delete atual evita que isso aconteça no fluxo normal.
        builder.HasOne(c => c.Student)
            .WithMany(s => s.CheckIns)
            .HasForeignKey(c => c.StudentId)
            .OnDelete(DeleteBehavior.Cascade);

        // ── Índice CRÍTICO para o ML ──────────────────────────────────────────
        // (StudentId, Timestamp) cobre a query principal do feature engineering.
        // O Python acessa: WHERE StudentId IN (...) ORDER BY StudentId, Timestamp
        // Lido por: database/queries.py → get_student_checkins() e get_academy_checkins()
        // SE REMOVER: latência do ML aumenta dramaticamente em produção.
        builder.HasIndex(c => new { c.StudentId, c.Timestamp })
            .HasDatabaseName("idx_student_timestamp");
    }
}
