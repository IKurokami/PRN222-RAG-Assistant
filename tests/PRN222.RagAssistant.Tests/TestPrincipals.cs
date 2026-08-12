using System.Security.Claims;

namespace PRN222.RagAssistant.Tests;

/// <summary>
/// Factory helpers for constructing <see cref="ClaimsPrincipal"/> instances
/// used in authorization tests.
/// </summary>
internal static class TestPrincipals
{
    /// <summary>Returns an authenticated principal with the given role.</summary>
    public static ClaimsPrincipal WithRole(string role)
    {
        var identity = new ClaimsIdentity(
            [
                new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()),
                new Claim(ClaimTypes.Name, $"test-{role}@prn222.edu"),
                new Claim(ClaimTypes.Role, role)
            ],
            authenticationType: "TestAuth");

        return new ClaimsPrincipal(identity);
    }

    /// <summary>Returns an unauthenticated (anonymous) principal.</summary>
    public static ClaimsPrincipal Anonymous() => new ClaimsPrincipal(new ClaimsIdentity());
}
