# Yakumi implementation validation

Word-mapping update, 2026-10-17: [Yakumi-WordMapping.md](Yakumi-WordMapping.md) documents expanded abbreviations, selected typos, Taglish, source phrases, organization roles, ownership, grouping, dates and follow-ups. Ambiguous completion and urgency wording offers explicit choices. Validation passes 2,440 offline assertions, 275 SELECT-only SQL variants, 53 local schema bindings, Debug/Release builds and isolated host checks. The 66 synthetic WPF previews rendered; representative mapped-count and clarification cards were visually reviewed at 100%, 125% and 150%. Three generic live AI interpretation questions passed; the guide used authored fallback. No business database was opened. Artifacts: `_build_verify/yakumi-word-mapping/`.

Response-speed update, 2026-10-17: [Yakumi-Performance.md](Yakumi-Performance.md) documents turn/stage timings, bounded session-scoped reference caches, indexed stock vocabulary, additional local count phrases, incremental general AI text and shorter simple-response budgets. Current validation passes 1,862 offline assertions, 226 SELECT-only SQL variants, 53 local schema bindings, Debug/Release builds and isolated host checks. The 57 existing card previews and 9 new streaming previews rendered; representative streaming states were visually reviewed at all three scales. A generic live AI stream passed with first text at 4,459 ms and completion at 4,472 ms; this is a point-in-time compatibility measurement, not a before/after speed benchmark. No business database was opened for this update. Artifacts: `_build_verify/yakumi-speed/`.

Friendly conversation update, 2026-10-17: [Yakumi-Friendly.md](Yakumi-Friendly.md) documents natural answers, local greetings and thanks that retain lookup context, focused organization clarifications, and supported follow-up buttons. Show tickets, Only open and By department retain the original request filters; dated zero results offer an explicit Check all dates action. Precise dates remain in Details and Copy. Validation passed 1,810 offline assertions, 226 SELECT-only SQL variants, 53 local schema bindings, Debug/Release builds, isolated host lifecycle checks and 57 synthetic WPF previews. Representative new cards were visually inspected at 100%, 125% and 150%. This update used no live business database or AI calls. Artifacts: `_build_verify/yakumi-friendly/`.

Organization update, 2026-10-16: organization mentions resolve against live Company/Branch/Department names and acronyms, binding the selected record's linked IDs; a complete stored name takes precedence over another organization's acronym, while shared or unknown acronyms ask one focused clarification instead of guessing. The current suite passes 1,781 offline assertions and 226 SELECT-only SQL variants with a Debug build. Live read-only validation matched all 5 YMC question variants against independent ticket ComId counts with no AI calls; the earlier failures in this workstream ran against a staged binary that predated the final edits, and rebuilding plus rerunning passes twice consecutively.

Follow-up update, 2026-10-16: [Yakumi-FollowUps.md](Yakumi-FollowUps.md) documents result-number references, same-product refinements and deterministic pre-search descriptions. The current suite passes 1,698 offline assertions and 219 SELECT-only SQL variants, with Debug/Release builds and 45 WPF previews. Three generic live interpretation examples passed; the guide used authored fallback.

Catalog matching update, 2026-10-16: [Yakumi-ProductMatching.md](Yakumi-ProductMatching.md) documents equivalent model formats and confirmed typo suggestions for stock lookups. That update passed 1,606 offline assertions and 215 SELECT-only SQL variants, with Debug/Release builds and 42 synthetic WPF previews; it used no live database or AI requests.

Clarification update, 2026-10-16: [Yakumi-Clarification.md](Yakumi-Clarification.md) documents tested AI examples and clickable clarification choices. That update passed 1,525 offline assertions and 209 SELECT-only SQL variants; all three generic live interpretation examples passed.

Understanding update, 2026-10-16: [Yakumi-Understanding.md](Yakumi-Understanding.md) documents shared question normalization, bounded AI planning, contextual follow-ups and deterministic English wording. That update passed 1,466 offline assertions and 209 SELECT-only SQL variants.

Interface update, 2026-10-16: [Yakumi-Interface.md](Yakumi-Interface.md) records the typed card presentation, controls, 839 passing offline assertions, Debug/Release builds, shared-host checks and 30 synthetic previews at three scales. The validation below documents the preceding data and connection work.

Validated locally on 2026-10-15. Desktop application only. No Git/GitHub operations or database modifications were performed. Authorized SELECT-only checks used the desktop's configured `Yakult_Inventory_System_DEV` database. The production desktop was not launched; its background services were not started.

