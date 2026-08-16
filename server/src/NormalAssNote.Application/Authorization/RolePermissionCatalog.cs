using System.Collections.Frozen;
using NormalAssNote.Domain.Tenants;

namespace NormalAssNote.Application.Authorization;

/// <summary>
/// Pure role-to-permission mappings.
///
/// This class contains no claims, HTTP, Identity, EF Core, database,
/// or dependency-injection behavior.
/// </summary>
public static class RolePermissionCatalog
{
    private static readonly FrozenSet<string> Empty = Array.Empty<string>().ToFrozenSet(StringComparer.Ordinal);

    private static readonly FrozenDictionary<string, FrozenSet<string>> PlatformPermissions =
        new Dictionary<string, FrozenSet<string>>(StringComparer.Ordinal)
            {
                [PlatformRoleNames.Admin] = Set(
                    Permissions.Platform.TenantsReadMetadata,
                    Permissions.Platform.TenantsCreate,
                    Permissions.Platform.TenantsUpdate,
                    Permissions.Platform.TenantsArchive,
                    Permissions.Platform.TenantsRestore),

                [PlatformRoleNames.Viewer] = Set(
                    Permissions.Platform.TenantsReadMetadata)
            }.ToFrozenDictionary(StringComparer.Ordinal);

    private static readonly FrozenDictionary<string, FrozenSet<string>> IdentityPermissions =
            new Dictionary<string, FrozenSet<string>>(StringComparer.Ordinal)
            {
                [IdentityRoleNames.MembershipAdmin] = Set(
                    Permissions.Directory.UsersRead,
                    Permissions.Directory.MembershipsReadAll,
                    Permissions.Directory.MembershipsManageAll),

                [IdentityRoleNames.MembershipViewer] = Set(
                    Permissions.Directory.UsersRead,
                    Permissions.Directory.MembershipsReadAll)
            }.ToFrozenDictionary(StringComparer.Ordinal);

    private static readonly FrozenDictionary<TenantRole, FrozenSet<string>> TenantPermissions =
            new Dictionary<TenantRole, FrozenSet<string>>
            {
                [TenantRole.Owner] = Set(
                    Permissions.Tenant.MetadataRead,
                    Permissions.Tenant.SettingsUpdate,
                    Permissions.Tenant.MembersRead,
                    Permissions.Tenant.MembersManage,
                    Permissions.Tenant.OwnershipTransfer,
                    Permissions.Tenant.NotesRead,
                    Permissions.Tenant.NotesWrite),

                [TenantRole.Editor] = Set(
                    Permissions.Tenant.MetadataRead,
                    Permissions.Tenant.NotesRead,
                    Permissions.Tenant.NotesWrite),

                [TenantRole.Viewer] = Set(
                    Permissions.Tenant.MetadataRead,
                    Permissions.Tenant.NotesRead)
            }.ToFrozenDictionary();

    public static IReadOnlySet<string> ForPlatformRole(string? role)
    {
        if (role is null || !PlatformPermissions.TryGetValue(role, out var permissions))
        {
            return Empty;
        }

        return permissions;
    }

    public static IReadOnlySet<string> ForIdentityRole(string? role)
    {
        if (role is null || !IdentityPermissions.TryGetValue(role, out var permissions))
        {
            return Empty;
        }

        return permissions;
    }

    public static IReadOnlySet<string> ForTenantRole(TenantRole role)
    {
        if (!TenantPermissions.TryGetValue(role, out var permissions))
        {
            return Empty;
        }

        return permissions;
    }

    private static FrozenSet<string> Set(params string[] permissions) => permissions.ToFrozenSet(StringComparer.Ordinal);
}