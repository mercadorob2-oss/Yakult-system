using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using Yakult.Inventory.App.Core;
using Yakult.Inventory.App.Wpf.RepairPortal.Reports.ViewModels;

namespace Yakult.Inventory.App.Wpf.RepairPortal.Reports.Views
{
    /// <summary>WPF replacement for the Repair Report's signatory picker — one bordered section per
    /// item set (single ticket, or one requester group in the batch flow), each showing a real
    /// DataGrid of the item(s) that set covers ONCE (using this app's own ListDataGridChrome grid
    /// style, the same one the Repair Reports list window uses), followed by every role that signs
    /// against that same item set — so Reviewed By/Received By don't repeat the table twice. Built
    /// specifically for the Repair Portal so the shared WinForms SignatoryPickerDialog (still used
    /// by Sets/Renewals/Invoice/Warranty) is left completely untouched.</summary>
    public partial class RepairSignatoryPickerWindow : Window
    {
        /// <summary>One role that needs to sign for a group's item set.</summary>
        public sealed class RoleSpec
        {
            public string RoleName;
            public string Hint;
            public int? DeptId;

            /// <summary>From RepairTicketRepository.GetLatestReportSignaturesAsync — who signed
            /// this role last time this ticket's (or group's) report was printed, if ever. Null/
            /// empty when there's no history yet, in which case the role builds blank exactly as
            /// before.</summary>
            public string PrefilledName;
            public string PrefilledTitle;
            public DateTime? PrefilledDate;
        }

        /// <summary>One item set (a single ticket, or one requester group) plus every role that
        /// needs to sign for it.</summary>
        public sealed class GroupSpec
        {
            public string GroupLabel;
            public List<SignatoryItemRow> Items;
            public List<RoleSpec> Roles;
            /// <summary>Draws an obvious divider after this group — marks the end of one requester's
            /// block in the Repair Report batch flow.</summary>
            public bool ShowDivider;
        }

        private readonly RepairSignatoryPickerViewModel _vm = new RepairSignatoryPickerViewModel();
        private readonly List<SignatoryEmployee> _employees = new List<SignatoryEmployee>();

        public bool Confirmed { get; private set; }

        public RepairSignatoryPickerWindow(IEnumerable<GroupSpec> groups)
        {
            InitializeComponent();
            DataContext = _vm;

            LoadEmployees();
            BuildGroups(groups);
        }

