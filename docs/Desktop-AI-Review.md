# Yakumi desktop read-only assistant

Implemented locally in `Yakult.Inventory.App`. No Git/GitHub operations, migrations, production application startup, or database modifications were performed. With the user's authorization, SELECT-only inspection and validation used the desktop's configured `Yakult_Inventory_System_DEV` database. The optional Ollama smoke test sent only a generic question and curated guides to the configured Ollama service. Web, API and mobile projects were not modified.

## Implemented behavior

The original 350-by-540 Yakumi card has been restored, including its red header, avatar, chat bubbles, topic chips, settings, minimize and input controls. One shared WPF view embeds at the bottom right of supported WPF module grids; WinForms modules use a reusable borderless host with the same card. Workspace, Inventory, Requests, Consumables, IT Call Monitoring, Borrowing, Repairs, Reports and Admin share the conversation. Ask Yakumi, the workspace header and Ctrl+Shift+Y open it. Module closure detaches the view and owner; final disposal occurs at application shutdown. Logout cancels work and clears chat. Changes to session, cached permissions or database fingerprint discard stale answers.

Known page types and allowlisted positive numeric saved-record keys provide context. Unsaved editor fields, arbitrary screen text and attachment contents are excluded. Modal editors do not inherit a selected record from the underlying page. Unknown screens may require an explicit reference. The [coverage matrix](Yakumi-Coverage.md) lists implemented sources, permissions, filters, groupings, sums, guide references and guidance-only screens.

There are 53 reviewed SELECT sources: stock and movements; master data; saved sets, contents and ownership; requests and authorizations; consumables and cartridges; refills and exchanges; tickets and history; borrows and aging; repairs, history and parts; invoices, items, staging and license review; renewals, receipts and archives; safe account, role and permission metadata; acronyms, holidays and email-log metadata. Each source has a specific record grain. It does not necessarily reproduce every tab or derived metric on a related page.

`AssistantPlanner` creates typed requests in C#. `AssistantCatalog` owns reviewed SELECT text, fields and access guards. `AssistantQueryEngine` validates the source, operation, fields, grouping, dates, references and pagination before opening a connection. Portal, page, explicit privilege, category and personal/requester restrictions apply before aggregation. User input is parameterized. The model cannot generate SQL, change filters, grant access or mutate records. Command timeout is 15 seconds; cancellation is supported.

Counts use `COUNT_BIG(*)` over all scoped matches. Decimal sums cover all matches independently of the eight displayed rows. Aggregate metadata survives an empty page. Zero matches, empty pages, denied access, cancellation and query failure are separate outcomes. Financial header values come from the stored invoice view; line totals do not recompute header override rules. Cartridge stock follows existing issuable-condition and IT-custody exclusions. Movement quantities are labelled movement units, rather than available stock.

Stock vocabulary comes from permitted active, nonarchived item names, models and categories in the connected database. New brands and categories do not need their own source-code entry. The bounded vocabulary cache refreshes after five minutes or a session/permission/database change; each answer queries current quantities afresh. Generic plural handling, stored category abbreviations and a small set of language synonyms map questions onto that vocabulary. A matched category uses an exact parameterized filter, avoiding similarly named categories; remaining brand/model terms use AND search. Available stock requires positive StockOnHand and excludes inactive/archived rows. The first line reports units, with matching catalog records labelled separately. An unavailable vocabulary refresh falls back to supported deterministic lookups; unknown terms may require clarification. Vocabulary is routing data, not an unrestricted SQL or semantic-search capability.

Live inspection covered 166 table/view definitions, 1,953 columns and 291 foreign-key column mappings. It exposed a missing `Set.CurrentEmployeeId` assumption. Set and set-item employee filtering now follows the existing Sets grid's first linked request; current branch and department follow the set. This does not establish the latest individual custodian. Ownership History remains the source for recorded transfers.

Employee and policy searches AND every meaningful token. Self-name searches resolve the current employee session first. Personal lookups use stable account/employee IDs; department accounts are labelled separately and do not become fuzzy employee matches. Policy search checks the full published body but returns bounded excerpts, without model rewriting.

