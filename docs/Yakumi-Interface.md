# Yakumi interface and answer presentation

Implemented and checked on 2026-10-16. The floating card remains 350 × 540 WPF units with its red header and Yakumi avatar. New chat resets the current turn and conversation. Minimize reduces the card and reusable WinForms host to a 56-unit header, preserving the bottom anchor and returning to the previous chat or settings view.

The Ask Yakumi launcher is a circular button showing the existing Yakumi bot avatar. It can be dragged around its app window in WinForms, the WPF overlay, and the WPF grid fallback. Its moved position is retained for that window and kept within the available area when resized. Ordinary clicks use native button capture and click handling; only a completed drag suppresses opening. Keyboard activation remains available, with a visible focus ring and an accessible Ask Yakumi label.

The avatar update was checked using real mouse press/release and drag actions in isolated WinForms, WPF grid and WPF overlay test windows. All three opened chat before and after moving the avatar, without toggling chat on drag release. Keyboard opening also passed in WinForms and WPF. Debug and Release builds passed, along with the 839 offline assertions and reusable-host checks. Latest launcher build logs: `_build_verify/yakumi-avatar-build.log` and `_build_verify/yakumi-avatar-release-build.log`.

Messages and input use 13 pt text. The conversation fills the space above a multiline composer. Enter sends; Shift+Enter inserts a newline. The existing 1,200-character question limit remains enforced. Permission-aware stock, request, ticket and page-help prompts appear before the first accepted question. Stop remains available during a turn, including from settings. Working text is neutral.

Factual replies use typed SQL results. A stock headline uses unit totals; ticket headlines use record counts and status. Filters and qualifications remain visible. Additional totals have explicit labels and appear once. Source, complete scope and retrieval time are available in collapsed Details. Native WPF controls render paragraphs, bold emphasis, bullets and numbered steps; response text is never loaded as XAML or HTML.

Lists initially show three record previews. Show more reveals the rest of the already loaded eight-record page without querying. Each preview leads with relevant fields, with remaining fields available in Record fields. Record-detail replies show all readable fields. Summary rows identify their grouping. Copy includes all loaded records and fields, filters, qualifications, explanation, guide citations and provenance, including collapsed content. It does not load other pages.

Follow-up actions carry independent copies of their original typed requests. They retain filters after intervening questions and pass through the service's permission, cancellation and session checks. Answers from an earlier conversation or changed session are discarded. Incoming replies preserve the scroll offset when reading older messages. New reply jumps to the latest answer's headline; replies arriving at the current end also begin at their headline.

`AssistantPresentationFormatter` consumes typed data and never extracts facts from the legacy `AssistantResult.Text`. That text remains available for compatibility. Combined replies retain typed child results, comparisons carry explicit comparison metadata, and guide explanation text and citations are separate. Formatting performs deterministic arithmetic on SQL totals without asking the model to rewrite quantities.

## Verification

All checks use synthetic context and records. No business database or live model was contacted for this interface work, and the production application was not launched.

| Check | Result |
| --- | --- |
| Original assistant regressions | 124 passed |
| Existing expanded offline checks | 655 passed |
| New presentation and interaction checks | 60 passed |
| Generated SELECT syntax and read-only checks | 209 query variants passed |
| Debug/x64 project build | Passed; 0 errors |
| Release/AnyCPU project build, targeting x64 | Passed; 0 errors |
| Existing project diagnostics | 142 warnings; none from the changed assistant/presentation files |
| Reusable WinForms host | Minimize/restore size, bottom anchor and HWND reuse; owner closure, module switch, normal close, reset and disposal passed |
| WPF embedding previews | 30 synthetic images at 100%, 125% and 150%; representative images visually inspected at all three scales |
| Project integrity | Valid XML, zero duplicate includes, zero ghost Pages includes; all new source files explicitly registered |

There are 839 offline assertions in total. New checks cover counts versus quantities, fractional quantities, amounts, true zero versus unavailable/denied/cancelled states, long names, missing fields, eight-record pages, details, grouped summaries, comparisons, guide citations, complete copy payloads, Show more without a query, retained paging filters, permission/session checks, multiline input, scrolling, settings restoration and stale-answer rejection. The Copy button is tested using a clipboard sink so validation preserves the user's clipboard.

Preview scaling uses WPF render targets at 96, 120 and 144 DPI. The WinForms host checks run at the desktop's current DPI; these checks do not certify physical multi-monitor DPI transitions or a real authenticated production session.

Builds and preview images are under `_build_verify/yakumi-polish/`. Build logs are `_build_verify/yakumi-polish-build.log` and `_build_verify/yakumi-polish-release-build.log`.

```powershell
$assembly = '_build_verify/yakumi-polish/app/Yakult.Inventory.App.exe'
.\tools\Test-ExpandedYakumi.ps1 -AssemblyPath $assembly
.\tools\Test-ExpandedYakumi.ps1 -AssemblyPath $assembly -HostLifecycle
.\tools\Test-ExpandedYakumi.ps1 -AssemblyPath $assembly -PresentationPreviews _build_verify/yakumi-polish/previews
```

The runner accepts `-ScriptDomPath` for the 209 generated SELECT syntax checks. Existing tools retain their original default assembly path; pass the explicit path above for this isolated build. Query coverage, permission policy, database contents, GitHub operations, installer packaging and conversation persistence remain outside this work.
