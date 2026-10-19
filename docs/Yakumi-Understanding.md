# Yakumi question interpretation and English answers

Implemented and checked on 2026-10-16. Desktop assistant only; database contents, widget layout, avatar, dragging, persistence and model settings are unchanged.

`AssistantQuestionLanguage` supplies shared phrase normalization for planning and stock vocabulary. “How many LX310 are still in stock” and “How many LX310 are in stock” produce the same `lx310` search and SQL parameters. The acceptance fixture returns **254 stock units across two matching records**. This is synthetic data, not a current database count.

Quoted names retain their words, numbers and spelling through parameterized search, including names containing “Still,” “Open,” dates, ownership wording or reference numbers. Clear brand/product phrases and model identifiers work locally. Unexplained modifiers and ambiguous extracted names require interpretation or one clarification. Exclusions without a supported predicate are clarified. A zero result keeps its original filters and is never retried with a wider scope.

Follow-ups replace a model search, retain compatible ownership/status filters when changing a period, and reset incompatible filters when changing sources. “All dates” explicitly clears the period. Multiple previous results require the user to identify which result to refine. Quantities are queried again for every question and follow-up; only routing vocabulary keeps its existing cache.

The configured AI can propose at most two typed requests: an allowed source, supported operation, grounded filters and an explicit inheritance flag. It receives the original question and a bounded projection of previous request scope. Rows, totals, session credentials and general-conversation answers are excluded. C# checks literal evidence, source agreement, supported fields, date parsing in the session timezone, meaningful-filter coverage and permissions. The service checks cancellation and stale sessions before executing the resulting requests. Unavailable AI or a rejected interpretation produces a clarification; clear local lookups continue to work.

Approval status and outstanding fulfillment quantities remain distinct. In particular, wording about awaiting approval cannot be silently converted into a count of unfulfilled requests.

`AssistantEnglish` in `AssistantPresentation.cs` provides explicit nouns for every supported record type, direct English answers, singular/plural agreement, source-specific date descriptions, identified details and comparison periods. Cards, Copy output and legacy text use the same deterministic facts. Financial amounts, matching records, stock units and line quantities remain separate. General and guide prompts request concise English and numbered procedures. Citation validation permits sequential step markers while still rejecting unsupported numeric facts.

Validation artifacts are under `_build_verify/yakumi-understanding/`:

- Debug/x64 and Release/AnyCPU builds (the project targets x64).
- 1,466 offline assertions: 124 original assistant, 655 expanded, 60 presentation/interaction and 627 understanding/English checks; results in `offline-tests.txt`.
- 209 generated SQL variants parsed with ScriptDom and checked for SELECT-only statements.
- Synthetic checks for equivalent queries, brands/models, quotes, typos, follow-ups, source changes, negation, true zero, connection failures, permissions, cancellation, stale sessions, unsupported model fields, wording, quantities, amounts, details, summaries and Copy output.
- 30 WPF previews at 100%, 125% and 150%; stock, details and comparison layouts visually reviewed.
- Generic live examples against the configured Ollama server, recorded in `live-ai.txt`: “support incidents” passed typed semantic validation; the model's status interpretations for awaiting fulfillment and the quoted “Still Open” name were safely rejected. The quoted-name question resolves locally in normal service use. The guide example used the authored-prose fallback. The configured model can still require clarification for unfamiliar wording; these examples do not certify every model response.
- Valid project XML, zero duplicate includes and zero ghost Pages entries. New source/test files are explicitly registered in the non-SDK project.

The offline runner is `tools/Test-ExpandedYakumi.ps1`. Use its `-AssemblyPath`, `-ScriptDomPath`, `-OllamaSmoke` and `-PresentationPreviews` options to repeat the relevant checks. Live examples send generic questions and curated guides only and do not open the database. No production data validation, deployment or database writes were performed for this change.
