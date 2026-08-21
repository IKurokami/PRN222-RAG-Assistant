using Microsoft.AspNetCore.Mvc.RazorPages;
using PRN222.RagAssistant.Data;

namespace PRN222.RagAssistant.Tests;

public sealed class PageModelArchitectureTests
{
    [Fact]
    public void PageModels_do_not_inject_ApplicationDbContext_directly()
    {
        var violations = typeof(PRN222.RagAssistant.Pages.IndexModel).Assembly
            .GetTypes()
            .Where(type => !type.IsAbstract && typeof(PageModel).IsAssignableFrom(type))
            .SelectMany(type => type.GetConstructors()
                .SelectMany(constructor => constructor.GetParameters()
                    .Where(parameter => parameter.ParameterType == typeof(ApplicationDbContext))
                    .Select(_ => type.FullName ?? type.Name)))
            .OrderBy(name => name)
            .ToList();

        Assert.Empty(violations);
    }
}
