# Yakumi: AI examples and clarification buttons

Implemented on 2026-10-16, following the question-understanding changes in [Yakumi-Understanding.md](Yakumi-Understanding.md).

The interpreter now selects up to two relevant synthetic examples from a reviewed set covering support incidents, outstanding fulfillment, quoted names, employee assignment, personal requests and date follow-ups. Every example passes the production C# validator in offline tests. Examples only use allowed candidate sources. Single-source questions receive examples for that source. Prompt construction respects the existing 3,000-byte UTF-8 input budget, including the original question and bounded previous-request scope.

The structured response schema narrows status and priority values to evidence present in the question. Quoted names do not expose their words as status or priority options. Unsupported fields for a recognized source are excluded. The existing C# evidence, permission and SQL checks remain in place. A separate parsing correction removes name introducers such as “named” and “called” before searching, while preserving those words inside quoted names.

Clarifications can now display native buttons:

| Situation | Choices |
| --- | --- |
| Missing record type | Stock, Tickets, Requests, limited to permitted sources |
| Ambiguous “resolved” ticket status | Solved, Closed, Resolved (Temporary) |
| Follow-up after two results | The previous results, each retaining its own filters |
| Unrecognized search wording | Use as search text, confirming the complete proposed text filter |

Buttons are created by C#, not by model-generated instructions. Each choice identifies a private snapshot of the original question or request. Selection rechecks the current session and permissions and queries fresh data. Choices are consumed once, disable together after selection, and expire after another question, a new chat or a session change. An old choice cannot invalidate a newer clarification. No choice removes an unsupported exclusion, changes a write request into an action, or broadens a zero result. Typing a displayed label, such as “Solved,” resolves the same saved question as clicking it. Other text remains a new question. Copy and legacy text include readable labels without internal identifiers.

Catalog-based model formatting and typo confirmation choices are documented in [Yakumi-ProductMatching.md](Yakumi-ProductMatching.md). They reuse the same pending-choice lifecycle and preserve the original search when a correction is declined.

Validation artifacts for the original example/clarification change: `_build_verify/yakumi-choices/`.

- 1,525 offline assertions: 124 original assistant, 655 expanded, 60 presentation/interaction, 627 understanding/English and 59 example/clarification checks.
- 209 SQL variants passed ScriptDom syntax and SELECT-only validation.
- Debug/x64 and Release/AnyCPU builds (the desktop project targets x64).
- 39 synthetic WPF previews at 100%, 125% and 150%, including source, status and previous-result choices. The new layouts were visually reviewed.
- All three generic live interpretation examples passed: support incidents, requests awaiting fulfillment, and the quoted “Still Open” name. The guide example used authored fallback prose. These are a small smoke check, not a guarantee for every wording.
- Valid project XML, no duplicate includes or ghost Pages entries, and all new C# files explicitly registered.

The tests use synthetic context and quantities. Live AI checks send generic questions and curated guides only. No database writes, production data checks, deployments, model-setting changes or conversation-persistence changes were performed.
