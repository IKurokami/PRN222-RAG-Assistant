using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using PRN222.RagAssistant.Application.Abstractions;
using PRN222.RagAssistant.Realtime;
using PRN222.RagAssistant.Security;

namespace PRN222.RagAssistant.Tests;

public sealed class ManagementRealtimeTests
{
    [Fact]
    public async Task Subject_manager_can_join_subject_group()
    {
        var subjectId = Guid.NewGuid();
        var subjectAccess = new Mock<ISubjectAccessService>();
        subjectAccess
            .Setup(access => access.CanManageSubjectAsync(
                It.IsAny<ClaimsPrincipal>(),
                subjectId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        var authorization = CreateAuthorizationMock(
            AppPolicies.ManageDocuments,
            AuthorizationResult.Success());
        var groups = CreateGroupManager();
        var hub = CreateHub(
            subjectAccess,
            authorization,
            groups,
            TestPrincipals.WithRole(AppRoles.SubjectLeader));

        await hub.SubscribeToSubject(subjectId);

        groups.Verify(
            manager => manager.AddToGroupAsync(
                hub.Context.ConnectionId,
                $"subject:{subjectId:D}",
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Subject_access_denial_throws_and_does_not_join_subject_group()
    {
        var subjectId = Guid.NewGuid();
        var subjectAccess = new Mock<ISubjectAccessService>();
        subjectAccess
            .Setup(access => access.CanManageSubjectAsync(
                It.IsAny<ClaimsPrincipal>(),
                subjectId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        var authorization = CreateAuthorizationMock(
            AppPolicies.ManageDocuments,
            AuthorizationResult.Success());
        var groups = CreateGroupManager();
        var hub = CreateHub(
            subjectAccess,
            authorization,
            groups,
            TestPrincipals.WithRole(AppRoles.SubjectLeader));

        await Assert.ThrowsAsync<HubException>(() => hub.SubscribeToSubject(subjectId));

        groups.Verify(
            manager => manager.AddToGroupAsync(
                It.IsAny<string>(),
                $"subject:{subjectId:D}",
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ManageUsers_failure_rejects_admin_users_subscription()
    {
        var authorization = CreateAuthorizationMock(AppPolicies.ManageUsers, AuthorizationResult.Failed());
        var groups = CreateGroupManager();
        var hub = CreateHub(
            new Mock<ISubjectAccessService>(),
            authorization,
            groups,
            TestPrincipals.WithRole(AppRoles.SubjectLeader));

        await Assert.ThrowsAsync<HubException>(() => hub.SubscribeToAdminUsers());

        groups.Verify(
            manager => manager.AddToGroupAsync(
                It.IsAny<string>(),
                "admin:users",
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ManageSubjects_success_joins_admin_subjects_group()
    {
        var authorization = CreateAuthorizationMock(AppPolicies.ManageSubjects, AuthorizationResult.Success());
        var groups = CreateGroupManager();
        var hub = CreateHub(
            new Mock<ISubjectAccessService>(),
            authorization,
            groups,
            TestPrincipals.WithRole(AppRoles.Admin));

        await hub.SubscribeToAdminSubjects();

        groups.Verify(
            manager => manager.AddToGroupAsync(
                hub.Context.ConnectionId,
                "admin:subjects",
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Anonymous_catalog_subscription_is_rejected()
    {
        var authorization = CreateAuthorizationMock(AppPolicies.ManageSubjects, AuthorizationResult.Failed());
        var groups = CreateGroupManager();
        var hub = CreateHub(
            new Mock<ISubjectAccessService>(),
            authorization,
            groups,
            TestPrincipals.Anonymous());

        await Assert.ThrowsAsync<HubException>(() => hub.SubscribeToSubjectCatalog());

        groups.Verify(
            manager => manager.AddToGroupAsync(
                It.IsAny<string>(),
                "subjects:catalog",
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Document_notification_is_published_to_its_subject_group()
    {
        var subjectId = Guid.NewGuid();
        var clients = new Mock<IHubClients>();
        var subjectClient = new Mock<IClientProxy>();
        var adminSubjectsClient = new Mock<IClientProxy>();
        var catalogClient = new Mock<IClientProxy>();
        clients.Setup(value => value.Group($"subject:{subjectId:D}")).Returns(subjectClient.Object);
        clients.Setup(value => value.Group("admin:subjects")).Returns(adminSubjectsClient.Object);
        clients.Setup(value => value.Group("subjects:catalog")).Returns(catalogClient.Object);

        var hubContext = new Mock<IHubContext<ManagementHub>>();
        hubContext.SetupGet(value => value.Clients).Returns(clients.Object);
        var notifier = new SignalRManagementRealtimeNotifier(
            hubContext.Object,
            NullLogger<SignalRManagementRealtimeNotifier>.Instance);
        var notification = new ManagementRealtimeEvent(
            ManagementResource.Document,
            ManagementChange.IndexStatusChanged,
            Guid.NewGuid(),
            subjectId,
            Status: "Completed");

        await notifier.PublishAsync(notification);

        VerifyEvent(subjectClient, notification);
        VerifyNoEvent(adminSubjectsClient);
        VerifyNoEvent(catalogClient);
    }

    [Fact]
    public async Task Subject_notification_is_published_to_admin_and_catalog_groups()
    {
        var clients = new Mock<IHubClients>();
        var subjectClient = new Mock<IClientProxy>();
        var adminSubjectsClient = new Mock<IClientProxy>();
        var catalogClient = new Mock<IClientProxy>();
        clients.Setup(value => value.Group("subject:00000000-0000-0000-0000-000000000001"))
            .Returns(subjectClient.Object);
        clients.Setup(value => value.Group("admin:subjects")).Returns(adminSubjectsClient.Object);
        clients.Setup(value => value.Group("subjects:catalog")).Returns(catalogClient.Object);

        var hubContext = new Mock<IHubContext<ManagementHub>>();
        hubContext.SetupGet(value => value.Clients).Returns(clients.Object);
        var notifier = new SignalRManagementRealtimeNotifier(
            hubContext.Object,
            NullLogger<SignalRManagementRealtimeNotifier>.Instance);
        var notification = new ManagementRealtimeEvent(
            ManagementResource.Subject,
            ManagementChange.Updated,
            Guid.NewGuid());

        await notifier.PublishAsync(notification);

        VerifyEvent(adminSubjectsClient, notification);
        VerifyEvent(catalogClient, notification);
        VerifyNoEvent(subjectClient);
    }

    private static Mock<IGroupManager> CreateGroupManager()
    {
        var groups = new Mock<IGroupManager>();
        groups
            .Setup(manager => manager.AddToGroupAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        return groups;
    }

    private static Mock<IAuthorizationService> CreateAuthorizationMock(
        string policy,
        AuthorizationResult result)
    {
        var authorization = new Mock<IAuthorizationService>();
        authorization
            .Setup(service => service.AuthorizeAsync(
                It.IsAny<ClaimsPrincipal>(),
                It.Is<object?>(resource => resource == null),
                policy))
            .ReturnsAsync(result);
        return authorization;
    }

    private static ManagementHub CreateHub(
        Mock<ISubjectAccessService> subjectAccess,
        Mock<IAuthorizationService> authorization,
        Mock<IGroupManager> groups,
        ClaimsPrincipal user)
    {
        var context = new Mock<HubCallerContext>();
        context.SetupGet(value => value.ConnectionId).Returns("test-connection");
        context.SetupGet(value => value.User).Returns(user);

        return new ManagementHub(subjectAccess.Object, authorization.Object)
        {
            Context = context.Object,
            Groups = groups.Object
        };
    }

    private static void VerifyEvent(Mock<IClientProxy> client, ManagementRealtimeEvent expected)
    {
        client.Verify(proxy => proxy.SendCoreAsync(
                "ManagementChanged",
                It.Is<object?[]>(arguments =>
                    arguments.Length == 1
                    && expected.Equals(arguments[0])),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    private static void VerifyNoEvent(Mock<IClientProxy> client)
    {
        client.Verify(
            proxy => proxy.SendCoreAsync(
                "ManagementChanged",
                It.IsAny<object?[]>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }
}
