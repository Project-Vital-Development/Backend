using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ProjectVital.Domain.Entities;

namespace ProjectVital.Infrastructure.Persistence.Configurations;

/// <summary>
/// Configura o mapeamento da entidade Academy para a tabela "Academies" no MySQL.
/// Chamado automaticamente pelo EF Core ao inicializar ProjectVitalDbContext.
///
/// RESPONSABILIDADE:
///   Define colunas e a FK crítica Academy.ManagerId → Users.Id, que estabelece
///   o vínculo 1:1 entre um Manager e sua academia no modelo multitenant.
///
/// ACADEMY É O TENANT ROOT:
///   Toda entidade do sistema (Student, Plan, CheckIn, Prediction) tem AcademyId
///   como coluna de isolamento multitenant. A Academy é o "namespace" de cada cliente.
///   Um Manager só vê dados da sua própria Academy (enforced pelo JWT claim `academyId`
///   e pelo ResolverAuthHelper.EnforceAcademyAccess em cada resolver).
///
/// MODELO ATUAL — UM MANAGER POR ACADEMY:
///   A FK ManagerId é 1:1 (HasOne/WithOne). Um Manager gerencia exatamente uma Academy.
///   SE NO FUTURO UM MANAGER PUDER GERENCIAR MÚLTIPLAS ACADEMIES:
///     1. Mudar para HasOne/WithMany ou introduzir tabela de junção Academy_Managers
///     2. Atualizar AcademyRepository.GetByManagerIdAsync (retorna null, precisaria retornar lista)
///     3. Atualizar JWT: claim `academyId` precisa de estratégia para múltiplas (lista ou seleção)
///     4. Atualizar ResolverAuthHelper.EnforceAcademyAccess (1 academy vs. lista)
///     5. Gerar migration para remover a constraint de unicidade em ManagerId
///
/// FK BEHAVIOR — RESTRICT:
///   DeleteBehavior.Restrict = MySQL não deixa deletar o User (Manager) enquanto
///   houver uma Academy vinculada a ele.
///   MOTIVO: a Academy é o tenant root — deletar o Manager não deve automaticamente
///   apagar toda a academia e seus dados. O processo correto seria:
///   1. Migrar a academia para outro Manager (ou desativá-la)
///   2. Depois deletar o User do Manager antigo
///
/// ADDRESS É OPCIONAL:
///   Address é nullable (sem IsRequired()). Academia pode ser cadastrada sem endereço.
///   Útil para cadastro rápido — endereço pode ser preenchido depois.
///   SE PRECISAR TORNAR OBRIGATÓRIO: adicionar .IsRequired() e gerar migration.
///   O frontend deve exibir "—" ou "Não informado" quando Address for null.
///
/// SEM ÍNDICE ADICIONAL:
///   Academies é uma tabela pequena (1 linha por cliente SaaS). Não precisa de índices
///   além da PK (Id) e da FK (ManagerId, que o MySQL indexa implicitamente).
///   A query mais comum é por Id (GetByIdAsync) ou por ManagerId (GetByManagerIdAsync)
///   — ambas já são eficientes pela PK e pela FK index.
///
/// QUANDO MUDAR ESTE ARQUIVO:
///   1. Qualquer mudança → rodar: dotnet ef migrations add <Nome>
///   2. Se adicionar coluna (ex: PhoneNumber, LogoUrl): revisar AcademyDto e AcademyType
///   3. Se alterar ManagerId para nullable: rever modelo de propriedade da academy
///   4. Se adicionar índice (ex: por Name para busca por nome): criar HasIndex aqui
///   5. Se mudar FK behavior para Cascade: PERIGO — deletar Manager apaga toda a Academy
/// </summary>
public class AcademyConfiguration : IEntityTypeConfiguration<Academy>
{
    public void Configure(EntityTypeBuilder<Academy> builder)
    {
        // Nome exato da tabela no MySQL
        builder.ToTable("Academies");

        // PK autoincrement — valor incluído no JWT claim `academyId` dos Managers
        builder.HasKey(a => a.Id);

        builder.Property(a => a.Id)
            .ValueGeneratedOnAdd();

        // NOT NULL, máximo 255 chars — nome da academia exibido no dashboard
        builder.Property(a => a.Name)
            .IsRequired()
            .HasMaxLength(255);

        // NULLABLE — endereço é opcional no cadastro inicial
        // NULL no banco → frontend exibe "Não informado"
        builder.Property(a => a.Address)
            .HasMaxLength(500);

        // NOT NULL — ID do Manager responsável pela academia
        // Único por academia (1:1 com User de role Manager)
        builder.Property(a => a.ManagerId)
            .IsRequired();

        // NOT NULL — DEFAULT CURRENT_TIMESTAMP(6) no MySQL
        // Registra quando a academia foi cadastrada na plataforma
        builder.Property(a => a.CreatedAt)
            .IsRequired()
            .HasDefaultValueSql("CURRENT_TIMESTAMP(6)");

        // FK Academies.ManagerId → Users.Id (1:1)
        // Restrict: não deixa deletar User (Manager) enquanto tiver Academy associada.
        // USADO POR: AcademyRepository.GetByManagerIdAsync → AuthService.LoginAsync
        //   (para incluir AcademyId no JWT do Manager após login)
        builder.HasOne(a => a.Manager)
            .WithOne(u => u.ManagedAcademy)
            .HasForeignKey<Academy>(a => a.ManagerId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
