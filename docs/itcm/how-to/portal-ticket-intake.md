# Employee portal ticket intake

The submission form collects Issue Type, Classification, device/application name, and Concern. Computer and Laptop require a computer name. Other classifications accept an optional device/application name. Classification choices are maintained in `HelpItSubmitViewModel.Classifications` and validated on the server as well as in the browser.

New portal tickets store a readable Issue Type, Classification, Computer/device name, and Concern block in `CallTicket.Issue`, so existing desktop/mobile clients see all submitted details in the description. Issue Type also uses `CallTicket.IssueType`. The complete description has a 2,000-character limit; the concern counter adjusts for the other fields. The new nullable columns are `Classification` (100 characters) and `DeviceName` (150 characters). Existing tickets and clients remain compatible. Desktop Incoming Tickets now loads Classification and DeviceName directly from the ticket table and composes the description for older tickets that stored only the concern. The desktop inspector shows the full description without the previous two-line limit, using the original single-panel layout. This does not rewrite existing records. Updated desktop/mobile detail screens read the fields with guards for databases where the columns are not installed.

## Deployment prerequisites

1. Have the DBA review and manually apply `DATABASES/Yakult-DB-Production/dbo/Scripts/Migration_CallTicket_AddClassificationAndDeviceName.sql` to the intended database. The script is idempotent and does not change stored procedures or views. The existing ContactEmail migration must also be installed. No migration runs automatically.
2. Configure the portal's dedicated support recipient and SMTP transport in server-local `appsettings.Local.json` or environment variables. This notification does not reuse ITCM department recipient rules or machine-bound encrypted SMTP passwords. Confirm the actual IT Support address; no address is guessed and there is no ITD fallback.
3. Publish the updated portal. Update desktop and mobile/API when the separate Classification and device/application fields should appear in their detail screens.

Example configuration (placeholders only):

```json
{
  "HelpIt": {
    "Email": {
      "SupportRecipient": "REPLACE_WITH_CONFIRMED_IT_SUPPORT_ADDRESS",
      "SmtpServer": "REPLACE_WITH_SMTP_HOST",
      "SmtpPort": 587,
      "UseSsl": true,
      "FromEmail": "REPLACE_WITH_AUTHORIZED_SENDER",
      "FromName": "Yakult Employee Portal"
    }
  }
}
```

For authenticated SMTP, supply `HelpIt__Email__Username` and `HelpIt__Email__Password` using the server's environment/secret configuration. Leave Username unset for a server-authorized anonymous relay. Never commit credentials.

## Save and notification behavior

- The existing creation procedure runs inside an outer SQL transaction. Classification, name, contact email, and Portal source are saved before that outer transaction commits. A failure rolls back the ticket rather than leaving a partially created request.
- After saving, the portal attempts one notification to SupportRecipient. The organization's contact is the Reply-To address, not the notification recipient. The message includes the ticket code, structured request details, requester/location, and an absolute tracking URL. Configure IIS/proxy request scheme and host correctly so the generated URL matches the portal's public address.
- SMTP has a 15-second bound. A missing configuration or delivery error is logged by the portal and displayed as an email warning with the saved ticket code. It does not cause a creation error or instruct the employee to resubmit. No automatic email retry is implemented.
- The review button and confirmation action guard against duplicate clicks. Reopening a successfully submitted form shows its existing confirmation. A page reload/new browser session is not an idempotency guarantee.
- Without the migration, submission returns an explanatory 503 before creating a ticket. Existing detail pages continue to work.

## Local checks without SQL or real email

Run `dotnet run --project tests/SystemsPortal.HelpItChecks --no-launch-profile`, then `node tests/SystemsPortal.HelpItChecks/browser-checks.cjs` in another terminal. Set PLAYWRIGHT_MODULE and PLAYWRIGHT_BROWSER when Playwright or the browser are installed outside Node's default paths. The fixture listens only on 127.0.0.1:5194, clears production configuration, and substitutes an in-memory repository and notification service.

The checks cover desktop and mobile layouts, classification changes, review-only behavior, explicit/repeat confirmation, field limits, schema readiness, saved-ticket email failure, detail rendering, and duplicate click protection. Screenshots are written to `artifacts/help-it`. SQL transaction behavior and actual SMTP delivery require a separately authorized environment smoke test after deployment prerequisites are met.