The planner supports common typos, basic English/Filipino wording, named references, filters, permitted groupings, calendar/relative/ISO date windows, paging, follow-ups, two lookups and two-scope comparisons. An abbreviated second comparison retains the first scope's filters. UTC sources use converted local-day bounds, displayed timestamps and month groupings. Existing inconsistently stored timestamps remain a source-data limitation.

## Exact ticket-count definition

`How may solved tickets are there?` normalizes the typo and counts current accessible IT ticket rows where `Status = 'Solved'`. Closed and Resolved (Temporary) are distinct. Bare "resolved" asks for clarification. Open/unsolved excludes Solved, Closed, Resolved (Temporary) and Cancelled; the applied filter is labelled. "My tickets" includes created or assigned rows; "assigned to me" filters assignment only.

"Solved tickets today" means tickets currently Solved with a Solved transition during the local day. An EXISTS predicate avoids double-counting repeated transitions. It does not count every historical resolution event. Monthly Solved summaries use the recorded latest SolvedAt timestamp and label that date field. Ask "tickets created this month" for creation-date scope.

## Role of Ollama and Qwen 2.5 3B

Supported facts, identities, totals, arithmetic and comparisons bypass the model. Business rows and session identity are never sent to Ollama. General model history is separate from query context and is cleared for application facts/guides.

The embedded JSON contains 33 authored workflow articles. Other capabilities have explicitly navigation-only articles. Retrieval ranks permitted local articles without crawling source/configuration files. Qwen can explain selected guides using structured JSON with allowlisted citations. Invalid, unavailable, unsupported-number/link or claimed-write replies fall back to authored text. Citation validation cannot prove perfect semantic accuracy; consult the cited guide when an explanation is unclear.

Unrecognized factual wording may use one model classification for a source suggestion. It asks the user to name that source explicitly before running a lookup. Interpretations contradicting a recognized C# source are rejected. General IT responses remain model-generated and are not verified application facts.

The live smoke test initially caught a wrong source classification. After strengthening the prompt, classification passed; the guide response used the authored-guide fallback. This is limited transport/behavior evidence, not broad model-quality certification. No model, package or Ollama configuration was replaced.

## Examples

| Question | Result definition |
| --- | --- |
| Who am I? / Do you know Rob Meca? | Labelled session identity when self-name tokens match |
| How many Redmi phones / phones / cellphones do I have in stock? | Stored Cellphone category, optional Redmi term, SQL sum of positive active/nonarchived stock |
| How many headsets / switches / CPUs / SSDs / TVs / NAS are in stock? | Category discovered from the connected catalog, exact category filter and current SQL unit sum |
| How many ASUS laptops are in stock? | AND item search, SQL record count and stock-unit sum |
| How many HP CF276X toners are in stock? | Consumable model/category count and unit sum |
| Refilled cartridge stock | Issuable refilled total, with condition totals shown separately |
| What is the borrowing policy? | Published policy excerpts and full matching count |
| How may solved tickets are there? | Exact current Solved count |
| High priority tickets assigned to John Smith this month | AND employee filter, priority and stated date scope |
| Ticket history TCK-69 | Saved ticket's history rows |
| SET-12 contents / SET-12 history | Set items / ownership-transfer history |
| My pending requests | Own outstanding request lines unless broader access applies |
| My returned borrows / loan aging | Returned records / open-loan whole elapsed days |
| Repair parts RPR-12 | Parts belonging to one repair ticket |
| Compare solved tickets this month with last month | Both scopes retain Solved; difference calculated in C# |
| Invoices by company | Stored financial totals and permitted grouping |
| Renewals expiring within 30 days | Renewal-record expiry window, not a unique contract count |
| Next page / show them / this month only | Re-query the previous single lookup with retained scope |
| Help with this page | Permitted guidance for the registered active screen |

## Limits and runtime validation

