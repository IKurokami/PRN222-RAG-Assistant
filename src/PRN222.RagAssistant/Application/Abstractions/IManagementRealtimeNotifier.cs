namespace PRN222.RagAssistant.Application.Abstractions;

public interface IManagementRealtimeNotifier
{
    Task PublishAsync(
        ManagementRealtimeEvent notification,
        CancellationToken cancellationToken = default);
}

public record ManagementRealtimeEvent(
    ManagementResource Resource,
    ManagementChange Change,
    Guid EntityId,
    Guid? SubjectId = null,
    string? Status = null);

public enum ManagementResource
{
    Document,
    Chapter,
    Subject,
    SubjectLeaderAssignments,
    User
}

public enum ManagementChange
{
    Created,
    Updated,
    Deleted,
    IndexStatusChanged,
    AssignmentsChanged,
    RoleChanged
}
