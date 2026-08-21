using Microsoft.AspNetCore.Authorization;
using PRN222.RagAssistant.Pages.Evaluation;
using PRN222.RagAssistant.Security;

namespace PRN222.RagAssistant.Tests;

/// <summary>
/// Regression tests for issue #44: Student accounts must not access the RAG Evaluation page.
/// Authorization is enforced server-side through the [Authorize] attribute on the PageModel.
/// </summary>
public sealed class EvaluationAuthorizationTests
{
    // -------------------------------------------------------------------------
    // Attribute-level contract tests (no DI required)
    // -------------------------------------------------------------------------

    [Fact]
    public void Evaluation_IndexModel_has_Authorize_attribute()
    {
        var attributes = typeof(IndexModel)
            .GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true)
            .Cast<AuthorizeAttribute>()
            .ToList();

        Assert.NotEmpty(attributes);
    }

    [Fact]
    public void Evaluation_IndexModel_requires_Admin_or_SubjectLeader_role()
    {
        // Issue #44: the attribute must restrict to Admin and SubjectLeader roles.
        // Student and unauthenticated users must be blocked.
        var authorizeAttribute = typeof(IndexModel)
            .GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true)
            .Cast<AuthorizeAttribute>()
            .Single();

        // Roles string must contain both Admin and SubjectLeader separated by comma.
        var roles = authorizeAttribute.Roles?.Split(',')
                                             .Select(r => r.Trim())
                                             .ToHashSet(StringComparer.OrdinalIgnoreCase)
                        ?? new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        Assert.Contains(AppRoles.Admin, roles);
        Assert.Contains(AppRoles.SubjectLeader, roles);
    }

    [Fact]
    public void Evaluation_IndexModel_does_NOT_allow_Student_role()
    {
        // Verify Student is not listed in the Roles string, so [Authorize] blocks them.
        var authorizeAttribute = typeof(IndexModel)
            .GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true)
            .Cast<AuthorizeAttribute>()
            .Single();

        var roles = authorizeAttribute.Roles?.Split(',')
                                             .Select(r => r.Trim())
                                             .ToHashSet(StringComparer.OrdinalIgnoreCase)
                        ?? new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        Assert.DoesNotContain(AppRoles.Student, roles);
    }

    [Fact]
    public void Evaluation_IndexModel_does_NOT_use_open_Authorize_attribute()
    {
        // Ensure there is no bare [Authorize] with no Roles/Policy that would let Students through.
        var authorizeAttribute = typeof(IndexModel)
            .GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true)
            .Cast<AuthorizeAttribute>()
            .Single();

        var isOpenAuthorize = string.IsNullOrWhiteSpace(authorizeAttribute.Roles)
                              && string.IsNullOrWhiteSpace(authorizeAttribute.Policy);

        Assert.False(
            isOpenAuthorize,
            "Evaluation.IndexModel must not use a bare [Authorize] that allows any authenticated user. " +
            "It must restrict to Admin and SubjectLeader roles (issue #44).");
    }
}
