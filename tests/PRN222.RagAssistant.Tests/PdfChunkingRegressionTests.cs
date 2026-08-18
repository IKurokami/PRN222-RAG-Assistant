using System;
using System.IO;
using System.Linq;
using PRN222.RagAssistant.Infrastructure.Parsing;
using Xunit;
using Xunit.Abstractions;

namespace PRN222.RagAssistant.Tests;

public sealed class PdfChunkingRegressionTests
{
    private readonly ITestOutputHelper _output;

    public PdfChunkingRegressionTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void ParseAndChunk_Part01_Pdf_PreservesContinuousSentences()
    {
        const string pdfPath = @"C:\Users\funny\Downloads\c-12-in-a-nutshell_26_parts\c-12-in-a-nutshell_26_parts\Part_01_Preface_and_Front_Matter.pdf";
        if (!File.Exists(pdfPath)) return;

        using var stream = File.OpenRead(pdfPath);
        var parser = new PdfDocumentParser();
        var pages = parser.Parse(stream);

        var chunker = TextChunker.Create(maxChunkSize: 1000, overlapSize: 0);
        var chunks = chunker.Chunk(pages);


        _output.WriteLine($"=== TOTAL CHUNKS: {chunks.Count} ===");
        for (int i = 0; i < chunks.Count; i++)
        {
            var c = chunks[i];
            _output.WriteLine($"\n------------------------------------------------------------");
            _output.WriteLine($"[CHUNK #{i}] (Page: {c.PageNumber}, Length: {c.Content.Length} chars)");
            _output.WriteLine($"------------------------------------------------------------");
            _output.WriteLine(c.Content);
        }

        // Verify key cross-page transitions
        Assert.Contains(chunks, c => c.Content.Contains("for release details."));
        Assert.Contains(chunks, c => c.Content.Contains("Windows Presentation Foundation (WPF)."));
    }
}