        private void LoadEmployees()
        {
            try
            {
                DatabaseConfig.EnsureConfigured();
                using (var con = new System.Data.SqlClient.SqlConnection(DatabaseConfig.ConnectionString))
                using (var cmd = new System.Data.SqlClient.SqlCommand(
                    @"SELECT e.EmpId, e.Name, e.Position, t.Code, e.DeptId
                      FROM dbo.Employee e
                      LEFT JOIN dbo.Title t ON e.TitleId = t.TitleId
                      WHERE e.Active = 1 ORDER BY e.Name", con))
                {
                    con.Open();
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            _employees.Add(new SignatoryEmployee
                            {
                                EmpId     = reader.GetInt32(0),
                                Name      = reader.GetString(1),
                                Position  = reader.IsDBNull(2) ? "" : reader.GetString(2),
                                TitleCode = reader.IsDBNull(3) ? "" : reader.GetString(3),
                                DeptId    = reader.IsDBNull(4) ? (int?)null : reader.GetInt32(4)
                            });
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("RepairSignatoryPickerWindow: failed to load employees", ex);
                MessageBox.Show("Could not load employee list.\n" + ex.Message,
                    "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void BuildGroups(IEnumerable<GroupSpec> groups)
        {
            foreach (var g in groups ?? Enumerable.Empty<GroupSpec>())
            {
                var group = new SignatoryGroupSection
                {
                    GroupLabel = g.GroupLabel ?? "",
                    ShowDivider = g.ShowDivider
                };
                foreach (var item in g.Items ?? Enumerable.Empty<SignatoryItemRow>())
                    group.Items.Add(item);

                foreach (var spec in g.Roles ?? Enumerable.Empty<RoleSpec>())
                {
                    var candidates = _employees;
                    if (spec.DeptId.HasValue)
                    {
                        var filtered = _employees.Where(e => e.DeptId == spec.DeptId.Value).ToList();
                        if (filtered.Count > 0) candidates = filtered;
                    }

                    // Prefills from this ticket's (or group's) last recorded report signature, if
                    // any — a first-ever print still has nothing to prefill from, so it builds
                    // blank exactly as before. OriginalEmployeeText/OriginalTitle snapshot the
                    // prefilled value so HasChangedFromOriginal has a fixed point to compare
                    // against regardless of what gets typed afterward.
                    var hasPrefill = !string.IsNullOrWhiteSpace(spec.PrefilledName);
                    var role = new SignatoryRoleInput
                    {
                        RoleName = spec.RoleName,
                        Hint = spec.Hint ?? "",
                        Candidates = candidates,
                        EmployeeText = hasPrefill ? spec.PrefilledName : "",
                        Title = hasPrefill ? (spec.PrefilledTitle ?? "") : "",
                        SignedDate = hasPrefill && spec.PrefilledDate.HasValue ? spec.PrefilledDate : DateTime.Today,
                        OriginalEmployeeText = hasPrefill ? spec.PrefilledName : "",
                        OriginalTitle = hasPrefill ? (spec.PrefilledTitle ?? "") : "",
                        OriginalSignedDate = hasPrefill ? spec.PrefilledDate : null,
                        HasPriorRecord = hasPrefill,
                        IsLocked = hasPrefill
                    };

                    group.Roles.Add(role);
                }

                _vm.Groups.Add(group);
            }
        }

        /// <summary>Results keyed by role name — (Employee name, Title, signed date) for every role,
        /// empty strings/null date for roles the user left blank.</summary>
        public Dictionary<string, (string name, string title, DateTime? date)> GetResults()
        {
            var result = new Dictionary<string, (string name, string title, DateTime? date)>(StringComparer.OrdinalIgnoreCase);
            foreach (var group in _vm.Groups)
                foreach (var role in group.Roles)
                {
                    var name = role.EmployeeText?.Trim() ?? "";
                    result[role.RoleName] = (name, role.Title?.Trim() ?? "", string.IsNullOrEmpty(name) ? (DateTime?)null : role.SignedDate);
                }
            return result;
        }

        // ── Autocomplete ─────────────────────────────────────────────────────

        private static bool Contains(string haystack, string needle) =>
            !string.IsNullOrEmpty(haystack) && haystack.IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0;

        private static Popup FindSiblingPopup(TextBox box) =>
            (box.Parent as Grid)?.Children.OfType<Popup>().FirstOrDefault();

        private void ShowCandidates(TextBox box, string typed)
        {
            if (!(box.DataContext is SignatoryRoleInput role)) return;
            var popup = FindSiblingPopup(box);
            var listBox = (popup?.Child as Border)?.Child as ListBox;
            if (popup == null || listBox == null) return;

            List<SignatoryEmployee> matches = string.IsNullOrEmpty(typed)
                ? role.Candidates
                : role.Candidates.Where(e =>
                    Contains(e.Name, typed) || Contains(e.Position, typed) || Contains(e.TitleCode, typed)).ToList();

            listBox.ItemsSource = matches;
            popup.IsOpen = matches.Count > 0;
        }

        // Unlocks a role that started read-only because it already has a prior recorded signature
        // — a deliberate click, not an accidental edit, per the "only do this if you made a
        // mistake" warning shown once unlocked. Revert/Save (both shown together, non-conditionally,
        // for the whole time a HasPriorRecord role stays unlocked) re-lock it either discarding or
        // keeping whatever was typed — neither one writes to the database itself, that still only
        // happens later in RepairReportBuilder once the whole dialog is confirmed.
        private void UnlockRole_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as FrameworkElement)?.DataContext is SignatoryRoleInput role)
                role.IsLocked = false;
        }

        private void RevertRole_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as FrameworkElement)?.DataContext is SignatoryRoleInput role)
                role.RevertToOriginal();
        }

        private void SaveRole_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as FrameworkElement)?.DataContext is SignatoryRoleInput role)
                role.IsLocked = true;
        }

        private void EmployeeBox_TextChanged(object sender, TextChangedEventArgs e) =>
            ShowCandidates((TextBox)sender, ((TextBox)sender).Text);

        private void EmployeeBox_GotFocus(object sender, RoutedEventArgs e) =>
            ShowCandidates((TextBox)sender, ((TextBox)sender).Text);

        private void EmployeeListBox_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            var listBox = (ListBox)sender;
            if (!(listBox.SelectedItem is SignatoryEmployee emp)) return;

            var popup = System.Windows.LogicalTreeHelper.GetParent(((Border)listBox.Parent)) as Popup;
            var box = popup?.PlacementTarget as TextBox;
            if (box?.DataContext is SignatoryRoleInput role)
            {
                role.SelectedEmployee = emp;
                role.EmployeeText = emp.Name;
                if (string.IsNullOrWhiteSpace(role.Title))
                    role.Title = emp.Position;
            }
            if (popup != null) popup.IsOpen = false;
        }

        // ── Footer ───────────────────────────────────────────────────────────

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            // Aborts report generation entirely — the caller (RepairReportBuilder) checks
            // ShowDialog() != true and returns without building anything. Distinct from Skip, which
            // still proceeds but with every signature left blank.
            Confirmed = false;
            DialogResult = false;
            Close();
        }

        private void Skip_Click(object sender, RoutedEventArgs e)
        {
            // Explicitly blank every field before proceeding, so "Skip" always means "no
            // signatures" even if the technician had partially typed something in first —
            // unambiguous, rather than silently keeping whatever happened to be there.
            foreach (var group in _vm.Groups)
                foreach (var role in group.Roles)
                {
                    role.SelectedEmployee = null;
                    role.EmployeeText = "";
                    role.Title = "";
                }

            Confirmed = true;
            DialogResult = true;
            Close();
        }

        private void Ok_Click(object sender, RoutedEventArgs e)
        {
            // Not saved to ReportPreferences — this picker never remembers a signatory between
            // uses, on request. Every report reopens with every role blank.
            Confirmed = true;
            DialogResult = true;
            Close();
        }
    }
}
