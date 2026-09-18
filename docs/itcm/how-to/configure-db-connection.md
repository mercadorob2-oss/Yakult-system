# How-to: Configure ITCM Database Connection (WinForms)

## Document Control

| Field | Value |
| --- | --- |
| Document owner | ITD / IT Support |
| Audience | IT support |
| System | IT Call Monitoring (ITCM) |
| Version | 1.0 |
| Effective date | 2026-03-10 |
| Last updated | 2026-03-10 |

## 1. Goal

Configure a workstation so the WinForms application can connect to the correct SQL Server database used by ITCM.

## 2. When to use this

Use this procedure when:

- ITCM shows “Database connection is not configured”, or
- The application cannot load ITCM pages, or
- A new workstation/user profile is being set up, or
- The SQL Server/database has changed (new host, port, or DB name).

## 3. Important behavior (corporate support notes)

### 3.1 Connection is stored per Windows user profile

The connection string is saved in user settings:

- Setting key: `Properties.Settings.Default.DbConnectionString`
- It is per Windows login profile. Two different Windows accounts on the same PC can have different DB connection settings.

### 3.2 Security handling

- Do not share passwords in chat/email.
- Prefer accounts with least privilege required for ITCM operations.
- If your environment requires encryption, review `Encrypt=` and certificate settings with your DBA/security team.

## 4. Procedure (DB Setup dialog)

This dialog is implemented in:

- `Yakult.Inventory.App/Pages/Admin/DBConn/DatabaseSetupForm.cs`

Steps:

1. Launch `Yakult.Inventory.App`.
2. If the DB Setup dialog appears, fill in:
   - **Server Address**: SQL Server host/IP (include instance/port if required)
   - **Database Name**: target database name (example often used: `YIMS`)
   - **Username** and **Password**
3. Click **Test Connection**.
4. If successful, click **Save & Continue**.

Expected result:

- The app continues to the main UI.
- ITCM pages load without “DB not configured” errors.

## 5. What the app saves (connection string format)

The dialog builds a SQL connection string similar to:

```
Data Source=<server>;Initial Catalog=<database>;User ID=<username>;Password=<password>;Encrypt=False;TrustServerCertificate=True;
```

Support notes:

- If your SQL Server enforces encryption, coordinate with DB/security to use the correct `Encrypt=` and certificate settings.
- If Windows Authentication is required, this dialog may not be sufficient without code/environment adjustments.

## 6. Validation (quick checks)

### 6.1 In-app validation (recommended)

- Open ITCM → Diagnostics.
- Click **Refresh** and confirm:
  - Server name and database name match the target environment.
  - Schema checks show the key ITCM objects as present.

### 6.2 Manual validation (optional)

From the DBA side, confirm the account can connect and read required objects (tables/views/procs).

## 7. Troubleshooting

### 7.1 “Unable to connect” during Test Connection

Common causes:

- Wrong server/port or DNS resolution issue
- Firewall blocking SQL port
- Wrong database name
- Invalid credentials
- SQL Server not allowing SQL authentication for that login

Actions:

- Confirm server is reachable (network route).
- Confirm SQL port is open.
- Validate credentials with DBA.
- Confirm the target database exists and is online.

### 7.2 ITCM loads but some pages fail

Possible causes:

- Partial schema deployment (missing table/view/proc)
- SQL user permissions are incomplete

Actions:

- Run schema verification: `docs/itcm/how-to/verify-itcm-schema.md`
- Review diagnostics schema checks and collect evidence for escalation.

## 8. Revision History

| Version | Date | Change |
| --- | --- | --- |
| 1.0 | 2026-03-10 | Initial release |

