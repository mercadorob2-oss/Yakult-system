## Context
Project: Yakult Inventory App (C:\Users\russel.mercado\source\repos\Yakult-System-for-Merging\Yakult.Inventory.App)
Task: Port "History" feature (Current/Previous/History tabs) from legacy WinForms ReceiptSetViewerDialog.cs into the new WPF ReceiptSetViewerWindow.xaml/.xaml.cs, matching the legacy dialog closely but with the new WPF visual design.

Legacy source file: Pages\Receipt\ReceiptSetViewerDialog.cs (~4000+ lines)
New WPF target files: Wpf\Receipt\ReceiptSetViewerWindow.xaml / .xaml.cs
Repository: Repositories\ReceiptSetRepository.cs (already has all needed methods)

## Two distinct data models the legacy dialog handles

### 1. LINKED receipts (attached to a Set via ReceiptSetLink, with CoverageStartDate/CoverageEndDate periods)
- Entry point: `InitializeCoverageTabsForSet(int setId)` (legacy line ~4054)
- Logic:
  - Calls `_repository.GetRenewedCoveragePeriodsForSet(setId)` → renewedPeriods list
  - Calls `_repository.GetSetCoveragePeriod(setId)` → setPeriod (fallback)
  - If renewedPeriods.Count > 0:
    - Current = renewedPeriods[0]
    - Previous = renewedPeriods[1] if Count > 1, else null
    - History = renewedPeriods.Skip(2) if Count > 2, else empty
  - Else: Current = setPeriod, Previous = null, History = empty
  - `_tpPrevious.Enabled = (Previous != null)`
  - `_tpHistory.Enabled = (History != null && History.Count > 0)`
  - Calls `PopulateHistoryList()` → `BuildLinkedHistoryEntries()`
  - `BuildLinkedHistoryEntries()`: for each history period p, calls `_repository.GetMetadataBySetIdCoverage(setId, p.StartDate, p.EndDate)`, builds HistoryEntry with Kind=LinkedPeriod, VersionLabel = "{StartDate:yyyy-MM-dd}→{EndDate:yyyy-MM-dd}", SavedAt = meta.ModifiedAt ?? meta.CreatedAt, ChangeSummary = BuildChangeSummary(currentLiveFormState, meta) — NOTE: linked "Changed" diffs against LIVE FORM STATE, not a true historical snapshot (documented inconsistency in legacy code).
  - Sort order for linked entries: descending by Period.StartDate

### 2. UNLINKED receipts (using RenewedFromReceiptSetId chain, no Set attachment)
- Entry point: `InitializeUnlinkedRenewalTabsForReceiptSet(int headReceiptSetId)` (legacy line ~3798)
- Logic:
  - Calls `_repository.GetReceiptRenewalChainMetadata(headReceiptSetId)` → chain list (chain[0]=head/current, chain[1]=previous, chain[2+]=older)
  - `_unlinkedPreviousReceiptSetId` = chain[1].ReceiptSetId if chain.Count > 1
  - `_unlinkedHistoryReceipts` = chain.Skip(1) (i.e., Previous + all older versions included in History list)
  - `_tpPrevious.Enabled = _unlinkedPreviousReceiptSetId.HasValue`
  - `_tpHistory.Enabled = _unlinkedHistoryReceipts.Count > 0`
  - Calls `PopulateUnlinkedHistoryList()` → `BuildUnlinkedHistoryEntries()`
  - Sort order for unlinked entries: ascending by DepthFromHead (1=Previous, 2=older, ...)

### Brand-new receipt (no Set link, no renewal chain)
- `DisableCoverageTabsUi()`: retitles `_tpCurrent.Text = "Receipt"`, disables both `_tpPrevious` and `_tpHistory` permanently.

