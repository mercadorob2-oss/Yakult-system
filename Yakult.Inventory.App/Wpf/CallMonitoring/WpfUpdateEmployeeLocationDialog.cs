using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using Yakult.Inventory.App.Models;
using Yakult.Inventory.App.Repositories;

namespace Yakult.Inventory.App.Wpf.CallMonitoring
{
    public sealed class WpfUpdateEmployeeLocationDialog : Window
    {
        private readonly EmployeeRepository _repository = new EmployeeRepository();
        private readonly int _employeeId;
        private readonly ComboBox _company = CreateLookup();
        private readonly ComboBox _department = CreateLookup();
        private readonly ComboBox _branch = CreateLookup();
        private readonly TextBox _reason = WpfItcmDialogService.CreateTextBox();
        private readonly TextBlock _employee = new TextBlock { FontSize = 17, FontWeight = FontWeights.SemiBold, TextWrapping = TextWrapping.Wrap };
        private readonly TextBlock _current = new TextBlock { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 6, 0, 16) };
        private readonly TextBlock _status = new TextBlock { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 14, 0, 0) };
        private readonly Button _save;
        private readonly Button _cancel;
        private EmployeeLocationDto _snapshot;
        private EmployeeLocationOptions _options;
        private bool _populating;
        private bool _saving;
        private bool _closed;

        public EmployeeLocationDto SavedLocation { get; private set; }

        public WpfUpdateEmployeeLocationDialog(int employeeId)
        {
            _employeeId = employeeId;
            Title = "Update Employee Location";
            Width = 570;
            Height = 650;
            MinWidth = 480;
            MinHeight = 520;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            ShowInTaskbar = false;
            FontFamily = new FontFamily("Segoe UI");
            Background = WpfItcmDialogService.BrushFromRgb(241, 245, 249);
            var root = new DockPanel();
            Content = root;
            var footer = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(20, 12, 20, 16) };
            DockPanel.SetDock(footer, Dock.Bottom);
            root.Children.Add(footer);
            _save = WpfItcmDialogService.CreateButton("Save Location", WpfItcmDialogService.BrushFromRgb(13, 148, 136));
            _cancel = WpfItcmDialogService.CreateButton("Cancel", WpfItcmDialogService.BrushFromRgb(71, 85, 105));
            _cancel.IsCancel = true;
            footer.Children.Add(_save);
            footer.Children.Add(_cancel);
            var body = new StackPanel { Margin = new Thickness(24) };
            root.Children.Add(new ScrollViewer { Content = body, VerticalScrollBarVisibility = ScrollBarVisibility.Auto });
            body.Children.Add(_employee);
            body.Children.Add(_current);
            body.Children.Add(new TextBlock
            {
                Text = "Update the employee's shared profile. Existing tickets keep their recorded location.",
                TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 16)
            });
            AddField(body, "Company *", _company);
            AddField(body, "Department", _department);
            AddField(body, "Branch *", _branch);
            _reason.MaxLength = 1000;
            _reason.AcceptsReturn = true;
            _reason.TextWrapping = TextWrapping.Wrap;
            _reason.MinHeight = 70;
            AddField(body, "Reason (optional)", _reason);
            body.Children.Add(_status);
            _company.SelectionChanged += (_, __) => { if (!_populating) PopulateDepartments(null, null); };
            _department.SelectionChanged += (_, __) => { if (!_populating) PopulateBranches(null); };
            foreach (var combo in new[] { _company, _department, _branch })
            {
                // ComboBox raises SelectionChanged before it synchronizes its editable text.
                combo.SelectionChanged += (_, __) => QueueSaveStateUpdate();
                combo.AddHandler(TextBoxBase.TextChangedEvent, new TextChangedEventHandler((_, __) => QueueSaveStateUpdate()));
            }
            _save.Click += async (_, __) => await SaveAsync();
            _cancel.Click += (_, __) => Close();
            Closing += (_, e) => { if (_saving) e.Cancel = true; };
            Closed += (_, __) => _closed = true;
            Loaded += async (_, __) => await LoadAsync();
            SetBusy(true);
        }

        private static ComboBox CreateLookup() => new ComboBox
        {
            DisplayMemberPath = "Name", SelectedValuePath = "Id", IsEditable = true,
            IsTextSearchEnabled = true, MinHeight = 34, Padding = new Thickness(8, 4, 8, 4), Background = Brushes.White
        };

        private static void AddField(Panel body, string label, UIElement control)
        {
            body.Children.Add(new TextBlock { Text = label, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 10, 0, 5) });
            body.Children.Add(control);
        }

        private async Task LoadAsync()
        {
            SetStatus("Loading employee location...", false);
            try
            {
                var locationTask = _repository.GetLocationAsync(_employeeId);
                var optionsTask = _repository.GetLocationOptionsAsync();
                await Task.WhenAll(locationTask, optionsTask);
                if (_closed) return;
                _options = optionsTask.Result;
                ShowSnapshot(locationTask.Result);
                SetStatus(_snapshot.Active ? "Type in a field to find an available location." : "This employee is inactive and cannot be updated.", !_snapshot.Active);
            }
            catch (Exception ex) { if (!_closed) SetStatus("Unable to load employee location. " + ex.Message, true); }
            finally { if (!_closed) SetBusy(false); }
        }

        private void ShowSnapshot(EmployeeLocationDto location)
        {
            if (location == null) throw new InvalidOperationException("This employee is no longer available.");
            _snapshot = location;
            _employee.Text = location.Name + (string.IsNullOrWhiteSpace(location.EmployeeNumber) ? "" : " (" + location.EmployeeNumber + ")");
            _current.Text = "Current location: " + location.LocationDisplay;
            _populating = true;
            try
            {
                _company.ItemsSource = _options.Companies;
                _company.SelectedValue = location.CompanyId;
                if (_company.SelectedItem == null) _company.Text = location.CompanyName ?? "";
            }
            finally { _populating = false; }
            PopulateDepartments(location.DepartmentId, location.BranchId);
        }

        private int? SelectedId(ComboBox combo)
        {
            var item = combo.SelectedItem as LookupItem;
            return item == null ? null : (int?)item.Id;
        }

        private static bool MatchesSelection(ComboBox combo) => combo.SelectedItem is LookupItem item
            && string.Equals(combo.Text, item.Name, StringComparison.CurrentCultureIgnoreCase);

        private void QueueSaveStateUpdate() => Dispatcher.BeginInvoke(new Action(() => { if (!_closed) UpdateSaveState(); }));

        private void PopulateDepartments(int? selectedDepartment, int? selectedBranch)
        {
            if (_options == null) return;
            var mappings = _options.Mappings.Where(m => m.CompanyId == SelectedId(_company)).ToList();
            var items = mappings.Where(m => m.DepartmentId.HasValue)
                .GroupBy(m => m.DepartmentId.Value).Select(g => new LookupItem { Id = g.Key, Name = g.First().DepartmentName }).OrderBy(x => x.Name).ToList();
            if (mappings.Any(m => !m.DepartmentId.HasValue)) items.Insert(0, new LookupItem { Id = 0, Name = "(No department)" });
            _populating = true;
            try
            {
                _department.ItemsSource = items;
                _department.SelectedValue = selectedDepartment ?? 0;
                if (_department.SelectedItem == null) _department.Text = "";
            }
            finally { _populating = false; }
            PopulateBranches(selectedBranch);
        }

        private void PopulateBranches(int? selectedBranch)
        {
            if (_options == null) return;
            int? dept = SelectedId(_department);
            var items = _options.Mappings.Where(m => m.CompanyId == SelectedId(_company)
                    && dept.HasValue && m.DepartmentId == (dept.Value == 0 ? (int?)null : dept))
                .GroupBy(m => m.BranchId).Select(g => new LookupItem { Id = g.Key, Name = g.First().BranchName }).OrderBy(x => x.Name).ToList();
            _populating = true;
            try
            {
                _branch.ItemsSource = items;
                _branch.SelectedValue = selectedBranch;
                if (_branch.SelectedItem == null) _branch.Text = "";
            }
            finally { _populating = false; }
            UpdateSaveState();
        }

        private void UpdateSaveState()
        {
            if (_save == null || _populating) return;
            int? company = SelectedId(_company), department = SelectedId(_department), branch = SelectedId(_branch);
            int? dept = department == 0 ? null : department;
            bool valid = _options != null && company.HasValue && department.HasValue && branch.HasValue
                && MatchesSelection(_company) && MatchesSelection(_department) && MatchesSelection(_branch)
                && _options.Mappings.Any(m => m.CompanyId == company && m.DepartmentId == dept && m.BranchId == branch);
            _save.IsEnabled = !_saving && _snapshot?.Active == true && EmployeeRepository.CanUpdateLocation && valid
                && (_snapshot.CompanyId != company || _snapshot.DepartmentId != dept || _snapshot.BranchId != branch);
        }

        private void SetBusy(bool busy)
        {
            _saving = busy;
            _company.IsEnabled = _department.IsEnabled = _branch.IsEnabled = _reason.IsEnabled = !busy;
            _cancel.IsEnabled = !busy;
            UpdateSaveState();
        }

        private void SetStatus(string message, bool error)
        {
            _status.Text = message;
            _status.Foreground = WpfItcmDialogService.BrushFromRgb(error ? (byte)185 : (byte)71, error ? (byte)28 : (byte)85, error ? (byte)28 : (byte)105);
        }

        private async Task SaveAsync()
        {
            UpdateSaveState();
            if (!_save.IsEnabled) return;
            int? department = SelectedId(_department);
            var change = new EmployeeLocationUpdate
            {
                EmpId = _snapshot.EmpId, CompanyId = SelectedId(_company).Value,
                DepartmentId = department == 0 ? null : department, BranchId = SelectedId(_branch).Value,
                ExpectedRowVersion = _snapshot.RowVersion, Reason = _reason.Text
            };
            SetBusy(true);
            SetStatus("Saving employee location...", false);
            try
            {
                SavedLocation = await _repository.UpdateLocationAsync(change);
                SetBusy(false);
                DialogResult = true;
            }
            catch (EmployeeLocationConflictException ex)
            {
                ShowSnapshot(ex.Latest);
                SetStatus("Another user changed this employee. The latest location is shown. Review it and select the intended location again before saving.", true);
            }
            catch (Exception ex) { SetStatus("Location was not saved. " + ex.Message, true); }
            finally { if (!_closed) SetBusy(false); }
        }
    }
}
