using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using PRN222.RagAssistant.Application.Abstractions;
using PRN222.RagAssistant.Application.Models;
using PRN222.RagAssistant.Infrastructure.Services;
using Xunit;

namespace PRN222.RagAssistant.Tests;

public sealed class EvaluationServiceTests
{
    [Fact]
    public void GetQuestions_Returns50HumanAuthoredQuestions()
    {
        // Arrange
        var mockRagService = new Mock<IRagQueryService>();
        var service = new EvaluationService(mockRagService.Object, NullLogger<EvaluationService>.Instance);

        // Act
        var questions = service.GetQuestions();

        // Assert
        Assert.NotNull(questions);
        Assert.Equal(50, questions.Count);
        Assert.All(questions, q =>
        {
            Assert.True(q.Id > 0);
            Assert.False(string.IsNullOrWhiteSpace(q.Module));
            Assert.False(string.IsNullOrWhiteSpace(q.QuestionText));
            Assert.False(string.IsNullOrWhiteSpace(q.GroundTruthAnswer));
            Assert.NotEmpty(q.ExpectedKeywords);
        });
    }

    [Fact]
    public async Task EvaluateQuestionAsync_CalculatesKeywordAccuracy_WithoutPersistingChatHistory()
    {
        // Arrange
        var mockRagService = new Mock<IRagQueryService>();
        var userId = Guid.NewGuid();
        var subjectId = Guid.NewGuid();

        mockRagService
            .Setup(s => s.AskStatelessAsync(
                It.IsAny<string>(),
                subjectId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RagQueryResult(
                "MulticastDelegate kết hợp các phương thức bằng toán tử += và gọi tuần tự qua _invocationList trong C#.",
                new List<RagCitation>
                {
                    new RagCitation(Guid.NewGuid(), Guid.NewGuid(), "PRN222_Guide.pdf", 1, "MulticastDelegate...", 1, null)
                }));

        var service = new EvaluationService(mockRagService.Object, NullLogger<EvaluationService>.Instance);

        // Act
        var result = await service.EvaluateQuestionAsync(userId, 1, subjectId);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(1, result.QuestionId);
        Assert.True(result.KeywordAccuracyPercent > 0);
        Assert.True(result.HasCitations);
        Assert.Equal(1, result.CitationsCount);

        mockRagService.Verify(
            s => s.AskStatelessAsync(It.IsAny<string>(), subjectId, It.IsAny<CancellationToken>()),
            Times.Once);
        mockRagService.Verify(
            s => s.GetOrCreateUserSessionAsync(It.IsAny<Guid>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()),
            Times.Never);
        mockRagService.Verify(
            s => s.AskAsync(
                It.IsAny<Guid>(),
                It.IsAny<Guid>(),
                It.IsAny<string>(),
                It.IsAny<Guid?>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task EvaluateQuestionAsync_RejectsMissingSubjectScope()
    {
        var mockRagService = new Mock<IRagQueryService>();
        var service = new EvaluationService(mockRagService.Object, NullLogger<EvaluationService>.Instance);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.EvaluateQuestionAsync(Guid.NewGuid(), 1, subjectId: null));

        mockRagService.Verify(
            s => s.AskStatelessAsync(
                It.IsAny<string>(),
                It.IsAny<Guid>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }
}
