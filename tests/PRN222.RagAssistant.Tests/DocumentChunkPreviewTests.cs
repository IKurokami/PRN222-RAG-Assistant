using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Pgvector;
using Pgvector.EntityFrameworkCore;
using PRN222.RagAssistant.Controllers;
using PRN222.RagAssistant.Data;
using PRN222.RagAssistant.Domain.Entities;
using PRN222.RagAssistant.Domain.Enums;
using PRN222.RagAssistant.Infrastructure.Services;
using PRN222.RagAssistant.Models.Documents;
using PRN222.RagAssistant.Security;

namespace PRN222.RagAssistant.Tests;

public sealed class DocumentChunkPreviewTests
{
    [Fact]
    public async Task Details_returns_persisted_chunks_in_index_order_with_embedding_state()
    {
        await using var dbContext = CreateDbContext();
        var subject = CreateSubject();
        var uploader = CreateUser();
        var document = CreateDocument(subject.Id, uploader.Id);
        var otherDocument = CreateDocument(subject.Id, uploader.Id);

        dbContext.AddRange(subject, document, otherDocument);
        dbContext.DocumentChunks.AddRange(Enumerable.Range(0, 14)
            .Reverse()
            .Select(index => new DocumentChunk
            {
                Id = Guid.NewGuid(),
                DocumentId = document.Id,
                ChunkIndex = index,
                Content = $"Persisted content {index}",
                PageNumber = index + 1,
                SlideNumber = index == 13 ? 3 : null,
                Embedding = index % 2 == 0 ? new Vector(new[] { 0.1f, 0.2f }) : null
            }));
        dbContext.DocumentChunks.Add(new DocumentChunk
        {
            Id = Guid.NewGuid(),
            DocumentId = otherDocument.Id,
            ChunkIndex = 0,
            Content = "Content from another document",
            Embedding = new Vector(new[] { 0.3f, 0.4f })
        });
        await dbContext.SaveChangesAsync();

        using var userManager = new TestUserManager(uploader);
        var controller = CreateController(dbContext, userManager, canView: true);

        var result = await controller.Details(document.Id, chunkPage: 2);

        var viewResult = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<DocumentDetailsViewModel>(viewResult.Model);
        Assert.Equal(14, model.ChunkPreview.TotalCount);
        Assert.Equal(7, model.ChunkPreview.EmbeddedCount);
        Assert.Equal(2, model.ChunkPreview.CurrentPage);
        Assert.Equal(2, model.ChunkPreview.TotalPages);
        Assert.False(model.ChunkPreview.AllChunksEmbedded);
        Assert.Collection(
            model.ChunkPreview.Items,
            chunk =>
            {
                Assert.Equal(12, chunk.ChunkIndex);
                Assert.Equal("Persisted content 12", chunk.Content);
                Assert.Equal(13, chunk.PageNumber);
                Assert.Null(chunk.SlideNumber);
                Assert.True(chunk.HasEmbedding);
            },
            chunk =>
            {
                Assert.Equal(13, chunk.ChunkIndex);
                Assert.Equal("Persisted content 13", chunk.Content);
                Assert.Equal(14, chunk.PageNumber);
                Assert.Equal(3, chunk.SlideNumber);
                Assert.False(chunk.HasEmbedding);
            });
    }

    [Fact]
    public async Task Details_clamps_an_out_of_range_chunk_page_to_the_last_page()
    {
        await using var dbContext = CreateDbContext();
        var subject = CreateSubject();
        var uploader = CreateUser();
        var document = CreateDocument(subject.Id, uploader.Id);

        dbContext.AddRange(subject, document);
        dbContext.DocumentChunks.AddRange(Enumerable.Range(0, 13).Select(index => new DocumentChunk
        {
            Id = Guid.NewGuid(),
            DocumentId = document.Id,
            ChunkIndex = index,
            Content = $"Chunk {index}"
        }));
        await dbContext.SaveChangesAsync();

        using var userManager = new TestUserManager(uploader);
        var controller = CreateController(dbContext, userManager, canView: true);

        var result = await controller.Details(document.Id, chunkPage: int.MaxValue);

        var viewResult = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<DocumentDetailsViewModel>(viewResult.Model);
        Assert.Equal(2, model.ChunkPreview.CurrentPage);
        var item = Assert.Single(model.ChunkPreview.Items);
        Assert.Equal(12, item.ChunkIndex);
    }

    [Fact]
    public async Task Details_does_not_expose_chunk_content_without_subject_view_access()
    {
        await using var dbContext = CreateDbContext();
        var subject = CreateSubject();
        var uploader = CreateUser();
        var document = CreateDocument(subject.Id, uploader.Id);

        dbContext.AddRange(subject, document);
        dbContext.DocumentChunks.Add(new DocumentChunk
        {
            Id = Guid.NewGuid(),
            DocumentId = document.Id,
            ChunkIndex = 0,
            Content = "Protected chunk content"
        });
        await dbContext.SaveChangesAsync();

        using var userManager = new TestUserManager(uploader);
        var controller = CreateController(dbContext, userManager, canView: false);

        var result = await controller.Details(document.Id);

        Assert.IsType<ForbidResult>(result);
    }

