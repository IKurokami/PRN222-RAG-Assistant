using PRN222.RagAssistant.Application.Abstractions;
using PRN222.RagAssistant.Infrastructure.Services;

namespace PRN222.RagAssistant.Infrastructure;

public static class ReportingServiceCollectionExtensions
{
    public static IServiceCollection AddReporting(this IServiceCollection services)
    {
        services.AddScoped<IReportQueryService, ReportQueryService>();
        return services;
    }
}
