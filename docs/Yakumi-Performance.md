# Yakumi response speed

Updated 2026-10-17. This update reduces repeated reference reads and local parsing work, expands deterministic count phrasing, streams general AI replies, and records where each turn spends time. Business totals remain fresh database queries. No answer or quantity cache was added.

## Changes

| Area | Behavior |
| --- | --- |
| Timing | One completion log per turn with elapsed milliseconds for planning, reference resolution, SQL, AI interpretation, guides, general replies and first streamed text, where applicable. Cache hit/miss counters are included. |
| Stock names | Three bounded cache entries retain the permitted items, consumables and cartridge vocabularies when switching sources. Product tokens and category aliases are indexed once per fill. |
| Organization names | Up to 32 recent bounded lookups can be reused when alternating company, branch and department filters. Empty reference results are cached too; failures and cancelled fills are not. |
| Local questions | Count phrases such as “Give me the number of tickets,” “What’s the total count of my requests?” and “Could you please count tickets?” resolve locally. Quotes, dates, exclusions and unexplained words retain their meaning. |
| AI display | General conversational replies appear incrementally. Query plans and cited guide JSON remain buffered until validated. SQL quantities never enter the model or streaming path. |
| Short replies | Simple general questions use a 160-token output budget instead of 384. Procedures, comparisons, requests for detail and continuation retain 384. Input limits and structured-output budgets are unchanged. |

Both reference caches expire after five minutes and clear when the session fingerprint changes, including account, permissions or database changes. Access checks run before cache use. Cache fills are serialized to avoid duplicate simultaneous reads; full caches evict the least recently used entry. Queries still enforce current permissions and cancellation before reading business records.

Streaming uses response headers immediately and reads complete UTF-8 NDJSON frames directly from the response stream, following the [Ollama streaming protocol](https://docs.ollama.com/api/streaming). Text updates are throttled and coalesced before reaching WPF. Stop, New chat, disposal and changed sessions reject late text. Only complete successful replies enter conversation history. Interrupted or invalid streams produce an unavailable/cancelled reply; guide validation and authored fallback remain intact. A generation-length stop is disclosed in the answer. Frame, reply and time limits bound the stream.

## Diagnosing a slow response

Timing lines have the prefix `[Yakumi timing]` in the existing application log:

`%APPDATA%\YakultInventoryApp\Logs\log_yyyy-MM-dd.txt`

For example, a turn can include `planning_ms`, `organization-reference_ms`, `sql_ms`, `ai-interpretation_ms`, `ai-general_ms`, `first-text_ms`, `stock-cache-hit` and `organization-cache-miss`. Stage durations may overlap with the total; first-text time is measured from the turn's start. Logs contain fixed stage names, durations, counters and outcomes, without questions, names, filter values, rows, quantities, replies, credentials or server addresses. Timing details do not appear in the user conversation.

Streaming reduces the wait for visible text once the server emits it. It does not remove model startup, server queueing or network delays. The generic live smoke output records first-text and completion times, so a delayed initial response can be distinguished from slow generation. Separate smoke runs are not a controlled before/after performance benchmark.

## Verification

Artifacts are in `_build_verify/yakumi-speed/`. The offline suite uses synthetic records, reference directories, model responses and a controlled HTTP stream. It verifies that text appears before completion without waiting for the rest of the reply, preserves Unicode, rejects malformed/incomplete/oversized streams, interrupts stalled reads, and discards old UI drafts. Tests also cover cache reuse, expiry, eviction, session isolation, concurrent fills, failures, cancellation, permission denial, quoted names, unknown wording, unsupported exclusions and fresh repeated quantities.

```powershell
$assembly = '_build_verify/yakumi-speed/app/Yakult.Inventory.App.exe'
.\tools\Test-ExpandedYakumi.ps1 -AssemblyPath $assembly
.\tools\Test-ExpandedYakumi.ps1 -AssemblyPath $assembly -SpeedPreviews _build_verify/yakumi-speed/streaming-previews
.\tools\Test-ExpandedYakumi.ps1 -AssemblyPath $assembly -SpeedSmoke
```

`-SpeedSmoke` is an explicit live call to the configured AI with a generic one-sentence RAM question. It opens no business database and sends no business data. It reports times, update counts and text lengths, without logging the generated answer. The production desktop is not launched by the test runner.

Final verification:

| Check | Result |
| --- | --- |
| Offline assertions | 1,862 passed, including 52 new performance/streaming checks |
| SELECT-only SQL variants | 226 passed |
| Local source-schema bindings | 53 passed |
| Debug/x64 and Release/AnyCPU builds | Passed; 0 errors and 142 existing unrelated warnings; none in touched performance files |
| Existing UI and host checks | Passed; 57 existing card previews and 9 new streaming previews rendered |
| Visual review | Streaming, completed and stopped cards inspected at 100%, 125% and 150% |
| Project integrity | Valid project XML; explicit compile entries present; no duplicate includes, ghost Pages entries or whitespace errors |
| Generic live AI stream | Passed; first text at 4,459 ms, complete at 4,472 ms, 2 UI update callbacks, first fragment 3 characters and completed reply 126 characters |

The live sample shows an initial delay before the first text reached the client. It verifies streaming compatibility, rather than demonstrating a particular speed improvement. Timings vary with model/server/network state. The controlled HTTP test independently proves that the client emits a draft before the remaining response is available.
