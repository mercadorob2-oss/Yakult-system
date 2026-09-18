# Automated Database Backups

Backs up `Yakult_Inventory_System_DEV` and `YIMS_PROD` on `.\SQLEXPRESS` every
Monday-Saturday at 5:00 PM (Asia/Manila), keeping 14 days of `.bak` files.

## One-time setup (run on the machine that will run the backups)

1. Open PowerShell **as the Windows account** that should own the task (this
   account's DPAPI key encrypts the SQL password, so the task must later run
   as this same account):
   ```powershell
   .\Setup-BackupCredential.ps1
   ```
   Enter the SQL login (`remote_user`) and its password when prompted.

2. Open PowerShell **as Administrator** and register the scheduled task:
   ```powershell
   .\Install-BackupTask.ps1
   ```
   You'll be prompted for the Windows account's password again so Task
   Scheduler can log the account on non-interactively.

3. Confirm the server's Windows time zone is Asia/Manila (UTC+8) — Task
   Scheduler fires at 5:00 PM in the server's own local clock, not a named
   time zone. If the server runs on a different time zone, either change it
   or re-run step 2 with `-TriggerTime` set to the equivalent local time.

## Test it manually

```powershell
Start-ScheduledTask -TaskName "Yakult DB Backup"
```

Check `Logs\backup_<timestamp>.log` for the result, and confirm new `.bak`
files appear in:
```
C:\Program Files\Microsoft SQL Server\MSSQL17.SQLEXPRESS\MSSQL\Backup
```

## Changing databases, retention, or backup path

Edit the `param()` defaults at the top of `Backup-YakultDatabases.ps1`
(`$Databases`, `$RetentionDays`, `$BackupFolder`).

## Notes

- The SQL password is never stored in plaintext — `sql-backup.cred.xml` is
  encrypted via Windows DPAPI to the account that created it, and is
  git-ignored.
- `BACKUP DATABASE` runs on the SQL Server itself, so `$BackupFolder` must be
  writable by the SQL Server *service account*, not the account running the
  script — the default path here is SQL Server's own default backup folder,
  which already satisfies this.
