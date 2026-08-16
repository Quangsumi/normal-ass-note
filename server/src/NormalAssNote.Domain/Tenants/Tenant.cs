namespace NormalAssNote.Domain.Tenants;

public sealed class Tenant
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string Slug { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public TenantStatus Status { get; set; } = TenantStatus.Active;

    /// <summary>
    /// Audit information only. This never grants tenant access.
    /// </summary>
    public string? CreatedByUserId { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }

    public DateTimeOffset UpdatedAtUtc { get; set; }

    /// <summary>
    /// Replace with a new value whenever the tenant is changed.
    /// EF uses it to detect concurrent updates.
    /// </summary>
    public string ConcurrencyStamp { get; set; } = Guid.NewGuid().ToString("N"); // N = 32 digits, no hyphens
}