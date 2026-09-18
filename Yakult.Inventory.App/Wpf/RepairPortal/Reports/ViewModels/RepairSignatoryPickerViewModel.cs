using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Yakult.Inventory.App.Session;

namespace Yakult.Inventory.App.Wpf.RepairPortal.Reports.ViewModels
{
    /// <summary>One employee candidate for the autocomplete list — mirrors the fields the WinForms
    /// SignatoryPickerDialog loads (Name/Position/TitleCode/DeptId), just as a plain WPF-bindable
    /// POCO instead of a private nested class.</summary>
    public sealed class SignatoryEmployee
    {
        public int EmpId { get; set; }
        public string Name { get; set; }
        public string Position { get; set; }
        public string TitleCode { get; set; }
        public int? DeptId { get; set; }

        /// <summary>Shown in the autocomplete list; only Name is committed into the textbox.</summary>
        public string Display => string.IsNullOrWhiteSpace(Position) ? Name : $"{Name}   —   {Position}";
    }

    /// <summary>One row of the item table shown above a group's role pickers.</summary>
    public sealed class SignatoryItemRow
    {
        public string Item { get; set; }
        public string Category { get; set; }
        public string Problem { get; set; }
        public string Status { get; set; }
    }

    /// <summary>One role's Employee/Title picker — several of these can share the same item table
    /// (e.g. Reviewed By and Received By for the same requester group), so the table isn't repeated
    /// once per role.</summary>
    public sealed class SignatoryRoleInput : INotifyPropertyChanged
    {
        public string RoleName { get; set; }
        public string Hint { get; set; }

        /// <summary>This role's candidate pool — already department-filtered (with the same
        /// fall-back-to-everyone-if-empty rule the WinForms dialog used) before the section is built.</summary>
        public List<SignatoryEmployee> Candidates { get; set; } = new List<SignatoryEmployee>();

        public SignatoryEmployee SelectedEmployee { get; set; }

        private string _employeeText = "";
        public string EmployeeText
        {
            get => _employeeText;
            set { _employeeText = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasChangedFromOriginal)); }
        }

        private string _title = "";
        public string Title
        {
            get => _title;
            set { _title = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasChangedFromOriginal)); }
        }

        /// <summary>The date this person actually signs — defaults to today, editable via the
        /// picker's own DatePicker so it doesn't have to be left blank for physical fill-in.</summary>
        private DateTime? _signedDate = DateTime.Today;
        public DateTime? SignedDate
        {
            get => _signedDate;
            set { _signedDate = value; OnPropertyChanged(); }
        }

        /// <summary>Snapshot of whatever RepairReportBuilder prefilled this role with, from
        /// RepairTicketRepository.GetLatestReportSignaturesAsync — set once when the role is built
        /// (see RepairSignatoryPickerWindow.BuildGroups) and never changed afterward, so it stays a
        /// fixed point of comparison regardless of what the technician types next.</summary>
        public string OriginalEmployeeText { get; set; } = "";
        public string OriginalTitle { get; set; } = "";
        public DateTime? OriginalSignedDate { get; set; }

        /// <summary>True when this role was prefilled from a prior report (i.e. this ticket's
        /// report has already been printed before). Set once in BuildGroups, never changed.</summary>
        public bool HasPriorRecord { get; set; }

        /// <summary>Starts locked (read-only) when HasPriorRecord is true — a technician has to
        /// deliberately click Edit to change an already-recorded signatory, rather than being able
        /// to silently overwrite it. A first-ever print (no prior record) is never locked, so it
        /// behaves exactly like before: blank and immediately editable.</summary>
        private bool _isLocked;
        public bool IsLocked
        {
            get => _isLocked;
            set { _isLocked = value; OnPropertyChanged(); OnPropertyChanged(nameof(IsEditable)); }
        }

        public bool IsEditable => !IsLocked;

        /// <summary>Non-blocking hint shown when the technician edits a prefilled field away from
        /// what was recorded on this ticket's last printed report — true only when there WAS a
        /// recorded original (a first-ever print has nothing to compare against, so never warns).</summary>
        public bool HasChangedFromOriginal =>
            !string.IsNullOrWhiteSpace(OriginalEmployeeText) &&
            (!string.Equals(EmployeeText?.Trim(), OriginalEmployeeText?.Trim(), StringComparison.OrdinalIgnoreCase) ||
             !string.Equals(Title?.Trim(), OriginalTitle?.Trim(), StringComparison.OrdinalIgnoreCase));

        /// <summary>e.g. "Juan Dela Cruz — IT Officer" — the recorded original, shown in the
        /// warning so the technician can see exactly what changed without re-opening old reports.</summary>
        public string OriginalSummaryText =>
            string.IsNullOrWhiteSpace(OriginalTitle) ? OriginalEmployeeText : $"{OriginalEmployeeText} — {OriginalTitle}";

        /// <summary>Restores this role to exactly what was recorded on the last printed report and
        /// re-locks it — used by the Revert button (RepairSignatoryPickerWindow.RevertRole_Click).</summary>
        public void RevertToOriginal()
        {
            EmployeeText = OriginalEmployeeText;
            Title = OriginalTitle;
            SignedDate = OriginalSignedDate;
            SelectedEmployee = null;
            IsLocked = true;
        }

        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string name = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    /// <summary>One item set (e.g. one requester group, or the single ticket) — the item table is
    /// shown ONCE, followed by every role that needs to sign for it (Reviewed By, Received By, …).
    /// ShowDivider draws an obvious separator after this section — used to mark the end of one
    /// requester's block in the Repair Report batch flow.</summary>
    public sealed class SignatoryGroupSection
    {
        public string GroupLabel { get; set; }
        public bool ShowDivider { get; set; }
        public ObservableCollection<SignatoryItemRow> Items { get; } = new ObservableCollection<SignatoryItemRow>();
        public ObservableCollection<SignatoryRoleInput> Roles { get; } = new ObservableCollection<SignatoryRoleInput>();
    }

    public sealed class RepairSignatoryPickerViewModel
    {
        public ObservableCollection<SignatoryGroupSection> Groups { get; } = new ObservableCollection<SignatoryGroupSection>();

        /// <summary>Always the current signed-in technician — same fallback used by
        /// RepairReportBuilder.ShowRepairReportAsync's own generatedByName, so the name shown here
        /// matches exactly who the report will actually print as Prepared By. Not editable — this
        /// is a display-only preview of what's already fixed by who's logged in.</summary>
        public string PreparedByName { get; } =
            string.IsNullOrWhiteSpace(AppSession.CurrentEmployeeName) ? AppSession.CurrentUserName : AppSession.CurrentEmployeeName;

        public string PreparedByPosition { get; } = AppSession.CurrentEmployeePosition ?? "";

        public string PreparedByDateText { get; } = DateTime.Today.ToString("MMM d, yyyy");
    }
}
