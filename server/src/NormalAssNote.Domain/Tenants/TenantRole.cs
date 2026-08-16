namespace NormalAssNote.Domain.Tenants;

/// <summary>
/// A user's authorization role inside one tenant.
///
/// Numeric values are identifiers only. They are not a role hierarchy.
/// Never authorize by comparing enum values. Always use the explicit
/// role-to-permission mapping.
/// </summary>
public enum TenantRole
{
    Owner = 1,
    Editor = 2,
    Viewer = 3
}
