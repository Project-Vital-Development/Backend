using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ProjectVital.Domain.Entities;
using ProjectVital.Domain.Enums;

namespace ProjectVital.Infrastructure.Persistence.Configurations;

/// <summary>
/// Configura o mapeamento da entidade User para a tabela "Users" no MySQL.
/// Chamado automaticamente pelo EF Core ao inicializar ProjectVitalDbContext.
///
/// RESPONSABILIDADE:
///   Define colunas, conversão do enum UserRole para string, soft delete (DeletedAt),
///   e o índice único de email que sustenta o sistema de autenticação.
///
/// CONVERSÃO DE ENUM UserRole → STRING:
///   O MySQL armazena Role como VARCHAR com valores "Admin", "Manager" ou "Student".
///   O C# usa o enum UserRole { Admin, Manager, Student }.
///   HasConversion serializa: Admin → "Admin", Manager → "Manager", Student → "Student"
///   (PascalCase — diferente do RiskLevel que usa lowercase).
///
///   SE ADICIONAR UM NOVO ROLE:
///     1. Adicionar ao enum UserRole em Domain/Enums/UserRole.cs
///     2. Atualizar HasConversion aqui se necessário
///     3. Atualizar ResolverAuthHelper.cs (autorização baseada em role)
///     4. Atualizar AuthService.GenerateJwtToken() (claim de role no JWT)
///     5. Gerar migration para refletir o novo valor permitido no banco
///     6. Revisar todas as queries que filtram por Role
///
/// SENHA — NUNCA ARMAZENADA EM TEXTO PLANO:
///   A coluna PasswordHash armazena EXCLUSIVAMENTE o hash BCrypt (work factor 12).
///   Exemplo de valor: "$2a$12$abc123..."
///   BCrypt.Net-Next verifica a senha em BCrypt.Verify(plainText, hash).
///   SE ALTERAR O WORK FACTOR: hashes antigos ainda funcionam (BCrypt extrai o fator do hash).
///   NUNCA faça SELECT * FROM Users e logue o resultado — PasswordHash seria exposto.
///
/// SOFT DELETE (LGPD Art. 18 — Direito de Exclusão):
///   A coluna DeletedAt é configurada pelo Global Query Filter em ProjectVitalDbContext,
///   não aqui. Mas o campo existe na entidade User.
///   Quando um usuário é "deletado", User.SoftDelete() preenche DeletedAt com DateTime.UtcNow.
///   O Global Query Filter filtra WHERE DeletedAt IS NULL em todas as queries EF Core.
///   IMPACTO: UserRepository.GetByEmailAsync e EmailExistsAsync nunca retornam usuários deletados.
///   Para HARD DELETE (ex: implementação futura de LGPD completo): usar IgnoreQueryFilters()
///   e depois remover o registro físico.
///
/// ÍNDICE idx_email_unique — BASE DO LOGIN:
///   Email tem UNIQUE constraint no MySQL. Isso garante dois efeitos:
///   1. Performance: login via GetByEmailAsync é O(log N) com index lookup.
///   2. Integridade: o banco nunca permite dois usuários com o mesmo email,
///      mesmo que UserRepository.EmailExistsAsync tenha uma race condition.
///
///   SE REMOVER O ÍNDICE ÚNICO: dois usuários podem ter o mesmo email, quebrando o login.
///   SE IMPLEMENTAR SOFT DELETE + MESMO EMAIL: um usuário deletado bloqueia re-cadastro com
///   o mesmo email. Solução: soft delete não pode usar email duplicado sem lógica extra.
///
/// RELACIONAMENTOS (configurados nas entidades filhas):
///   User → Student: configurado em StudentConfiguration (HasOne/WithOne)
///   User → Academy: configurado em AcademyConfiguration (HasOne/WithOne para Manager)
///   A UserConfiguration não configura navegações — apenas a tabela Users em si.
///
/// QUANDO MUDAR ESTE ARQUIVO:
///   1. Qualquer mudança → rodar: dotnet ef migrations add <Nome>
///   2. Se alterar HasMaxLength(255) no Email: verificar se emails longos são suportados
///   3. Se alterar HasMaxLength(255) no PasswordHash: BCrypt gera ~60 chars, 255 é seguro
///   4. Se alterar CURRENT_TIMESTAMP(6): precisão de microsegundos — MySQL 8 suporta
///   5. Se remover UserRole: impacta JWT claims, ResolverAuthHelper, todos os [Authorize]
/// </summary>
public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        // Nome exato da tabela no MySQL
        builder.ToTable("Users");

        // PK autoincrement — sub claim do JWT
        builder.HasKey(u => u.Id);

        builder.Property(u => u.Id)
            .ValueGeneratedOnAdd();

        // NOT NULL, máximo 255 chars — nome de exibição do usuário no dashboard
        builder.Property(u => u.Name)
            .IsRequired()
            .HasMaxLength(255);

        // NOT NULL, máximo 255 chars — credencial de login
        // Garantia de unicidade via índice abaixo (idx_email_unique)
        builder.Property(u => u.Email)
            .IsRequired()
            .HasMaxLength(255);

        // NOT NULL — SEMPRE hash BCrypt, nunca texto plano
        // BCrypt gera strings de ~60 chars; 255 dá margem para algoritmos futuros
        builder.Property(u => u.PasswordHash)
            .IsRequired()
            .HasMaxLength(255);

        // UserRole enum → VARCHAR PascalCase no MySQL
        // Conversão: Admin → "Admin", Manager → "Manager", Student → "Student"
        // HasMaxLength(20): "Manager" é o mais longo (7 chars) — 20 dá margem folgada
        builder.Property(u => u.Role)
            .IsRequired()
            .HasConversion(
                v => v.ToString(),                                      // C# → MySQL: enum.ToString()
                v => (UserRole)Enum.Parse(typeof(UserRole), v))        // MySQL → C#: parse string
            .HasMaxLength(20);

        // NOT NULL — DEFAULT CURRENT_TIMESTAMP(6) no MySQL
        // (6) = precisão de microsegundos — permite ordenar registros criados no mesmo segundo
        builder.Property(u => u.CreatedAt)
            .IsRequired()
            .HasDefaultValueSql("CURRENT_TIMESTAMP(6)");

        // Índice único sobre Email — duas funções:
        // 1. Performance: login busca por email (O(log N))
        // 2. Integridade: MySQL rejeita INSERT duplicado mesmo sem validação no código
        // USADO POR: UserRepository.GetByEmailAsync (AuthService), EmailExistsAsync (StudentService)
        builder.HasIndex(u => u.Email)
            .IsUnique()
            .HasDatabaseName("idx_email_unique");
    }
}
