# Yakult.Inventory.Gateway

The desktop app signs in here instead of connecting to SQL Server itself.
The database connection string exists only on the gateway server, the same way
`Yakult.ITCM.Server` keeps its own. Nothing secret is committed.

## Endpoints

| Method | Path | Auth | Purpose |
|---|---|---|---|
| GET | `/api/health` | none | `{ status, databaseReachable, environments[] }` (`databaseReachable` = the default environment) |
| GET | `/api/environments` | none | databases the desktop switcher can pick: name, display name, kind (Production/Test). No database names or connection strings |
| POST | `/api/auth/login` | none, 10/min per IP | `{ userName, password, environment? }` → token + full session (user, roles, employee, department account, permissions, notification setting) |
| GET | `/api/auth/me` | bearer | who the token belongs to, and its environment |
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

## Databases (the Ctrl+Shift+D switcher)

The desktop's database switcher lists what the gateway offers instead of taking
a server, username and password. In the server's `appsettings.Local.json`:

- `ConnectionStrings` is the **Production** database (the default).
- Each entry under `Environments` is another database the switcher can pick, e.g.
  `"Test"` for YIMS_PROD, the dummy database. See `appsettings.Local.example.json`.

Edits apply without a restart. Each database checks passwords against its own
`dbo.[User]` table. The chosen environment is locked into the login token, so the
connection handed to the desktop always matches the database the user signed in to.
The PC stores only the chosen name
(`%LocalAppData%\Yakult\Inventory\gateway-environment.txt`).

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

## Migration status

| Phase | Module | Status |
|---|---|---|
| 1 | Sign-in, session, permissions snapshot | Done |
| 2 | Reference data: branches, company delete, department create, item conditions, categories, vendors, holidays, SMTP switch (`Endpoints/OrganizationEndpoints.cs`, `CatalogEndpoints.cs`, `AdminSettingsEndpoints.cs`) | Done |
| 3 | Accounts & security | To do |
| 4 | Items & inventory (incl. `VendorRepository.UpdateItemVendorAsync`) | To do |
| 5 | Requests, Sets, Invoices, Renewals | To do |
| 6 | Cartridges & consumables | To do |
| 7 | Call Monitoring, Repair tickets, Email/Notifications | To do |
| 8 | Leftovers (inline SQL in pages) + turn off the bridge | To do |

Not ported in phase 2 because nothing calls them: all of `EmployeeRepository`,
`CompanyRepository` except `DeleteCompanyAsync`, `CategoryRepository.GetByIdAsync`,
`VendorRepository.GetVendorByIdAsync` / `DeleteVendorAsync`.

Also: `/api/auth/refresh` swaps a valid token for a new one. The desktop calls it
when fewer than 3 hours remain, so an open app never expires. If a token is rejected
anyway (e.g. the PC slept past 12 hours), the desktop asks for the password again
(`GatewaySessionGuard` + `GatewayReauthDialog`) without logging out.

## Moving a module off direct SQL

1. Add an endpoint group on the gateway (for example `Endpoints/ItemEndpoints.cs`) with
   `.RequireAuthorization()`. Move the SQL from the desktop repository into it.
2. **Check permissions on the server.** The client's `PermissionResolver` checks are UI
   hints only. Read the user id from the token (`ClaimTypes.NameIdentifier`), never from
   the request body.
3. In the desktop repository, add a gateway branch at the top of the method and
   **keep the SQL below it**:
   ```csharp
   if (GatewayClient.UseForData)
       return await GatewayClient.GetAsync<List<VendorDto>>("api/vendors");
   // existing SQL: still used in direct mode (GatewayUrl empty) and by the ITCM scheduler
   ```
   Synchronous methods use the blocking `GatewayClient.Get/Post/Put/Delete`. Keep
   client-side side effects (`ActivityLogger`, `Logger`) in the gateway branch too.
   Keep multi-statement transactions inside **one** endpoint; never span a
   transaction across HTTP calls.
4. Server models use the same property names as the desktop DTOs so the desktop
   deserializes straight into its own classes.
5. SQL errors come back as HTTP 409 with SQL's own message, so code that checks
   `ForeignKeyErrorHelper.IsForeignKeyViolation(ex)` still works. A
   `catch (SqlException)` does **not** catch them: widen it (see `BranchPageViewModel`).
6. When no desktop code uses `DatabaseConfig.ConnectionString` any more, disable the bridge.
