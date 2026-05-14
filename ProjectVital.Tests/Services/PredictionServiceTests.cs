using Moq;
using Xunit;
using ProjectVital.Application.Services;
using ProjectVital.Domain.Entities;
using ProjectVital.Domain.Enums;
using ProjectVital.Domain.Interfaces;

namespace ProjectVital.Tests.Services;

public class PredictionServiceTests
{
    private readonly Mock<IPredictionRepository> _predRepo;
    private readonly PredictionService _sut;

    public PredictionServiceTests()
    {
        _predRepo = new Mock<IPredictionRepository>();
        _sut = new PredictionService(_predRepo.Object);
    }

    [Fact]
    public async Task GetLatestByStudentAsync_ReturnsMappedDto()
    {
        var prediction = new Prediction
        {
            Id = 1,
            StudentId = 5,
            Probability = 0.75,
            RiskLevel = RiskLevel.High,
            ModelVersion = "rf_v1.0",
            PredictedAt = DateTime.UtcNow
        };

        _predRepo.Setup(r => r.GetLatestByStudentAsync(5, It.IsAny<CancellationToken>()))
            .ReturnsAsync(prediction);

        var result = await _sut.GetLatestByStudentAsync(5);

        Assert.NotNull(result);
        Assert.Equal(0.75, result.Probability);
        Assert.Equal(RiskLevel.High, result.RiskLevel);
    }

    [Fact]
    public async Task GetLatestByStudentAsync_WhenNoPrediction_ReturnsNull()
    {
        _predRepo.Setup(r => r.GetLatestByStudentAsync(99, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Prediction?)null);

        var result = await _sut.GetLatestByStudentAsync(99);

        Assert.Null(result);
    }

    [Fact]
    public async Task RunAsync_IsStub_ThrowsNotImplemented()
    {
        await Assert.ThrowsAsync<NotImplementedException>(
            () => _sut.RunAsync(1));
    }

    [Fact]
    public async Task RunBatchAsync_IsStub_ThrowsNotImplemented()
    {
        await Assert.ThrowsAsync<NotImplementedException>(
            () => _sut.RunBatchAsync(1));
    }
}
