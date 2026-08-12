namespace NormalAssNote.Domain.Tenants;

public sealed class TenantMembership
{
    /*
     (TenantId, ApplicationUserId) as the composite primary key. Therefore:
        One user can have only one membership in a tenant.
        That membership has exactly one tenant role.
        Changing Viewer to Editor updates the existing row.
        Suspending a user updates Status; it does not create another membership.
     */

    public Guid TenantId { get; set; }

    public string ApplicationUserId { get; set; } = string.Empty;

    public TenantRole Role { get; set; }

    public TenantMembershipStatus Status { get; set; } = TenantMembershipStatus.Active;

    /// <summary>
    /// Audit information only.
    /// Null is allowed for bootstrap operations or a deleted actor.
    /// </summary>
    public string? AddedByUserId { get; set; }

    public DateTimeOffset AddedAtUtc { get; set; }

    public DateTimeOffset UpdatedAtUtc { get; set; }

    public string ConcurrencyStamp { get; set; } = Guid.NewGuid().ToString("N");
}