## OnPeriodTabChanged (legacy line ~4212)
- Guards against re-entrancy (`_updatingTabs`) and disabled coverage tabs.
- If dirty and switching tabs: confirms discard via MessageBox Yes/No; reverts tab selection if user cancels.
- For UNLINKED case: switching to Current tab reloads head receipt data into the live form (editable); switching to Previous tab sets `_readOnly = true` and reloads the previous receipt's data into the SAME form fields (read-only); switching to History tab does NOT reload the form — history entries open in a separate read-only viewer instead.
- This "swap the whole editable form into read-only previous-mode" pattern is tightly coupled to the legacy single-image-slot design and should NOT be literally replicated in the new WPF gallery-based window (decided during design discussion) — instead use separate read-only views/snapshots.

## History list columns (7 total)
Version/Period | Saved | Supplier | Doc # | Imgs | By | Changed
- FormatDocColumn (legacy ~3982): combines SI/DR/PO numbers; shows single value if all three equal, else "SI:x DR:y PO:z" style parts joined by double-space; "—" if all empty.
- FormatImageIndicators (legacy ~4012): "SI✓ DR✓ PO—" style using ImagePath presence only (NOTE: legacy bug/inconsistency — checks path only, not bytes).
- BuildChangeSummary (legacy ~4025): diffs Supplier/SiNumber/DrNumber/PoNumber/SI-DR-PO image presence between two ReceiptSetDto snapshots (newer vs older); returns comma-joined list of changed field names, capped at 3 with "+N" suffix if more, "—" if no changes.
- ResolveUserName (legacy ~3967): looks up CreatedBy/ModifiedBy user IDs via `_repository.GetUserNamesByIds`, falls back to "#{id}" if name not found, "—" if null/0.

## History details panel (legacy BuildHistoryDetailsPanel ~2842, UpdateHistoryDetailsFromSelection ~3053)
Shows when a history list item is selected: Supplier, Doc #s, Created At, Modified At, User (By), Changes summary, plus 3 fixed thumbnail slots (SI/DR/PO) — legacy single-image-slot model. In the new WPF design this should be replaced with the existing multi-image gallery concept (show counts/thumbnails per doc type, not just 3 fixed slots).

## Context menu on history list (BuildHistoryContextMenu ~2811)
Three items: "Open (read-only)", "Copy details", "Make current" (separator before last item).
Enablement (on Opening event):
- Open/Copy enabled if selected entry has ReceiptSetId > 0.
- "Make current" enabled ONLY if entry.Kind == UnlinkedVersion AND !_setId.HasValue (i.e., only for unlinked chains, never for linked/Set-attached receipts).

## Double-click behavior (OpenSelectedHistoryEntry, legacy ~4514)
Opens a NEW dialog instance in forced read-only mode via constructor flag cascading through `ApplyReadOnlyState()` (~3545).
Design decision: in WPF, reuse the existing ReceiptSetViewerWindow with a read-only flag/mode instead of building a bespoke separate viewer.

## Revert feature
- `BtnRevert_Click` (legacy ~1326): preconditions — blocked if `_forceReadOnly || _readOnly` (shows info msg), blocked if `_receiptSetId <= 0` (must save first), confirms discard if dirty. Revert always operates from the Current tab context (force-switches to Current tab first). Then branches: `_setId.HasValue && > 0` → `RevertLinkedReceiptToPrevious()`; else → `RevertUnlinkedReceiptToPrevious()`.
- `RevertUnlinkedReceiptToPrevious()` (legacy ~1384): requires `_unlinkedPreviousReceiptSetId.HasValue` (else info msg "No previous receipt set exists to revert to."). Confirms via Yes/No: "Revert to the previous receipt set? The previous receipt will become Current, and the current receipt will move to Previous." Calls `_repository.RevertUnlinkedReceiptSetToPrevious(headId, userId)` → returns newHeadId. Reloads dto via `GetByReceiptSetId(newHeadId)`, calls `LoadFromDto`, updates `_unlinkedHeadReceiptSetId`, re-calls `InitializeUnlinkedRenewalTabsForReceiptSet(newHeadId)`, force-switches to Current tab, shows success msg.
- `RevertLinkedReceiptToPrevious()` (legacy ~1437): requires `_currentCoverage != null && _previousCoverage != null` (else info msg "No previous coverage period exists to revert to."). Fetches currentDto via `GetBySetIdCoverage(setId, currentCoverage.Start, currentCoverage.End)` and prevDto via same for previousCoverage; requires currentDto.ReceiptSetId > 0 (else info msg "No current receipt set is saved for the current coverage period."). Uses `_repository.SwapReceiptSetCoverageLinks(...)` to swap the two coverage periods between the two ReceiptSetLink rows (implementation swaps CoverageStartDate/CoverageEndDate values, going through a NULL intermediate step to avoid unique-filtered-index collisions — see SQL in ReceiptSetRepository.SwapReceiptSetCoverageLinks). Then reloads and re-initializes tabs similarly.

