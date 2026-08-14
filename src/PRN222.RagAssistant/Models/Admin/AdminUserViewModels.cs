using System.ComponentModel.DataAnnotations;
using PRN222.RagAssistant.Security;

namespace PRN222.RagAssistant.Models.Admin;

public sealed class AdminUserListViewModel
{
    public List<AdminUserListItemViewModel> Users { get; set; } = [];

    public string? StatusMessage { get; set; }
}

public sealed class AdminUserListItemViewModel
{
    public Guid Id { get; set; }

    public string DisplayName { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public DateTime CreatedAtUtc { get; set; }

    public List<string> Roles { get; set; } = [];

    public bool IsCurrentUser { get; set; }
}

public sealed class AdminUserCreateViewModel
{
    [Required]
    [StringLength(150)]
    [Display(Name = "Display name")]
    public string DisplayName { get; set; } = string.Empty;

    [Required]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required]
    [DataType(DataType.Password)]
    [StringLength(100, MinimumLength = 8)]
    public string Password { get; set; } = string.Empty;

    [Required]
    [DataType(DataType.Password)]
    [Compare(nameof(Password), ErrorMessage = "Password confirmation does not match.")]
    [Display(Name = "Confirm password")]
    public string ConfirmPassword { get; set; } = string.Empty;

    [Required]
    public string Role { get; set; } = AppRoles.Student;
}

public sealed class AdminUserRoleEditViewModel
{
    public Guid Id { get; set; }

    public string DisplayName { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    [Required]
    public string Role { get; set; } = AppRoles.Student;

    public bool IsCurrentUser { get; set; }
}
