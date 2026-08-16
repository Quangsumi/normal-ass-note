# OIDC Logout Behavior Matrix

This document describes the expected logout behavior for the note application when it uses Keycloak OpenID Connect with RP-initiated, front-channel, and back-channel logout.

## Terminology

| Term | Meaning |
|---|---|
| Keycloak session | The upstream SSO session owned by Keycloak. A user can have different sessions in different browsers or devices. |
| Note session | The application session stored in the `AuthenticationSessions` PostgreSQL table. |
| `sid` | The Keycloak session identifier. `(iss, sid)` identifies one upstream login session. |
| `sub` | The Keycloak user identifier. `(iss, sub)` identifies the user across that issuer. |
| RP | Relying Party—the note application in this design. |

## Combined Logout Behavior Matrix

| Action | Logout channel | Keycloak sessions affected | Note database sessions affected | Other browsers affected? | Expected reliability |
|---|---|---:|---:|---:|---|
| User clicks logout in the note application | RP-initiated, followed by the client's configured notification channel | Current Keycloak browser session | Current note session is removed directly by the note app | No; independent browser sessions normally remain | Reliable for the current note session; Keycloak propagation depends on the configured channel |
| User signs out from the Keycloak Account Console | Front-channel when enabled | Current Keycloak browser session | Matching `(iss, sid)` note session | No; another browser cannot be relied upon to receive the iframe | Best effort because it depends on the browser and iframe delivery |
| Administrator removes one user session while front-channel is enabled | Front-channel cannot reliably run | Selected Keycloak session | Usually none; the note session can remain until expiration | No | Not supported as a reliable administrator-initiated application logout |
| Administrator removes all sessions for one user while front-channel is enabled | Front-channel cannot reliably run | All Keycloak sessions for that user | Note sessions can remain until expiration | No | Not reliable; there is no affected end-user browser in which to render all required iframes |
| Administrator removes one user session while back-channel is enabled | Back-channel | Selected Keycloak session | Matching `(iss, sid)` note session | The targeted browser becomes anonymous on its next request | Reliable server-to-server notification |
| Administrator removes all sessions for one user while back-channel is enabled | Back-channel | All Keycloak sessions for that user | All targeted note sessions, using one or more `sid` notifications or an `(iss, sub)` notification | Yes; every invalidated browser becomes anonymous on its next request | Reliable server-to-server notification |
| Keycloak Account Console performs a user-wide logout while back-channel is enabled | Back-channel | All selected Keycloak sessions | All targeted note sessions | Yes, on each browser's next request | Reliable server-to-server notification |
| Keycloak or the note application is temporarily unreachable | Either channel | Keycloak may still remove its own session | Notification may fail, leaving the note session until expiry | Not immediately | Front-channel has no server retry guarantee; back-channel can return an error so the failure is observable |

## Front-Channel Logout Matrix

Front-channel logout is browser-mediated. Keycloak returns an HTML logout page containing an iframe whose URL points to the note application's front-channel endpoint.

```text
Keycloak logout page
        |
        v
User's browser loads iframe
        |
        v
GET /oidc/frontchannel-logout?iss=...&sid=...
        |
        v
Delete AuthenticationSessions row matching (iss, sid)
```

| Trigger | Is an affected user browser involved? | Should Keycloak render the note iframe? | Expected note-app result |
|---|---:|---:|---|
| User signs out through Keycloak Account Console in Browser A | Yes | Yes | Browser A's note session is deleted by `(iss, sid)` |
| User performs RP-initiated logout from the note app in Browser A | Yes | Yes, although the note app already removes its local session | Current note session is deleted; subsequent front-channel deletion is idempotent |
| Administrator logs out User A from Admin Console in Browser A | No—the request is an administrator operation, not User A's logout page | No reliable iframe | Keycloak session can disappear while the note database session remains |
| Administrator logs out User A from another browser | No | No reliable iframe | Keycloak session can disappear while the note database session remains |
| User A is logged into Browser A and Browser B, then signs out in Browser A | Only Browser A participates | Browser A can receive the iframe; Browser B cannot be relied upon to receive it | Browser A's matching note session is deleted; Browser B can remain logged in |
| Browser blocks the iframe through CSP, frame policy, tracking protection, navigation, or page closure | Browser exists but delivery is blocked/interrupted | Maybe not | Note session can remain until expiry |

### Front-channel success evidence

All of the following should be visible:

1. Browser Developer Tools shows:

   ```http
   GET https://note.lab/oidc/frontchannel-logout?iss=...&sid=...
   ```

2. The response status is `200`.
3. The note application logs a message similar to:

   ```text
   Applied OIDC front-channel logout ... revoked 1 session(s).
   ```

4. The matching PostgreSQL row disappears.
5. The browser becomes anonymous on its next session check or API request.

### Front-channel failure diagnosis

