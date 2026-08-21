using System.Security.Claims;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using PRN222.RagAssistant.Security;

namespace PRN222.RagAssistant.Realtime;

[Authorize]
public sealed class ManagementHub : Hub
{
    public const string ManagementChangedEvent = "ManagementChanged";
    public const string AdminUsersGroup = "admin:users";
    public const string AdminSubjectsGroup = "admin:subjects";
    public const string SubjectCatalogGroup = "subjects:catalog";

    private readonly ISubjectAccessService _subjectAccessService;
    private readonly IAuthorizationService _authorizationService;

    public ManagementHub(
        ISubjectAccessService subjectAccessService,
        IAuthorizationService authorizationService)
    {
        _subjectAccessService = subjectAccessService;
        _authorizationService = authorizationService;
    }

    [Authorize(Policy = AppPolicies.ManageDocuments)]
    public async Task SubscribeToSubject(Guid subjectId)
    {
        var user = RequireAuthenticatedUser();
        await RequirePolicyAsync(user, AppPolicies.ManageDocuments);

        if (!await _subjectAccessService.CanManageSubjectAsync(
                user,
                subjectId,
                Context.ConnectionAborted))
        {
            throw new HubException("You are not authorized to manage this subject.");
        }

        await Groups.AddToGroupAsync(
            Context.ConnectionId,
            GetSubjectGroup(subjectId),
            Context.ConnectionAborted);
    }

    [Authorize(Policy = AppPolicies.ManageUsers)]
    public async Task SubscribeToAdminUsers()
    {
        var user = RequireAuthenticatedUser();
        await RequirePolicyAsync(user, AppPolicies.ManageUsers);
        await Groups.AddToGroupAsync(
            Context.ConnectionId,
            AdminUsersGroup,
            Context.ConnectionAborted);
    }

    [Authorize(Policy = AppPolicies.ManageSubjects)]
    public async Task SubscribeToAdminSubjects()
    {
        var user = RequireAuthenticatedUser();
        await RequirePolicyAsync(user, AppPolicies.ManageSubjects);
        await Groups.AddToGroupAsync(
            Context.ConnectionId,
            AdminSubjectsGroup,
            Context.ConnectionAborted);
    }

    public async Task SubscribeToSubjectCatalog()
    {
        RequireAuthenticatedUser();
        await Groups.AddToGroupAsync(
            Context.ConnectionId,
            SubjectCatalogGroup,
            Context.ConnectionAborted);
    }

    public static string GetSubjectGroup(Guid subjectId) => $"subject:{subjectId:D}";

    private ClaimsPrincipal RequireAuthenticatedUser()
    {
        var user = Context.User;
        if (user is null || user.Identity?.IsAuthenticated != true)
        {
            throw new HubException("Authentication is required.");
        }

        return user;
    }

    private async Task RequirePolicyAsync(ClaimsPrincipal user, string policyName)
    {
        var authorizationResult = await _authorizationService.AuthorizeAsync(
            user,
            resource: null,
            policyName: policyName);

        if (!authorizationResult.Succeeded)
        {
            throw new HubException("You are not authorized to join this management group.");
        }
    }
}
