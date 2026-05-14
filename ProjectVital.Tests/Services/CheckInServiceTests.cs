using Moq;
using Xunit;
using ProjectVital.Application.Services;
using ProjectVital.Domain.Entities;
using ProjectVital.Domain.Interfaces;

namespace ProjectVital.Tests.Services;

public class CheckInServiceTests
{
    private readonly Mock<ICheckInRepository> _checkInRepo;
    private readonly Mock<IStudentRepository> _studentRepo;
    private readonly CheckInService _sut;

    public CheckInServiceTests()
    {
        _checkInRepo = new Mock<ICheckInRepository>();
        _studentRepo = new Mock<IStudentRepository>();
        _sut = new CheckInService(_checkInRepo.Object, _studentRepo.Object);
    }

    [Fact]
    public async Task RegisterAsync_WithActiveStudent_CreatesCheckIn()
    {
        var student = new Student { Id = 1, Active = true };
        _studentRepo.Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(student);

        var checkIn = new CheckIn { Id = 1, StudentId = 1, Timestamp = DateTime.UtcNow };
        _checkInRepo.Setup(r => r.AddAsync(It.IsAny<CheckIn>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(checkIn);
        _checkInRepo.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        var result = await _sut.RegisterAsync(1);

        Assert.NotNull(result);
        Assert.Equal(1, result.StudentId);
    }

    [Fact]
    public async Task RegisterAsync_WithInactiveStudent_ThrowsInvalidOperation()
    {
        var student = new Student { Id = 2, Active = false };
        _studentRepo.Setup(r => r.GetByIdAsync(2, It.IsAny<CancellationToken>()))
            .ReturnsAsync(student);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _sut.RegisterAsync(2));
    }

    [Fact]
    public async Task RegisterAsync_WithUnknownStudent_ThrowsKeyNotFound()
    {
        _studentRepo.Setup(r => r.GetByIdAsync(99, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Student?)null);

        await Assert.ThrowsAsync<KeyNotFoundException>(
            () => _sut.RegisterAsync(99));
    }
}
