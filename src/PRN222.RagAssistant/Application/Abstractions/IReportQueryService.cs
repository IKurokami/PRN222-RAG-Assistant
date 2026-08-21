using PRN222.RagAssistant.Application.Models;

namespace PRN222.RagAssistant.Application.Abstractions;

public interface IReportQueryService
{
    Task<SubjectReportSnapshot?> GetSubjectReportAsync(
        Guid subjectId,
        CancellationToken cancellationToken = default);
}
