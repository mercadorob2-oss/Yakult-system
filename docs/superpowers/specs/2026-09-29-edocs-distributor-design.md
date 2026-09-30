# E-Docs Requisition Company/Distributor Toggle Design

Date: 2026-09-29 (revised: toggle instead of replace)
Scope: E-Documents standalone Requisition tab only. Set-based requisitions unchanged.

## Goal

Section 1 gets a Company/Distributor toggle. Company mode shows the legacy
YPI / YMC / El Salvador radios. Distributor mode shows a dropdown sourced
from the inventory `dbo.Distributor` catalog. Print shows the company
checkboxes in Company mode and `Distributor: <name>` in Distributor mode.

## Changes

### 1. RequisitionFormViewModel (shared model, additive only)

File: `Yakult.Inventory.App\Wpf\Set\RequisitionForm\ViewModels\RequisitionFormViewModel.cs`

- Add `int? DistributorId` and `string DistributorName`.
- `Clone()` keeps working via `MemberwiseClone`.
- `FromSet` leaves both null, so Set/Invoice prints keep legacy checkboxes.

### 2. EDocsDashboardViewModel (entry + loading)

File: `Yakult.Inventory.App\Wpf\EDocs\ViewModels\EDocsDashboardViewModel.cs`

- Add `IsDistributorMode` (default false) with `ShowCompanyModeCommand` /
  `ShowDistributorModeCommand`, plus `ObservableCollection<DistributorDto>`
  `Distributors` and `SelectedDistributor` (INotifyPropertyChanged).
- Load on construction (fire-and-forget, best-effort):
  `SELECT DistributorId, Name FROM dbo.Distributor WHERE IsActive = 1
   ORDER BY SortOrder, Name`, guarded by `OBJECT_ID('dbo.Distributor','U')`.
  Empty list on failure, same as `AddSetPage.cs`.
- `PrintRequisition()` / `SaveRequisitionPdf()` require a selection only in
  distributor mode; `BuildRequisitionItems()` copies it into
  `RequisitionInput.DistributorId/DistributorName`, or clears both in
  company mode so the legacy checkboxes print.

### 3. EDocsDashboardView.xaml (entry UI)

File: `Yakult.Inventory.App\Wpf\EDocs\Views\EDocsDashboardView.xaml`

- Section 1 title becomes `Company / Distributor` with Company and
  Distributor toggle buttons. Company panel (3 `ReqCompany` radios) shows
  when `IsDistributorMode` is false; editable search ComboBox bound to
  `Distributors` shows when true. DataTriggers only, no converter.

### 4. RequisitionFormPrintView.xaml (print header)

File: `Yakult.Inventory.App\Wpf\Set\RequisitionForm\Views\RequisitionFormPrintView.xaml`

- Keep legacy checkbox StackPanel; collapse it via DataTrigger when
  `DistributorName` is non-empty/non-null.
- Add centered bold `Distributor: <name>` TextBlock, visible only when
  `DistributorName` is set (inverse triggers).
- No new converters. No `LetterSpacing`. No `--` in comments.

## Validation

- Distributor required only in distributor mode; company mode keeps the legacy all-optional behavior.
- Items behavior unchanged (blank rows dropped, REMARKS brace grouping kept).
- Build must register no new .cs files, so no csproj change needed.

## Out of scope

- Gatepass / Transmittal tabs.
- Set / Invoice / Request distributor flows.
- DB migration (table already exists).