| Check | Result | What this establishes |
| --- | --- | --- |
| Debug/x64 .NET Framework desktop build | Passed, exit 0 | All new source files and embedded guides compile with the desktop project |
| Release/AnyCPU build (project targets x64) | Passed, exit 0 | Updated application files are present in the installer's `Yakult.Inventory.App/bin/Release` source folder; installer packaging itself was not changed or certified |
| Original AI regressions | 124 passed | Existing identity, search, snapshot and transport safeguards remain covered |
| Expanded offline acceptance suite | 655 passed | Typed planning, stock vocabulary and exact categories, filters, personal/permission scope, comparisons, dates, paging, metadata totals, model boundaries, timeouts, connection validation and UI cancellation using synthetic fixtures |
| Generated SQL variants | 209 passed | ScriptDom parses SELECT-only statements; SELECT INTO is forbidden |
| Local source-schema binding lint | 53 capability sources passed | Referenced table/view fields and declared result fields match checked local definitions and migrations; not a deployed-schema check |
| Shared host lifecycle | Passed | Dummy forms and hidden handles survive owner closure, module switch and normal chat close; conversation reset and final disposal complete |
| Original WPF card preview | Restored, rendered and visually inspected | The original 350-by-540 layout, red header, avatar, bubbles, chips, settings and input are retained; the preview's records are synthetic |
| Embedded authored guide references | 33 articles; all source paths exist | Reviewed references are available locally; runtime uses embedded prose rather than ingesting source/config files |
| Project integrity | Valid XML; zero duplicate includes; zero ghost Pages entries; all assistant files registered | Required explicit compile/resource entries are present |
| C# / XAML pair audit | Existing ViewSetDetailPage helper pair retained | Documented helper-file exception remains intact |
| New assistant diagnostics | No compiler warnings in the new assistant/test files | Existing unrelated project warnings remain |
| PowerShell runners | Six scripts parse | Offline/live checks, source lint, coverage, deployed-schema, inspection and AI connection tooling syntax is valid |
| Live LAN connection validation | Passed for `http://192.168.27.80:11434` | Ollama 0.40.4 responds and qwen2.5:3b is among four installed models; GET metadata only, no chat or database data |
| Connection persistence | Passed in fresh isolated settings profile | URL/model persist together across process restarts; invalid input preserves saved pair; executable config hash remains unchanged |
| Connection settings UI | Rendered and visually inspected | Existing gear view retains the original card; field edits invalidate stale tests, programmatic updates do not cancel their own result, controls recover after cancellation |
| Live Ollama structured source classification | Passed after prompt refinement | The configured model accepted the structured request and selected the expected source in one smoke example |
| Live Ollama guide response | Authored-guide fallback used | A response that failed guide validation was replaced with verified local prose |
| Connected DEV schema inspection | 166 tables/views, 1,953 columns, 291 FK column mappings | Actual schema and stored item vocabulary were read without database changes |
| Deployed DEV query metadata | 209 generated SELECT variants passed | Queries compile against actual connected objects/columns, including the corrected set employee lookup |
| Live DEV capability queries | 53 count/aggregate queries completed | Reviewed sources execute under the isolated operator inspection context |
| Live DEV stock answers | 13 questions matched independent reference SQL | Actual brand/category routing and unit totals agree for phones and other item categories; no model calls |
| Real-user permission parity / production database | Not run | Synthetic operator checks do not certify a real user's session or a different deployment |

There are 779 offline assertions in total. Model smoke examples, DEV count checks, local LAN reachability and UI previews do not establish production-wide correctness or connectivity from a different desktop. The preview's count is deliberately synthetic and is not a count from the user's database. The connection check verifies installed-model metadata rather than running inference.

Build output: `_build_verify/ai-expanded/app/Yakult.Inventory.App.exe`.

Build log: `_build_verify/ai-expanded-build.log`.

Latest connection feature build logs: `_build_verify/ai-connection-build.log` and `_build_verify/ai-connection-release-build.log`.

Restored UI artifact: [yakumi-restored-preview.png](../_build_verify/ai-expanded/yakumi-restored-preview.png).

Connection settings artifact: [yakumi-connection-settings.png](../_build_verify/ai-expanded/yakumi-connection-settings.png). Its saved status is synthetic; the separate live connection check established server/model metadata availability.

Scope and definitions: [Desktop-AI-Review.md](Desktop-AI-Review.md).

Source/page/guide coverage: [Yakumi-Coverage.md](Yakumi-Coverage.md).

Live read-only results: [live-readonly-validation.txt](../_build_verify/ai-expanded/live-readonly-validation.txt). At validation time, DEV returned 367 Redmi units, 370 cellphone units, 3 Samsung cellphone units, 13 headset units, 14 switch units, 24 access-point units, 500 CPU units, 15 SSD units, 3 TV units, 4 NAS units and 5,000 boot units. Headsets, switches, CPUs and boots demonstrate why unit sums must be separate from record counts. These are point-in-time DEV results with active/nonarchived/positive-stock scope, not production totals.

The stock vocabulary reads permitted stored item names, models and categories rather than maintaining a per-product code list. Its session-scoped five-minute cache affects routing vocabulary only; quantities are queried fresh. The live check first exposed partial category matching for Switch; an exact stored-category predicate fixed the mismatch, and all reference comparisons then passed.

`tools/Test-YakumiDeployedSchema.ps1` passed against connected DEV metadata. Its fixed metadata SELECT does not execute business queries. The separately invoked live harness did execute count/aggregate SELECTs under a synthetic full operator context; authenticated-user permission parity remains unverified. Neither check changes the database or certifies a different deployment.
