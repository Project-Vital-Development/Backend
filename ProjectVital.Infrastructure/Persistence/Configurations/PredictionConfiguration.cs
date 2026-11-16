using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ProjectVital.Domain.Entities;
using ProjectVital.Domain.Enums;

namespace ProjectVital.Infrastructure.Persistence.Configurations;

/// <summary>
/// Configura o mapeamento da entidade Prediction para a tabela "Predictions" no MySQL.
/// Chamado automaticamente pelo EF Core ao inicializar ProjectVitalDbContext.
///
/// RESPONSABILIDADE:
///   Define colunas, conversão de enum RiskLevel para string MySQL, FK e índice
///   para busca eficiente da predição mais recente por aluno.
///
/// CONVERSÃO DE ENUM RiskLevel → STRING LOWERCASE:
///   O MySQL armazena RiskLevel como VARCHAR com valores "low", "medium" ou "high".
///   O C# usa o enum RiskLevel { Low, High, Medium }.
///   HasConversion serializa: Low → "low", High → "high", Medium → "medium".
///   O Python também usa lowercase ("low"/"medium"/"high") no JSON de resposta.
///
///   ⚠ PONTO DE SINCRONIZAÇÃO CRÍTICO — 4 lugares devem estar alinhados:
///     1. ml-service/prediction/predictor.py → _classify_risk(): thresholds <0.3 / 0.3-0.6 / >0.6
///     2. backend/ProjectVital.Domain/Extensions/RiskLevelExtensions.cs → FromProbability()
///     3. frontend/src/components/ui/Badge.tsx → riskConfig (cores por nível)
///     4. frontend/src/components/ui/RiskBar.tsx → getRiskColor() (barra de risco)
///   Se mudar o threshold em qualquer um desses lugares, mudar nos outros 3 também.
///
///   SE ADICIONAR UM NOVO NÍVEL (ex: "critical"):
///     1. Adicionar ao enum RiskLevel em Domain/Enums/
///     2. Atualizar HasConversion aqui (o parser já usa Enum.Parse com ignoreCase=true)
///     3. Atualizar _classify_risk() no Python
///     4. Atualizar riskConfig no Badge.tsx
///     5. Atualizar getRiskColor() no RiskBar.tsx
///     6. Gerar migration para alterar a coluna (HasMaxLength pode precisar de ajuste)
///
/// IMUTABILIDADE DAS PREDIÇÕES:
///   Predições não são atualizadas — apenas inseridas. Cada chamada a runPrediction
///   cria um novo registro. Não há lógica de UPDATE em PredictionRepository.
///   Isso garante rastreabilidade histórica: o gestor pode ver como o risco do aluno
///   evoluiu ao longo do tempo.
///
/// FK BEHAVIOR — CASCADE:
///   Se um Student for deletado (hard delete), todas as suas Predictions são deletadas.
///   Consistente com CheckIns: sem o aluno, as predições não têm contexto útil.
///   O soft delete atual evita que esse cascade ocorra no fluxo normal.
///
/// ÍNDICE idx_student_predictions:
///   Índice composto em (StudentId, PredictedAt). Otimiza a query:
///     SELECT * FROM Predictions WHERE StudentId = X ORDER BY PredictedAt DESC LIMIT 1
///   Essa query busca a predição mais recente por aluno — executada em:
///     PredictionRepository.GetLatestByAcademyAsync() (para listar dashboard de predições)
///     PredictionsQuery (StudentResolver aninhado — última predição do aluno)
///   O índice permite que o MySQL resolva ORDER BY PredictedAt DESC via index scan reverso.
///
/// QUANDO MUDAR ESTE ARQUIVO:
///   1. Qualquer mudança → rodar: dotnet ef migrations add <Nome>
///   2. Se alterar HasConversion: verificar que Python ainda retorna o mesmo valor de string
///   3. Se alterar ModelVersion MaxLength: verificar formato em trainer.py (atualmente "xgb_v{N}")
///   4. Se alterar índice: verificar GetLatestByAcademyAsync em PredictionRepository
/// </summary>
public class PredictionConfiguration : IEntityTypeConfiguration<Prediction>
{
    public void Configure(EntityTypeBuilder<Prediction> builder)
    {
        // Nome exato da tabela no MySQL
        builder.ToTable("Predictions");

        // PK autoincrement
        builder.HasKey(p => p.Id);

        builder.Property(p => p.Id)
            .ValueGeneratedOnAdd();

        // NOT NULL — toda predição pertence a um aluno
        builder.Property(p => p.StudentId)
            .IsRequired();

        // FLOAT (equivalente a double em C#) — probabilidade de churn entre 0.0 e 1.0
        // Gerado por predict_proba()[:,1] no Python (probabilidade da classe positiva = churn)
        // NÃO tem constraint de range no banco — a responsabilidade de 0≤p≤1 é do modelo ML.
        builder.Property(p => p.Probability)
            .IsRequired();

        // RiskLevel enum → VARCHAR lowercase no MySQL
        // Conversão bidirecional: C# enum ↔ string lowercase no banco
        // HasMaxLength(10): "medium" é o mais longo (6 chars) — 10 dá margem para futuros
        // SE ADICIONAR NÍVEL "critical" (8 chars): MaxLength(10) ainda comporta
        builder.Property(p => p.RiskLevel)
            .IsRequired()
            .HasConversion(
                v => v.ToString().ToLowerInvariant(),    // C# → MySQL: "Medium" → "medium"
                v => (RiskLevel)Enum.Parse(typeof(RiskLevel), v, true)) // MySQL → C#: ignoreCase=true
            .HasMaxLength(10);

        // ModelVersion — ex: "xgb_v3"
        // Registra qual versão do modelo gerou esta predição (rastreabilidade)
        // SE O FORMATO MUDAR no trainer.py: MaxLength(50) ainda comporta versões mais longas
        builder.Property(p => p.ModelVersion)
            .IsRequired()
            .HasMaxLength(50);

        // NOT NULL — UTC, gerado por PredictionService.RunAsync no momento da predição
        builder.Property(p => p.PredictedAt)
            .IsRequired();

        // FK Predictions.StudentId → Students.Id
        // CASCADE: delete de Student apaga todas as suas Predictions.
        // Soft delete atual impede que ocorra no fluxo normal.
        builder.HasOne(p => p.Student)
            .WithMany(s => s.Predictions)
            .HasForeignKey(p => p.StudentId)
            .OnDelete(DeleteBehavior.Cascade);

        // Índice composto (StudentId, PredictedAt)
        // Otimiza: WHERE StudentId = X ORDER BY PredictedAt DESC (busca de última predição)
        // USADO POR: PredictionRepository.GetLatestByAcademyAsync (MAX subquery + JOIN)
        // SE REMOVER: queries de dashboard de predições ficam O(N) sobre a tabela inteira.
        builder.HasIndex(p => new { p.StudentId, p.PredictedAt })
            .HasDatabaseName("idx_student_predictions");
    }
}
