# Previous renewal backlog

Renewal Details and Manage Items have a **Backlog / Previous Renewals** button. It opens a dedicated, maximized previous-renewal screen and selects the Renewal History tab. The screen follows the Renew Items layout: invoice details and an editable item table on the left, coverage settings, amount, notes, and a summary on the right, with fixed save/cancel actions below.

## Database setup

Run `DATABASES/Yakult-DB-Production/dbo/Scripts/Migration_LicenseHistoricalPeriod_CreateTables.sql` against the database selected by the desktop application before using the feature. The migration is idempotent and creates two new history tables. It does not change existing invoice or renewal records.

Deploy the rebuilt desktop application after the migration. Until the tables exist, the history panel displays an availability message; existing renewal history remains usable.

## Recording history

1. Open an invoice-backed renewal and click **Backlog / Previous Renewals**.
2. Enter the old coverage start and end dates. Use **Years → Calculate End Date** to calculate the inclusive end date from a start date when helpful. The period must end before the earliest recorded invoice or line coverage in the chain.
3. Optionally enter a reference/invoice number, period amount, and notes. Leave the amount blank if unknown.
4. Invoice items are preloaded and checked. Uncheck items that were not covered, or use **Check All / Uncheck All**. Use **Copy Invoice Items** to reset the draft, **+ Add Row** to select catalog items, or **Add Historical Item** for removed items. Edit names, codes, and quantities to match the old paperwork. The live summary shows the checked item count and total quantity.
5. Click **Save Previous Renewal**. Only checked items are saved. Exact duplicate periods are rejected; overlapping historical periods require confirmation.

For example, a current A/C/D invoice can have an older A/B period followed by an A/C period. Each historical record retains its own item list and names. A catalog rename does not change the snapshot.

Saved periods are visible from every invoice in the same renewal chain, including after separately recorded chains are linked. They do not change active coverage, financial totals, stock, renewal counters, or expiry reminders. Historical entries are add/view only; attachments and historical invoice generation are outside this version.

## Validation

Build the .NET Framework desktop project with Visual Studio MSBuild. The integration script accepts the resulting application executable:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -STA -File .\Yakult.Inventory.App\Tests\LicenseHistoricalPeriodIntegration.ps1 -AppPath 'path\to\Yakult.Inventory.App.exe'
```

The script creates a uniquely named disposable LocalDB database with generated fixtures, runs the migration twice, exercises the real repository and WPF constructors, and removes the database afterward. It overrides the application connection string only inside the test process and never reads the user's configured database.
