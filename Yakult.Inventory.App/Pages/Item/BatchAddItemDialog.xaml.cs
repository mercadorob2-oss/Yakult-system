using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using Yakult.Inventory.App.Core;
using Yakult.Inventory.App.Models;
using Yakult.Inventory.App.Pages.Category;
using Yakult.Inventory.App.Pages.Vendor;
using Yakult.Inventory.App.Repositories;
using Yakult.Inventory.App.Security;
using Yakult.Inventory.App.Session;

// Disambiguate MessageBox — callers may have both namespaces open
using WinMsgBox = System.Windows.MessageBox;
using WinForms  = System.Windows.Forms;

namespace Yakult.Inventory.App.Pages.Item
{
    public partial class BatchAddItemDialog : Window, IDisposable
    {
        // ── Static instance tracker (replaces Application.OpenForms lookup) ──────
        public static BatchAddItemDialog CurrentInstance { get; private set; }

        // ── Re-entry guard ───────────────────────────────────────────────────────
        private volatile bool _isProcessing = false;

        // ── Suppress flags (prevent cascading events during programmatic updates) ─
        private bool _suppressModelTextChanged  = false;
        private bool _suppressComboBoxEvents    = false;

        // ── Session counter ───────────────────────────────────────────────────────
        private int _totalItemsAdded = 0;

        /// <summary>Every ItemId successfully inserted this session, across every batch — lets a
        /// caller (e.g. the Repair Portal's "+ Add Item" button) pick up newly created item(s)
        /// without re-querying by name/serial.</summary>
        public List<int> AddedItemIds { get; } = new List<int>();

        // ── Cell phone batch rows (Category = CellPhone) ────────────────────────────
        // _cellPhoneRows always holds EVERY row across every page — it's the source of truth
        // for validation and saving. DgvCellPhones.ItemsSource is only ever a 10-row window
        // into it, rebuilt by RefreshCellPhoneRowsView() whenever rows change or the page flips.
        private readonly System.Collections.ObjectModel.ObservableCollection<CellPhoneRowItem> _cellPhoneRows
            = new System.Collections.ObjectModel.ObservableCollection<CellPhoneRowItem>();
        private const int CellPhoneRowsPerPage = 10;
        private int _cellPhoneCurrentPage = 1;

        // Set when this dialog is opened in "copy mode" (EditItemDialog's "Add a Copy" button) —
        // every field except SerialNumber/CellPhoneNumber/IMEI1/IMEI2 is pre-filled from it.
        private readonly ItemDto _copySourceItem;

        // Pre-checks "Available for borrowing" when opened from the Borrow Items picker's
        // "Add New Item" button, so items added there show up in the picker without an extra step.
        private readonly bool _presetBorrowable;

        // ─────────────────────────────────────────────────────────────────────────
        public BatchAddItemDialog(bool presetBorrowable = false)
        {
            InitializeComponent();
            _presetBorrowable = presetBorrowable;
            Loaded  += OnLoaded;
            Closed  += (s, e) => { if (CurrentInstance == this) CurrentInstance = null; };
            Activated += (s, e) => CurrentInstance = this;
        }

        public BatchAddItemDialog(ItemDto copySourceItem) : this()
        {
            _copySourceItem = copySourceItem;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            // Populate static dropdowns (strings only — no ComboBoxItem wrappers needed)
            CmbItemType.Items.Add("Hardware");
            CmbItemType.Items.Add("Software/License");
            CmbItemType.Items.Add("Services");

            CmbSubType.Items.Add(""); // blank = no default Sub-Type
            foreach (var subType in ItemSubTypeCatalog.ValidSubTypes)
                CmbSubType.Items.Add(subType);
            CmbSubType.SelectedIndex = 0;

            CmbCartridgeOrigin.Items.Add("Brand New");
            CmbCartridgeOrigin.Items.Add("Refilled");

            RefreshCellPhoneRowsView();

            LoadCategories();
            LoadConditions();
            LoadVendors();

            // Trigger default state (Hardware defaults)
            CmbItemType.SelectedIndex = 0;

            if (_presetBorrowable)
                ChkIsBorrowable.IsChecked = true;

            if (_copySourceItem != null)
                ApplyCopySourceItem(_copySourceItem);
        }

        // Pre-fills every applicable field from an existing item ("Add a Copy" from
        // EditItemDialog). Serial Number, Cell Phone Number, IMEI1 and IMEI2 are deliberately
        // left untouched — they start blank like any new batch and must pass the normal
        // uniqueness validation (including against the item being copied).
        private void ApplyCopySourceItem(ItemDto item)
        {
            TxtItemName.Text = item.Name ?? string.Empty;
            TxtDescription.Text = item.Description ?? string.Empty;
            TxtModelNumber.Text = item.ModelNumber ?? string.Empty;

            for (int i = 0; i < CmbCategory.Items.Count; i++)
            {
                if (CmbCategory.Items[i] is CategoryItem cat &&
                    string.Equals(cat.Name, item.Category, StringComparison.OrdinalIgnoreCase))
                { CmbCategory.SelectedIndex = i; break; }
            }

            for (int i = 0; i < CmbSubType.Items.Count; i++)
            {
                if (string.Equals(CmbSubType.Items[i] as string, item.SubType, StringComparison.OrdinalIgnoreCase))
                { CmbSubType.SelectedIndex = i; break; }
            }

            for (int i = 0; i < CmbItemType.Items.Count; i++)
            {
                if (string.Equals(CmbItemType.Items[i]?.ToString(), item.ItemType, StringComparison.OrdinalIgnoreCase))
                { CmbItemType.SelectedIndex = i; break; }
            }

            bool isCartridge = string.Equals(item.Category, "Cartridge", StringComparison.OrdinalIgnoreCase);
            if (isCartridge)
            {
                CmbCartridgeOrigin.SelectedIndex =
                    string.Equals(item.RefillStatus, "Available", StringComparison.OrdinalIgnoreCase) ? 1 : 0;
                LoadCartridgeModels(item.ModelNumber, item.VendorId);
            }

            if (CanonicalConsumableCategory(item.Category) != null)
                LoadConsumableModels(item.ModelNumber);

            CmbUnitOfMeasure.Text = item.UnitOfMeasure;
            TxtAmount.Text = item.Amount.ToString("0.00");

            for (int i = 0; i < CmbCondition.Items.Count; i++)
            {
                if (CmbCondition.Items[i] is ConditionItem cond && cond.ConditionId == item.ConditionId)
                { CmbCondition.SelectedIndex = i; break; }
            }

            if (item.VendorId.HasValue && item.VendorId.Value > 0)
                SelectVendorInCombo(item.VendorId.Value);

            TxtRemarks.Text = item.Remarks ?? string.Empty;
            TxtLicenseNumber.Text = item.LicenseNumber ?? string.Empty;
            TxtPartNumber.Text = item.PartNumber ?? string.Empty;
            TxtWarrantyYears.Text = item.WarrantyYears.ToString();
            if (item.WarrantyStartDate.HasValue)
            {
                ChkWarrantyStart.IsChecked = true;
                DpWarrantyStart.SelectedDate = item.WarrantyStartDate.Value;
            }
            if (item.WarrantyEndDate.HasValue)
            {
                ChkWarrantyEnd.IsChecked = true;
                DpWarrantyEnd.SelectedDate = item.WarrantyEndDate.Value;
            }
            ChkIsTrackedAsset.IsChecked = item.IsTrackedAsset;
            ChkIsBorrowable.IsChecked = item.IsBorrowable == true || _presetBorrowable;

            if (item.DatePurchased.HasValue)
            {
                ChkDatePurchased.IsChecked = true;
                DpDatePurchased.SelectedDate = item.DatePurchased.Value;
            }

            if (item.StartDate.HasValue) DpStartDate.SelectedDate = item.StartDate.Value;
            if (item.EndDate.HasValue)
            {
                DpEndDate.SelectedDate = item.EndDate.Value;
                ChkServiceContractDates.IsChecked = true;
            }

            // Quantity always starts at 1 (default from XAML) — the user can raise it if
            // they want more than one additional copy. Serial Number / cell phone rows are
            // left at their default blank state.
        }

        // ─────────────────────────────────────────────────────────────────────────
        // IDisposable — no-op; required for WinForms `using` compatibility
        // ─────────────────────────────────────────────────────────────────────────
        public void Dispose() { }

        // ─────────────────────────────────────────────────────────────────────────
        // ShowDialog overloads — shadow WPF's bool? version and return WinForms type
        // ─────────────────────────────────────────────────────────────────────────
        public new WinForms.DialogResult ShowDialog()
        {
            bool? result = base.ShowDialog();
            return (result == true) ? WinForms.DialogResult.OK : WinForms.DialogResult.Cancel;
        }

        public WinForms.DialogResult ShowDialog(WinForms.IWin32Window owner)
        {
            if (owner != null)
                new System.Windows.Interop.WindowInteropHelper(this).Owner = owner.Handle;
            return ShowDialog();
        }

        // ─────────────────────────────────────────────────────────────────────────
        // Header drag / close
        // ─────────────────────────────────────────────────────────────────────────
        private void OnHeaderDrag(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left && e.ClickCount == 1)
            {
                // Double-click on header toggles maximize
                DragMove();
            }
        }

        private void OnMinimizeClick(object sender, MouseButtonEventArgs e)
        {
            e.Handled = true;
            // Minimizes without handing activation to another program (which is what made
            // the whole app look minimized); see ModalMinimizeGuard.
            Yakult.Inventory.App.Helpers.ModalMinimizeGuard.Minimize(this);
        }

        private void OnMaximizeClick(object sender, MouseButtonEventArgs e)
        {
            e.Handled = true;
            if (WindowState == WindowState.Maximized)
                WindowState = WindowState.Normal;
            else
                WindowState = WindowState.Maximized;
        }

        protected override void OnStateChanged(EventArgs e)
        {
            base.OnStateChanged(e);
            BtnMaximizeGlyph.Text = WindowState == WindowState.Maximized ? "❐" : "□";
        }

