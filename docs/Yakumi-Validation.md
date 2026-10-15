# Yakumi implementation validation

Validated locally on 2026-10-15. Desktop application only. No Git/GitHub operations or database modifications were performed. Authorized SELECT-only checks used the desktop's configured `Yakult_Inventory_System_DEV` database. The production desktop was not launched; its background services were not started.

| Check | Result | What this establishes |
| --- | --- | --- |
| Debug/x64 .NET Framework desktop build | Passed, exit 0 | All new source files and embedded guides compile with the desktop project |
| Original AI regressions | 124 passed | Existing identity, search, snapshot and transport safeguards remain covered |
| Expanded offline acceptance suite | 611 passed | Typed planning, stock vocabulary and exact categories, filters, personal/permission scope, comparisons, dates, paging, metadata totals, model boundaries, timeouts and cancellation using synthetic fixtures |
| Generated SQL variants | 209 passed | ScriptDom parses SELECT-only statements; SELECT INTO is forbidden |
| Local source-schema binding lint | 53 capability sources passed | Referenced table/view fields and declared result fields match checked local definitions and migrations; not a deployed-schema check |
| Shared host lifecycle | Passed | Dummy forms and hidden handles survive owner closure, module switch and normal chat close; conversation reset and final disposal complete |
| Original WPF card preview | Restored, rendered and visually inspected | The original 350-by-540 layout, red header, avatar, bubbles, chips, settings and input are retained; the preview's records are synthetic |
| Embedded authored guide references | 33 articles; all source paths exist | Reviewed references are available locally; runtime uses embedded prose rather than ingesting source/config files |
| Project integrity | Valid XML; zero duplicate includes; zero ghost Pages entries; all assistant files registered | Required explicit compile/resource entries are present |
| C# / XAML pair audit | Existing ViewSetDetailPage helper pair retained | Documented helper-file exception remains intact |
| New assistant diagnostics | No compiler warnings in the new assistant/test files | Existing unrelated project warnings remain |
| PowerShell runners | Five scripts parse | Offline/live checks, source lint, coverage, deployed-schema and inspection tooling syntax is valid |
| Live Ollama structured source classification | Passed after prompt refinement | The configured model accepted the structured request and selected the expected source in one smoke example |
| Live Ollama guide response | Authored-guide fallback used | A response that failed guide validation was replaced with verified local prose |
| Connected DEV schema inspection | 166 tables/views, 1,953 columns, 291 FK column mappings | Actual schema and stored item vocabulary were read without database changes |
| Deployed DEV query metadata | 209 generated SELECT variants passed | Queries compile against actual connected objects/columns, including the corrected set employee lookup |
| Live DEV capability queries | 53 count/aggregate queries completed | Reviewed sources execute under the isolated operator inspection context |
| Live DEV stock answers | 13 questions matched independent reference SQL | Actual brand/category routing and unit totals agree for phones and other item categories; no model calls |
| Real-user permission parity / production database | Not run | Synthetic operator checks do not certify a real user's session or a different deployment |

There are 735 offline assertions in total. Model smoke examples, DEV count checks and the UI preview do not establish production-wide correctness. The preview's count is deliberately synthetic and is not a count from the user's database.

Build output: `_build_verify/ai-expanded/app/Yakult.Inventory.App.exe`.

Build log: `_build_verify/ai-expanded-build.log`.

Restored UI artifact: [yakumi-restored-preview.png](../_build_verify/ai-expanded/yakumi-restored-preview.png).

Scope and definitions: [Desktop-AI-Review.md](Desktop-AI-Review.md).

Source/page/guide coverage: [Yakumi-Coverage.md](Yakumi-Coverage.md).

Live read-only results: [live-readonly-validation.txt](../_build_verify/ai-expanded/live-readonly-validation.txt). At validation time, DEV returned 367 Redmi units, 370 cellphone units, 3 Samsung cellphone units, 13 headset units, 14 switch units, 24 access-point units, 500 CPU units, 15 SSD units, 3 TV units, 4 NAS units and 5,000 boot units. Headsets, switches, CPUs and boots demonstrate why unit sums must be separate from record counts. These are point-in-time DEV results with active/nonarchived/positive-stock scope, not production totals.

The stock vocabulary reads permitted stored item names, models and categories rather than maintaining a per-product code list. Its session-scoped five-minute cache affects routing vocabulary only; quantities are queried fresh. The live check first exposed partial category matching for Switch; an exact stored-category predicate fixed the mismatch, and all reference comparisons then passed.

`tools/Test-YakumiDeployedSchema.ps1` passed against connected DEV metadata. Its fixed metadata SELECT does not execute business queries. The separately invoked live harness did execute count/aggregate SELECTs under a synthetic full operator context; authenticated-user permission parity remains unverified. Neither check changes the database or certifies a different deployment.
