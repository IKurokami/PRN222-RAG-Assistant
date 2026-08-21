using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using PRN222.RagAssistant.Application.Abstractions;
using PRN222.RagAssistant.Data;
using PRN222.RagAssistant.Domain.Entities;
using PRN222.RagAssistant.Domain.Enums;
using PRN222.RagAssistant.Infrastructure.Services;
using PRN222.RagAssistant.Pages.Chat;

namespace PRN222.RagAssistant.Tests;

public sealed class ChatPageServiceTests
{
    [Fact]
    public void Chat_page_depends_on_chat_page_service_instead_of_db_context()
    {
        var constructor = Assert.Single(typeof(IndexModel).GetConstructors());
        var parameterTypes = constructor.GetParameters()
            .Select(parameter => parameter.ParameterType)
            .ToList();

        Assert.Contains(typeof(IChatPageService), parameterTypes);
        Assert.DoesNotContain(typeof(ApplicationDbContext), parameterTypes);
    }

    [Fact]
    public async Task GetPageAsync_scopes_sessions_and_messages_to_user_and_subject()
    {
        await using var dbContext = CreateContext();

        var userId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();
        var targetSubjectId = Guid.NewGuid();
        var otherSubjectId = Guid.NewGuid();
        var targetSessionId = Guid.NewGuid();
        var otherUserSessionId = Guid.NewGuid();
        var otherSubjectSessionId = Guid.NewGuid();
        var now = DateTime.UtcNow;

        dbContext.Subjects.AddRange(
            new Subject
            {
                Id = targetSubjectId,
                Code = "PRN222",
                Name = "ASP.NET Core",
                IsActive = true
            },
            new Subject
            {
                Id = otherSubjectId,
                Code = "SWT301",
                Name = "Software Testing",
                IsActive = true
            });

        dbContext.ChatSessions.AddRange(
            CreateSession(targetSessionId, userId, targetSubjectId, now),
            CreateSession(otherUserSessionId, otherUserId, targetSubjectId, now.AddMinutes(-1)),
            CreateSession(otherSubjectSessionId, userId, otherSubjectId, now.AddMinutes(-2)));

        dbContext.ChatMessages.AddRange(
            new ChatMessage
            {
                Id = Guid.NewGuid(),
                ChatSessionId = targetSessionId,
                Role = ChatMessageRole.User,
                Content = "target message",
                CreatedAtUtc = now
            },
            new ChatMessage
            {
                Id = Guid.NewGuid(),
                ChatSessionId = otherUserSessionId,
                Role = ChatMessageRole.User,
                Content = "other user message",
                CreatedAtUtc = now
            });

        await dbContext.SaveChangesAsync();

        var service = new ChatPageService(dbContext);
        var page = await service.GetPageAsync(userId, targetSubjectId, targetSessionId);

        Assert.NotNull(page.SelectedSubject);
        Assert.Equal(targetSubjectId, page.SelectedSubject!.Id);
        Assert.Equal(targetSessionId, page.ActiveSession?.Id);

        var session = Assert.Single(page.Sessions);
        Assert.Equal(targetSessionId, session.Id);

        var message = Assert.Single(page.Messages);
        Assert.Equal("target message", message.Content);
    }

    [Fact]
    public async Task DeleteSessionAsync_does_not_delete_another_users_session()
    {
        await using var dbContext = CreateContext();

        var ownerId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();
        var subjectId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();

        dbContext.Subjects.Add(new Subject
        {
            Id = subjectId,
            Code = "PRN222",
            Name = "ASP.NET Core",
            IsActive = true
        });
        dbContext.ChatSessions.Add(CreateSession(sessionId, ownerId, subjectId, DateTime.UtcNow));
        await dbContext.SaveChangesAsync();

        var service = new ChatPageService(dbContext);
        await service.DeleteSessionAsync(otherUserId, sessionId);

        Assert.True(await dbContext.ChatSessions.AnyAsync(session => session.Id == sessionId));
    }

    private static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"chat-page-tests-{Guid.NewGuid()}")
            .ReplaceService<IModelCustomizer, ChatPageTestModelCustomizer>()
            .Options;

        return new ApplicationDbContext(options);
    }

    private static ChatSession CreateSession(
        Guid id,
        Guid userId,
        Guid subjectId,
        DateTime createdAtUtc)
    {
        return new ChatSession
        {
            Id = id,
            UserId = userId,
            SubjectId = subjectId,
            Title = "Session",
            CreatedAtUtc = createdAtUtc,
            UpdatedAtUtc = createdAtUtc
        };
    }

    private sealed class ChatPageTestModelCustomizer : ModelCustomizer
    {
        public ChatPageTestModelCustomizer(ModelCustomizerDependencies dependencies)
            : base(dependencies)
        {
        }

        public override void Customize(ModelBuilder modelBuilder, DbContext context)
        {
            base.Customize(modelBuilder, context);
            modelBuilder.Entity<DocumentChunk>().Ignore(chunk => chunk.Embedding);
        }
    }
}
