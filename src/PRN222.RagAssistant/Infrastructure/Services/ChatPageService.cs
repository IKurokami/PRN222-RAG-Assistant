using Microsoft.EntityFrameworkCore;
using PRN222.RagAssistant.Application.Abstractions;
using PRN222.RagAssistant.Application.Models;
using PRN222.RagAssistant.Data;
using PRN222.RagAssistant.Domain.Entities;

namespace PRN222.RagAssistant.Infrastructure.Services;

public sealed class ChatPageService : IChatPageService
{
    private readonly ApplicationDbContext _dbContext;

    public ChatPageService(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<ChatPageSnapshot> GetPageAsync(
        Guid userId,
        Guid? subjectId,
        Guid? sessionId,
        CancellationToken cancellationToken = default)
    {
        var subjects = await _dbContext.Subjects
            .AsNoTracking()
            .Where(subject => subject.IsActive)
            .OrderBy(subject => subject.Code)
            .Select(subject => new ChatSubjectItem
            {
                Id = subject.Id,
                Code = subject.Code,
                Name = subject.Name
            })
            .ToListAsync(cancellationToken);

        if (subjects.Count == 0)
        {
            return new ChatPageSnapshot();
        }

        var selectedSubject = subjects.FirstOrDefault(subject => subject.Id == subjectId) ?? subjects[0];

        var sessions = await _dbContext.ChatSessions
            .AsNoTracking()
            .Where(session => session.UserId == userId
                && (session.SubjectId == null || session.SubjectId == selectedSubject.Id))
            .OrderByDescending(session => session.CreatedAtUtc)
            .Select(session => new ChatSessionItem
            {
                Id = session.Id,
                Title = session.Title,
                CreatedAtUtc = session.CreatedAtUtc
            })
            .ToListAsync(cancellationToken);

        ChatSessionItem? activeSession = null;
        if (sessionId.HasValue)
        {
            activeSession = sessions.FirstOrDefault(session => session.Id == sessionId.Value);
        }

        if (activeSession == null)
        {
            var activeSessionId = await GetOrCreateSessionAsync(
                userId,
                selectedSubject.Id,
                cancellationToken);

            activeSession = sessions.FirstOrDefault(session => session.Id == activeSessionId);
            if (activeSession == null)
            {
                activeSession = await _dbContext.ChatSessions
                    .AsNoTracking()
                    .Where(session => session.Id == activeSessionId && session.UserId == userId)
                    .Select(session => new ChatSessionItem
                    {
                        Id = session.Id,
                        Title = session.Title,
                        CreatedAtUtc = session.CreatedAtUtc
                    })
                    .SingleOrDefaultAsync(cancellationToken);

                if (activeSession != null)
                {
                    sessions.Insert(0, activeSession);
                }
            }
        }

        var messages = activeSession == null
            ? new List<ChatMessageItem>()
            : await LoadMessagesAsync(activeSession.Id, cancellationToken);

        return new ChatPageSnapshot
        {
            Subjects = subjects,
            SelectedSubject = selectedSubject,
            Sessions = sessions,
            ActiveSession = activeSession,
            Messages = messages
        };
    }

    public async Task<Guid> CreateSessionAsync(
        Guid userId,
        Guid subjectId,
        CancellationToken cancellationToken = default)
    {
        var nowUtc = DateTime.UtcNow;
        var session = new ChatSession
        {
            Id = Guid.CreateVersion7(),
            UserId = userId,
            SubjectId = subjectId,
            Title = string.Empty,
            CreatedAtUtc = nowUtc,
            UpdatedAtUtc = nowUtc
        };

        _dbContext.ChatSessions.Add(session);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return session.Id;
    }

    public async Task DeleteSessionAsync(
        Guid userId,
        Guid sessionId,
        CancellationToken cancellationToken = default)
    {
        var session = await _dbContext.ChatSessions
            .FirstOrDefaultAsync(
                candidate => candidate.Id == sessionId && candidate.UserId == userId,
                cancellationToken);

        if (session == null)
        {
            return;
        }

        _dbContext.ChatSessions.Remove(session);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task<Guid> GetOrCreateSessionAsync(
        Guid userId,
        Guid subjectId,
        CancellationToken cancellationToken)
    {
        var existingSessionId = await _dbContext.ChatSessions
            .AsNoTracking()
            .Where(session => session.UserId == userId && session.SubjectId == subjectId)
            .OrderByDescending(session => session.CreatedAtUtc)
            .Select(session => (Guid?)session.Id)
            .FirstOrDefaultAsync(cancellationToken);

        return existingSessionId ?? await CreateSessionAsync(userId, subjectId, cancellationToken);
    }

    private async Task<List<ChatMessageItem>> LoadMessagesAsync(
        Guid sessionId,
        CancellationToken cancellationToken)
    {
        var messages = await _dbContext.ChatMessages
            .AsNoTracking()
            .Where(message => message.ChatSessionId == sessionId)
            .OrderBy(message => message.CreatedAtUtc)
            .Select(message => new ChatMessageItem
            {
                Id = message.Id,
                Role = message.Role,
                Content = message.Content,
                CreatedAtUtc = message.CreatedAtUtc
            })
            .ToListAsync(cancellationToken);

        if (messages.Count == 0)
        {
            return messages;
        }

        var messageIds = messages.Select(message => message.Id).ToList();
        var citations = await _dbContext.MessageCitations
            .AsNoTracking()
            .Where(citation => messageIds.Contains(citation.ChatMessageId))
            .Join(
                _dbContext.DocumentChunks.AsNoTracking(),
                citation => citation.DocumentChunkId,
                chunk => chunk.Id,
                (citation, chunk) => new { citation, chunk })
            .Join(
                _dbContext.Documents.AsNoTracking(),
                combined => combined.chunk.DocumentId,
                document => document.Id,
                (combined, document) => new ChatCitationItem
                {
                    ChatMessageId = combined.citation.ChatMessageId,
                    Rank = combined.citation.Rank,
                    DocumentTitle = document.Title,
                    PageNumber = combined.chunk.PageNumber ?? 1,
                    ChunkContent = combined.chunk.Content
                })
            .OrderBy(citation => citation.Rank)
            .ToListAsync(cancellationToken);

        var citationsLookup = citations.ToLookup(citation => citation.ChatMessageId);
        foreach (var message in messages)
        {
            message.Citations = citationsLookup[message.Id].ToList();
        }

        return messages;
    }
}
