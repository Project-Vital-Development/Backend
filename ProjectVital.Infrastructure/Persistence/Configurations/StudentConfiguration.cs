using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ProjectVital.Domain.Entities;

namespace ProjectVital.Infrastructure.Persistence.Configurations;

/// <summary>
/// Configura o mapeamento da entidade Student para a tabela "Students" no MySQL.
/// Chamado automaticamente pelo EF Core ao inicializar ProjectVitalDbContext
/// (via modelBuilder.ApplyConfigurationsFromAssembly).
///
/// RESPONSABILIDADE:
///   Define colunas, tipos, restrições, chaves estrangeiras e índices para Students.
///   Tudo que está aqui vira DDL SQL quando você roda "dotnet ef migrations add".
///
/// POR QUE FLUENT API EM VEZ DE DATA ANNOTATIONS:
///   Mantém o Domain limpo (sem dependência de EF Core nas entidades).
///   Permite configurações que Data Annotations não suportam (ex: índices compostos).
///
/// FK BEHAVIOR — POR QUE RESTRICT EM TODOS OS FKs:
///   DeleteBehavior.Restrict = MySQL não deixa deletar o pai enquanto houver filhos.
///   Se você deletar um User, Academy ou Plan diretamente no banco, o MySQL retorna erro.
///   MOTIVO: preservamos histórico de alunos — um aluno deletado ainda pode ter
///   check-ins e predições históricos que não devem ser perdidos.
///   O soft delete (DeletedAt) é o mecanismo correto para "desativar" registros.
///   SE MUDAR PARA CASCADE: PERIGO — delete de Academy apaga todos os Students e,
///   por cascade chain, todos os CheckIns e Predictions desses Students.
///
/// ÍNDICE idx_academy_students:
///   Índice composto em (AcademyId, Active). Usado pela query:
///     SELECT * FROM Students WHERE AcademyId = X AND Active = TRUE
///   Essa query é executada toda vez que o dashboard lista alunos ativos de uma academia.
///   Sem este índice: O(N) full table scan. Com ele: O(log N).
///   SE REMOVER: dashboard fica lento com academias grandes.
///   SE ADICIONAR NOVAS COLUNAS DE FILTRO FREQUENTE: avaliar novo índice aqui.
///
/// SOFT DELETE (LGPD Art. 18):
///   A coluna DeletedAt é adicionada pelo Global Query Filter em ProjectVitalDbContext,
///   não aqui. O filtro WHERE DeletedAt IS NULL é aplicado automaticamente em todas
///   as queries EF Core. Para incluir registros deletados: use IgnoreQueryFilters().
///
/// QUANDO MUDAR ESTE ARQUIVO:
///   1. Mudar qualquer propriedade ou FK → rodar: dotnet ef migrations add <Nome>
///   2. Se alterar DeleteBehavior: revisar o que acontece com CheckIns e Predictions
///   3. Se adicionar coluna: revisar StudentDto, RegisterStudentInput, UpdateStudentInput
///   4. Se alterar índice: revisar performance das queries em StudentService e resolvers
/// </summary>
public class StudentConfiguration : IEntityTypeConfiguration<Student>
{
    public void Configure(EntityTypeBuilder<Student> builder)
    {
        // Nome exato da tabela no MySQL — se mudar, gerar migration
        builder.ToTable("Students");

        // PK autoincrement — MySQL gera o valor no INSERT
        builder.HasKey(s => s.Id);

        builder.Property(s => s.Id)
            .ValueGeneratedOnAdd();

        // Campos NOT NULL — sem default, obrigatórios no INSERT
        builder.Property(s => s.UserId).IsRequired();
        builder.Property(s => s.AcademyId).IsRequired();
        builder.Property(s => s.PlanId).IsRequired();

        builder.Property(s => s.EnrollmentDate)
            .IsRequired();

        // DEFAULT TRUE — novo aluno já entra como ativo
        // Mudança de Active para false = aluno inativo (não aparece nas predições)
        // SE MUDAR O DEFAULT: revisar StudentService.RegisterAsync e seed de dados
        builder.Property(s => s.Active)
            .IsRequired()
            .HasDefaultValue(true);

        // ── FKs ──────────────────────────────────────────────────────────────
        // Student.UserId → User.Id (1:1)
        // Restrict: não deixa deletar User se tiver Student associado.
        // POR QUE 1:1: cada conta de usuário pertence a exatamente um aluno.
        // SE NO FUTURO UM USER PUDER SER ALUNO EM MÚLTIPLAS ACADEMIAS:
        //   mudar para HasMany/WithOne e remover a constraint de unicidade.
        builder.HasOne(s => s.User)
            .WithOne(u => u.Student)
            .HasForeignKey<Student>(s => s.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        // Student.AcademyId → Academy.Id (N:1) — isolamento multitenant
        // Restrict: não deixa deletar Academy com Students. Soft delete da Academy seria necessário.
        // IMPACTO NO ML: AcademyId é usado para filtrar alunos no feature engineering.
        builder.HasOne(s => s.Academy)
            .WithMany(a => a.Students)
            .HasForeignKey(s => s.AcademyId)
            .OnDelete(DeleteBehavior.Restrict);

        // Student.PlanId → Plan.Id (N:1)
        // Restrict: não deixa deletar Plan com Students matriculados.
        // SE IMPLEMENTAR CRUD DE PLANOS: precisará de lógica para migrar alunos
        //   antes de deletar um plano, ou mudar para SetNull com PlanId nullable.
        builder.HasOne(s => s.Plan)
            .WithMany(p => p.Students)
            .HasForeignKey(s => s.PlanId)
            .OnDelete(DeleteBehavior.Restrict);

        // ── Índices ──────────────────────────────────────────────────────────
        // Índice composto (AcademyId, Active) — otimiza listagem de alunos ativos.
        // USADO POR: StudentsQuery (StudentResolver), GetKPIsAsync (AcademyService),
        //   PredictionRepository.GetLatestByAcademyAsync (via JOIN com Students).
        // ORDEM IMPORTA: AcademyId primeiro (filtro de maior seletividade em multitenant).
        builder.HasIndex(s => new { s.AcademyId, s.Active })
            .HasDatabaseName("idx_academy_students");
    }
}
