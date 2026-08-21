using Microsoft.AspNetCore.SignalR;
using PRN222.RagAssistant.Application.Abstractions;

namespace PRN222.RagAssistant.Realtime;

public sealed class SignalRManagementRealtimeNotifier(
    IHubContext<ManagementHub> hubContext,
    ILogger<SignalRManagementRealtimeNotifier> logger) : IManagementRealtimeNotifier
{
    public async Task PublishAsync(
        ManagementRealtimeEvent notification,
        CancellationToken cancellationToken = default)
    {
        switch (notification.Resource)
        {
            case ManagementResource.Document:
            case ManagementResource.Chapter:
                await SendToSubjectAsync(notification, cancellationToken);
                break;

            case ManagementResource.Subject:
                await SendToGroupAsync(
                    ManagementHub.AdminSubjectsGroup,
                    notification,
                    cancellationToken);
                await SendToGroupAsync(
                    ManagementHub.SubjectCatalogGroup,
                    notification,
                    cancellationToken);
                break;

            case ManagementResource.SubjectLeaderAssignments:
                await SendToGroupAsync(
                    ManagementHub.AdminSubjectsGroup,
                    notification,
                    cancellationToken);
                await SendToSubjectAsync(notification, cancellationToken);
                break;

            case ManagementResource.User:
                await SendToGroupAsync(
                    ManagementHub.AdminUsersGroup,
                    notification,
                    cancellationToken);
                break;

            default:
                logger.LogWarning(
                    "Ignoring unsupported management realtime resource {Resource} for {EntityId}.",
                    notification.Resource,
                    notification.EntityId);
                break;
        }
    }

    private async Task SendToSubjectAsync(
        ManagementRealtimeEvent notification,
        CancellationToken cancellationToken)
    {
        if (!notification.SubjectId.HasValue || notification.SubjectId.Value == Guid.Empty)
        {
            logger.LogWarning(
                "Skipping management realtime notification {Resource}/{Change} for {EntityId} because it has no subject scope.",
                notification.Resource,
                notification.Change,
                notification.EntityId);
            return;
        }

        await SendToGroupAsync(
            ManagementHub.GetSubjectGroup(notification.SubjectId.Value),
            notification,
            cancellationToken);
    }

    private async Task SendToGroupAsync(
        string groupName,
        ManagementRealtimeEvent notification,
        CancellationToken cancellationToken)
    {
        try
        {
            await hubContext.Clients
                .Group(groupName)
                .SendAsync(
                    ManagementHub.ManagementChangedEvent,
                    notification,
                    cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(
                ex,
                "Failed to fan out management realtime notification {Resource}/{Change} for {EntityId} to group {GroupName}.",
                notification.Resource,
                notification.Change,
                notification.EntityId,
                groupName);
        }
    }
}