| Observation | Meaning |
|---|---|
| No browser request and no note-app log | Keycloak did not render the iframe, or the browser blocked it before requesting the URL |
| HTTP `400` | `iss` or `sid` is missing, invalid, too long, or does not exactly match the configured issuer |
| HTTP `200`, log says `revoked 0 session(s)` | The endpoint ran, but no database row matched `(iss, sid)` |
| HTTP `200`, log says `revoked 1 session(s)` | Front-channel logout succeeded |
| Keycloak warns that the client was not logged out after an administrator action | Keycloak removed its own session but could not propagate a reliable front-channel logout to the application |

## Back-Channel Logout Matrix

Back-channel logout is server-to-server. It does not require the affected user's browser.

```text
Keycloak server
        |
        | POST application/x-www-form-urlencoded
        | logout_token=<signed JWT>
        v
POST /oidc/backchannel-logout
        |
        v
Validate signature, alg, iss, aud, iat, jti, events,
sid/sub, prohibited nonce, and replay protection
        |
        v
Delete matching AuthenticationSessions rows
```

| Logout Token identity | Meaning | Database operation |
|---|---|---|
| `iss` + `sid` | One Keycloak login session | Delete the session matching `(Issuer, SessionId)`; if `sub` is also supplied, require the subject to match |
| `iss` + `sub`, without `sid` | Every RP session for that Keycloak user | Delete all sessions matching `(Issuer, Subject)` |
| Both `sid` and `sub` | One session belonging to the specified user | Match the issuer, session ID, and subject |
| Neither `sid` nor `sub` | Invalid Logout Token | Reject with HTTP `400` |

### Back-channel trigger behavior

| Trigger | Expected Keycloak notifications | Expected note-app result |
|---|---|---|
| Administrator removes one Keycloak session | Normally one Logout Token containing `sid` when session-required is enabled | Delete one note session |
| Administrator removes all sessions for one user | Keycloak may send one `sid` notification per client session, or a user-scoped notification depending on token shape and settings | Delete all note sessions for that user |
| User performs user-wide logout from Account Console | Notifications for the affected client sessions | Delete all targeted note sessions |
| Same valid Logout Token is delivered again | Replay is detected by hashed `jti` | Return success without applying the deletion again |
| Valid token matches no database rows | Valid, idempotent logout | Return HTTP `200`; do not disclose whether the session existed |
| Keycloak metadata or JWKS cannot be loaded | Token cannot be safely validated | Return HTTP `503` and preserve the sessions |
| Token has invalid signature, issuer, audience, algorithm, claims, or event | Untrusted request | Return HTTP `400` and preserve the sessions |

### Back-channel Keycloak settings

```text
Front channel logout:                    Off
Backchannel logout URL:                  https://note.lab/oidc/backchannel-logout
Backchannel logout session required:     On
Backchannel logout revoke offline sessions: Off
```

With `Backchannel logout session required: On`, expect `sid`-specific revocation. To exercise the application's `(iss, sub)` branch, turn the setting off in a controlled test and perform a user-wide logout. Turning the setting off only changes the Logout Token shape; it does not itself terminate all Keycloak sessions.

## Browser UI Synchronization

Successful back-channel logout immediately invalidates the server-side note session, but it cannot directly modify an already-rendered browser page.

```text
Back-channel POST succeeds
        |
        v
PostgreSQL session row is deleted immediately
        |
        v
Browser retains a stale opaque cookie and rendered page
        |
        v
Next /api/auth/session call or authenticated API request discovers logout
```

Therefore, "logged out everywhere" means every server-side session is invalid immediately. Each browser visibly changes when it next:

- reloads;
- regains focus and revalidates `/api/auth/session`;
- calls an authenticated API and receives `401`; or
- performs periodic session polling.

Instant visual synchronization requires an additional client mechanism such as periodic session polling, SignalR, or Server-Sent Events. It is not provided by OIDC Back-Channel Logout itself.

## Recommended Mechanism by Use Case

| Requirement | Recommended mechanism |
|---|---|
| User logs out of the current note session | RP-initiated logout |
| Learn and demonstrate browser-mediated OIDC logout | Front-channel logout |
| Administrator logs out one session | Back-channel logout with `sid` |
| Administrator logs a user out everywhere | Back-channel logout covering all of the user's sessions |
| User self-service "log out all devices" | Keycloak user-wide logout plus back-channel propagation |
| Immediate visual changes in all open note tabs | Back-channel invalidation plus polling, SignalR, or SSE |
| Security incident or compromised account | Back-channel/user-wide session termination; do not depend on front-channel iframe delivery |

## Expected HTTP Statuses

| Situation | Response |
|---|---:|
| Valid front-channel logout | `200` |
| Unknown but well-formed front-channel `sid` | `200` |
| Invalid or missing front-channel `iss`/`sid` | `400` |
| Valid back-channel Logout Token | `200` |
| Replayed valid Logout Token | `200` |
| Valid Logout Token matching no sessions | `200` |
| Invalid Logout Token | `400` |
| Wrong back-channel media type | `415` |
| Keycloak metadata or JWKS unavailable | `503` |

The application should not reveal whether a particular user or session existed.
