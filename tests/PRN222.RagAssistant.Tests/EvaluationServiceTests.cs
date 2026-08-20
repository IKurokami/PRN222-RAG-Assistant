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
    public async Task EvaluateQuestionAsync_CalculatesKeywordAccuracy()
    {
        // Arrange
        var mockRagService = new Mock<IRagQueryService>();
        var userId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();

        mockRagService
            .Setup(s => s.GetOrCreateUserSessionAsync(userId, It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(sessionId);

        mockRagService
            .Setup(s => s.AskAsync(userId, sessionId, It.IsAny<string>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RagAnswer(
                sessionId,
                Guid.NewGuid(),
                Guid.NewGuid(),
                "MulticastDelegate kết hợp các phương thức bằng toán tử += và gọi tuần tự qua _invocationList trong C#.",
                new List<RagCitation>
                {
                    new RagCitation(Guid.NewGuid(), Guid.NewGuid(), "PRN222_Guide.pdf", 1, "MulticastDelegate...", 1, null)
                }));

        var service = new EvaluationService(mockRagService.Object, NullLogger<EvaluationService>.Instance);

        // Act
        var result = await service.EvaluateQuestionAsync(userId, 1);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(1, result.QuestionId);
        Assert.True(result.KeywordAccuracyPercent > 0);
        Assert.True(result.HasCitations);
        Assert.Equal(1, result.CitationsCount);
    }
}
