# Yakumi follow-ups and search descriptions

Yakumi resolves references to the most recent answer’s lookup scopes. “The second result,” “2nd result,” “result 2” and “show me the first result” select the corresponding request, then query it freshly. A refinement such as “the second result, only this month” retains its compatible status, ownership and product filters. It selects a lookup rather than a position in its database rows. An unavailable number or “the second item” requires clarification or a saved record reference.

“Same item,” “same model,” “same product” and “same one” retain the previous product search and model evidence. “Same item only brand new” changes the condition while preserving the product, category and period. “Brand-new” and “brandnew” are accepted outside quoted names. “Same records” and “same filters” retain a complete previous lookup scope. The existing “same but” grammar no longer introduces the words “same” and “but” into text filters.

When a previous answer contains two lookups, ambiguous follow-ups still offer choices. A “same item” choice includes compatible product results only. Typing “the second result” resolves a pending result-selection question; adding “only high priority” also retains the pending period and ownership filters. Unknown products, missing product identity, unsupported exclusions and conflicting sources are clarified. Source changes clear incompatible inherited filters. No database rows or answer totals are retained as reference context.

The configured AI receives the original question and bounded previous request context. The prompt explains displayed result ordering, and C# resolves that reference before validating inheritance. A model cannot substitute another result’s source. Prompt budgets and SQL/filter validation remain in place.

Before a validated lookup reaches the data reader, the chat displays a deterministic description such as **Checking available “LX310” stock (status: Active).** Important condition, ownership, grouping, archive and date context appears when applicable. Date wording identifies what the date measures. For two lookups, the status identifies result 1 or result 2. Descriptions contain no retrieved totals or model-written facts. The final answer replaces the status.

Permission checks precede the search status. Cancellation and session/generation checks run before and after the progress callback, preventing an expired turn from starting a query. UI callbacks are scoped to the current chat revision and session. Stop replaces progress with a cancellation reply; a new chat prevents an old status or result from reappearing. Public service signatures remain compatible, and card actions use the same path.

Validation artifacts: `_build_verify/yakumi-followups/`.

- 1,698 offline assertions, including 92 new follow-up, description and UI lifecycle checks.
- 219 query variants passed SQL syntax and SELECT-only validation.
- Debug/x64 and Release/AnyCPU builds (the app targets x64); no diagnostics in the touched assistant files.
- 45 WPF previews across 100%, 125% and 150%; search-progress layouts visually inspected at 100% and 150%.
- Three generic live AI interpretation examples passed with the updated prompt. The guide response used verified authored fallback text. These examples sent no database content and opened no database connection.
- Valid project XML, explicit registration of all three new C# files, and no duplicate includes or ghost Pages entries.

The data and quantities in offline checks are synthetic. No database changes, production desktop startup, deployment, avatar changes or conversation-persistence changes were performed.
