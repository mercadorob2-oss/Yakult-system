Policy presentation checks use an isolated host with no production configuration or database registration. Policy saves are held in fixture memory only; no database is registered. The fixture authentication header exists only in this test project.

Run the fixture host:

```powershell
dotnet run --project tests/SystemsPortal.PolicyChecks -c Release
```

In another terminal, run browser checks:

```powershell
node tests/SystemsPortal.PolicyChecks/browser-checks.cjs
node tests/SystemsPortal.PolicyChecks/save-review-checks.cjs
```

The scripts use the existing Playwright installation at `%TEMP%/yakult-learning-qa/node_modules/playwright` and Chromium at `%LOCALAPPDATA%/ms-playwright/chromium-1228/chrome-win64/chrome.exe`. Reports and screenshots go to `artifacts/it-policy-upgrade`.

Checks cover approved instructions, source order, all nine image URLs and dimensions, the Profile Account editor round-trip, shared public/preview output, safe Markdown, invalid-edit preview retention, section navigation, search without hiding content, paired screenshots, image keyboard controls and focus restoration, anti-forgery and authorization, and overflow at 320, 390, 768, and 1440 pixels.

`live-checks.cjs` verifies the existing production URL using the saved pre-deployment HTML and local asset hashes. It performs GET requests and an unauthenticated preview request; it never saves policy content. Authenticated preview updates are tested only against the isolated host.
