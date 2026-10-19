# Yakumi everyday word mapping

Updated 2026-10-17. Yakumi now resolves more common wording locally, including abbreviations, selected typos and Taglish. Unfamiliar wording still uses the existing bounded AI interpretation and validation. Answers remain in English.

## Examples

| You can ask | Meaning |
| --- | --- |
| `Pls count tix in co. YMC this wk` | Count tickets for the linked YMC company during this week. |
| `Ilan ang mga tix sa company YMC ngayong buwan?` | Count tickets for YMC during this month. |
| `Pakita ng mga reqs` | Show requests. |
| `How many ongoing tix?` | Count tickets with In Progress status. |
| `Show requests waiting for fulfillment` | Show unfulfilled requests. |
| `How many LX310 items on-hand?` | Sum available stock units for LX310; matching record counts remain distinct. |
| `Count tickets broken down by dept` | Group ticket counts by department. |
| `Show tickets raised by me last mo` | Show tickets created by the current user last month. |
| `Count tickets MTD` | Count tickets from the start of this month through today. |
| `Show tix #69` | Look up ticket reference 69. |

Organization names and acronyms use the existing reference directory and linked Company, Branch and Department IDs. For example, `in co YMC in dept IT at br MO` combines all three relationships when those names resolve. Shared names and acronyms still require a choice. The examples do not establish what exists in a particular deployment.

## Reviewed mappings

| Area | Added wording |
| --- | --- |
| Polite and informal openings | `pls`, `plz`, `kindly`, `can u`, `hw mny`, `show me`, `lemme see` |
| Selected Taglish | `ilan`, `ilang`, `gaano karaming`, `pakita`, `ipakita`, `patingin`, `sa company`, `ngayong araw/buwan/linggo/taon`, `nakaraang linggo/taon` |
| Record names | `tix`, `tkt`, `tkts`, `tckt`, `tckts`, `ticktes`, `tikets`; `req`, `reqs`, `requets`, `requestes` |
| Ticket wording | `helpdesk tickets`, `service desk tickets`, `IT support tickets`, `technical support tickets`, `trouble tickets` |
| Filters | `dept`, `co.`, `br.`, `prio High/Medium/Low/Critical`, `ongoing`, `wip`, `inprogress`, `in-progress` |
| Ownership | `assigned to myself`, `handled by me`, `raised/filed/logged/submitted by me` |
| Grouping | `broken down by`, `breakdown by`, `split by`, `for each`, followed by a supported grouping field |
| Dates | `current/previous month/week/year`, `this/last mo/wk/yr`, `MTD`, `WTD`, `YTD` |

These are context-sensitive aliases. Ticket status aliases require ticket context; fulfillment aliases require requests. Stock-context product names such as `TIX items` retain their name. Quoted values and explicit named searches retain meaningful words, even when those words resemble an alias. `IT support tickets` is treated as a source phrase rather than an inferred department name; use `in dept IT` to filter the department explicitly.

`MTD`, `WTD` and `YTD` end after today in the session timezone. Weeks start on Monday. `This month/week/year` retains the existing whole-period meaning. `Right now` and `at the moment` describe the current lookup without adding a creation-date filter. Conflicting periods ask for clarification. Date wording continues to identify what each source's date filter measures.

## Follow-ups and choices

After a single result, `how about LX350?` replaces the model, `filter to brand new` changes condition, `limit to this wk` changes dates, and `in co YPI` changes company while preserving compatible filters. `Same as before` retains the request. `Show more` after a count now starts at the first page of records; subsequent paging follows the existing list behavior. Missing or multiple possible prior results require clarification.

Completion words such as `done`, `fixed`, `finished` and `completed` offer Solved, Closed and Resolved (Temporary) choices for tickets. `Urgent` and `ASAP` offer High or Critical priority when the source supports priority. Choosing a button preserves the original organization, ownership and date filters. The system does not silently assign either meaning.

Unknown wording, unsupported exclusions and approval workflows still require clarification. True zero results remain zero. Query quantities are read freshly, and database totals never enter AI interpretation. Permission, cancellation, stale-session, parameterization and read-only safeguards remain in force. Two additional synthetic AI examples cover company-filtered incidents and repair priority/date wording within the existing prompt budget.

## Verification

| Check | Result |
| --- | --- |
| Offline assertions | 2,440 passed, including 572 everyday-wording checks and 6 additional reviewed-example assertions |
| Equivalent wording | 49 question pairs, each also checked with uppercase and punctuation; typed filters, parameterized SQL and fixture totals agree |
| SELECT-only SQL variants | 275 passed |
| Local source-schema bindings | 53 passed; no SQL executed |
| Builds | Debug/x64 and Release/AnyCPU passed; 0 errors, 142 existing unrelated warnings |
| UI | 66 synthetic WPF previews rendered; mapped company counts, priority choices and status choices visually reviewed at representative 100%, 125% and 150% scales |
| Lifecycle | Isolated host lifecycle checks passed |
| Project integrity | Valid XML, explicit compile entries present, zero duplicate includes or ghost Pages entries; runner syntax and whitespace checks passed |
| Configured AI | All three generic interpretation smoke questions passed typed semantic validation; the guide used the existing authored fallback |

Tests cover literal names, linked organization IDs, retained follow-up filters, ambiguous meanings, true zero, AI failure, permission denial, cancellation and input limits. The stock quantity 254 is synthetic test data, not a guaranteed database count. Only generic questions and curated guides were sent to the configured AI. No business database was opened for this update.

Artifacts: `_build_verify/yakumi-word-mapping/`. Run the offline suite with:

```powershell
.\tools\Test-ExpandedYakumi.ps1 -AssemblyPath _build_verify/yakumi-word-mapping/app/Yakult.Inventory.App.exe
```
