using System.Collections.Frozen;

namespace NormalAssNote.Application.Authorization;

/// <summary>
/// Global application roles stored in AspNetRoles and AspNetUserRoles.
///
/// These roles manage the local user/membership directory.
/// They never grant access to tenant note content.
/// </summary>
public static class IdentityRoleNames
{
    public const string MembershipAdmin = "membership-admin";
    public const string MembershipViewer = "membership-viewer";

    /// <summary>
    /// Role definitions that the Identity role seeder will create later.
    /// </summary>
    public static FrozenSet<string> All { get; } =
        new[]
        {
            MembershipAdmin,
            MembershipViewer
        }.ToFrozenSet(StringComparer.Ordinal);
}