## Edge cases / guard clauses worth preserving
- DB-enforced linked/unlinked mutual exclusivity: SQL THROW 54030 in RevertUnlinkedReceiptSetToPrevious if the receipt is actually linked to a Set (revert must go through the linked-coverage path instead).
- MAXRECURSION 100 cap in the chain-walking CTE (GetReceiptRenewalChainMetadata SQL) plus an app-side 100-iteration guard in MakeSelectedHistoryCurrent to prevent runaway loops.
- Many catch blocks silently swallow errors and just leave the UI usable without period tabs (e.g., if renewal columns/tables don't exist in an unmigrated DB) — same defensive pattern should be preserved in the WPF port.
- History/Previous overlap in unlinked model: `_unlinkedHistoryReceipts` includes Previous itself (chain.Skip(1)), so History list contains Previous + older, not just strictly-older versions.
- FormatImageIndicators only checks ImagePath, not ImageBytes — a known inconsistency, worth flagging but can be preserved or fixed at implementer's discretion.
- Linked "Changed" column compares against live in-memory form state, not a true persisted historical current snapshot — documented quirk, decide whether to fix or preserve when porting.

## Design decisions made for the WPF port (agreed with user: "matching the dialog closely but with new wpf design")
1. Add a "Current | Previous | History" tab bar above the existing Supplier/SI/DR/PO fields + gallery in ReceiptSetViewerWindow. Previous/History disabled (grayed) when no Set link and no renewal chain exist.
2. Current tab = existing editable behavior (unchanged).
3. Previous tab = read-only snapshot of previous linked-period or previous-in-chain receipt; same fields/gallery but disabled; includes a "Revert to this version" button wired to the ported RevertLinkedReceiptToPrevious/RevertUnlinkedReceiptSetToPrevious logic.
4. History tab = WPF DataGrid (styled per new card language) with the same 7 columns, left side; details panel on right showing selected entry's fields + gallery-style thumbnails/counts (replacing legacy's 3 fixed slots). Context menu: Open (read-only), Copy details, Make current (unlinked-only, same enablement rule as legacy).
5. Double-click a history row opens a NEW ReceiptSetViewerWindow instance in a read-only mode/flag, reusing the existing window rather than building a separate bespoke viewer.
6. Explicitly NOT replicating: the legacy "live-swap the entire editable form into read-only previous-mode when switching tabs" interaction — this was tightly coupled to the old single-image-slot design and doesn't map cleanly onto the new multi-image-per-doctype gallery model. Previous/History use separate read-only snapshots/views instead.

## Status at time of saving this note
Design was proposed and confirmed by user. Implementation (actual XAML/code-behind changes for the History tab) had NOT yet been started — this is the spec to build from when implementation resumes. Tasks 1 and 2 (DataGrid visual redesign, Viewer window visual redesign) were already completed and build-verified. Task 3 (History tab port) and Task 4 (final build/regression check) remain.