- Natural-language and data coverage are bounded. Unsupported fields, joins, workflows, grammar and ambiguous scopes require clarification. Some screens have guidance only. Maintain curated guides when workflows change.
- No write actions, model-generated SQL, credentials, signatures, QR tokens, attachment bytes, arbitrary exports, migration execution, email sending or device commands are exposed. SMTP/configuration screens have guidance, not secret-value retrieval. System and ticket email logs are separate; recipients/error payloads are excluded.
- Borrow records have no due date, so overdue status is unavailable. Lowest-stock sorting does not invent a threshold. Stock snapshots are current values, not reconstructed balances for an earlier date. Holidays show stored date/recurrence rules, not an expanded annual calendar. Permission queries show explicit stored overrides, not another user's complete effective authorization.
- Warranty lookup uses item warranty dates. Renewal records, invoice periods and unique contracts have different grains. Related-source screen mappings do not guarantee parity with specialized reports.
- All 209 generated SELECT variants compiled against the configured DEV schema. All 53 capability count/aggregate queries executed, and 13 stock questions matched independent reference SQL. These operator-scope checks do not establish production database parity or effective permissions in a real authenticated desktop session. No production-wide readiness claim is made.

## Verification tools

The latest counts and artifacts are in [Yakumi-Validation.md](Yakumi-Validation.md). Offline checks use synthetic context, rows and model responses. SQL checks parse SELECT-only statements and bind declared result/source fields against local SQL definitions without connecting to SQL Server. The UI preview's displayed numbers are synthetic. Host checks create hidden test handles and dummy forms, without real module constructors.

```powershell
& 'C:\Program Files\Microsoft Visual Studio\18\Community\MSBuild\Current\Bin\MSBuild.exe' `
    Yakult.Inventory.App\Yakult.Inventory.App.csproj /t:Build `
    /p:Configuration=Debug /p:Platform=x64 `
    /p:OutDir=..\_build_verify\ai-expanded\app\ `
    /p:IntermediateOutputPath=obj\AiExpanded\ /v:quiet /nologo

.\tools\Test-ExpandedYakumi.ps1 -ScriptDomPath 'C:\path\to\Microsoft.SqlServer.TransactSql.ScriptDom.dll'
.\tools\Test-YakumiLocalSchema.ps1 -ScriptDomPath 'C:\path\to\Microsoft.SqlServer.TransactSql.ScriptDom.dll'
.\tools\Test-ExpandedYakumi.ps1 -HostLifecycle
.\tools\Test-ExpandedYakumi.ps1 -PreviewPath _build_verify\ai-expanded\yakumi-restored-preview.png
.\tools\Export-YakumiCoverage.ps1
```

Optional Ollama-only check: `tools/Test-ExpandedYakumi.ps1 -OllamaSmoke`. It reads Ollama URL/model settings from the built executable's config into its own process without saving configuration. It sends no business data.

Deployed-schema check: `tools/Test-YakumiDeployedSchema.ps1 -ConnectionStringEnvironmentVariable YAKUMI_READONLY_CONNECTION`. Supply a SELECT-only login via that environment variable. It runs only a fixed metadata SELECT against sys.dm_exec_describe_first_result_set, with generated SQL and exact parameter declarations as values. It does not execute business SELECTs, read their rows, start application services or modify the database. It passed against the configured DEV database. Metadata compilation does not verify counts or effective per-user permissions.

Explicit live count validation: `tools/Test-ExpandedYakumi.ps1 -LiveConnectionEnvironmentVariable YAKUMI_READONLY_CONNECTION`. This isolated harness executes capability count/aggregate SELECTs under a synthetic full operator context, then compares 13 natural-language stock answers against independently written parameterized SQL. It does not log in as a desktop user or verify that user's effective permissions. It never starts Program.Main or writes database/configuration settings. The live results are recorded in `_build_verify/ai-expanded/live-readonly-validation.txt`.

Runners use isolated Windows PowerShell processes for .NET Framework/STA compatibility and process-only execution policy. Machine/user policy is unchanged. Existing unrelated tests that connect to or mutate live data are not invoked.
