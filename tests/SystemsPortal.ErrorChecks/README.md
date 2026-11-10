Run the isolated host with `dotnet run --project tests/SystemsPortal.ErrorChecks`.
It uses the real error action, Razor view, static assets, and ASP.NET exception/status middleware with no database connection or production settings.

Run the browser checks with `node tests/SystemsPortal.ErrorChecks/browser-checks.cjs`.
Set `PLAYWRIGHT_MODULE` to the installed Playwright module path if it is not available through Node's normal module resolution.
Set `PLAYWRIGHT_BROWSER` to a Chromium executable path to use an existing browser installation.
Screenshots are written to `artifacts/error-page/`.
