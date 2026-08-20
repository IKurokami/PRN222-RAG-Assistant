using System.Reflection;
using PRN222.RagAssistant.Application.Abstractions;
using PRN222.RagAssistant.Infrastructure.Rag;
using Xunit;

namespace PRN222.RagAssistant.Tests;

public sealed class RagArchitectureTests
{
    [Fact]
    public void RagQueryService_DoesNotReferencePgvectorDirectly()
    {
        var assembly = typeof(RagQueryService).Assembly;
        var ragQueryServiceType = typeof(RagQueryService);

        // Check that RagQueryService doesn't use Pgvector types directly
        var pgvectorNamespace = "Pgvector";
        var usesPgvector = ragQueryServiceType.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static)
            .SelectMany(m => m.GetParameters().Select(p => p.ParameterType))
            .Concat(ragQueryServiceType.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static)
                .SelectMany(m => m.ReturnType.GetGenericArguments()))
            .Concat(new[] { ragQueryServiceType.BaseType }.Where(t => t != null))
            .Any(t => t?.Namespace?.StartsWith(pgvectorNamespace) == true);

        // Also check field types
        var fieldTypes = ragQueryServiceType.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static)
            .Select(f => f.FieldType);
        
        var propertyTypes = ragQueryServiceType.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static)
            .Select(p => p.PropertyType);

        var allTypes = fieldTypes.Concat(propertyTypes);
        
        Assert.False(allTypes.Any(t => t.Namespace?.StartsWith(pgvectorNamespace) == true),
            "RagQueryService should not directly reference Pgvector types");
    }

    [Fact]
    public void RagQueryService_DoesNotReferenceNpgsqlDirectly()
    {
        var ragQueryServiceType = typeof(RagQueryService);
        var npgsqlNamespace = "Npgsql";

        var fieldTypes = ragQueryServiceType.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static)
            .Select(f => f.FieldType);
        
        var propertyTypes = ragQueryServiceType.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static)
            .Select(p => p.PropertyType);

        var parameterTypes = ragQueryServiceType.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static)
            .SelectMany(m => m.GetParameters().Select(p => p.ParameterType));

        var returnTypes = ragQueryServiceType.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static)
            .Select(m => m.ReturnType);

        var allTypes = fieldTypes.Concat(propertyTypes).Concat(parameterTypes).Concat(returnTypes);
        
        Assert.False(allTypes.Any(t => t.Namespace?.StartsWith(npgsqlNamespace) == true),
            "RagQueryService should not directly reference Npgsql types");
    }

    [Fact]
    public void RagQueryService_DoesNotReferenceHttpClientDirectly()
    {
        var ragQueryServiceType = typeof(RagQueryService);
        var httpNamespace = "System.Net.Http";

        var fieldTypes = ragQueryServiceType.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static)
            .Select(f => f.FieldType);
        
        var propertyTypes = ragQueryServiceType.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static)
            .Select(p => p.PropertyType);

        var allTypes = fieldTypes.Concat(propertyTypes);
        
        Assert.False(allTypes.Any(t => t.Namespace?.StartsWith(httpNamespace) == true),
            "RagQueryService should not directly reference HttpClient");
    }

    [Fact]
    public void RagQueryService_OnlyDependsOnAllowedNamespaces()
    {
        var ragQueryServiceType = typeof(RagQueryService);
        
        var allowedNamespaces = new[]
        {
            "PRN222.RagAssistant.Application",
            "PRN222.RagAssistant.Infrastructure.Rag",
            "PRN222.RagAssistant.Data",
            "PRN222.RagAssistant.Domain",
            "Microsoft.EntityFrameworkCore",
            "Microsoft.Extensions.Options",
            "Microsoft.Extensions.Logging",
            "Microsoft.Extensions.Time.Testing",
            "System",
            "System.Collections.Generic",
            "System.Linq",
            "System.Threading",
            "System.Threading.Tasks",
            "System.Diagnostics"
        };

        var fieldTypes = ragQueryServiceType.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static)
            .Select(f => f.FieldType);
        
        var propertyTypes = ragQueryServiceType.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static)
            .Select(p => p.PropertyType);

        var parameterTypes = ragQueryServiceType.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static)
            .SelectMany(m => m.GetParameters().Select(p => p.ParameterType));

        var returnTypes = ragQueryServiceType.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static)
            .Select(m => m.ReturnType);

        var allTypes = fieldTypes.Concat(propertyTypes).Concat(parameterTypes).Concat(returnTypes)
            .Where(t => t != null && t.Namespace != null && !t.IsPrimitive && t != typeof(string) && t != typeof(void))
            .Distinct();

        var disallowedTypes = allTypes.Where(t => 
            !allowedNamespaces.Any(ns => t.Namespace!.StartsWith(ns)) &&
            !t.Namespace!.StartsWith("System.") &&
            !t.Namespace!.StartsWith("Microsoft.")).ToList();

        Assert.Empty(disallowedTypes);
    }

    [Fact]
    public void AllConfigurationViaIOptions_RagOptions()
    {
        var ragQueryServiceType = typeof(RagQueryService);
        
        // Verify constructor takes IOptions<RagOptions>
        var constructors = ragQueryServiceType.GetConstructors();
        Assert.Single(constructors);
        
        var ctorParams = constructors[0].GetParameters();
        var optionsParam = ctorParams.FirstOrDefault(p => p.ParameterType.Name.Contains("IOptions"));
        
        Assert.NotNull(optionsParam);
        Assert.Contains("RagOptions", optionsParam.ParameterType.GenericTypeArguments[0].Name);
    }

    [Fact]
    public void RagOptions_RetrievalOptions_HasAllRequiredProperties()
    {
        var retrievalOptionsType = typeof(RagOptions.RetrievalOptions);
        
        var requiredProperties = new[]
        {
            "TopK",
            "MinimumSimilarityScore",
            "MaxContextChars",
            "IncludeConversationHistory",
            "HistoryTurns",
            "ExcerptChars"
        };

        foreach (var propName in requiredProperties)
        {
            var prop = retrievalOptionsType.GetProperty(propName);
            Assert.NotNull(prop);
            Assert.True(prop.CanRead && prop.CanWrite);
        }
    }

    [Fact]
    public void RagOptions_ChatOptions_HasAllRequiredProperties()
    {
        var chatOptionsType = typeof(RagOptions.ChatOptions);
        
        var requiredProperties = new[]
        {
            "NoEvidenceMessage"
        };

        foreach (var propName in requiredProperties)
        {
            var prop = chatOptionsType.GetProperty(propName);
            Assert.NotNull(prop);
            Assert.True(prop.CanRead && prop.CanWrite);
        }
    }

    [Fact]
    public void IRagQueryService_IsInApplicationAbstractions()
    {
        var interfaceType = typeof(IRagQueryService);
        Assert.True(interfaceType.Namespace?.StartsWith("PRN222.RagAssistant.Application.Abstractions") == true);
    }

    [Fact]
    public void IChatCompletionService_IsInApplicationAbstractions()
    {
        var interfaceType = typeof(IChatCompletionService);
        Assert.True(interfaceType.Namespace?.StartsWith("PRN222.RagAssistant.Application.Abstractions") == true);
    }

    [Fact]
    public void ITextEmbeddingService_IsInApplicationAbstractions()
    {
        var interfaceType = typeof(ITextEmbeddingService);
        Assert.True(interfaceType.Namespace?.StartsWith("PRN222.RagAssistant.Application.Abstractions") == true);
    }

    [Fact]
    public void IDocumentChunkRetriever_IsInternalInterface()
    {
        var interfaceType = typeof(IDocumentChunkRetriever);
        Assert.True(interfaceType.Namespace?.StartsWith("PRN222.RagAssistant.Infrastructure.Rag") == true);
    }

    [Fact]
    public void PgVectorDocumentChunkRetriever_ImplementsInternalInterface()
    {
        var retrieverType = typeof(PgVectorDocumentChunkRetriever);
        Assert.True(retrieverType.GetInterfaces().Contains(typeof(IDocumentChunkRetriever)));
    }
}
