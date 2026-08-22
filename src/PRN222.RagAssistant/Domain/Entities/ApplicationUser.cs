using Microsoft.AspNetCore.Identity;

namespace PRN222.RagAssistant.Domain.Entities;

public sealed class ApplicationUser : IdentityUser<Guid>
{
    public string DisplayName { get; set; } = string.Empty;

    public DateTime CreatedAtUtc { get; set; }

    public int QuotaRemaining { get; set; } = 10;
}
