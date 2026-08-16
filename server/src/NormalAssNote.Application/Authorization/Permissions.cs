using System.Collections.Frozen;

namespace NormalAssNote.Application.Authorization;

/// <summary>
/// Stable authorization permission identifiers.
///
/// Endpoints will request permissions. Roles only grant permissions.
/// </summary>
public static class Permissions
{
    public static class Platform
    {
        public const string TenantsReadMetadata = "Platform.Tenants.ReadMetadata";

        public const string TenantsCreate = "Platform.Tenants.Create";

        public const string TenantsUpdate = "Platform.Tenants.Update";

        public const string TenantsArchive = "Platform.Tenants.Archive";

        public const string TenantsRestore = "Platform.Tenants.Restore";
    }

    public static class Directory
    {
        public const string UsersRead = "Directory.Users.Read";

        public const string MembershipsReadAll = "Directory.Memberships.ReadAll";

        public const string MembershipsManageAll = "Directory.Memberships.ManageAll";
    }

    public static class Tenant
    {
        public const string MetadataRead = "Tenant.Metadata.Read";

        public const string SettingsUpdate = "Tenant.Settings.Update";

        public const string MembersRead = "Tenant.Members.Read";

        public const string MembersManage = "Tenant.Members.Manage";

        public const string OwnershipTransfer = "Tenant.Ownership.Transfer";

        public const string NotesRead = "Tenant.Notes.Read";

        /// <summary>
        /// Includes create, update, soft-delete, restore, and synchronization
        /// for the current note API design.
        /// </summary>
        public const string NotesWrite = "Tenant.Notes.Write";
    }

    public static FrozenSet<string> All { get; } = new[]
        {
            Platform.TenantsReadMetadata,
            Platform.TenantsCreate,
            Platform.TenantsUpdate,
            Platform.TenantsArchive,
            Platform.TenantsRestore,

            Directory.UsersRead,
            Directory.MembershipsReadAll,
            Directory.MembershipsManageAll,

            Tenant.MetadataRead,
            Tenant.SettingsUpdate,
            Tenant.MembersRead,
            Tenant.MembersManage,
            Tenant.OwnershipTransfer,
            Tenant.NotesRead,
            Tenant.NotesWrite
        }.ToFrozenSet(StringComparer.Ordinal);
}