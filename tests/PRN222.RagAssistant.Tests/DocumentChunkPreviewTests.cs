using PRN222.RagAssistant.Models.Documents;

namespace PRN222.RagAssistant.Tests;

public sealed class DocumentChunkPreviewTests
{
    [Fact]
    public void DocumentDetailsViewModel_structure_is_valid()
    {
        var model = new DocumentDetailsViewModel
        {
            Document = new DocumentDetailViewModel(),
            ChunkPreview = new DocumentChunkPreviewPageViewModel
            {
                Items = new List<DocumentChunkPreviewItemViewModel>
                {
                    new DocumentChunkPreviewItemViewModel
                    {
                        ChunkIndex = 0,
                        Content = "Test content",
                        PageNumber = 1,
                        SlideNumber = null,
                        HasEmbedding = true
                    }
                },
                TotalCount = 1,
                EmbeddedCount = 1,
                CurrentPage = 1,
                TotalPages = 1
            },
            CanManageDocuments = true
        };

        Assert.NotNull(model.Document);
        Assert.NotNull(model.ChunkPreview);
        Assert.NotNull(model.ChunkPreview.Items);
        Assert.Single(model.ChunkPreview.Items);
    }

    [Fact]
    public void ChunkPreviewViewModel_calculates_all_embedded_correctly()
    {
        var model = new DocumentChunkPreviewPageViewModel
        {
            Items = new List<DocumentChunkPreviewItemViewModel>
            {
                new() { ChunkIndex = 0, HasEmbedding = true },
                new() { ChunkIndex = 1, HasEmbedding = false },
                new() { ChunkIndex = 2, HasEmbedding = true }
            },
            TotalCount = 3,
            EmbeddedCount = 2,
            CurrentPage = 1,
            TotalPages = 1
        };

        Assert.Equal(3, model.TotalCount);
        Assert.Equal(2, model.EmbeddedCount);
        Assert.False(model.AllChunksEmbedded);
    }

    [Fact]
    public void ChunkPreviewViewModel_all_embedded_flag()
    {
        var model = new DocumentChunkPreviewPageViewModel
        {
            Items = new List<DocumentChunkPreviewItemViewModel>
            {
                new() { ChunkIndex = 0, HasEmbedding = true },
                new() { ChunkIndex = 1, HasEmbedding = true }
            },
            TotalCount = 2,
            EmbeddedCount = 2,
            CurrentPage = 1,
            TotalPages = 1
        };

        Assert.True(model.AllChunksEmbedded);
        Assert.Equal(model.TotalCount, model.EmbeddedCount);
    }

    [Fact]
    public void ChunkPreviewItem_has_correct_properties()
    {
        var item = new DocumentChunkPreviewItemViewModel
        {
            ChunkIndex = 5,
            Content = "Test chunk content",
            PageNumber = 3,
            SlideNumber = 2,
            HasEmbedding = true
        };

        Assert.Equal(5, item.ChunkIndex);
        Assert.Equal("Test chunk content", item.Content);
        Assert.Equal(3, item.PageNumber);
        Assert.Equal(2, item.SlideNumber);
        Assert.True(item.HasEmbedding);
    }

    [Fact]
    public void ChunkPreviewItem_calculates_character_count()
    {
        var item = new DocumentChunkPreviewItemViewModel
        {
            ChunkIndex = 0,
            Content = "Hello World",
            PageNumber = 1,
            HasEmbedding = true
        };

        Assert.Equal(11, item.CharacterCount);
    }
}
