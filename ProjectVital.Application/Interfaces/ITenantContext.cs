namespace ProjectVital.Application.Interfaces;

/// <summary>
/// Academia (tenant) do contexto atual, derivada SEMPRE do servidor:
///   - requisição HTTP autenticada de gestor → academyId do JWT;
///   - requisição HTTP sem academia (anônima, aluno) → nenhum acesso a dados de academias;
///   - código sem requisição HTTP (seed, jobs em segundo plano) → escopo de sistema.
///
/// Usado pelos filtros globais do EF Core (ProjectVitalDbContext) como segunda
/// barreira de isolamento, além das checagens nos resolvers GraphQL.
/// </summary>
public interface ITenantContext
{
    /// <summary>true = sem filtro de academia (somente processos internos).</summary>
    bool IsSystem { get; }

    /// <summary>Academia autorizada; -1 quando não há nenhuma.</summary>
    int AcademyId { get; }
}

/// <summary>Escopo de sistema explícito (migrations, design-time, seed).</summary>
public sealed class SystemTenantContext : ITenantContext
{
    public static readonly SystemTenantContext Instance = new();
    public bool IsSystem => true;
    public int AcademyId => -1;
}
