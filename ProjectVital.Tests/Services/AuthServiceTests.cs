using Moq;
using ProjectVital.Application.Inputs;
using ProjectVital.Application.Services;
using ProjectVital.Domain.Entities;
using ProjectVital.Domain.Enums;
using ProjectVital.Domain.Interfaces;
using Xunit;
using Microsoft.Extensions.Configuration;

namespace ProjectVital.Tests.Services;

/// <summary>
/// Testes unitários de AuthService.
///
/// ESTRATÉGIA DE TESTES:
///   - Usa Moq para simular IUserRepository e IAcademyRepository (sem banco de dados real)
///   - Usa IConfiguration mockado com JWT_SECRET válido (>= 32 chars)
///   - _sut (System Under Test) = a instância real de AuthService
///
/// POR QUE SEM BANCO REAL:
///   Testes unitários devem ser rápidos e isolados. O banco é testado em integração.
///   Se os testes de integração forem implementados (WebApplicationFactory), eles
///   complementam estes testando o fluxo ponta a ponta.
///
/// EXECUTAR: dotnet test --filter "FullyQualifiedName~AuthServiceTests"
///
/// SE ADICIONAR MÉTODO AO IAuthService:
///   Adicionar teste correspondente aqui seguindo o padrão Arrange-Act-Assert.
///   Cada teste deve cobrir: caminho feliz + casos de erro esperados.
/// </summary>
public class AuthServiceTests
{
    private readonly Mock<IUserRepository> _userRepo;
    private readonly Mock<IAcademyRepository> _academyRepo;
    private readonly AuthService _sut;

    public AuthServiceTests()
    {
        _userRepo = new Mock<IUserRepository>();
        _academyRepo = new Mock<IAcademyRepository>();

        // JWT_SECRET deve ter >= 32 chars — AuthService lança se menor
        var config = new Mock<IConfiguration>();
        config.Setup(c => c["JWT_SECRET"]).Returns("test-jwt-secret-that-is-at-least-32-chars!!");
        config.Setup(c => c["Jwt:ExpiryHours"]).Returns("24");

        _sut = new AuthService(_userRepo.Object, _academyRepo.Object, config.Object);
    }

    /// <summary>
    /// HashPassword deve retornar hash BCrypt verificável.
    /// Garante que o work factor está correto e o hash é gerado sem corrupção.
    /// </summary>
    [Fact]
    public void HashPassword_ReturnsBCryptHash()
    {
        var hash = _sut.HashPassword("senha123");
        Assert.True(BCrypt.Net.BCrypt.Verify("senha123", hash));
    }

    /// <summary>
    /// Login com credenciais válidas de Manager deve retornar AuthPayload completo.
    /// Verifica: token gerado, role correta, academyId mapeado do banco.
    ///
    /// SE MUDAR OS CLAIMS DO JWT: este teste pode quebrar se AcademyId não for mais
    /// incluído no AuthPayloadDto — verificar AuthService.LoginAsync().
    /// </summary>
    [Fact]
    public async Task LoginAsync_WithValidManagerCredentials_ReturnsAuthPayload()
    {
        var password = "MinhaS3nha!";
        var user = new User
        {
            Id = 1,
            Name = "Gestor Teste",
            Email = "gestor@test.dev",
            PasswordHash = _sut.HashPassword(password),
            Role = UserRole.Manager
        };

        _userRepo.Setup(r => r.GetByEmailAsync("gestor@test.dev", It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);
        // Manager precisa de uma academia vinculada — AuthService busca pelo ManagerId
        _academyRepo.Setup(r => r.GetByManagerIdAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Academy { Id = 10, ManagerId = 1, Name = "Test" });

        var result = await _sut.LoginAsync(new LoginInput("gestor@test.dev", password));

        Assert.NotNull(result);
        Assert.NotEmpty(result.Token);
        Assert.Equal(UserRole.Manager, result.User.Role);
        Assert.Equal(10, result.AcademyId);  // AcademyId deve vir do banco, não do input
    }

    /// <summary>
    /// Senha incorreta deve lançar UnauthorizedAccessException.
    /// BCrypt.Verify retorna false → AuthService lança sem revelar que o email existe.
    /// </summary>
    [Fact]
    public async Task LoginAsync_WithWrongPassword_ThrowsUnauthorized()
    {
        var user = new User
        {
            Id = 1,
            Email = "gestor@test.dev",
            PasswordHash = _sut.HashPassword("senhaCorreta"),
            Role = UserRole.Manager
        };

        _userRepo.Setup(r => r.GetByEmailAsync("gestor@test.dev", It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => _sut.LoginAsync(new LoginInput("gestor@test.dev", "senhaErrada")));
    }

    /// <summary>
    /// Email não cadastrado deve lançar UnauthorizedAccessException (mesma exceção que senha errada).
    /// ANTI-PATTERN EVITADO: não deve lançar NotFoundException — isso revelaria que o email não existe.
    /// O AppErrorFilter mapeia ambos os casos para "Credenciais inválidas." no GraphQL.
    /// </summary>
    [Fact]
    public async Task LoginAsync_WithUnknownEmail_ThrowsUnauthorized()
    {
        _userRepo.Setup(r => r.GetByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => _sut.LoginAsync(new LoginInput("desconhecido@test.dev", "senha")));
    }
}
