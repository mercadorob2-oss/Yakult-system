# Friendly Yakumi replies

Updated 2026-10-17. Yakumi gives short, natural answers using the existing typed database results. For example, a company lookup reads “YMC has 9 IT tickets.” The number is illustrative; the application always queries current quantities. Zero results say what could not be found and offer a useful next step. Stock units, matching records and financial amounts keep their separate meanings.

Greetings and brief thanks work locally without calling the model or database. They retain the preceding lookup, so “thanks” followed by “only this month” still refines the same records. The welcome, access, unavailable-data, cancellation and organization clarification messages use everyday wording. General and guide prompts request the same tone, concise paragraphs and numbered procedures, retaining the existing guide validation, citations and prompt budgets.

Supported results offer up to three follow-up buttons, alongside Copy and existing page controls:

| Button | Behavior |
| --- | --- |
| Show tickets / requests / other supported records | Loads a fresh list with the original filters. |
| Only open | Refines tickets without an existing status filter using the existing open-ticket definition. |
| By department | Groups a supported source by department when no department filter is already selected. |
| Check all dates | Offered for dated zero-result searches. Removes the date limits only after a click, retaining other filters. |

Each action owns an independent copy of the original request, resets the page where appropriate, and goes through the existing permission, cancellation and session checks. Intervening questions cannot change its scope. Detail/history lookups do not offer broader suggestions. Failed or denied lookups never become zero counts or offer broader searches.

Human-readable dates and their meaning remain in the answer. Precise boundaries and timezone are available under Details and included in Copy. Empty-result cards avoid repeating the no-match message. No meaningful filter is silently removed, and suggestions never execute while a card is being formatted.

## Verification

The isolated build and artifacts are under `_build_verify/yakumi-friendly/`. Validation uses synthetic records, contexts and model responses, without connecting to a business database or live AI. The production desktop was not launched.

| Check | Result |
| --- | --- |
| Offline assertions | 1,810 passed, including 89 presentation and interaction checks |
| Generated SELECT-only SQL variants | 226 passed |
| Local source-schema bindings | 53 passed |
| Debug/x64 and Release/AnyCPU builds | Passed; 0 errors, 142 existing unrelated warnings |
| Isolated host lifecycle | Passed |
| WPF previews | 57 rendered; representative new cards visually inspected at all three scales |
| Project integrity | Valid project XML, no duplicate or ghost Pages includes, clean whitespace check |

```powershell
$assembly = '_build_verify/yakumi-friendly/app/Yakult.Inventory.App.exe'
.\tools\Test-ExpandedYakumi.ps1 -AssemblyPath $assembly
.\tools\Test-ExpandedYakumi.ps1 -AssemblyPath $assembly -PresentationPreviews _build_verify/yakumi-friendly/previews
```

Tests cover button clicks, preserved company links and other filters, unchanged model/condition evidence, explicit date broadening, zero results, short conversational replies followed by refinements, unavailable data, denied access and Copy. Preview images cover the welcome, ticket actions, empty dated results and organization choices at 100%, 125% and 150% scaling. These checks do not certify live model phrasing or another deployment.
