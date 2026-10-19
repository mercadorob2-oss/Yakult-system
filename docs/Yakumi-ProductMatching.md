# Yakumi catalog model matching and typo choices

Stock questions use permitted catalog evidence to treat model spacing and hyphens consistently. For a catalog model such as LX310, “how many LX310 are still in stock,” “how many LX-310 in stock” and “how many LX 310 are currently available” produce the same model filter and SQL parameters. The filter compares the complete model after removing spaces and hyphens; digits and suffix letters remain meaningful. This applies to items, consumables and cartridges. Remaining brand, category, condition, ownership and date filters still apply together.

Likely product-name and model typos produce a focused clarification before any quantity lookup. For example, LX301 can offer LX310 and **Keep original search**; LX330 can offer both LX310 and LX350. Choices use a deterministic one-edit or adjacent-transposition comparison against the accessible catalog. Short, distant, quoted or multiply ambiguous words are not guessed. At most two alternatives appear. Clicking a choice or typing its label confirms it. Keeping the original performs the original filtered search, including a true zero when appropriate.

Quoted product names retain literal search semantics. Unknown one-character model suffixes require clarification rather than dropping the suffix. Model replacement clears an inherited model predicate, while compatible date and condition refinements retain it. A source change clears incompatible predicates. Copied Show records/page requests preserve immutable model evidence.

The catalog cache holds names, models and categories for five minutes, scoped to the session and source. The bounded metadata SELECTs respect source and category permissions and do not retrieve stock quantities. No catalog rows go to AI. Quantities are queried fresh through the existing engine. A catalog outage leaves clear literal lookups available without inventing suggestions. Cancellation, access checks, new-chat resets, stale choices and session changes use the existing service boundary. No database schema, contents, AI configuration, avatar or conversation persistence changes are needed.

Verification artifacts: `_build_verify/yakumi-products/`.

- 1,606 offline assertions, including 81 new catalog/model/confirmation checks.
- 215 SQL variants passed ScriptDom syntax and SELECT-only validation, including the new normalized-model predicates and all three permission-filtered catalog SELECTs.
- Debug/x64 and Release/AnyCPU builds (the app targets x64).
- 42 synthetic WPF previews at 100%, 125% and 150%; product-choice layouts visually inspected at 100% and 150%.
- The 254-unit fixture spans three stored model spellings. Independent synthetic rows for LX350 and LX310A verify that the exact full model, remaining filters and fresh quantities are respected. This is not a current database count.
- A manual 10,000-model synthetic lookup took approximately 262 ms on this machine; this is a local sample, not a latency guarantee.
- Valid project XML, zero duplicate includes or ghost Pages entries, and explicit compile registration for both new C# files.

No live database or AI requests were needed for this change. The offline fixture, generated SQL and UI checks do not certify an installed desktop or production permissions.