    private static ApplicationDbContext CreateDbContext()
    {
        var postgresOptions = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql(
                "Host=localhost;Database=unused;Username=unused;Password=unused",
                npgsql => npgsql.UseVector())
            .Options;
        using var postgresContext = new ApplicationDbContext(postgresOptions);

        var inMemoryOptions = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"document-chunk-preview-{Guid.NewGuid()}")
            .UseModel(postgresContext.Model)
            .Options;
        return new ApplicationDbContext(inMemoryOptions);
    }

    private static Subject CreateSubject() => new()
    {
        Id = Guid.NewGuid(),
        Code = "PRN222",
        Name = "Advanced Programming with .NET",
        IsActive = true
    };

    private static ApplicationUser CreateUser() => new()
    {
        Id = Guid.NewGuid(),
        UserName = "leader@prn222.test",
        Email = "leader@prn222.test",
        DisplayName = "Subject Leader",
        CreatedAtUtc = DateTime.UtcNow
    };

    private static Document CreateDocument(Guid subjectId, Guid uploaderId) => new()
    {
        Id = Guid.NewGuid(),
        SubjectId = subjectId,
        UploadedByUserId = uploaderId,
        Title = "Preview document",
        OriginalFileName = "preview.pdf",
        StoragePath = "storage/uploads/preview.pdf",
        ContentType = "application/pdf",
        FileExtension = ".pdf",
        FileSizeBytes = 2048,
        IndexStatus = DocumentIndexStatus.Indexed,
        UploadedAtUtc = DateTime.UtcNow,
        IndexedAtUtc = DateTime.UtcNow
    };

    private static DocumentsController CreateController(
        ApplicationDbContext dbContext,
        UserManager<ApplicationUser> userManager,
        bool canView)
    {
        var identity = new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString())],
            authenticationType: "TestAuth");
        var controller = new DocumentsController(
            dbContext,
            new InMemoryDocumentIndexingQueue(),
            new StubSubjectAccessService(canView),
            new ConfigurationBuilder().Build(),
            userManager,
            NullLogger<DocumentsController>.Instance)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(identity)
                }
            }
        };

        return controller;
    }

    private sealed class StubSubjectAccessService(bool canView) : ISubjectAccessService
    {
        public Task<IReadOnlyList<Subject>> GetAccessibleSubjectsAsync(
            ClaimsPrincipal user,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<Subject>>([]);

        public Task<IReadOnlySet<Guid>> GetManageableSubjectIdsAsync(
            ClaimsPrincipal user,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlySet<Guid>>(new HashSet<Guid>());

        public Task<bool> CanViewSubjectAsync(
            ClaimsPrincipal user,
            Guid subjectId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(canView);

        public Task<bool> CanManageSubjectAsync(
            ClaimsPrincipal user,
            Guid subjectId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(false);
    }

    private sealed class TestUserManager(ApplicationUser user)
        : UserManager<ApplicationUser>(
            new StubUserStore(),
            Microsoft.Extensions.Options.Options.Create(new IdentityOptions()),
            new PasswordHasher<ApplicationUser>(),
            [],
            [],
            new UpperInvariantLookupNormalizer(),
            new IdentityErrorDescriber(),
            EmptyServiceProvider.Instance,
            NullLogger<UserManager<ApplicationUser>>.Instance)
    {
        public override Task<ApplicationUser?> FindByIdAsync(string userId) =>
            Task.FromResult(Guid.TryParse(userId, out var id) && id == user.Id ? user : null);
    }

    private sealed class StubUserStore : IUserStore<ApplicationUser>
    {
        public void Dispose()
        {
        }

        public Task<string> GetUserIdAsync(ApplicationUser user, CancellationToken cancellationToken) =>
            Task.FromResult(user.Id.ToString());

        public Task<string?> GetUserNameAsync(ApplicationUser user, CancellationToken cancellationToken) =>
            Task.FromResult(user.UserName);

        public Task SetUserNameAsync(ApplicationUser user, string? userName, CancellationToken cancellationToken)
        {
            user.UserName = userName;
            return Task.CompletedTask;
        }

        public Task<string?> GetNormalizedUserNameAsync(ApplicationUser user, CancellationToken cancellationToken) =>
            Task.FromResult(user.NormalizedUserName);

        public Task SetNormalizedUserNameAsync(
            ApplicationUser user,
            string? normalizedName,
            CancellationToken cancellationToken)
        {
            user.NormalizedUserName = normalizedName;
            return Task.CompletedTask;
        }

        public Task<IdentityResult> CreateAsync(ApplicationUser user, CancellationToken cancellationToken) =>
            Task.FromResult(IdentityResult.Success);

        public Task<IdentityResult> UpdateAsync(ApplicationUser user, CancellationToken cancellationToken) =>
            Task.FromResult(IdentityResult.Success);

        public Task<IdentityResult> DeleteAsync(ApplicationUser user, CancellationToken cancellationToken) =>
            Task.FromResult(IdentityResult.Success);

        public Task<ApplicationUser?> FindByIdAsync(string userId, CancellationToken cancellationToken) =>
            Task.FromResult<ApplicationUser?>(null);

        public Task<ApplicationUser?> FindByNameAsync(string normalizedUserName, CancellationToken cancellationToken) =>
            Task.FromResult<ApplicationUser?>(null);
    }

    private sealed class EmptyServiceProvider : IServiceProvider
    {
        public static EmptyServiceProvider Instance { get; } = new();

        public object? GetService(Type serviceType) => null;
    }
}
