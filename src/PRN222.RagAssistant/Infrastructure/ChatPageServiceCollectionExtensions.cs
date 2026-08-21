using PRN222.RagAssistant.Application.Abstractions;
using PRN222.RagAssistant.Infrastructure.Services;

namespace PRN222.RagAssistant.Infrastructure;

public static class ChatPageServiceCollectionExtensions
{
    public static IServiceCollection AddChatPageServices(this IServiceCollection services)
    {
        services.AddScoped<IChatPageService, ChatPageService>();
        return services;
    }
}
