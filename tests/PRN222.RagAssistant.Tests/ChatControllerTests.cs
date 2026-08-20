using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using PRN222.RagAssistant.Application.Abstractions;
using PRN222.RagAssistant.Application.Models;
using PRN222.RagAssistant.Controllers;
using PRN222.RagAssistant.Data;
using PRN222.RagAssistant.Domain.Entities;

namespace PRN222.RagAssistant.Tests;

public sealed class ChatControllerTests
{
    private static ApplicationDbContext CreateInMemoryDbContext()
    {
        var postgresOptions = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql(
                "Host=unused;Database=unused;Username=unused;Password=unused",
                npgsql => npgsql.UseVector())
            .Options;
        using var postgresContext = new ApplicationDbContext(postgresOptions);

        var inMemoryOptions = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"chat-test-{Guid.NewGuid()}")
            .UseModel(postgresContext.Model)
            .Options;

        return new ApplicationDbContext(inMemoryOptions);
    }

    [Fact]
    public async Task Ask_ReturnsForbidden_WhenNonAdminAccessesInactiveSubject()
    {
        // Arrange
        await using var dbContext = CreateInMemoryDbContext();
        var userId = Guid.NewGuid();
        var inactiveSubjectId = Guid.NewGuid();

        dbContext.Subjects.Add(new Subject
        {
            Id = inactiveSubjectId,
            Code = "INACTIVE",
            Name = "Inactive Subject",
            IsActive = false
        });
        await dbContext.SaveChangesAsync();

        var mockRagService = new Mock<IRagQueryService>();
        var mockUserManager = CreateMockUserManager();
        mockUserManager.Setup(x => x.GetRolesAsync(It.IsAny<ApplicationUser>()))
            .ReturnsAsync(new[] { "Student" });
        mockUserManager.Setup(x => x.GetUserAsync(It.IsAny<ClaimsPrincipal>()))
            .ReturnsAsync(new ApplicationUser { Id = userId });

        var controller = new ChatController(
            mockRagService.Object,
            dbContext,
            mockUserManager.Object,
            Mock.Of<ILogger<ChatController>>());

        var dto = new AskRequestDto
        {
            SessionId = Guid.NewGuid(),
            SubjectId = inactiveSubjectId,
            Question = "Test question"
        };

        // Act
        var result = await controller.Ask(dto, CancellationToken.None);

        // Assert
        Assert.IsType<ForbidResult>(result);
        mockRagService.Verify(
            x => x.AskAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Ask_Proceeds_WhenNonAdminAccessesActiveSubject()
    {
        // Arrange
        await using var dbContext = CreateInMemoryDbContext();
        var userId = Guid.NewGuid();
        var activeSubjectId = Guid.NewGuid();

        dbContext.Subjects.Add(new Subject
        {
            Id = activeSubjectId,
            Code = "ACTIVE",
            Name = "Active Subject",
            IsActive = true
        });
        dbContext.ChatSessions.Add(new ChatSession
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            SubjectId = activeSubjectId,
            Title = "Test session"
        });
        await dbContext.SaveChangesAsync();

        var mockRagService = new Mock<IRagQueryService>();
        mockRagService.Setup(x => x.AskAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RagAnswer(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Answer", Array.Empty<RagCitation>()));

        var mockUserManager = CreateMockUserManager();
        mockUserManager.Setup(x => x.GetRolesAsync(It.IsAny<ApplicationUser>()))
            .ReturnsAsync(new[] { "Student" });
        mockUserManager.Setup(x => x.GetUserAsync(It.IsAny<ClaimsPrincipal>()))
            .ReturnsAsync(new ApplicationUser { Id = userId });

        var controller = new ChatController(
            mockRagService.Object,
            dbContext,
            mockUserManager.Object,
            Mock.Of<ILogger<ChatController>>());

        var dto = new AskRequestDto
        {
            SessionId = Guid.NewGuid(),
            SubjectId = activeSubjectId,
            Question = "Test question"
        };

        // Act
        var result = await controller.Ask(dto, CancellationToken.None);

        // Assert
        var jsonResult = Assert.IsType<JsonResult>(result);
        mockRagService.Verify(
            x => x.AskAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Ask_Proceeds_WhenAdminAccessesAnySubject()
    {
        // Arrange
        await using var dbContext = CreateInMemoryDbContext();
        var userId = Guid.NewGuid();
        var anySubjectId = Guid.NewGuid();

        // No subject in DB - admin should still proceed
        dbContext.ChatSessions.Add(new ChatSession
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            SubjectId = anySubjectId,
            Title = "Test session"
        });
        await dbContext.SaveChangesAsync();

        var mockRagService = new Mock<IRagQueryService>();
        mockRagService.Setup(x => x.AskAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RagAnswer(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Answer", Array.Empty<RagCitation>()));

        var mockUserManager = CreateMockUserManager();
        mockUserManager.Setup(x => x.GetRolesAsync(It.IsAny<ApplicationUser>()))
            .ReturnsAsync(new[] { "Admin" });
        mockUserManager.Setup(x => x.GetUserAsync(It.IsAny<ClaimsPrincipal>()))
            .ReturnsAsync(new ApplicationUser { Id = userId });

        var controller = new ChatController(
            mockRagService.Object,
            dbContext,
            mockUserManager.Object,
            Mock.Of<ILogger<ChatController>>());

        var dto = new AskRequestDto
        {
            SessionId = Guid.NewGuid(),
            SubjectId = anySubjectId,
            Question = "Test question"
        };

        // Act
        var result = await controller.Ask(dto, CancellationToken.None);

        // Assert
        var jsonResult = Assert.IsType<JsonResult>(result);
        mockRagService.Verify(
            x => x.AskAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Ask_ReturnsBadRequest_WhenQuestionIsEmpty()
    {
        // Arrange
        await using var dbContext = CreateInMemoryDbContext();
        var userId = Guid.NewGuid();
        var subjectId = Guid.NewGuid();

        dbContext.Subjects.Add(new Subject { Id = subjectId, Code = "TEST", Name = "Test", IsActive = true });
        await dbContext.SaveChangesAsync();

        var mockRagService = new Mock<IRagQueryService>();
        var mockUserManager = CreateMockUserManager();
        mockUserManager.Setup(x => x.GetRolesAsync(It.IsAny<ApplicationUser>()))
            .ReturnsAsync(new[] { "Student" });
        mockUserManager.Setup(x => x.GetUserAsync(It.IsAny<ClaimsPrincipal>()))
            .ReturnsAsync(new ApplicationUser { Id = userId });

        var controller = new ChatController(
            mockRagService.Object,
            dbContext,
            mockUserManager.Object,
            Mock.Of<ILogger<ChatController>>());

        var dto = new AskRequestDto
        {
            SessionId = Guid.NewGuid(),
            SubjectId = subjectId,
            Question = "   "
        };

        // Act
        var result = await controller.Ask(dto, CancellationToken.None);

        // Assert
        Assert.IsType<BadRequestObjectResult>(result);
    }

    private static Mock<UserManager<ApplicationUser>> CreateMockUserManager()
    {
        var store = new Mock<IUserStore<ApplicationUser>>();
        var options = new Mock<IOptions<IdentityOptions>>();
        var passwordHasher = new Mock<IPasswordHasher<ApplicationUser>>();
        var userValidators = new List<IUserValidator<ApplicationUser>>();
        var passwordValidators = new List<IPasswordValidator<ApplicationUser>>();
        var keyNormalizer = new Mock<ILookupNormalizer>();
        var errors = new IdentityErrorDescriber();
        var services = new Mock<IServiceProvider>();
        var logger = new Mock<ILogger<UserManager<ApplicationUser>>>();

        return new Mock<UserManager<ApplicationUser>>(
            store.Object, options.Object, passwordHasher.Object,
            userValidators, passwordValidators,
            keyNormalizer.Object, errors, services.Object, logger.Object);
    }
}
