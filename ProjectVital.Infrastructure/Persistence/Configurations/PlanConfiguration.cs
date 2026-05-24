using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ProjectVital.Domain.Entities;

namespace ProjectVital.Infrastructure.Persistence.Configurations;

/// <summary>
/// Configura o mapeamento da entidade Plan para a tabela "Plans" no MySQL.
/// Chamado automaticamente pelo EF Core ao inicializar ProjectVitalDbContext.
///
/// RESPONSABILIDADE:
///   Define colunas, precision de preço e a FK que isola planos por academia
///   (multitenant — um plano pertence a exatamente uma academia).
///
/// PLANOS SÃO SCOPED POR ACADEMIA:
///   AcademyId é NOT NULL e obrigatório — cada plano pertence a uma academia específica.
///   Um Manager da Academia A nunca deve ver ou atribuir planos da Academia B.
///   O isolamento é enforced em duas camadas:
///     1. Banco: FK Plans.AcademyId → Academies.Id
///     2. Código: StudentService.RegisterAsync valida via AcademyRepository.GetWithPlansAsync
///        que o PlanId informado no input pertence à academia do aluno.
///
/// DECIMAL(10, 2) PARA PREÇO:
///   HasPrecision(10, 2) gera DECIMAL(10,2) no MySQL.
///   DECIMAL é exato (não ponto flutuante) — adequado para valores monetários.
///   Precisão: até 99.999.999,99 (10 dígitos, 2 decimais).
///   SE USAR FLOAT/DOUBLE: risco de erros de arredondamento (ex: 0.1 + 0.2 ≠ 0.3).
///   Não use float para preço — é erro clássico de sistema financeiro.
///   SE PRECISAR DE MAIS CASAS DECIMAIS (ex: criptomoedas): HasPrecision(18, 8).
///
/// FK BEHAVIOR — CASCADE:
///   DeleteBehavior.Cascade = se uma Academy for deletada, todos os seus Plans são
///   automaticamente deletados pelo MySQL.
///   JUSTIFICATIVA: planos sem academia são dados sem sentido — não têm uso isolado.
///   ATENÇÃO: antes de deletar uma Academy, os Students que referenciam Plans dessa Academy
///   seriam bloqueados pelo Restrict em StudentConfiguration (Student.PlanId → Plan.Id).
///   Portanto, a ordem de deleção seria: Students → Plans → Academy (ou soft delete tudo).
///
///   SE MUDAR PARA RESTRICT: delete de Academy falha enquanto tiver Plans → consistência
///   maior mas exige lógica de deleção em cascata no código.
///
/// DURATIONDAYS — FORMATO ESPERADO:
///   Inteiro representando dias. Exemplos de planos comuns:
///   30 = mensal, 90 = trimestral, 180 = semestral, 365 = anual.
///   O frontend deve converter para exibição legível (ex: "30 dias" ou "1 mês").
///   O ML não usa DurationDays diretamente, mas poderia ser uma feature futura
///   (alunos em planos anuais tendem a ter maior engajamento → menor churn?).
///
/// SEM ÍNDICE ADICIONAL:
///   Plans é acessada principalmente via navigation property (Academy.Plans) com eager loading
///   em GetWithPlansAsync. O índice implícito na FK (AcademyId) já cobre essa query.
///   PlanRepository.GetByAcademyIdAsync usa WHERE AcademyId = X ORDER BY Price —
///   o índice da FK cobre o WHERE; ORDER BY Price adiciona sort sobre resultado filtrado.
///   SE ESSA QUERY FICAR LENTA (academia com muitos planos): adicionar índice composto
///   (AcademyId, Price) aqui com HasIndex(p => new { p.AcademyId, p.Price }).
///
/// QUANDO MUDAR ESTE ARQUIVO:
///   1. Qualquer mudança → rodar: dotnet ef migrations add <Nome>
///   2. Se alterar HasPrecision: verificar PlanDto.Price e frontend (formatação de moeda)
///   3. Se adicionar coluna (ex: IsActive, Description): revisar PlanDto e PlanRepository
///   4. Se mudar FK para Restrict: implementar lógica de deleção manual de Plans
///   5. Se implementar CRUD de planos via GraphQL: criar PlanService, PlanResolver, PlanType
///      e registrar IPlanRepository no Program.cs (já existe, mas sem resolver exposto)
/// </summary>
public class PlanConfiguration : IEntityTypeConfiguration<Plan>
{
    public void Configure(EntityTypeBuilder<Plan> builder)
    {
        // Nome exato da tabela no MySQL
        builder.ToTable("Plans");

        // PK autoincrement — referenciado em Student.PlanId
        builder.HasKey(p => p.Id);

        builder.Property(p => p.Id)
            .ValueGeneratedOnAdd();

        // NOT NULL — chave de isolamento multitenant dos planos
        builder.Property(p => p.AcademyId)
            .IsRequired();

        // NOT NULL, máximo 255 chars — ex: "Mensal", "Trimestral", "Anual"
        // Exibido nos dropdowns de cadastro de aluno no frontend
        builder.Property(p => p.Name)
            .IsRequired()
            .HasMaxLength(255);

        // DECIMAL(10,2) — valor monetário exato (sem erro de ponto flutuante)
        // Máximo: 99.999.999,99. Mínimo: 0,01.
        // Usado por PlanRepository.GetByAcademyIdAsync (ORDER BY Price para listar do mais barato)
        builder.Property(p => p.Price)
            .IsRequired()
            .HasPrecision(10, 2);

        // NOT NULL — duração do plano em dias (30=mensal, 90=trimestral, 365=anual)
        // Atualmente não usado pelo ML — possível feature futura de churn prediction
        builder.Property(p => p.DurationDays)
            .IsRequired();

        // FK Plans.AcademyId → Academies.Id
        // CASCADE: delete de Academy apaga todos os seus Plans automaticamente.
        // Impacto: Students com esses Plans são bloqueados pelo Restrict em StudentConfiguration.
        //   Portanto delete de Academy exige: primeiro deletar/migrar Students, depois Plans são
        //   apagados em cascade, finalmente Academy pode ser deletada.
        builder.HasOne(p => p.Academy)
            .WithMany(a => a.Plans)
            .HasForeignKey(p => p.AcademyId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
