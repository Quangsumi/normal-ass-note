using System.Collections.Frozen;

namespace NormalAssNote.Application.Authorization;

/// <summary>
/// Keycloak client roles issued by the normal-ass-note OIDC client.
///
/// These roles control the platform tenant catalog.
/// They never grant access to tenant note content.
/// </summary>
public static class PlatformRoleNames
{
    public const string Admin = "platform-admin";
    public const string Viewer = "platform-viewer";

    /// <summary>
    /// Allowlist used later when translating validated Keycloak claims.
    /// </summary>
    public static FrozenSet<string> All { get; } = new[] { Admin, Viewer }.ToFrozenSet(StringComparer.Ordinal);
}
