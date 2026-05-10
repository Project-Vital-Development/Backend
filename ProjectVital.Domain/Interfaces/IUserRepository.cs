using ProjectVital.Domain.Entities;

namespace ProjectVital.Domain.Interfaces;

/// <summary>
/// Repositório de usuários. Centraliza acesso à entidade User.
/// </summary>
public interface IUserRepository : IRepository<User>
{
    /// <summary>Busca um usuário pelo email (usado no login).</summary>
    Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken = default);

    /// <summary>Verifica se já existe um usuário com o email fornecido.</summary>
    Task<bool> EmailExistsAsync(string email, CancellationToken cancellationToken = default);
}
