using ProjectVital.Application.DTOs;
using ProjectVital.Application.Interfaces;
using ProjectVital.Domain.Entities;
using ProjectVital.Domain.Enums;
using ProjectVital.Domain.Exceptions;
using ProjectVital.Domain.Interfaces;

namespace ProjectVital.Application.Services;

public class PredictionService : IPredictionService
{
    private readonly IPredictionRepository _predictions;
    private readonly IStudentRepository _students;
    private readonly IMLServiceClient _mlClient;

    public PredictionService(
        IPredictionRepository predictions,
        IStudentRepository students,
        IMLServiceClient mlClient)
    {
        _predictions = predictions;
        _students = students;
        _mlClient = mlClient;
    }

    public async Task<PredictionDto> RunAsync(int studentId, CancellationToken cancellationToken = default)
    {
        var student = await _students.GetByIdAsync(studentId, cancellationToken)
            ?? throw new NotFoundException("Student", studentId);

        var mlResult = await _mlClient.PredictAsync(studentId, student.AcademyId, cancellationToken);

        var prediction = new Prediction
        {
            StudentId = studentId,
            Probability = mlResult.Probability,
            RiskLevel = RiskLevelExtensions.FromProbability(mlResult.Probability),
            ModelVersion = mlResult.ModelVersion,
            PredictedAt = DateTime.UtcNow,
        };

        await _predictions.AddAsync(prediction, cancellationToken);
        await _predictions.SaveChangesAsync(cancellationToken);

        return MapToDto(prediction);
    }

    public async Task<IReadOnlyList<PredictionDto>> RunBatchAsync(int academyId, CancellationToken cancellationToken = default)
    {
        var batchItems = await _mlClient.PredictBatchAsync(academyId, cancellationToken);

        var saved = new List<PredictionDto>(batchItems.Count);
        foreach (var item in batchItems)
        {
            var prediction = new Prediction
            {
                StudentId = item.StudentId,
                Probability = item.Probability,
                RiskLevel = RiskLevelExtensions.FromProbability(item.Probability),
                ModelVersion = string.Empty,
                PredictedAt = DateTime.UtcNow,
            };

            await _predictions.AddAsync(prediction, cancellationToken);
            saved.Add(MapToDto(prediction));
        }

        if (saved.Count > 0)
            await _predictions.SaveChangesAsync(cancellationToken);

        return saved.AsReadOnly();
    }

    public async Task<IReadOnlyList<PredictionDto>> ListLatestByAcademyAsync(
        int academyId,
        CancellationToken cancellationToken = default)
    {
        var predictions = await _predictions.GetLatestByAcademyAsync(academyId, cancellationToken);
        return predictions.Select(p => MapToDto(p)).ToList().AsReadOnly();
    }

    public async Task<PredictionDto?> GetLatestByStudentAsync(
        int studentId,
        CancellationToken cancellationToken = default)
    {
        var p = await _predictions.GetLatestByStudentAsync(studentId, cancellationToken);
        return p is null ? null : MapToDto(p);
    }

    private static PredictionDto MapToDto(Prediction p) =>
        new(p.Id, p.StudentId, p.Probability, p.RiskLevel, p.ModelVersion, p.PredictedAt);
}