        // WindowStyle="None" + ResizeMode="CanResizeWithGrip" windows keep the invisible
        // resize-frame Windows normally reserves for sizable windows. When such a window is
        // maximized, Windows insets it by that frame's thickness on every edge instead of
        // filling the actual work area, leaving a visible gap on the right/bottom (and a
        // sliver on the top/left). SystemParameters.WorkArea + MaxWidth/MaxHeight (the
        // previous approach here) doesn't compensate for that inset. The standard fix is to
        // intercept WM_GETMINMAXINFO and report the true monitor work area ourselves.
        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            if (PresentationSource.FromVisual(this) is HwndSource hwndSource)
                hwndSource.AddHook(WmGetMinMaxInfoHook);
        }

        private IntPtr WmGetMinMaxInfoHook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            const int WM_GETMINMAXINFO = 0x0024;
            if (msg == WM_GETMINMAXINFO)
            {
                var mmi = (NativeMethods.MINMAXINFO)Marshal.PtrToStructure(lParam, typeof(NativeMethods.MINMAXINFO));

                var monitor = NativeMethods.MonitorFromWindow(hwnd, NativeMethods.MONITOR_DEFAULTTONEAREST);
                if (monitor != IntPtr.Zero)
                {
                    var monitorInfo = new NativeMethods.MONITORINFO();
                    monitorInfo.cbSize = Marshal.SizeOf(typeof(NativeMethods.MONITORINFO));
                    NativeMethods.GetMonitorInfo(monitor, ref monitorInfo);

                    var workArea    = monitorInfo.rcWork;
                    var monitorArea = monitorInfo.rcMonitor;

                    mmi.ptMaxPosition.X = Math.Abs(workArea.Left - monitorArea.Left);
                    mmi.ptMaxPosition.Y = Math.Abs(workArea.Top - monitorArea.Top);
                    mmi.ptMaxSize.X     = workArea.Right - workArea.Left;
                    mmi.ptMaxSize.Y     = workArea.Bottom - workArea.Top;
                    mmi.ptMaxTrackSize  = mmi.ptMaxSize;
                }

                Marshal.StructureToPtr(mmi, lParam, true);
                handled = true;
            }
            return IntPtr.Zero;
        }

        private static class NativeMethods
        {
            public const int MONITOR_DEFAULTTONEAREST = 0x00000002;

            [StructLayout(LayoutKind.Sequential)]
            public struct POINT { public int X; public int Y; }

            [StructLayout(LayoutKind.Sequential)]
            public struct RECT { public int Left, Top, Right, Bottom; }

            [StructLayout(LayoutKind.Sequential)]
            public struct MINMAXINFO
            {
                public POINT ptReserved;
                public POINT ptMaxSize;
                public POINT ptMaxPosition;
                public POINT ptMinTrackSize;
                public POINT ptMaxTrackSize;
            }

            [StructLayout(LayoutKind.Sequential)]
            public struct MONITORINFO
            {
                public int cbSize;
                public RECT rcMonitor;
                public RECT rcWork;
                public int dwFlags;
            }

            [DllImport("user32.dll")]
            public static extern IntPtr MonitorFromWindow(IntPtr handle, int flags);

            [DllImport("user32.dll")]
            public static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO lpmi);
        }

        private void OnCloseClick(object sender, MouseButtonEventArgs e)
        {
            e.Handled = true;
            DialogResult = _totalItemsAdded > 0;
        }

        // ─────────────────────────────────────────────────────────────────────────
        // Data loading
        // ─────────────────────────────────────────────────────────────────────────
        private void LoadCategories()
        {
            try
            {
                CmbCategory.Items.Clear();
                using (var con = new System.Data.SqlClient.SqlConnection(DatabaseConfig.ConnectionString))
                {
                    con.Open();
                    using (var cmd = new System.Data.SqlClient.SqlCommand(
                        "SELECT CategoryId, Name FROM dbo.ItemCategory WHERE Active = 1 ORDER BY Name", con))
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            string name = reader.GetString(1);
                            if (!PermissionResolver.CanUseItemCategory(name)) continue;
                            CmbCategory.Items.Add(new CategoryItem { CategoryId = reader.GetInt32(0), Name = name });
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                WinMsgBox.Show($"Failed to load categories: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void LoadConditions()
        {
            try
            {
                CmbCondition.Items.Clear();
                using (var con = new System.Data.SqlClient.SqlConnection(DatabaseConfig.ConnectionString))
                {
                    con.Open();
                    using (var cmd = new System.Data.SqlClient.SqlCommand(
                        "SELECT ConditionId, ConditionName FROM dbo.Condition ORDER BY ConditionId", con))
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                            CmbCondition.Items.Add(new ConditionItem { ConditionId = reader.GetInt32(0), ConditionName = reader.GetString(1) });
                    }
                }

                // Default to "Good"
                for (int i = 0; i < CmbCondition.Items.Count; i++)
                {
                    if (CmbCondition.Items[i] is ConditionItem ci &&
                        ci.ConditionName.Equals("Good", StringComparison.OrdinalIgnoreCase))
                    { CmbCondition.SelectedIndex = i; break; }
                }
                if (CmbCondition.SelectedIndex < 0 && CmbCondition.Items.Count > 0)
                    CmbCondition.SelectedIndex = 0;
            }
            catch (Exception ex)
            {
                WinMsgBox.Show($"Failed to load conditions: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void LoadVendors()
        {
            try
            {
                CmbVendor.Items.Clear();
                CmbVendor.Items.Add(new VendorItem { VendorId = 0, VendorName = "(None)" });

                using (var con = new System.Data.SqlClient.SqlConnection(DatabaseConfig.ConnectionString))
                {
                    con.Open();
                    const string sql = @"
                        SELECT v.VendorID, v.VendorName
                        FROM dbo.Vendor v
                        LEFT JOIN dbo.ArchiveStatus arc ON arc.EntityType = 'Vendor' AND arc.EntityId = v.VendorID
                        WHERE v.IsActive = 1 AND arc.ArchiveId IS NULL
                        ORDER BY v.VendorName";
                    using (var cmd = new System.Data.SqlClient.SqlCommand(sql, con))
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                            CmbVendor.Items.Add(new VendorItem { VendorId = reader.GetInt32(0), VendorName = reader.GetString(1) });
                    }
                }

                if (CmbVendor.Items.Count > 0) CmbVendor.SelectedIndex = 0;
            }
            catch (Exception ex)
            {
                WinMsgBox.Show($"Failed to load vendors: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void LoadCartridgeModels(string selectModelNumber = null, int? selectVendorId = null)
        {
            try
            {
                CmbCartridgeModel.Items.Clear();
                CmbCartridgeModel.IsEnabled = false;
                CmbCartridgeModel.Items.Add(new CartridgeModelItem
                {
                    CartridgeModelId = 0,
                    ModelNumber = "(Select cartridge model)",
                    VendorName = null,
                    VendorId = null
                });

                var repo = new CartridgeModelRepository();
                var models = await repo.GetAllActiveModelsAsync();
                foreach (var m in models)
                    CmbCartridgeModel.Items.Add(new CartridgeModelItem
                    {
                        CartridgeModelId = m.CartridgeModelId,
                        ModelNumber = m.ModelNumber,
                        VendorName = m.VendorName,
                        VendorId = m.VendorId
                    });

                if (!string.IsNullOrWhiteSpace(selectModelNumber))
                {
                    for (int i = 0; i < CmbCartridgeModel.Items.Count; i++)
                    {
                        if (CmbCartridgeModel.Items[i] is CartridgeModelItem item
                            && string.Equals(item.ModelNumber?.Trim(), selectModelNumber.Trim(), StringComparison.OrdinalIgnoreCase))
                        { CmbCartridgeModel.SelectedIndex = i; break; }
                    }
                }

                if (CmbCartridgeModel.SelectedIndex < 0 && CmbCartridgeModel.Items.Count > 0)
                    CmbCartridgeModel.SelectedIndex = 0;

                if (selectVendorId.HasValue && selectVendorId.Value > 0)
                    SelectVendorInCombo(selectVendorId.Value);

                CmbCartridgeModel.IsEnabled = true;
            }
            catch (Exception ex)
            {
                CmbCartridgeModel.IsEnabled = true;
                WinMsgBox.Show($"Failed to load cartridge models: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // Categories in scope for the ConsumableModel grouping — matches the fuzzy
        // (case/space-insensitive) filter used in RequestRepository.FulfillmentTrackedRequestsCte.
        private static string CanonicalConsumableCategory(string categoryName)
        {
            if (string.IsNullOrWhiteSpace(categoryName)) return null;
            var normalized = categoryName.Replace(" ", "").ToLowerInvariant();
            if (normalized.Contains("ink")) return "Ink";
            if (normalized.Contains("toner")) return "Toner";
            if (normalized.Contains("printhead")) return "Print Head";
            return null;
        }

        private async void LoadConsumableModels(string selectModelNumber = null)
        {
            try
            {
                CmbConsumableModel.Items.Clear();
                CmbConsumableModel.IsEnabled = false;
                CmbConsumableModel.Items.Add(new ConsumableModelItem
                {
                    ConsumableModelId = 0,
                    ModelNumber = "(Select model)"
                });

                string category = CanonicalConsumableCategory((CmbCategory.SelectedItem as CategoryItem)?.Name);

                var repo = new ConsumableModelRepository();
                var models = await repo.GetAllActiveModelsAsync(category);
                foreach (var m in models)
                    CmbConsumableModel.Items.Add(new ConsumableModelItem
                    {
                        ConsumableModelId = m.ConsumableModelId,
                        ModelNumber = m.ModelNumber
                    });

                if (!string.IsNullOrWhiteSpace(selectModelNumber))
                {
                    for (int i = 0; i < CmbConsumableModel.Items.Count; i++)
                    {
                        if (CmbConsumableModel.Items[i] is ConsumableModelItem item
                            && string.Equals(item.ModelNumber?.Trim(), selectModelNumber.Trim(), StringComparison.OrdinalIgnoreCase))
                        { CmbConsumableModel.SelectedIndex = i; break; }
                    }
                }

                if (CmbConsumableModel.SelectedIndex < 0 && CmbConsumableModel.Items.Count > 0)
                    CmbConsumableModel.SelectedIndex = 0;

                CmbConsumableModel.IsEnabled = true;
            }
            catch (Exception ex)
            {
                CmbConsumableModel.IsEnabled = true;
                WinMsgBox.Show($"Failed to load consumable models: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void SelectVendorInCombo(int vendorId)
        {
            for (int i = 0; i < CmbVendor.Items.Count; i++)
            {
                if (CmbVendor.Items[i] is VendorItem v && v.VendorId == vendorId)
                { CmbVendor.SelectedIndex = i; return; }
            }
        }

        // ─────────────────────────────────────────────────────────────────────────
        // ComboBox event handlers
        // ─────────────────────────────────────────────────────────────────────────

        // A closed ComboBox still consumes the mouse wheel to change its selected value,
        // which caused accidental field changes while users scrolled through the dialog.
        // Swallow the wheel event here and re-raise it on the parent so the dialog's
        // ScrollViewer scrolls instead. Only suppressed while the dropdown is closed --
        // scrolling an open dropdown list still works normally.
        private void FormCombo_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            var combo = (System.Windows.Controls.ComboBox)sender;
            if (combo.IsDropDownOpen)
                return;

            e.Handled = true;

            if (combo.Parent is UIElement parent)
            {
                var bubbled = new MouseWheelEventArgs(e.MouseDevice, e.Timestamp, e.Delta)
                {
                    RoutedEvent = UIElement.MouseWheelEvent,
                    Source = combo
                };
                parent.RaiseEvent(bubbled);
            }
        }

        private void CmbItemType_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (_suppressComboBoxEvents) return;

            var selectedType = CmbItemType.SelectedItem?.ToString();
            bool isSoftware = string.Equals(selectedType, "Software/License", StringComparison.OrdinalIgnoreCase);
            bool isServices = string.Equals(selectedType, "Services", StringComparison.OrdinalIgnoreCase);

            // Services date toggle visibility
            RowServiceDateToggle.Visibility = isServices ? Visibility.Visible : Visibility.Collapsed;
            if (!isServices) ChkServiceContractDates.IsChecked = false;

            // Fixed asset defaults
            if (isServices)
            {
                ChkIsTrackedAsset.IsChecked = true;
                ChkIsTrackedAsset.IsEnabled = false;
            }
            else
            {
                ChkIsTrackedAsset.IsEnabled = true;
                ChkIsTrackedAsset.IsChecked = isSoftware;
            }

            UpdateContractFieldsVisibility();

            // Warranty rows. For Software/License the warranty period is derived from
            // Start/End Date (the license period) instead — same reason the manual
            // Warranty Start/End override hides there too, matching Edit Item.
            if (isSoftware)
            {
                RowWarrantyYears.Visibility     = Visibility.Collapsed;
                RowWarrantyStart.Visibility     = Visibility.Collapsed;
                RowWarrantyEnd.Visibility       = Visibility.Collapsed;
                RowWarrantyInfoBand.Visibility  = Visibility.Visible;
                UpdateWarrantyDisplay();
            }
            else
            {
                RowWarrantyYears.Visibility    = Visibility.Visible;
                RowWarrantyStart.Visibility    = Visibility.Visible;
                RowWarrantyEnd.Visibility      = Visibility.Visible;
                RowWarrantyInfoBand.Visibility = Visibility.Collapsed;
            }

            // Unit of Measure options
            _suppressComboBoxEvents = true;
            try
            {
                CmbUnitOfMeasure.Items.Clear();
                if (isSoftware)
                {
                    foreach (var s in new[] { "Monthly", "Annually", "One Time" })
                        CmbUnitOfMeasure.Items.Add(s);
                }
                else if (isServices)
                {
                    CmbUnitOfMeasure.Items.Add("Contract");
                }
                else
                {
                    foreach (var s in new[] { "Unit", "Piece", "Box", "Pack", "Set" })
                        CmbUnitOfMeasure.Items.Add(s);
                }
            }
            finally { _suppressComboBoxEvents = false; }

            if (CmbUnitOfMeasure.Items.Count > 0)
                CmbUnitOfMeasure.SelectedIndex = 0;
        }

        private void CmbSubType_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (_suppressComboBoxEvents) return;
            UpdateContractFieldsVisibility();
        }

        /// <summary>
        /// Contract period (Start/End Date) and a reference number are available for every
        /// Item Type — Hardware included, unconditionally, not just when a Sub-Type is picked
        /// (a Hardware item can be invoiced under a Contract, or simply have a known period).
        /// Services still gates dates behind its own opt-in checkbox, since most Services
        /// items don't have a period; everything else (Hardware, Software/License) always
        /// shows the fields. Sub-Type only changes the reference field's label, not whether
        /// these fields are shown.
        /// </summary>
        private void UpdateContractFieldsVisibility()
        {
            var selectedType = CmbItemType.SelectedItem?.ToString();
            bool isSoftware = string.Equals(selectedType, "Software/License", StringComparison.OrdinalIgnoreCase);
            bool isServices = string.Equals(selectedType, "Services", StringComparison.OrdinalIgnoreCase);
            bool isHardware = string.Equals(selectedType, "Hardware", StringComparison.OrdinalIgnoreCase);

            bool showContractDates = isSoftware || isHardware || (isServices && ChkServiceContractDates.IsChecked == true);
            RowContractDates.Visibility = showContractDates ? Visibility.Visible : Visibility.Collapsed;

            bool showLicense = isSoftware || isHardware || isServices;
            RowLicenseNumber.Visibility = showLicense ? Visibility.Visible : Visibility.Collapsed;
        }

        private void CmbCategory_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (_suppressComboBoxEvents) return;

            var cat = CmbCategory.SelectedItem as CategoryItem;
            bool isCartridge = cat != null &&
                string.Equals(cat.Name, "Cartridge", StringComparison.OrdinalIgnoreCase);

            RowCartridgeModel.Visibility  = isCartridge ? Visibility.Visible  : Visibility.Collapsed;
            RowCartridgeOrigin.Visibility = isCartridge ? Visibility.Visible  : Visibility.Collapsed;

            if (isCartridge && CmbCartridgeOrigin.SelectedIndex < 0)
                CmbCartridgeOrigin.SelectedIndex = 0;

            // Skip the auto-load while applying a copy-source item — ApplyCopySourceItem()
            // makes its own LoadCartridgeModels() call with the correct model to select, and
            // two concurrent async loads would race to populate the combo.
            if (isCartridge && CmbCartridgeModel.Items.Count == 0 && _copySourceItem == null)
                LoadCartridgeModels();

            bool isConsumable = CanonicalConsumableCategory(cat?.Name) != null;
            RowConsumableModel.Visibility = isConsumable ? Visibility.Visible : Visibility.Collapsed;

            // Unlike Cartridge (a single category with one fixed model list), Ink/Toner/Print
            // Head all share this same dropdown — the list must be reloaded on every category
            // change so it reflects the newly selected category, not just the first time it's
            // shown. ApplyCopySourceItem() makes its own LoadConsumableModels() call when
            // copying an existing item, so skip the automatic reload in that case.
            if (isConsumable && _copySourceItem == null)
                LoadConsumableModels();

            // Cell phone rows replace the plain Quantity / Serial Numbers entry —
            // each cell phone needs its own phone number and IMEI(s).
            bool isCellPhone = IsCellPhoneCategory(cat);

            RowQuantity.Visibility           = isCellPhone ? Visibility.Collapsed : Visibility.Visible;
            RowSerialNumbersEntry.Visibility = isCellPhone ? Visibility.Collapsed : Visibility.Visible;
            RowPreviewSection.Visibility     = isCellPhone ? Visibility.Collapsed : Visibility.Visible;
            RowCellPhoneEntries.Visibility   = isCellPhone ? Visibility.Visible   : Visibility.Collapsed;

            if (isCellPhone && _cellPhoneRows.Count == 0)
            {
                _cellPhoneRows.Add(new CellPhoneRowItem());
                RefreshCellPhoneRowsView();
            }
        }

        private static bool IsCellPhoneCategory(CategoryItem cat)
            => cat != null && string.Equals(cat.Name?.Replace(" ", ""), "CellPhone", StringComparison.OrdinalIgnoreCase);

        private void BtnAddCellPhoneRow_Click(object sender, RoutedEventArgs e)
        {
            _cellPhoneRows.Add(new CellPhoneRowItem());
            _cellPhoneCurrentPage = int.MaxValue; // jump to the last page so the new row is visible
            RefreshCellPhoneRowsView();
        }

        private void BtnAddCellPhoneRows10_Click(object sender, RoutedEventArgs e)
        {
            for (int i = 0; i < 10; i++)
                _cellPhoneRows.Add(new CellPhoneRowItem());

            _cellPhoneCurrentPage = int.MaxValue; // jump to the last page so the new rows are visible
            RefreshCellPhoneRowsView();
        }

        private void BtnRemoveCellPhoneRow_Click(object sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.Button btn && btn.DataContext is CellPhoneRowItem row)
                _cellPhoneRows.Remove(row);

            RefreshCellPhoneRowsView();
        }

        // Deletes every ROW that has at least one selected/highlighted cell — distinct from
        // Delete-key clearing (which blanks cell contents but keeps the rows). Works whether
        // the user highlighted whole rows or just a range of cells within them.
        private void BtnDeleteCellPhoneRows_Click(object sender, RoutedEventArgs e)
        {
            var selectedCells = DgvCellPhones.SelectedCells;
            if (selectedCells == null || selectedCells.Count == 0) return;

            DgvCellPhones.CommitEdit(System.Windows.Controls.DataGridEditingUnit.Cell, true);
            DgvCellPhones.CommitEdit(System.Windows.Controls.DataGridEditingUnit.Row, true);

            var rowsToRemove = selectedCells
                .Select(c => c.Item)
                .OfType<CellPhoneRowItem>()
                .Distinct()
                .ToList();

            foreach (var row in rowsToRemove)
                _cellPhoneRows.Remove(row);

            RefreshCellPhoneRowsView();
        }

        private void BtnCellPhonePrevPage_Click(object sender, RoutedEventArgs e)
        {
            _cellPhoneCurrentPage--;
            RefreshCellPhoneRowsView();
        }

        private void BtnCellPhoneNextPage_Click(object sender, RoutedEventArgs e)
        {
            _cellPhoneCurrentPage++;
            RefreshCellPhoneRowsView();
        }

        // Renumbers every row (absolute position across all pages, not just the visible one),
        // clamps the current page against the row count, and rebinds the grid to just that
        // page's slice — _cellPhoneRows itself always keeps every row, on every page.
        private void RefreshCellPhoneRowsView()
        {
            for (int i = 0; i < _cellPhoneRows.Count; i++)
                _cellPhoneRows[i].RowNumber = i + 1;

            int totalRows = _cellPhoneRows.Count;
            int totalPages = Math.Max(1, (int)Math.Ceiling(totalRows / (double)CellPhoneRowsPerPage));
            if (_cellPhoneCurrentPage > totalPages) _cellPhoneCurrentPage = totalPages;
            if (_cellPhoneCurrentPage < 1) _cellPhoneCurrentPage = 1;

            DgvCellPhones.ItemsSource = _cellPhoneRows
                .Skip((_cellPhoneCurrentPage - 1) * CellPhoneRowsPerPage)
                .Take(CellPhoneRowsPerPage)
                .ToList();

            TxtCellPhonePageInfo.Text = $"Page {_cellPhoneCurrentPage} of {totalPages}  ({totalRows} row{(totalRows == 1 ? "" : "s")})";
            BtnCellPhonePrevPage.IsEnabled = _cellPhoneCurrentPage > 1;
            BtnCellPhoneNextPage.IsEnabled = _cellPhoneCurrentPage < totalPages;
        }

        // ── Excel-style copy/paste/delete for the Cell Phone Details grid ──────────
        // Mirrors UpdateCellphoneDetailsDialog's behavior: paste always anchors on the
        // selected cell (never the text caret), maps clipboard columns onto consecutive
        // editable grid columns, ignores cells beyond the available rows/columns, and
        // Delete clears every selected editable cell without needing to enter edit mode.
        // _cellPhoneRows always holds every row across every page, so a paste/delete can
        // freely touch rows outside the page currently on screen.

        private void DgvCellPhones_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.V && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
            {
                e.Handled = true;
                PasteCellPhoneRowsFromClipboard();
            }
            else if (e.Key == Key.Delete && Keyboard.Modifiers == ModifierKeys.None)
            {
                // While a cell's TextBox editor is focused, Delete should behave like a
                // normal TextBox (delete-forward character) — only clear whole cells when
                // a range is merely selected, not actively being typed into.
                if (!(Keyboard.FocusedElement is System.Windows.Controls.TextBox))
                {
                    e.Handled = true;
                    DeleteSelectedCellPhoneCells();
                }
            }
        }

        // Right-click doesn't move the grid's selection on its own, so Paste from the
        // context menu would target whatever cell was last active instead of the one
        // under the cursor.
        private void DgvCellPhones_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            var cell = FindVisualParent<System.Windows.Controls.DataGridCell>(e.OriginalSource as DependencyObject);
            if (cell == null) return;

            var row = FindVisualParent<System.Windows.Controls.DataGridRow>(cell);
            if (row?.Item == null) return;

            DgvCellPhones.CommitEdit(System.Windows.Controls.DataGridEditingUnit.Cell, true);
            DgvCellPhones.CommitEdit(System.Windows.Controls.DataGridEditingUnit.Row, true);

            var cellInfo = new System.Windows.Controls.DataGridCellInfo(row.Item, cell.Column);
            DgvCellPhones.SelectedCells.Clear();
            DgvCellPhones.SelectedCells.Add(cellInfo);
            DgvCellPhones.CurrentCell = cellInfo;
        }

        private void MnuPasteCellPhoneRows_Click(object sender, RoutedEventArgs e)
        {
            PasteCellPhoneRowsFromClipboard();
        }

        private void PasteCellPhoneRowsFromClipboard()
        {
            if (!System.Windows.Clipboard.ContainsText()) return;
            string text = System.Windows.Clipboard.GetText();
            if (string.IsNullOrEmpty(text)) return;

            // Exit edit mode BEFORE reading the anchor cell — paste always maps from the
            // selected cell, never from wherever the text caret was mid-edit.
            DgvCellPhones.CommitEdit(System.Windows.Controls.DataGridEditingUnit.Cell, true);
            DgvCellPhones.CommitEdit(System.Windows.Controls.DataGridEditingUnit.Row, true);

            // Map clipboard columns onto consecutive EDITABLE grid columns only — the "#"
            // row-number column and the remove-row button column are never destinations.
            var editableColumns = DgvCellPhones.Columns
                .OfType<System.Windows.Controls.DataGridBoundColumn>()
                .Where(c => !c.IsReadOnly && c.Binding is Binding)
                .OrderBy(c => c.DisplayIndex)
                .ToList();
            if (editableColumns.Count == 0) return;

            // Anchor on the top-left of the current selection (a drag-selected range's
            // CurrentCell is wherever the drag ended, not its top-left) — falls back to
            // CurrentCell for a plain single-cell click.
            System.Windows.Controls.DataGridColumn anchorColumn;
            int anchorPageRowIndex;

            var selectedCells = DgvCellPhones.SelectedCells;
            if (selectedCells != null && selectedCells.Count > 0)
            {
                anchorColumn = selectedCells.Select(c => c.Column).OrderBy(c => c.DisplayIndex).First();
                anchorPageRowIndex = selectedCells.Min(c => DgvCellPhones.Items.IndexOf(c.Item));
            }
            else if (DgvCellPhones.CurrentCell.Column != null && DgvCellPhones.CurrentCell.Item != null)
            {
                anchorColumn = DgvCellPhones.CurrentCell.Column;
                anchorPageRowIndex = DgvCellPhones.Items.IndexOf(DgvCellPhones.CurrentCell.Item);
            }
            else
            {
                return;
            }
            if (anchorPageRowIndex < 0) return;

            // The grid only ever displays one page — translate the page-local row index
            // back to its absolute position in the full _cellPhoneRows backing list.
            int startRow = (_cellPhoneCurrentPage - 1) * CellPhoneRowsPerPage + anchorPageRowIndex;

            int startEditableIndex = editableColumns.FindIndex(c => c.DisplayIndex >= anchorColumn.DisplayIndex);
            if (startEditableIndex < 0) return;

            var lines = text.Replace("\r\n", "\n").TrimEnd('\n').Split('\n');
            for (int r = 0; r < lines.Length; r++)
            {
                int targetRow = startRow + r;
                if (targetRow >= _cellPhoneRows.Count) break; // exceeds available rows — ignore the rest

                var values = lines[r].Split('\t');
                for (int c = 0; c < values.Length; c++)
                {
                    int editableIndex = startEditableIndex + c;
                    if (editableIndex >= editableColumns.Count) break; // exceeds available editable columns

                    var propertyName = ((Binding)editableColumns[editableIndex].Binding).Path.Path;
                    SetCellPhoneRowValue(_cellPhoneRows[targetRow], propertyName, values[c].Trim());
                }
            }

            RefreshCellPhoneRowsView();
        }

        // Excel's Delete key clears every selected cell's contents without needing to
        // enter edit mode first — mirrors UpdateCellphoneDetailsDialog's behavior for a
        // highlighted multi-cell range.
        private void DeleteSelectedCellPhoneCells()
        {
            var selectedCells = DgvCellPhones.SelectedCells;
            if (selectedCells == null || selectedCells.Count == 0) return;

            DgvCellPhones.CommitEdit(System.Windows.Controls.DataGridEditingUnit.Cell, true);
            DgvCellPhones.CommitEdit(System.Windows.Controls.DataGridEditingUnit.Row, true);

            foreach (var cellInfo in selectedCells)
            {
                if (!(cellInfo.Column is System.Windows.Controls.DataGridBoundColumn boundCol) || boundCol.IsReadOnly) continue;
                if (!(boundCol.Binding is Binding binding)) continue;
                if (!(cellInfo.Item is CellPhoneRowItem row)) continue;

                SetCellPhoneRowValue(row, binding.Path.Path, string.Empty);
            }

            RefreshCellPhoneRowsView();
        }

        private static void SetCellPhoneRowValue(CellPhoneRowItem row, string propertyName, string value)
        {
            switch (propertyName)
            {
                case "CellPhoneNumber": row.CellPhoneNumber = value; break;
                case "SerialNumber":    row.SerialNumber    = value; break;
                case "IMEI1":           row.IMEI1           = value; break;
                case "IMEI2":           row.IMEI2           = value; break;
            }
        }

        private static T FindVisualParent<T>(DependencyObject child) where T : DependencyObject
        {
            while (child != null && !(child is T))
                child = VisualTreeHelper.GetParent(child);
            return child as T;
        }

        private void CmbCartridgeModel_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            var cat = CmbCategory.SelectedItem as CategoryItem;
            bool isCartridge = cat != null &&
                string.Equals(cat.Name, "Cartridge", StringComparison.OrdinalIgnoreCase);
            if (!isCartridge) return;

            var modelItem = CmbCartridgeModel.SelectedItem as CartridgeModelItem;
            if (modelItem == null || modelItem.CartridgeModelId <= 0) return;

            var modelNumber = modelItem.ModelNumber?.Trim();
            if (!string.IsNullOrWhiteSpace(modelNumber))
            {
                var current = TxtModelNumber.Text?.Trim() ?? string.Empty;
                if (!string.Equals(current, modelNumber, StringComparison.OrdinalIgnoreCase))
                {
                    _suppressModelTextChanged = true;
                    try { TxtModelNumber.Text = modelNumber; }
                    finally { _suppressModelTextChanged = false; }
                }
            }

            if (modelItem.VendorId.HasValue && modelItem.VendorId.Value > 0)
            {
                var currentVendor = CmbVendor.SelectedItem as VendorItem;
                if (currentVendor == null || currentVendor.VendorId <= 0)
                    SelectVendorInCombo(modelItem.VendorId.Value);
            }
        }

        private void ChkServiceContractDates_Changed(object sender, RoutedEventArgs e)
        {
            // Re-run item-type logic to recalculate contract-date row visibility
            CmbItemType_SelectionChanged(sender, null);
        }

        // ─────────────────────────────────────────────────────────────────────────
        // TextBox event handlers
        // ─────────────────────────────────────────────────────────────────────────
        private void TxtModelNumber_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            if (_suppressModelTextChanged) return;

            var cat = CmbCategory.SelectedItem as CategoryItem;
            bool isCartridge = cat != null &&
                string.Equals(cat.Name, "Cartridge", StringComparison.OrdinalIgnoreCase);
            if (!isCartridge) return;

            var typed = TxtModelNumber.Text?.Trim();
            if (string.IsNullOrWhiteSpace(typed)) return;

            for (int i = 0; i < CmbCartridgeModel.Items.Count; i++)
            {
                if (CmbCartridgeModel.Items[i] is CartridgeModelItem item
                    && !string.IsNullOrWhiteSpace(item.ModelNumber)
                    && string.Equals(item.ModelNumber.Trim(), typed, StringComparison.OrdinalIgnoreCase))
                {
                    if (CmbCartridgeModel.SelectedIndex != i)
                        CmbCartridgeModel.SelectedIndex = i;
                    return;
                }
            }

            if (CmbCartridgeModel.Items.Count > 0 && CmbCartridgeModel.SelectedIndex != 0)
                CmbCartridgeModel.SelectedIndex = 0;
        }

        // ─────────────────────────────────────────────────────────────────────────
        // DatePicker event handler
        // ─────────────────────────────────────────────────────────────────────────
        private void DpLicenseDate_SelectedDateChanged(object sender,
            System.Windows.Controls.SelectionChangedEventArgs e)
        {
            UpdateWarrantyDisplay();
        }

        // Warranty Years is a display/reporting convenience derived from Start/End when both are
        // manually set — Start and End stay the source of truth the user edits directly, exactly
        // like Edit Item. Does not feed back the other way: typing Years never touches the dates.
        private void WarrantyDates_ValueChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (ChkWarrantyStart.IsChecked != true || ChkWarrantyEnd.IsChecked != true) return;
            if (DpWarrantyStart.SelectedDate == null || DpWarrantyEnd.SelectedDate == null) return;

            var start = DpWarrantyStart.SelectedDate.Value;
            var end = DpWarrantyEnd.SelectedDate.Value;
            if (end <= start) return;

            int years = end.Year - start.Year;
            if (end.Month < start.Month || (end.Month == start.Month && end.Day < start.Day))
                years--;

            years = Math.Max(0, Math.Min(10, years));
            TxtWarrantyYears.Text = years.ToString();
        }

        private void UpdateWarrantyDisplay()
        {
            if (!string.Equals(CmbItemType.SelectedItem?.ToString(), "Software/License",
                    StringComparison.OrdinalIgnoreCase)) return;

            // Don't fabricate a period out of Today/Tomorrow when the user hasn't actually
            // picked dates yet — just hide the band until both are set.
            if (!DpStartDate.SelectedDate.HasValue || !DpEndDate.SelectedDate.HasValue)
            {
                RowWarrantyInfoBand.Visibility = Visibility.Collapsed;
                return;
            }

            RowWarrantyInfoBand.Visibility = Visibility.Visible;
            var startDate = DpStartDate.SelectedDate.Value;
            var endDate   = DpEndDate.SelectedDate.Value;

            if (endDate <= startDate)
            {
                LblWarrantyInfo.Text       = $"End Date must be after Start Date. ({startDate:MMM dd, yyyy} → {endDate:MMM dd, yyyy})";
                LblWarrantyInfo.Foreground = Brushes.Firebrick;
                return;
            }

            var totalDays    = (int)(endDate - startDate).TotalDays;
            var years        = totalDays / 365;
            var months       = (totalDays % 365) / 30;
            string periodText = (years > 0 && months > 0) ? $"{years} year(s) and {months} month(s)"
                              : years  > 0                ? $"{years} year(s)"
                              : months > 0                ? $"{months} month(s)"
                                                          : $"{totalDays} day(s)";

            LblWarrantyInfo.Text       = $"Warranty/License Period: {startDate:MMM dd, yyyy} to {endDate:MMM dd, yyyy} ({periodText})";
            LblWarrantyInfo.Foreground = new SolidColorBrush(Color.FromRgb(0x1C, 0x7A, 0x38));
        }

        // ─────────────────────────────────────────────────────────────────────────
        // Quick-add dialogs (Category, Vendor, CartridgeModel)
        // ─────────────────────────────────────────────────────────────────────────
        private void BtnAddCategory_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new QuickAddCategory();
            if (dialog.ShowDialog() == WinForms.DialogResult.OK)
            {
                LoadCategories();
                for (int i = 0; i < CmbCategory.Items.Count; i++)
                {
                    if (CmbCategory.Items[i] is CategoryItem item && item.Name == dialog.NewCategoryName)
                    { CmbCategory.SelectedIndex = i; break; }
                }
            }
        }

        private void BtnAddVendor_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new QuickAddVendorDialog();
            if (dialog.ShowDialog() == WinForms.DialogResult.OK)
            {
                LoadVendors();
                if (dialog.NewVendorId.HasValue)
                {
                    for (int i = 0; i < CmbVendor.Items.Count; i++)
                    {
                        if (CmbVendor.Items[i] is VendorItem v && v.VendorId == dialog.NewVendorId.Value)
                        { CmbVendor.SelectedIndex = i; break; }
                    }
                }
            }
        }

        private async void BtnAddCartridgeModel_Click(object sender, RoutedEventArgs e)
        {
            using (var dialog = CreateQuickAddCartridgeModelDialog())
            {
                if (dialog.ShowDialog() == WinForms.DialogResult.OK)
                {
                    if (dialog.Tag is QuickAddCartridgeModelResult result)
                        LoadCartridgeModels(result.ModelNumber);
                    else
                        LoadCartridgeModels();
                }
            }
        }

        private WinForms.Form CreateQuickAddCartridgeModelDialog()
        {
            var dialog = new WinForms.Form
            {
                Text            = "Add Cartridge Model",
                Width           = 450,
                Height          = 320,
                FormBorderStyle = WinForms.FormBorderStyle.FixedDialog,
                StartPosition   = WinForms.FormStartPosition.CenterScreen,
                MaximizeBox     = false,
                MinimizeBox     = false
            };

            var panel = new WinForms.TableLayoutPanel
            {
                Dock        = WinForms.DockStyle.Fill,
                ColumnCount = 2,
                RowCount    = 4,
                Padding     = new WinForms.Padding(20)
            };
            panel.ColumnStyles.Add(new WinForms.ColumnStyle(WinForms.SizeType.Absolute, 120F));
            panel.ColumnStyles.Add(new WinForms.ColumnStyle(WinForms.SizeType.Percent, 100F));
            dialog.Controls.Add(panel);

            var lblMN = new WinForms.Label { Text = "Model Number *", Dock = WinForms.DockStyle.Fill };
            var txtMN = new WinForms.TextBox { Dock = WinForms.DockStyle.Fill };
            panel.Controls.Add(lblMN, 0, 0);
            panel.Controls.Add(txtMN, 1, 0);

            var initModel = TxtModelNumber.Text?.Trim();
            if (!string.IsNullOrWhiteSpace(initModel)) txtMN.Text = initModel;

            var chkReq = new WinForms.CheckBox { Text = "Is Requestable", Checked = true, Dock = WinForms.DockStyle.Fill };
            var chkRef = new WinForms.CheckBox { Text = "Is Refillable",  Checked = true, Dock = WinForms.DockStyle.Fill };
            panel.Controls.Add(new WinForms.Label(), 0, 1); panel.Controls.Add(chkReq, 1, 1);
            panel.Controls.Add(new WinForms.Label(), 0, 2); panel.Controls.Add(chkRef, 1, 2);

            var btnFlow = new WinForms.FlowLayoutPanel
            {
                FlowDirection = WinForms.FlowDirection.RightToLeft,
                Dock          = WinForms.DockStyle.Fill,
                AutoSize      = true
            };
            var btnSave   = new WinForms.Button { Text = "Save",   Width = 80, DialogResult = WinForms.DialogResult.None };
            var btnCancel = new WinForms.Button { Text = "Cancel", Width = 80, DialogResult = WinForms.DialogResult.Cancel };

            btnSave.Click += async (s, ev) =>
            {
                if (string.IsNullOrWhiteSpace(txtMN.Text))
                {
                    WinForms.MessageBox.Show("Please enter a model number.", "Validation",
                        WinForms.MessageBoxButtons.OK, WinForms.MessageBoxIcon.Warning);
                    return;
                }
                try
                {
                    btnSave.Enabled = false; btnSave.Text = "Saving...";
                    var repo     = new CartridgeModelRepository();
                    var existing = await repo.FindByModelNumberAsync(txtMN.Text.Trim());
                    if (existing != null)
                    {
                        WinForms.MessageBox.Show($"Model '{txtMN.Text.Trim()}' already exists.", "Duplicate",
                            WinForms.MessageBoxButtons.OK, WinForms.MessageBoxIcon.Warning);
                        btnSave.Enabled = true; btnSave.Text = "Save";
                        return;
                    }
                    var model = new CartridgeModelDto
                    {
                        ModelNumber   = txtMN.Text.Trim(),
                        IsRequestable = chkReq.Checked,
                        IsRefillable  = chkRef.Checked,
                        IsActive      = true,
                        CreatedBy     = AppSession.CurrentUserId,
                        CreatedAt     = DateTime.Now
                    };
                    var newId = await repo.CreateAsync(model);
                    WinForms.MessageBox.Show("Cartridge model added successfully!", "Success",
                        WinForms.MessageBoxButtons.OK, WinForms.MessageBoxIcon.Information);
                    dialog.Tag          = new QuickAddCartridgeModelResult { CartridgeModelId = newId, ModelNumber = model.ModelNumber };
                    dialog.DialogResult = WinForms.DialogResult.OK;
                }
                catch (Exception ex)
                {
                    WinForms.MessageBox.Show($"Error: {ex.Message}", "Error",
                        WinForms.MessageBoxButtons.OK, WinForms.MessageBoxIcon.Error);
                    btnSave.Enabled = true; btnSave.Text = "Save";
                }
            };

            btnFlow.Controls.Add(btnSave);
            btnFlow.Controls.Add(btnCancel);
            panel.Controls.Add(btnFlow, 0, 3);
            panel.SetColumnSpan(btnFlow, 2);
            dialog.AcceptButton = btnSave;
            dialog.CancelButton = btnCancel;
            return dialog;
        }

        private async void BtnAddConsumableModel_Click(object sender, RoutedEventArgs e)
        {
            using (var dialog = CreateQuickAddConsumableModelDialog())
            {
                if (dialog.ShowDialog() == WinForms.DialogResult.OK)
                {
                    if (dialog.Tag is QuickAddConsumableModelResult result)
                        LoadConsumableModels(result.ModelNumber);
                    else
                        LoadConsumableModels();
                }
            }
        }

        private WinForms.Form CreateQuickAddConsumableModelDialog()
        {
            string category = CanonicalConsumableCategory((CmbCategory.SelectedItem as CategoryItem)?.Name) ?? "Ink";

            var dialog = new WinForms.Form
            {
                Text            = "Add " + category + " Model",
                Width           = 450,
                Height          = 260,
                FormBorderStyle = WinForms.FormBorderStyle.FixedDialog,
                StartPosition   = WinForms.FormStartPosition.CenterScreen,
                MaximizeBox     = false,
                MinimizeBox     = false
            };

            var panel = new WinForms.TableLayoutPanel
            {
                Dock        = WinForms.DockStyle.Fill,
                ColumnCount = 2,
                RowCount    = 3,
                Padding     = new WinForms.Padding(20)
            };
            panel.ColumnStyles.Add(new WinForms.ColumnStyle(WinForms.SizeType.Absolute, 120F));
            panel.ColumnStyles.Add(new WinForms.ColumnStyle(WinForms.SizeType.Percent, 100F));
            dialog.Controls.Add(panel);

            var lblMN = new WinForms.Label { Text = "Model Number *", Dock = WinForms.DockStyle.Fill };
            var txtMN = new WinForms.TextBox { Dock = WinForms.DockStyle.Fill };
            panel.Controls.Add(lblMN, 0, 0);
            panel.Controls.Add(txtMN, 1, 0);

            var initModel = TxtModelNumber.Text?.Trim();
            if (!string.IsNullOrWhiteSpace(initModel)) txtMN.Text = initModel;

            var chkReq = new WinForms.CheckBox { Text = "Is Requestable", Checked = true, Dock = WinForms.DockStyle.Fill };
            panel.Controls.Add(new WinForms.Label(), 0, 1); panel.Controls.Add(chkReq, 1, 1);

            var btnFlow = new WinForms.FlowLayoutPanel
            {
                FlowDirection = WinForms.FlowDirection.RightToLeft,
                Dock          = WinForms.DockStyle.Fill,
                AutoSize      = true
            };
            var btnSave   = new WinForms.Button { Text = "Save",   Width = 80, DialogResult = WinForms.DialogResult.None };
            var btnCancel = new WinForms.Button { Text = "Cancel", Width = 80, DialogResult = WinForms.DialogResult.Cancel };

            btnSave.Click += async (s, ev) =>
            {
                if (string.IsNullOrWhiteSpace(txtMN.Text))
                {
                    WinForms.MessageBox.Show("Please enter a model number.", "Validation",
                        WinForms.MessageBoxButtons.OK, WinForms.MessageBoxIcon.Warning);
                    return;
                }
                try
                {
                    btnSave.Enabled = false; btnSave.Text = "Saving...";
                    var repo     = new ConsumableModelRepository();
                    var existing = await repo.FindByModelNumberAsync(txtMN.Text.Trim(), category);
                    if (existing != null)
                    {
                        WinForms.MessageBox.Show($"Model '{txtMN.Text.Trim()}' already exists for {category}.", "Duplicate",
                            WinForms.MessageBoxButtons.OK, WinForms.MessageBoxIcon.Warning);
                        btnSave.Enabled = true; btnSave.Text = "Save";
                        return;
                    }
                    var model = new ConsumableModelDto
                    {
                        ModelNumber   = txtMN.Text.Trim(),
                        Category      = category,
                        IsRequestable = chkReq.Checked,
                        IsActive      = true,
                        CreatedBy     = AppSession.CurrentUserId,
                        CreatedAt     = DateTime.Now
                    };
                    var newId = await repo.CreateAsync(model);
                    WinForms.MessageBox.Show($"{category} model added successfully!", "Success",
                        WinForms.MessageBoxButtons.OK, WinForms.MessageBoxIcon.Information);
                    dialog.Tag          = new QuickAddConsumableModelResult { ConsumableModelId = newId, ModelNumber = model.ModelNumber };
                    dialog.DialogResult = WinForms.DialogResult.OK;
                }
                catch (Exception ex)
                {
                    WinForms.MessageBox.Show($"Error: {ex.Message}", "Error",
                        WinForms.MessageBoxButtons.OK, WinForms.MessageBoxIcon.Error);
                    btnSave.Enabled = true; btnSave.Text = "Save";
                }
            };

            btnFlow.Controls.Add(btnSave);
            btnFlow.Controls.Add(btnCancel);
            panel.Controls.Add(btnFlow, 0, 2);
            panel.SetColumnSpan(btnFlow, 2);
            dialog.AcceptButton = btnSave;
            dialog.CancelButton = btnCancel;
            return dialog;
        }

        // ─────────────────────────────────────────────────────────────────────────
        // Preview
        // ─────────────────────────────────────────────────────────────────────────
        private void BtnPreview_Click(object sender, RoutedEventArgs e)
        {
            var vr = ValidateInput();
            if (!vr.IsValid)
            {
                WinMsgBox.Show(vr.ErrorMessage, "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            LstPreview.Items.Clear();

            if (vr.IsNonSerialized)
            {
                LstPreview.Items.Add($"Items to be created: 1");
                LstPreview.Items.Add($"Item Name: {TxtItemName.Text}");
                LstPreview.Items.Add($"Category: {(CmbCategory.SelectedItem as CategoryItem)?.Name ?? CmbCategory.Text}");
                LstPreview.Items.Add($"Quantity / StockOnHand: {vr.Quantity}");
                LstPreview.Items.Add("");
                LstPreview.Items.Add("One item record will be created with the specified quantity.");
            }
            else
            {
                var serials = vr.SerialNumbers;
                LstPreview.Items.Add($"Items to be created: {serials.Length}");
                LstPreview.Items.Add($"Item Name: {TxtItemName.Text}");
                LstPreview.Items.Add($"Category: {(CmbCategory.SelectedItem as CategoryItem)?.Name ?? CmbCategory.Text}");
                LstPreview.Items.Add("");
                LstPreview.Items.Add("Serial Numbers:");
                LstPreview.Items.Add("────────────────────────────────────");
                for (int i = 0; i < Math.Min(serials.Length, 20); i++)
                    LstPreview.Items.Add($"  {(i + 1),3}. {serials[i]}");
                if (serials.Length > 20)
                    LstPreview.Items.Add($"  ... and {serials.Length - 20} more");
            }

            PnlPreview.Visibility = Visibility.Visible;
        }

        // ─────────────────────────────────────────────────────────────────────────
        // Add Batch — re-entry protected
        // ─────────────────────────────────────────────────────────────────────────
        private void BtnAdd_Click(object sender, RoutedEventArgs e)
        {
            lock (this)
            {
                if (_isProcessing) return;
                _isProcessing = true;
            }

            if (!BtnAdd.IsEnabled) { _isProcessing = false; return; }

            BtnAdd.IsEnabled = false;
            BtnAdd.Content   = "Processing...";

            try
            {
                var vr = ValidateInput();
                if (!vr.IsValid)
                {
                    WinMsgBox.Show(vr.ErrorMessage, "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var serials        = vr.SerialNumbers;
                bool isNonSerial   = vr.IsNonSerialized;
                int  quantity      = vr.IsCellPhone ? vr.CellPhoneRows.Count : (isNonSerial ? vr.Quantity : serials.Length);

                BtnAdd.IsEnabled = true;
                BtnAdd.Content   = "Add Batch";

                var cat           = CmbCategory.SelectedItem as CategoryItem;
                bool isCartridge  = cat != null && string.Equals(cat.Name, "Cartridge", StringComparison.OrdinalIgnoreCase);
                string originLine = isCartridge ? $"Origin: {CmbCartridgeOrigin.SelectedItem}\n" : string.Empty;

                string confirmMsg = vr.IsCellPhone
                    ? $"Add {quantity} cell phone items?\n\nItem: {TxtItemName.Text}\nCategory: {cat?.Name ?? CmbCategory.Text}\nRows: {quantity} unique items\nEach item will have StockOnHand = 1\n\nContinue?"
                    : isNonSerial
                        ? $"Add 1 item with quantity {quantity}?\n\nItem: {TxtItemName.Text}\nCategory: {cat?.Name ?? CmbCategory.Text}\n{originLine}Quantity: {quantity}\nStockOnHand will be set to {quantity}\n\nContinue?"
                        : $"Add {quantity} items with unique serial numbers?\n\nItem: {TxtItemName.Text}\nCategory: {cat?.Name ?? CmbCategory.Text}\n{originLine}Serial Numbers: {quantity} unique items\nEach item will have StockOnHand = 1\n\nContinue?";

                if (WinMsgBox.Show(confirmMsg, "Confirm Batch Add", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
                    return;

                BtnAdd.IsEnabled = false;
                BtnAdd.Content   = "Adding...";
                Mouse.OverrideCursor = Cursors.Wait;

                var addResult = vr.IsCellPhone
                    ? BatchAddCellPhoneItems(vr.CellPhoneRows)
                    : BatchAddItems(serials, isNonSerial, quantity);

                AddedItemIds.AddRange(addResult.ItemIds);

                if (addResult.FailedSerials.Any())
                {
                    var lines = new List<string> { $"Successfully added {addResult.SuccessCount} items.", "", $"Failed to add {addResult.FailedSerials.Count} items:" };
                    int show = Math.Min(15, addResult.FailedSerials.Count);
                    for (int i = 0; i < show; i++) lines.Add($"  • {addResult.FailedSerials[i]}");
                    if (addResult.FailedSerials.Count > show) lines.Add($"  ... and {addResult.FailedSerials.Count - show} more");
                    WinMsgBox.Show(string.Join("\n", lines), "Partial Success", MessageBoxButton.OK, MessageBoxImage.Warning);
                    _totalItemsAdded += addResult.SuccessCount;
                }
                else
                {
                    _totalItemsAdded += addResult.SuccessCount;
                    UpdateSessionLabel();

                    if (WinMsgBox.Show(
                            $"Successfully added {addResult.SuccessCount} items!\n\nTotal items added this session: {_totalItemsAdded}\n\nAdd another batch?",
                            "Success", MessageBoxButton.YesNo, MessageBoxImage.Information) == MessageBoxResult.Yes)
                        ClearForm();
                    else
                        DialogResult = true;
                }
            }
            catch (Exception ex)
            {
                WinMsgBox.Show($"Error adding items: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                Mouse.OverrideCursor = null;
                BtnAdd.IsEnabled = true;
                BtnAdd.Content   = "Add Batch";
                _isProcessing    = false;
            }
        }

        private void UpdateSessionLabel()
        {
            LblSessionCount.Text = _totalItemsAdded > 0
                ? $"{_totalItemsAdded} item{(_totalItemsAdded == 1 ? "" : "s")} added this session"
                : string.Empty;
        }

        // ─────────────────────────────────────────────────────────────────────────
        // Footer buttons
        // ─────────────────────────────────────────────────────────────────────────
        private void BtnDone_Click(object sender, RoutedEventArgs e)
            => DialogResult = _totalItemsAdded > 0;

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
            => DialogResult = false;

        private void BtnImportCsv_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new CartridgeCsvImportDialog { Owner = this };
            dialog.ShowDialog();
        }

        // Fast-pass invoice import: a CSV of invoice lines is turned into Items +
        // Sales Invoice Sets (grouped by Document #) + seeded Renewals in one pass,
        // bypassing the manual Item → Set → Renewal steps.
        private void BtnImportInvoiceCsv_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new InvoiceCsvImportDialog { Owner = this };
            dialog.ShowDialog();
        }

        // On-screen counterpart to Import Invoice CSV: same one-pass creation, entered on a page
        // instead of a spreadsheet. Saves through the identical transaction.
        private void BtnBuildInvoice_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new InvoiceBuilderDialog { Owner = this };
            dialog.ShowDialog();
        }

        // Request-Set analogue of Build Invoice: enter request lines (existing or brand-new
        // items) on one page; new items are created in the system only when the Set is built.
        private void BtnBuildRequestSet_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new Yakult.Inventory.App.Pages.Request.RequestSetBuilderDialog { Owner = this };
            dialog.ShowDialog();
        }

        // ─────────────────────────────────────────────────────────────────────────
        // Validation
        // ─────────────────────────────────────────────────────────────────────────
        private ValidationResult ValidateInput()
        {
            if (string.IsNullOrWhiteSpace(TxtItemName.Text))
            {
                TxtItemName.Focus();
                return new ValidationResult { IsValid = false, ErrorMessage = "Please enter an item name." };
            }
            if (string.IsNullOrWhiteSpace(TxtModelNumber.Text))
            {
                TxtModelNumber.Focus();
                return new ValidationResult { IsValid = false, ErrorMessage = "Please enter a model number." };
            }
            if (CmbCategory.SelectedIndex < 0)
            {
                CmbCategory.Focus();
                return new ValidationResult { IsValid = false, ErrorMessage = "Please select a category." };
            }
            if (CmbItemType.SelectedIndex < 0)
            {
                CmbItemType.Focus();
                return new ValidationResult { IsValid = false, ErrorMessage = "Please select an item type." };
            }

            var cat = CmbCategory.SelectedItem as CategoryItem;
            bool isCartridge = cat != null && string.Equals(cat.Name, "Cartridge", StringComparison.OrdinalIgnoreCase);

            if (isCartridge && (CmbCartridgeModel.SelectedIndex <= 0 || !(CmbCartridgeModel.SelectedItem is CartridgeModelItem cm && cm.CartridgeModelId > 0)))
            {
                CmbCartridgeModel.Focus();
                return new ValidationResult { IsValid = false, ErrorMessage = "Please select a cartridge model." };
            }
            if (isCartridge && CmbCartridgeOrigin.SelectedIndex < 0)
            {
                CmbCartridgeOrigin.Focus();
                return new ValidationResult { IsValid = false, ErrorMessage = "Please select whether the cartridge is Brand New or Refilled." };
            }
            if (CmbCondition.SelectedIndex < 0)
            {
                CmbCondition.Focus();
                return new ValidationResult { IsValid = false, ErrorMessage = "Please select a condition." };
            }

            if (IsCellPhoneCategory(cat))
            {
                var rows = _cellPhoneRows
                    .Where(r => !string.IsNullOrWhiteSpace(r.CellPhoneNumber)
                             || !string.IsNullOrWhiteSpace(r.SerialNumber)
                             || !string.IsNullOrWhiteSpace(r.IMEI1)
                             || !string.IsNullOrWhiteSpace(r.IMEI2))
                    .ToList();

                if (rows.Count == 0)
                    return new ValidationResult { IsValid = false, ErrorMessage = "Please add at least one cell phone row." };

                var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var row in rows)
                {
                    if (string.IsNullOrWhiteSpace(row.SerialNumber))
                        return new ValidationResult { IsValid = false, ErrorMessage = "Please enter a Serial Number for every cell phone row." };

                    var serial = row.SerialNumber.Trim();
                    if (!seen.Add(serial.ToUpperInvariant()))
                        return new ValidationResult { IsValid = false, ErrorMessage = $"Duplicate Serial Number in batch: {serial}" };
                }

                return new ValidationResult { IsValid = true, IsCellPhone = true, CellPhoneRows = rows, Quantity = rows.Count };
            }

            // Serial numbers vs. quantity
            var splitLines = TxtSerialNumbers.Text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            var uniqueSerials = splitLines
                .Select(s => s.Trim().ToUpperInvariant())
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();

            if (uniqueSerials.Length > 0)
                return new ValidationResult { IsValid = true, SerialNumbers = uniqueSerials, Quantity = uniqueSerials.Length, IsNonSerialized = false };

            if (!int.TryParse(TxtQuantity.Text, out int qty) || qty <= 0)
            {
                TxtQuantity.Focus();
                return new ValidationResult { IsValid = false, ErrorMessage = "Please provide either serial numbers OR a quantity greater than 0." };
            }

            return new ValidationResult { IsValid = true, SerialNumbers = new string[] { null }, Quantity = qty, IsNonSerialized = true };
        }

        // ─────────────────────────────────────────────────────────────────────────
        // Database insert
        // ─────────────────────────────────────────────────────────────────────────
        private BatchAddResult BatchAddItems(string[] serialNumbers, bool isNonSerialized, int quantity)
        {
            var repo   = new ItemRepository();
            var result = new BatchAddResult();
            var processed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var f = CaptureCommonItemFields();

            using var _activitySource = Yakult.Inventory.App.Services.InventoryActivityNotifier.Source("Batch Add");

            foreach (var serialNumber in serialNumbers)
            {
                var normalized = string.IsNullOrWhiteSpace(serialNumber) ? null : serialNumber.Trim();

                if (!string.IsNullOrEmpty(normalized) && processed.Contains(normalized))
                { result.FailedSerials.Add($"{serialNumber} (duplicate in batch - skipped)"); continue; }

                if (!string.IsNullOrEmpty(normalized)) processed.Add(normalized);

                try
                {
                    var dateCreated = DateTime.Now;
                    var item = f.BuildItem(dateCreated);
                    item.SerialNumber = normalized;
                    item.StockOnHand  = isNonSerialized ? quantity : 1;
                    // Manual override wins when checked; otherwise fall back to the auto rule.
                    item.WarrantyStartDate = f.WarrantyStartOverride ?? (f.WarrantyYears > 0 ? dateCreated : (DateTime?)null);
                    item.WarrantyEndDate   = f.WarrantyEndOverride;

                    var newItemId = repo.AddItem(item);
                    result.SuccessCount++;
                    result.ItemIds.Add(newItemId);
                }
                catch (System.Data.SqlClient.SqlException ex) when (ex.Number == 2627 || ex.Number == 2601)
                {
                    if (ex.Message.Contains("PRIMARY KEY") || ex.Message.Contains("PK__Item"))
                        result.FailedSerials.Add($"{serialNumber} (database error - ItemId conflict)");
                    else if (ex.Message.Contains("SerialNumber") || ex.Message.Contains("UQ_"))
                        result.FailedSerials.Add($"{serialNumber} (already exists)");
                    else
                        result.FailedSerials.Add($"{serialNumber} (duplicate key)");
                }
                catch (Exception ex)
                {
                    result.FailedSerials.Add($"{serialNumber} (Error: {ex.Message})");
                }
            }

            return result;
        }

        private BatchAddResult BatchAddCellPhoneItems(List<CellPhoneRowItem> rows)
        {
            var repo   = new ItemRepository();
            var result = new BatchAddResult();
            var processed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var f = CaptureCommonItemFields();

            using var _activitySource = Yakult.Inventory.App.Services.InventoryActivityNotifier.Source("Batch Add");

            foreach (var row in rows)
            {
                var normalized = row.SerialNumber?.Trim();

                if (!string.IsNullOrEmpty(normalized) && processed.Contains(normalized))
                { result.FailedSerials.Add($"{normalized} (duplicate in batch - skipped)"); continue; }

                if (!string.IsNullOrEmpty(normalized)) processed.Add(normalized);

                try
                {
                    var dateCreated = DateTime.Now;
                    var item = f.BuildItem(dateCreated);
                    item.SerialNumber    = normalized;
                    item.StockOnHand     = 1;
                    // Manual override wins when checked; otherwise fall back to the auto rule.
                    item.WarrantyStartDate = f.WarrantyStartOverride ?? (f.WarrantyYears > 0 ? dateCreated : (DateTime?)null);
                    item.WarrantyEndDate   = f.WarrantyEndOverride;
                    item.CellPhoneNumber = string.IsNullOrWhiteSpace(row.CellPhoneNumber) ? null : row.CellPhoneNumber.Trim();
                    item.IMEI1           = string.IsNullOrWhiteSpace(row.IMEI1) ? null : row.IMEI1.Trim();
                    item.IMEI2           = string.IsNullOrWhiteSpace(row.IMEI2) ? null : row.IMEI2.Trim();

                    var newItemId = repo.AddItem(item);
                    result.SuccessCount++;
                    result.ItemIds.Add(newItemId);
                }
                catch (System.Data.SqlClient.SqlException ex) when (ex.Number == 2627 || ex.Number == 2601)
                {
                    if (ex.Message.Contains("PRIMARY KEY") || ex.Message.Contains("PK__Item"))
                        result.FailedSerials.Add($"{normalized} (database error - ItemId conflict)");
                    else if (ex.Message.Contains("SerialNumber") || ex.Message.Contains("UQ_"))
                        result.FailedSerials.Add($"{normalized} (already exists)");
                    else
                        result.FailedSerials.Add($"{normalized} (duplicate key)");
                }
                catch (Exception ex)
                {
                    result.FailedSerials.Add($"{normalized} (Error: {ex.Message})");
                }
            }

            return result;
        }

        // Captures the form fields shared by every item created in a batch (everything
        // except per-row SerialNumber/StockOnHand/cell-phone fields, which vary per item).
        private ItemCommonFields CaptureCommonItemFields()
        {
            var cat         = CmbCategory.SelectedItem as CategoryItem;
            var categoryName = cat?.Name ?? CmbCategory.Text;
            bool isCartridge = string.Equals(categoryName, "Cartridge", StringComparison.OrdinalIgnoreCase);

            int? cartridgeModelId = null;
            if (isCartridge) cartridgeModelId = (CmbCartridgeModel.SelectedItem as CartridgeModelItem)?.CartridgeModelId;

            string cartridgeRefillStatus = null;
            if (isCartridge)
            {
                var origin = CmbCartridgeOrigin.SelectedItem?.ToString() ?? "Brand New";
                if (string.Equals(origin, "Refilled", StringComparison.OrdinalIgnoreCase))
                    cartridgeRefillStatus = "Available";
            }

            int? consumableModelId = null;
            if (CanonicalConsumableCategory(categoryName) != null)
                consumableModelId = (CmbConsumableModel.SelectedItem as ConsumableModelItem)?.ConsumableModelId;

            var subType = CmbSubType.SelectedItem as string;
            var selectedType  = CmbItemType.SelectedItem?.ToString() ?? "Hardware";
            bool isSoftware   = string.Equals(selectedType, "Software/License", StringComparison.OrdinalIgnoreCase);
            bool isServices   = string.Equals(selectedType, "Services", StringComparison.OrdinalIgnoreCase);
            bool isHardware   = string.Equals(selectedType, "Hardware", StringComparison.OrdinalIgnoreCase);

            decimal.TryParse(TxtAmount.Text, out decimal amount);
            var selectedVendor = CmbVendor.SelectedItem as VendorItem;
            int.TryParse(TxtWarrantyYears.Text, out int warrantyYears);
            bool hasContractDates = isSoftware || isHardware || (isServices && ChkServiceContractDates.IsChecked == true);

            return new ItemCommonFields
            {
                ItemName          = TxtItemName.Text.Trim(),
                Description       = string.IsNullOrWhiteSpace(TxtDescription.Text) ? null : TxtDescription.Text.Trim(),
                ModelNumber       = TxtModelNumber.Text.Trim(),
                CategoryId        = cat?.CategoryId ?? 0,
                CategoryName      = categoryName,
                CartridgeModelId  = cartridgeModelId,
                CartridgeRefillStatus = cartridgeRefillStatus,
                ConsumableModelId = consumableModelId,
                UnitOfMeasure     = CmbUnitOfMeasure.Text,
                ItemType          = selectedType,
                SubType           = string.IsNullOrWhiteSpace(subType) ? null : subType,
                AffectsInventory  = isHardware,
                AcquisitionType   = isHardware ? "Both" : "Invoice",
                IsTrackedAsset    = ChkIsTrackedAsset.IsChecked == true,
                IsBorrowable      = ChkIsBorrowable.IsChecked == true,
                Amount            = amount,
                VendorId          = (selectedVendor != null && selectedVendor.VendorId > 0) ? (int?)selectedVendor.VendorId : null,
                VendorName        = selectedVendor?.VendorName,
                ConditionId       = (CmbCondition.SelectedItem as ConditionItem)?.ConditionId ?? 1,
                ConditionName     = (CmbCondition.SelectedItem as ConditionItem)?.ConditionName ?? "Good",
                Remarks           = string.IsNullOrWhiteSpace(TxtRemarks.Text) ? null : TxtRemarks.Text.Trim(),
                PartNumber        = string.IsNullOrWhiteSpace(TxtPartNumber.Text) ? null : TxtPartNumber.Text.Trim(),
                WarrantyYears     = warrantyYears,
                // Manual override, same as Edit Item: checked means the user's own date wins;
                // unchecked means fall back to the auto rule (Start = creation time when
                // Years > 0, End computed by ItemRepository.AddItem from Start + Years).
                WarrantyStartOverride = (ChkWarrantyStart.IsChecked == true) ? DpWarrantyStart.SelectedDate : null,
                WarrantyEndOverride   = (ChkWarrantyEnd.IsChecked == true) ? DpWarrantyEnd.SelectedDate : null,
                DatePurchased     = (ChkDatePurchased.IsChecked == true) ? DpDatePurchased.SelectedDate : null,
                LicenseNumber     = string.IsNullOrWhiteSpace(TxtLicenseNumber.Text) ? null : TxtLicenseNumber.Text.Trim(),
                StartDate         = hasContractDates ? DpStartDate.SelectedDate : null,
                EndDate           = hasContractDates ? DpEndDate.SelectedDate   : null
            };
        }

        // Holds the batch-wide item fields captured once per Add Batch click, plus a
        // factory that stamps them onto a fresh ItemDto for each row.
        private class ItemCommonFields
        {
            public string ItemName;
            public string Description;
            public string ModelNumber;
            public int CategoryId;
            public string CategoryName;
            public int? CartridgeModelId;
            public string CartridgeRefillStatus;
            public int? ConsumableModelId;
            public string UnitOfMeasure;
            public string ItemType;
            public string SubType;
            public bool AffectsInventory;
            public string AcquisitionType;
            public bool IsTrackedAsset;
            public bool IsBorrowable;
            public decimal Amount;
            public int? VendorId;
            public string VendorName;
            public int ConditionId;
            public string ConditionName;
            public string Remarks;
            public string PartNumber;
            public int WarrantyYears;
            public DateTime? WarrantyStartOverride;
            public DateTime? WarrantyEndOverride;
            public DateTime? DatePurchased;
            public string LicenseNumber;
            public DateTime? StartDate;
            public DateTime? EndDate;

            public ItemDto BuildItem(DateTime dateCreated) => new ItemDto
            {
                Name             = ItemName,
                Description      = Description,
                ModelNumber      = ModelNumber,
                CartridgeModelId = CartridgeModelId,
                ConsumableModelId = ConsumableModelId,
                CategoryId       = CategoryId,
                Category         = CategoryName,
                UnitOfMeasure    = UnitOfMeasure,
                Amount           = Amount,
                ItemType         = ItemType,
                SubType          = SubType,
                StartDate        = StartDate,
                EndDate          = EndDate,
                ConditionId      = ConditionId,
                ConditionName    = ConditionName,
                VendorId         = VendorId,
                VendorName       = VendorName,
                Remarks          = Remarks,
                WarrantyYears    = WarrantyYears,
                DatePurchased    = DatePurchased,
                LicenseNumber    = LicenseNumber,
                PartNumber       = PartNumber,
                AffectsInventory = AffectsInventory,
                AcquisitionType  = AcquisitionType,
                IsTrackedAsset   = IsTrackedAsset,
                IsBorrowable     = IsBorrowable,
                RefillStatus     = CartridgeRefillStatus,
                DateCreated      = dateCreated,
                CreatedByUserId  = AppSession.CurrentUserId,
                CreatedByName    = AppSession.CurrentUserName,
                Active           = true
            };
        }

        // ─────────────────────────────────────────────────────────────────────────
        // Clear form
        // ─────────────────────────────────────────────────────────────────────────
        private void ClearForm()
        {
            TxtItemName.Clear();
            TxtDescription.Clear();
            TxtModelNumber.Clear();
            TxtSerialNumbers.Clear();
            TxtQuantity.Text = "1";

            CmbCategory.SelectedIndex = -1;
            CmbItemType.SelectedIndex = 0;
            CmbSubType.SelectedIndex = 0;

            _cellPhoneRows.Clear();
            _cellPhoneCurrentPage = 1;
            RefreshCellPhoneRowsView();
            RowCellPhoneEntries.Visibility   = Visibility.Collapsed;
            RowQuantity.Visibility           = Visibility.Visible;
            RowSerialNumbersEntry.Visibility = Visibility.Visible;
            RowPreviewSection.Visibility     = Visibility.Visible;

            DpStartDate.SelectedDate = DateTime.Today;
            DpEndDate.SelectedDate   = DateTime.Today.AddDays(1);
            CmbItemType_SelectionChanged(CmbItemType, null);

            if (CmbUnitOfMeasure.Items.Count > 0) CmbUnitOfMeasure.SelectedIndex = 0;
            TxtAmount.Text = "0";

            for (int i = 0; i < CmbCondition.Items.Count; i++)
            {
                if (CmbCondition.Items[i] is ConditionItem ci &&
                    ci.ConditionName.Equals("Good", StringComparison.OrdinalIgnoreCase))
                { CmbCondition.SelectedIndex = i; break; }
            }

            if (CmbVendor.Items.Count > 0) CmbVendor.SelectedIndex = 0;
            if (CmbCartridgeOrigin.Items.Count > 0) CmbCartridgeOrigin.SelectedIndex = 0;

            TxtRemarks.Clear();
            TxtLicenseNumber.Clear();
            TxtPartNumber.Clear();
            TxtWarrantyYears.Text      = "0";
            ChkWarrantyStart.IsChecked = false;
            ChkWarrantyEnd.IsChecked   = false;
            ChkDatePurchased.IsChecked = false;

            LstPreview.Items.Clear();
            PnlPreview.Visibility = Visibility.Collapsed;

            UpdateSessionLabel();
            TxtItemName.Focus();
        }

        // ─────────────────────────────────────────────────────────────────────────
        // Public API — mobile scanner integration
        // ─────────────────────────────────────────────────────────────────────────
        public void AddSerialFromMobile(string serialNumber)
        {
            AddMobileItemFromMobile(serialNumber, null, null, null);
        }

        public void AddMobileItemFromMobile(string serialNumber, string cellPhoneNumber, string imei1, string imei2)
        {
            if (string.IsNullOrWhiteSpace(serialNumber)) return;
            var trimmed = serialNumber.Trim();
            var hasPhoneDetails = !string.IsNullOrWhiteSpace(cellPhoneNumber)
                || !string.IsNullOrWhiteSpace(imei1)
                || !string.IsNullOrWhiteSpace(imei2);

            if (hasPhoneDetails)
            {
                SelectCellPhoneCategoryIfAvailable();

                if (!_cellPhoneRows.Any(r => string.Equals((r.SerialNumber ?? string.Empty).Trim(), trimmed, StringComparison.OrdinalIgnoreCase)))
                {
                    _cellPhoneRows.Add(new CellPhoneRowItem
                    {
                        CellPhoneNumber = string.IsNullOrWhiteSpace(cellPhoneNumber) ? null : cellPhoneNumber.Trim(),
                        SerialNumber = trimmed,
                        IMEI1 = string.IsNullOrWhiteSpace(imei1) ? null : imei1.Trim(),
                        IMEI2 = string.IsNullOrWhiteSpace(imei2) ? null : imei2.Trim()
                    });
                }

                DgvCellPhones.Focus();
                Activate();
                return;
            }

            var existing = TxtSerialNumbers.Text
                .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(s => s.Trim());

            if (!existing.Any(s => string.Equals(s, trimmed, StringComparison.OrdinalIgnoreCase)))
            {
                TxtSerialNumbers.Text = TxtSerialNumbers.Text.Length == 0
                    ? trimmed
                    : TxtSerialNumbers.Text + Environment.NewLine + trimmed;
            }

            TxtSerialNumbers.Focus();
            TxtSerialNumbers.CaretIndex = TxtSerialNumbers.Text.Length;
            Activate();
        }

        private void SelectCellPhoneCategoryIfAvailable()
        {
            foreach (var item in CmbCategory.Items)
            {
                var cat = item as CategoryItem;
                if (IsCellPhoneCategory(cat))
                {
                    CmbCategory.SelectedItem = cat;
                    CmbCategory_SelectionChanged(CmbCategory, null);
                    return;
                }
            }
        }
        // ─────────────────────────────────────────────────────────────────────────
        // Private inner classes (ported verbatim)
        // ─────────────────────────────────────────────────────────────────────────
        private class CategoryItem
        {
            public int    CategoryId { get; set; }
            public string Name       { get; set; }
            public override string ToString() => Name;
        }

        private class ConditionItem
        {
            public int    ConditionId   { get; set; }
            public string ConditionName { get; set; }
            public override string ToString() => ConditionName;
        }

        private class VendorItem
        {
            public int    VendorId   { get; set; }
            public string VendorName { get; set; }
            public override string ToString() => VendorName;
        }

        private class CartridgeModelItem
        {
            public int     CartridgeModelId { get; set; }
            public string  ModelNumber      { get; set; }
            public string  VendorName       { get; set; }
            public int?    VendorId         { get; set; }
            public override string ToString() => string.IsNullOrWhiteSpace(VendorName)
                ? ModelNumber
                : $"{ModelNumber} - {VendorName}";
        }

        private sealed class QuickAddCartridgeModelResult
        {
            public int    CartridgeModelId { get; set; }
            public string ModelNumber      { get; set; }
        }

        private class ConsumableModelItem
        {
            public int    ConsumableModelId { get; set; }
            public string ModelNumber       { get; set; }
            public override string ToString() => ModelNumber;
        }

        private sealed class QuickAddConsumableModelResult
        {
            public int    ConsumableModelId { get; set; }
            public string ModelNumber       { get; set; }
        }

        private class ValidationResult
        {
            public bool     IsValid        { get; set; }
            public string   ErrorMessage   { get; set; }
            public string[] SerialNumbers  { get; set; }
            public int      Quantity       { get; set; }
            public bool     IsNonSerialized { get; set; }
            public bool     IsCellPhone    { get; set; }
            public List<CellPhoneRowItem> CellPhoneRows { get; set; }
        }

        private class CellPhoneRowItem
        {
            // Display-only absolute row position across all pages — recomputed by
            // RefreshCellPhoneRowsView(), never persisted.
            public int    RowNumber       { get; set; }
            public string CellPhoneNumber { get; set; }
            public string SerialNumber    { get; set; }
            public string IMEI1           { get; set; }
            public string IMEI2           { get; set; }
        }

        private class BatchAddResult
        {
            public int          SuccessCount   { get; set; }
            public List<string> FailedSerials  { get; set; } = new List<string>();
            public List<int>    ItemIds        { get; set; } = new List<int>();
        }
    }
}

