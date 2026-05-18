using Moq;
using Xunit;
using ProjectVital.Application.DTOs;
using ProjectVital.Application.Interfaces;
using ProjectVital.Application.Services;
using ProjectVital.Domain.Entities;
using ProjectVital.Domain.Enums;
using ProjectVital.Domain.Exceptions;
using ProjectVital.Domain.Interfaces;

namespace ProjectVital.Tests.Services;

public class PredictionServiceTests
{
    private readonly Mock<IPredictionRepository> _predRepo;
    private readonly Mock<IStudentRepository> _studentRepo;
    private readonly Mock<IMLServiceClient> _mlClient;
    private readonly PredictionService _sut;

    public PredictionServiceTests()
    {
        _predRepo = new Mock<IPredictionRepository>();
        _studentRepo = new Mock<IStudentRepository>();
        _mlClient = new Mock<IMLServiceClient>();
        _sut = new PredictionService(_predRepo.Object, _studentRepo.Object, _mlClient.Object);
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
    public async Task RunAsync_CallsMLClientAndSavesPrediction()
    {
        var student = new Student { Id = 1, AcademyId = 10, Active = true };
        _studentRepo.Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(student);

        _mlClient.Setup(c => c.PredictAsync(1, 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MLPredictionResult(0.8, "high", "rf_v1.0"));

        _predRepo.Setup(r => r.AddAsync(It.IsAny<Prediction>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Prediction p, CancellationToken _) => p);
        _predRepo.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        var result = await _sut.RunAsync(1);

        Assert.NotNull(result);
        Assert.Equal(0.8, result.Probability);
        Assert.Equal(RiskLevel.High, result.RiskLevel);
        Assert.Equal("rf_v1.0", result.ModelVersion);
        _predRepo.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RunAsync_WithUnknownStudent_ThrowsNotFoundException()
    {
        _studentRepo.Setup(r => r.GetByIdAsync(99, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Student?)null);

        await Assert.ThrowsAsync<NotFoundException>(() => _sut.RunAsync(99));
    }

    [Fact]
    public async Task RunBatchAsync_ReturnsEmptyList_WhenMLReturnsEmpty()
    {
        _mlClient.Setup(c => c.PredictBatchAsync(10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<MLBatchPredictionItem>());

        var result = await _sut.RunBatchAsync(10);

        Assert.Empty(result);
        _predRepo.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }
}
