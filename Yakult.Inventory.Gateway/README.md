# Yakult.Inventory.Gateway

The desktop app signs in here instead of connecting to SQL Server itself.
The database connection string exists only on the gateway server, the same way
`Yakult.ITCM.Server` keeps its own. Nothing secret is committed.

## Endpoints

| Method | Path | Auth | Purpose |
|---|---|---|---|
| GET | `/api/health` | none | `{ status, databaseReachable }` |
| POST | `/api/auth/login` | none, 10/min per IP | `{ userName, password }` → token + full session (user, roles, employee, department account, permissions, notification setting) |
| GET | `/api/auth/me` | bearer | who the token belongs to |
| GET | `/api/session/db-connection` | bearer | **migration bridge**, see below |

Tokens are ASP.NET Core bearer tokens (Data Protection), valid for
`Gateway:TokenLifetimeHours` (12). Keys persist in
`Gateway:DataProtectionKeysPath`, so a redeploy does not sign everyone out.

## Deploy (IIS on 192.168.100.186, same as ITCM)

1. `dotnet publish -c Release -o <site folder>` (needs the .NET 8 Hosting Bundle, already there for ITCM).
2. Create an IIS site on a free port (for example 7018) with its own app pool (No Managed Code).
3. Create `appsettings.Local.json` **beside the published exe** (copy `appsettings.Local.example.json`):
   - `ConnectionStrings:Yakult_Inventory_System`: what the gateway itself uses. Prefer
     `Integrated Security=true` and grant the app-pool identity (`IIS APPPOOL\<pool>`) a SQL login,
     so no password is stored anywhere.
   - `ConnectionStrings:LegacyClient`: the SQL login handed to desktops by the bridge (below).
4. Give the app-pool identity Modify on `C:\ProgramData\Yakult\Gateway\DataProtection-Keys`.
5. Check `http://<server>:<port>/api/health` shows `"databaseReachable": true`.
6. In the desktop `App.config`, set `GatewayUrl` to the site URL, then build and ship the installer.

Before production, bind an HTTPS certificate and set `Gateway:RequireHttps` to `true`:
over plain HTTP, passwords and tokens cross the LAN unencrypted.

## The migration bridge (temporary)

About 1,160 places in the desktop app still query SQL directly. Until each is moved
to a gateway endpoint, a signed-in desktop fetches the `LegacyClient` connection string
from `/api/session/db-connection` and keeps it **in memory only**
(`DatabaseConfig.SetGatewayConnection`). That already removes the password from the
code, the repo, the installer and users' disks, but a signed-in user can still pull it
out of memory. The goal is to turn it off
(`Gateway:LegacyClientConnection:Enabled = false`) once every module is migrated.

Until then, keep `LegacyClient` a **separate, least-privilege SQL login** (not `sa`, not
the gateway's own identity), so it can be rotated or cut off without touching the gateway.

## Moving a module off direct SQL

1. Add an endpoint group on the gateway (for example `Endpoints/ItemEndpoints.cs`) with
   `.RequireAuthorization()`. Move the SQL from the desktop repository into it.
2. **Check permissions on the server.** The client's `PermissionResolver` checks are UI
   hints only. Read the user id from the token (`ClaimTypes.NameIdentifier`), never from
   the request body.
3. In the desktop repository, replace the `SqlConnection` code with
   `GatewayClient.GetAsync<T>("api/...")` / `GatewayClient.PostAsync<T>(...)`.
   Keep multi-statement transactions inside **one** endpoint; never span a
   transaction across HTTP calls.
4. When no desktop code uses `DatabaseConfig.ConnectionString` any more, disable the bridge.
