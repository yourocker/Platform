using Core.MultiTenancy;

namespace CRM.ViewModels.CompanySettings;

public sealed class TenantMembersFilterInput
{
    public string? Search { get; set; }
    public string? Status { get; set; } = "active";
}

public sealed class InviteTenantMemberInput
{
    public string Login { get; set; } = string.Empty;
    public bool MakeDefault { get; set; }
}

public sealed class TenantMemberRowViewModel
{
    public Guid MembershipId { get; set; }
    public Guid EmployeeId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string? Login { get; set; }
    public string? PrimaryEmail { get; set; }
    public bool IsActive { get; set; }
    public bool IsDismissed { get; set; }
    public bool IsDefault { get; set; }
    public bool IsCurrentUser { get; set; }
    public DateTime JoinedAt { get; set; }
}

public sealed class TenantMembersViewModel
{
    public Guid TenantId { get; set; }
    public string TenantName { get; set; } = string.Empty;
    public TenantMembersFilterInput Filter { get; set; } = new();
    public InviteTenantMemberInput Invite { get; set; } = new();
    public IReadOnlyList<TenantMemberRowViewModel> Items { get; set; } = Array.Empty<TenantMemberRowViewModel>();
}
