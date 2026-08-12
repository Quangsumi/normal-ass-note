### Role matrix
| Authority | Role | Scope |
|---|---|---|
| Keycloak client | `platform-admin` | Manage the tenant catalog |
| Keycloak client | `platform-viewer` | Read tenant-catalog metadata |
| ASP.NET Identity | `membership-admin` | Manage non-owner memberships globally |
| ASP.NET Identity | `membership-viewer` | Read users and memberships globally |
| Tenant membership | `Owner` | Manage one tenant's settings, owners, members, and notes |
| Tenant membership | `Editor` | Read and write notes in one tenant |
| Tenant membership | `Viewer` | Read notes in one tenant |

```
*"Non-owner" means tenant members whose role is Editor or Viewer.
membership-admin can globally manage: Viewer, Editor. But it cannot grant, remove, or modify Owner.
Only a tenant Owner can transfer ownership, protecting against a global membership admin giving themselves access

Owner     = controls the tenant
Non-owner = Editor or Viewer
```

```
Read Tenant A members:

Directory.Memberships.ReadAll
OR
Tenant.Members.Read for Tenant A
```
```
Manage non-owner members in Tenant A:

Directory.Memberships.ManageAll
OR
Tenant.Members.Manage for Tenant A
```

```
Note access has no global alternative:
Read Tenant A notes:

Tenant.Notes.Read for Tenant A only
```
```
Write Tenant A notes:

Tenant.Notes.Write for Tenant A only
```

```
Therefore, none of these alone grants note access:
platform-admin
platform-viewer
membership-admin
membership-viewer
```

### Membership-admin restrictions
membership-admin is powerful but is not a tenant owner.
It may:
	Add another user as Viewer or Editor.
	Change another user between Viewer and Editor.
	Remove or suspend a non-owner membership.
	Perform those operations across tenants.
It may not:
	Add itself to a tenant through global authority.
	Change its own membership through global authority.
	Grant, remove, or transfer Owner status.
	Read note content without a normal tenant membership.
	Assign ASP.NET Identity roles.
	Manage Keycloak identities or credentials.
Owner changes require Tenant.Ownership.Transfer.

### Tenant state rules
Active tenants behave normally.
Archived tenants allow authorized reads.
Archived tenants reject settings, membership, ownership, and note mutations.
Restoring a tenant requires Platform.Tenants.Restore.
Physical tenant deletion is outside V1.

### Non-negotiable invariants
Every request resolves to one trusted ApplicationUser.Id.
OIDC users are linked by validated (issuer, subject), never email or username.
Keycloak roles come only from an allowlisted validated OIDC claim.
Keycloak roles are never copied into AspNetUserRoles.
Identity roles are loaded from PostgreSQL.
Tenant roles come from one active (TenantId, ApplicationUserId) membership.
A user has at most one role per tenant.
Platform and Identity roles never grant note permissions.
Tenant roles never grant global platform or directory permissions.
CreatedByUserId is audit data and never determines access.
Every note query includes the authorized route tenantId.
A body-supplied tenant ID is never authoritative.
An inaccessible cross-tenant note returns 404.
A tenant always has at least one active Owner.
The final active Owner cannot be removed or demoted.
Suspended memberships grant no tenant permissions.
/api/me describes permissions but never replaces server authorization.
Identity-role and membership revocation takes effect on the next request.
Keycloak role revocation follows the application-cookie snapshot behavior.
Legacy users may receive Identity and tenant permissions, but never Keycloak platform permissions.
Multiple grants form a union; V1 has no deny roles or wildcards.

### HTTP behavior
401  No valid authentication
403  Authenticated but missing a global permission
404  Tenant resource is missing or outside accessible scope
409  Final-owner, archived-state, or concurrency conflict
400  Invalid input or role value

### Permission matrix
| Role | Read catalog | Manage catalog | Read directory | Manage memberships | Read notes | Write notes | Manage tenant |
|---|---:|---:|---:|---:|---:|---:|---:|
| `platform-admin` | Yes | Yes | No | No | No | No | No |
| `platform-viewer` | Yes | No | No | No | No | No | No |
| `membership-admin` | No | No | Yes | Global non-owner | No | No | No |
| `membership-viewer` | No | No | Yes | No | No | No | No |
| Tenant `Owner` | No | No | Own tenant | Own tenant | Own tenant | Own tenant | Own tenant |
| Tenant `Editor` | No | No | No | No | Own tenant | Own tenant | No |
| Tenant `Viewer` | No | No | No | No | Own tenant | No | No |