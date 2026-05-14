using Moq;
using ProjectVital.Application.Inputs;
using ProjectVital.Application.Services;
using ProjectVital.Domain.Entities;
using ProjectVital.Domain.Enums;
using ProjectVital.Domain.Interfaces;
using Xunit;
using Microsoft.Extensions.Configuration;

namespace ProjectVital.Tests.Services;

public class AuthServiceTests
{
    private readonly Mock<IUserRepository> _userRepo;
    private readonly Mock<IAcademyRepository> _academyRepo;
    private readonly AuthService _sut;

    public AuthServiceTests()
    {
        _userRepo = new Mock<IUserRepository>();
        _academyRepo = new Mock<IAcademyRepository>();

        var config = new Mock<IConfiguration>();
        config.Setup(c => c["JWT_SECRET"]).Returns("test-jwt-secret-that-is-at-least-32-chars!!");
        config.Setup(c => c["Jwt:ExpiryHours"]).Returns("24");

        _sut = new AuthService(_userRepo.Object, _academyRepo.Object, config.Object);
    }

    [Fact]
    public void HashPassword_ReturnsBCryptHash()
    {
        var hash = _sut.HashPassword("senha123");
        Assert.True(BCrypt.Net.BCrypt.Verify("senha123", hash));
    }

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
        _academyRepo.Setup(r => r.GetByManagerIdAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Academy { Id = 10, ManagerId = 1, Name = "Test" });

        var result = await _sut.LoginAsync(new LoginInput("gestor@test.dev", password));

        Assert.NotNull(result);
        Assert.NotEmpty(result.Token);
        Assert.Equal(UserRole.Manager, result.User.Role);
        Assert.Equal(10, result.AcademyId);
    }

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

    [Fact]
    public async Task LoginAsync_WithUnknownEmail_ThrowsUnauthorized()
    {
        _userRepo.Setup(r => r.GetByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => _sut.LoginAsync(new LoginInput("desconhecido@test.dev", "senha")));
    }
}
