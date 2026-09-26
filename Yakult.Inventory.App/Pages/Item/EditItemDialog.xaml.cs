using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using Yakult.Inventory.App.Core;
using Yakult.Inventory.App.Models;
using Yakult.Inventory.App.Pages.Category;
using Yakult.Inventory.App.Repositories;
using Yakult.Inventory.App.Session;

// Disambiguate MessageBox / DialogResult — callers may have both namespaces open
using WinMsgBox = System.Windows.MessageBox;
using WinForms  = System.Windows.Forms;

namespace Yakult.Inventory.App.Pages.Item
{
    public partial class EditItemDialog : Window, IDisposable
    {
        private readonly ItemDto _item;
        private readonly string _connectionString;
        private readonly bool _originalIsArchived;
        private bool _copyAdded; // true once "Add a Copy" has successfully created at least one new item

        public EditItemDialog(ItemDto item)
        {
            _item = item ?? throw new ArgumentNullException(nameof(item));
            _connectionString = DatabaseConfig.ConnectionString;
            _originalIsArchived = _item.IsArchived;

            InitializeComponent();

            CmbItemType.Items.Add("Hardware");
            CmbItemType.Items.Add("Software/License");
            CmbItemType.Items.Add("Services");

            CmbSubType.Items.Add(""); // blank = no default Sub-Type
            foreach (var subType in ItemSubTypeCatalog.ValidSubTypes)
                CmbSubType.Items.Add(subType);

            CmbCartridgeOrigin.Items.Add("Brand New");
            CmbCartridgeOrigin.Items.Add("Refilled");

            CmbAcquisitionType.Items.Add("(None)");
            CmbAcquisitionType.Items.Add("Request");
            CmbAcquisitionType.Items.Add("Invoice");
            CmbAcquisitionType.Items.Add("Both");
            CmbAcquisitionType.SelectedIndex = 0;

            CmbUnitOfMeasure.Items.Add("Unit");
            CmbUnitOfMeasure.Items.Add("Piece");
            CmbUnitOfMeasure.Items.Add("Cartridge");
            CmbUnitOfMeasure.Items.Add("Box");
            CmbUnitOfMeasure.Items.Add("Set");

            LoadCategories();
            LoadConditions();
            LoadVendors();
            LoadCartridgeModels(selectCartridgeModelId: _item.CartridgeModelId);
            LoadConsumableModelIdForItem(); // populates _item.ConsumableModelId, then LoadConsumableModels
            LoadItemData();
            LoadUnavailabilityInfo();
            LoadLastRepairedInfo();

            // "Add a Copy" adds new items through BatchAddItemDialog independently of Save.
            // If the dialog is later dismissed via Cancel/close, promote the result so the
            // caller still reloads the grid and picks up the newly-created copies.
            Closing += (s, e) =>
            {
                if (_copyAdded && DialogResult != true)
                    DialogResult = true;
            };
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
            // This dialog is opened from ItemsPageView, a WPF UserControl hosted inside a
            // WinForms ElementHost — there is no WPF Window ancestor to inherit an Owner from.
            // Without a real Win32 owner, the window doesn't participate correctly in the
            // taskbar's Z-order: switching to another application leaves it stuck on top
            // instead of going behind. Resolve the active WinForms window as owner, same
            // fallback WpfPortalTourService uses for this exact interop gap.
            var activeForm = WinForms.Form.ActiveForm;
            if (activeForm != null)
                new WindowInteropHelper(this).Owner = activeForm.Handle;

            ClampToWorkArea();

            bool? result = base.ShowDialog();
            return (result == true) ? WinForms.DialogResult.OK : WinForms.DialogResult.Cancel;
        }

        // WindowStartupLocation="CenterOwner" centers using the full screen bounds, not the
        // taskbar-trimmed work area, so a window this tall can land with its footer
        // (Save/Cancel/Add a Copy) rendered behind the taskbar. Shrink to fit and center
        // within the actual work area before the window is shown.
        private void ClampToWorkArea()
        {
            var workArea = SystemParameters.WorkArea;
            const double margin = 24;

            if (Width > workArea.Width - margin) Width = Math.Max(MinWidth, workArea.Width - margin);
            if (Height > workArea.Height - margin) Height = Math.Max(MinHeight, workArea.Height - margin);

            WindowStartupLocation = WindowStartupLocation.Manual;
            Left = workArea.Left + (workArea.Width - Width) / 2;
            Top = workArea.Top + (workArea.Height - Height) / 2;
        }

        // ─────────────────────────────────────────────────────────────────────────
        // Header drag / minimize / maximize / close
        // ─────────────────────────────────────────────────────────────────────────
        private void OnHeaderDrag(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left && e.ClickCount == 1)
                DragMove();
        }

        private void OnMinimizeClick(object sender, MouseButtonEventArgs e)
        {
            e.Handled = true;
            Yakult.Inventory.App.Helpers.ModalMinimizeGuard.Minimize(this);
        }

        private void OnMaximizeClick(object sender, MouseButtonEventArgs e)
        {
            e.Handled = true;
            WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
        }

        private void OnCloseClick(object sender, MouseButtonEventArgs e)
        {
            e.Handled = true;
            Close();
        }

        protected override void OnStateChanged(EventArgs e)
        {
            base.OnStateChanged(e);
            BtnMaximizeGlyph.Text = WindowState == WindowState.Maximized ? "❐" : "□";
        }

        // WindowStyle="None" + ResizeMode="CanResizeWithGrip" windows keep the invisible
        // resize-frame Windows normally reserves for sizable windows, which insets a
        // maximized window from the real work area unless we report it ourselves.
        // See BatchAddItemDialog.xaml.cs for the original diagnosis of this issue.
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

        // A closed ComboBox still consumes the mouse wheel to change its selected value,
        // which caused accidental field changes while users scrolled through the dialog.
        // Swallow the wheel event here and re-raise it on the parent so the dialog's
        // ScrollViewer scrolls instead. Only suppressed while the dropdown is closed —
        // scrolling an open dropdown list still works normally.
        private void FormCombo_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            var combo = (ComboBox)sender;
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

        // ─────────────────────────────────────────────────────────────────────────
        // Data loading
        // ─────────────────────────────────────────────────────────────────────────
        private void LoadCategories()
        {
            try
            {
                CmbCategory.Items.Clear();

                using (var con = new SqlConnection(_connectionString))
                {
                    con.Open();

                    string query = @"
                        SELECT CategoryId, Name
                        FROM dbo.ItemCategory
                        WHERE Active = 1
                        ORDER BY Name";

                    using (var cmd = new SqlCommand(query, con))
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            CmbCategory.Items.Add(new CategoryItem
                            {
                                CategoryId = reader.GetInt32(0),
                                Name = reader.GetString(1)
                            });
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

                using (var con = new SqlConnection(_connectionString))
                {
                    con.Open();

                    string query = @"
                        SELECT ConditionId, ConditionName
                        FROM dbo.Condition
                        ORDER BY ConditionId";

                    using (var cmd = new SqlCommand(query, con))
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            CmbCondition.Items.Add(new ConditionItem
                            {
                                ConditionId = reader.GetInt32(0),
                                ConditionName = reader.GetString(1)
                            });
                        }
                    }
                }

                for (int i = 0; i < CmbCondition.Items.Count; i++)
                {
                    if (CmbCondition.Items[i] is ConditionItem item &&
                        item.ConditionName.Equals("Good", StringComparison.OrdinalIgnoreCase))
                    {
                        CmbCondition.SelectedIndex = i;
                        break;
                    }
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

                CmbVendor.Items.Add(new VendorItem
                {
                    VendorId = 0,
                    VendorName = "(None)"
                });

                using (var con = new SqlConnection(_connectionString))
                {
                    con.Open();

                    string query = @"
                        SELECT v.VendorID, v.VendorName
                        FROM dbo.Vendor v
                        LEFT JOIN dbo.ArchiveStatus arc ON arc.EntityType = 'Vendor' AND arc.EntityId = v.VendorID AND arc.IsArchived = 1
                        WHERE v.IsActive = 1 AND arc.ArchiveId IS NULL
                        ORDER BY v.VendorName";

                    using (var cmd = new SqlCommand(query, con))
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            CmbVendor.Items.Add(new VendorItem
                            {
                                VendorId = reader.GetInt32(0),
                                VendorName = reader.GetString(1)
                            });
                        }
                    }
                }

                if (CmbVendor.Items.Count > 0)
                    CmbVendor.SelectedIndex = 0;
            }
            catch (Exception ex)
            {
                WinMsgBox.Show($"Failed to load vendors: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void LoadCartridgeModels(string selectModelNumber = null, int? selectVendorId = null, int? selectCartridgeModelId = null)
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

                var cartridgeModelRepo = new CartridgeModelRepository();
                var models = await cartridgeModelRepo.GetAllActiveModelsAsync();

                foreach (var model in models)
                {
                    CmbCartridgeModel.Items.Add(new CartridgeModelItem
                    {
                        CartridgeModelId = model.CartridgeModelId,
                        ModelNumber = model.ModelNumber,
                        VendorName = model.VendorName,
                        VendorId = model.VendorId
                    });
                }

                if (selectCartridgeModelId.HasValue && selectCartridgeModelId.Value > 0)
                {
                    for (int i = 0; i < CmbCartridgeModel.Items.Count; i++)
                    {
                        if (CmbCartridgeModel.Items[i] is CartridgeModelItem item
                            && item.CartridgeModelId == selectCartridgeModelId.Value)
                        {
                            CmbCartridgeModel.SelectedIndex = i;
                            break;
                        }
                    }
                }
                else if (!string.IsNullOrWhiteSpace(selectModelNumber))
                {
                    for (int i = 0; i < CmbCartridgeModel.Items.Count; i++)
                    {
                        if (CmbCartridgeModel.Items[i] is CartridgeModelItem item
                            && !string.IsNullOrWhiteSpace(item.ModelNumber)
                            && string.Equals(item.ModelNumber.Trim(), selectModelNumber.Trim(), StringComparison.OrdinalIgnoreCase))
                        {
                            CmbCartridgeModel.SelectedIndex = i;
                            break;
                        }
                    }
                }

                if (CmbCartridgeModel.SelectedIndex < 0 && CmbCartridgeModel.Items.Count > 0)
                    CmbCartridgeModel.SelectedIndex = 0;

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

        private async void LoadConsumableModelIdForItem()
        {
            try
            {
                using (var con = new SqlConnection(_connectionString))
                {
                    await con.OpenAsync();
                    using (var cmd = new SqlCommand("SELECT ConsumableModelId FROM dbo.Item WHERE ItemId = @ItemId", con))
                    {
                        cmd.Parameters.AddWithValue("@ItemId", _item.ItemId);
                        var result = await cmd.ExecuteScalarAsync();
                        _item.ConsumableModelId = (result == null || result == DBNull.Value) ? (int?)null : Convert.ToInt32(result);
                    }
                }
            }
            catch
            {
                _item.ConsumableModelId = null;
            }

            LoadConsumableModels(selectConsumableModelId: _item.ConsumableModelId);
        }

        private async void LoadConsumableModels(string selectModelNumber = null, int? selectConsumableModelId = null)
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

                foreach (var model in models)
                {
                    CmbConsumableModel.Items.Add(new ConsumableModelItem
                    {
                        ConsumableModelId = model.ConsumableModelId,
                        ModelNumber = model.ModelNumber
                    });
                }

                if (selectConsumableModelId.HasValue && selectConsumableModelId.Value > 0)
                {
                    for (int i = 0; i < CmbConsumableModel.Items.Count; i++)
                    {
                        if (CmbConsumableModel.Items[i] is ConsumableModelItem item
                            && item.ConsumableModelId == selectConsumableModelId.Value)
                        {
                            CmbConsumableModel.SelectedIndex = i;
                            break;
                        }
                    }
                }
                else if (!string.IsNullOrWhiteSpace(selectModelNumber))
                {
                    for (int i = 0; i < CmbConsumableModel.Items.Count; i++)
                    {
                        if (CmbConsumableModel.Items[i] is ConsumableModelItem item
                            && !string.IsNullOrWhiteSpace(item.ModelNumber)
                            && string.Equals(item.ModelNumber.Trim(), selectModelNumber.Trim(), StringComparison.OrdinalIgnoreCase))
                        {
                            CmbConsumableModel.SelectedIndex = i;
                            break;
                        }
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

        private void LoadItemData()
        {
            TxtItemId.Text = _item.ItemId.ToString();
            TxtName.Text = _item.Name;
            TxtDescription.Text = _item.Description;
            TxtSerialNumber.Text = _item.SerialNumber;
            TxtModelNumber.Text = _item.ModelNumber;
            CmbUnitOfMeasure.Text = _item.UnitOfMeasure;
            TxtStockOnHand.Text = _item.StockOnHand.ToString();
            TxtAmount.Text = _item.Amount.ToString("0.00");
            ChkActive.IsChecked = _item.Active;
            ChkIsBorrowable.IsChecked = _item.IsBorrowable == true;
            ChkIsArchived.IsChecked = _item.IsArchived;

            // Select the correct category
            if (!string.IsNullOrWhiteSpace(_item.Category))
            {
                for (int i = 0; i < CmbCategory.Items.Count; i++)
                {
                    if (CmbCategory.Items[i] is CategoryItem cat && cat.Name == _item.Category)
                    {
                        CmbCategory.SelectedIndex = i;
                        break;
                    }
                }
            }

            // Show/hide cartridge-specific fields (model selection happens in LoadCartridgeModels after async load)
            bool isCartridgeCategory = string.Equals(_item.Category, "Cartridge", StringComparison.OrdinalIgnoreCase);
            RowCartridgeModel.Visibility = isCartridgeCategory ? Visibility.Visible : Visibility.Collapsed;
            RowCartridgeOrigin.Visibility = isCartridgeCategory ? Visibility.Visible : Visibility.Collapsed;
            if (isCartridgeCategory)
            {
                // NULL = Brand New (index 0), 'Available' = Refilled (index 1)
                CmbCartridgeOrigin.SelectedIndex =
                    string.Equals(_item.RefillStatus, "Available", StringComparison.OrdinalIgnoreCase) ? 1 : 0;
            }

            // Show/hide consumable model field (Category = Ink/Toner/Print Head)
            bool isConsumableCategory = CanonicalConsumableCategory(_item.Category) != null;
            RowConsumableModel.Visibility = isConsumableCategory ? Visibility.Visible : Visibility.Collapsed;

            // Show/hide cell phone tracking fields (Category = CellPhone)
            bool isCellPhoneCategory = IsCellPhoneCategory(_item.Category);
            RowCellPhoneNumber.Visibility = isCellPhoneCategory ? Visibility.Visible : Visibility.Collapsed;
            RowImei1.Visibility = isCellPhoneCategory ? Visibility.Visible : Visibility.Collapsed;
            RowImei2.Visibility = isCellPhoneCategory ? Visibility.Visible : Visibility.Collapsed;
            TxtCellPhoneNumber.Text = _item.CellPhoneNumber ?? string.Empty;
            TxtImei1.Text = _item.IMEI1 ?? string.Empty;
            TxtImei2.Text = _item.IMEI2 ?? string.Empty;

            // Select the correct ItemType
            if (!string.IsNullOrWhiteSpace(_item.ItemType))
            {
                for (int i = 0; i < CmbItemType.Items.Count; i++)
                {
                    if (CmbItemType.Items[i].ToString() == _item.ItemType)
                    {
                        CmbItemType.SelectedIndex = i;
                        break;
                    }
                }
            }
            else
            {
                CmbItemType.SelectedIndex = 0;
            }

            // Select the correct Sub-Type (blank if none)
            CmbSubType.SelectedIndex = 0;
            if (!string.IsNullOrWhiteSpace(_item.SubType))
            {
                for (int i = 0; i < CmbSubType.Items.Count; i++)
                {
                    if (string.Equals(CmbSubType.Items[i] as string, _item.SubType, StringComparison.OrdinalIgnoreCase))
                    {
                        CmbSubType.SelectedIndex = i;
                        break;
                    }
                }
            }

            // Select the correct condition
            if (_item.ConditionId > 0)
            {
                for (int i = 0; i < CmbCondition.Items.Count; i++)
                {
                    if (CmbCondition.Items[i] is ConditionItem cond && cond.ConditionId == _item.ConditionId)
                    {
                        CmbCondition.SelectedIndex = i;
                        break;
                    }
                }
            }

            // Select the correct vendor
            if (_item.VendorId.HasValue && _item.VendorId.Value > 0)
            {
                for (int i = 0; i < CmbVendor.Items.Count; i++)
                {
                    if (CmbVendor.Items[i] is VendorItem vendor && vendor.VendorId == _item.VendorId.Value)
                    {
                        CmbVendor.SelectedIndex = i;
                        break;
                    }
                }
            }
            else
            {
                CmbVendor.SelectedIndex = 0;
            }

            bool isServices = string.Equals(_item.ItemType, "Services", StringComparison.OrdinalIgnoreCase);
            bool isSoftwareLicense = string.Equals(_item.ItemType, "Software/License", StringComparison.OrdinalIgnoreCase);
            bool isHardware = string.Equals(_item.ItemType, "Hardware", StringComparison.OrdinalIgnoreCase);
            bool enableServiceDates = (isServices || isSoftwareLicense || isHardware)
                                     && (_item.EndDate.HasValue
                                         || (_item.StartDate.HasValue && _item.StartDate.Value != _item.DateCreated));

            ChkServiceContractDates.IsChecked = enableServiceDates;

            // StartDate/EndDate:
            // - Default behavior: StartDate tracks DateCreated (read-only), EndDate optional.
            // - Services/Software-License (when enabled via checkbox): allow editing both StartDate and EndDate.
            DpStartDate.SelectedDate = _item.StartDate ?? _item.DateCreated;

            if (enableServiceDates)
            {
                DpEndDate.SelectedDate = _item.EndDate ?? DpStartDate.SelectedDate.Value.AddDays(1);
                ChkEndDate.IsChecked = true;
            }
            else
            {
                if (_item.EndDate.HasValue)
                {
                    DpEndDate.SelectedDate = _item.EndDate.Value;
                    ChkEndDate.IsChecked = true;
                }
                else
                {
                    ChkEndDate.IsChecked = false;
                }
            }

            ApplyServiceContractDateMode();

            TxtRemarks.Text = _item.Remarks ?? string.Empty;

            // === LICENSE NUMBER FIELD ===
            bool showLicenseNumber = _item.ItemType == "Software/License" || _item.ItemType == "Service" || _item.ItemType == "Services" || _item.ItemType == "Hardware";
            RowLicenseNumber.Visibility = showLicenseNumber ? Visibility.Visible : Visibility.Collapsed;
            TxtLicenseNumber.Text = _item.LicenseNumber ?? string.Empty;

            // === WARRANTY FIELDS ===
            TxtWarrantyYears.Text = _item.WarrantyYears.ToString();

            if (_item.WarrantyStartDate.HasValue)
            {
                DpWarrantyStart.SelectedDate = _item.WarrantyStartDate.Value;
                ChkWarrantyStart.IsChecked = true;
            }
            else
            {
                ChkWarrantyStart.IsChecked = false;
            }

            if (_item.WarrantyEndDate.HasValue)
            {
                DpWarrantyEnd.SelectedDate = _item.WarrantyEndDate.Value;
                ChkWarrantyEnd.IsChecked = true;
            }
            else
            {
                ChkWarrantyEnd.IsChecked = false;
            }

            // === DATE PURCHASED FIELD ===
            if (_item.DatePurchased.HasValue)
            {
                DpDatePurchased.SelectedDate = _item.DatePurchased.Value;
                ChkDatePurchased.IsChecked = true;
            }
            else
            {
                ChkDatePurchased.IsChecked = false;
            }

            // === FIXED ASSET (IsTrackedAsset) CHECKBOX ===
            // Always editable — a Set/Renewal tie doesn't get retroactively rewritten when this
            // changes, but that's fine: this flag is just a classification toggle, not something
            // those records depend on for correctness. Still surface a heads-up (non-blocking)
            // when the item has that history, so changing it is a deliberate choice.
            ChkIsTrackedAsset.IsChecked = _item.IsTrackedAsset;
            RowTrackedAssetWarning.Visibility = CheckIfItemIsTracked(_item.ItemId)
                ? Visibility.Visible
                : Visibility.Collapsed;

            // === ACQUISITION TYPE ===
            if (!string.IsNullOrWhiteSpace(_item.AcquisitionType))
            {
                for (int i = 0; i < CmbAcquisitionType.Items.Count; i++)
                {
                    if (CmbAcquisitionType.Items[i].ToString().Equals(_item.AcquisitionType, StringComparison.OrdinalIgnoreCase))
                    {
                        CmbAcquisitionType.SelectedIndex = i;
                        break;
                    }
                }
            }
            else
            {
                CmbAcquisitionType.SelectedIndex = 0;
            }

            // === PART NUMBER (from dbo.Renewals) ===
            try
            {
                using (var con = new SqlConnection(_connectionString))
                {
                    con.Open();
                    const string renewalSql = @"
                        SELECT TOP 1 PartNumber
                        FROM dbo.Renewals
                        WHERE ItemId = @ItemId
                          AND IsArchived = 0";
                    using (var cmd = new SqlCommand(renewalSql, con))
                    {
                        cmd.Parameters.AddWithValue("@ItemId", _item.ItemId);
                        var value = cmd.ExecuteScalar();
                        TxtPartNumber.Text = (value == null || value == DBNull.Value)
                            ? string.Empty
                            : value.ToString();
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[EditItem] Failed to load PartNumber: {ex.Message}");
            }
        }

        // Item shows as active in the DB (DbActive) but effectively unavailable (Active/EffectiveActive
        // is false) because it's tied up in one or more active Requests. Surface which Request(s)/Set(s)
        // are holding it so the user doesn't have to guess why it can't be reactivated.
        private void LoadUnavailabilityInfo()
        {
            if (!_item.DbActive || _item.Active)
                return; // Not the "purple" unavailable-due-to-transaction case

            try
            {
                using (var con = new SqlConnection(_connectionString))
                {
                    con.Open();
                    const string sql = @"
                        SELECT r.ReqId, s.SetCode
                        FROM dbo.Request r
                        LEFT JOIN dbo.[Set] s ON s.SetId = r.SetId
                        WHERE r.ItemId = @ItemId AND r.Active = 1
                        ORDER BY r.ReqId";
                    var parts = new List<string>();
                    using (var cmd = new SqlCommand(sql, con))
                    {
                        cmd.Parameters.AddWithValue("@ItemId", _item.ItemId);
                        using (var reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                int reqId = reader.GetInt32(0);
                                string setCode = reader.IsDBNull(1) ? null : reader.GetString(1);
                                parts.Add(setCode != null ? $"Req #{reqId} ({setCode})" : $"Req #{reqId}");
                            }
                        }
                    }

                    if (parts.Count > 0)
                    {
                        LblUnavailableInfo.Text = "Unavailable – held by " + string.Join(", ", parts);
                        LblUnavailableInfo.Visibility = Visibility.Visible;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[EditItem] Failed to load unavailability info: {ex.Message}");
            }
        }

        // Informational only — surfaces the Repair Technician Portal's most recent Completed
        // ticket for this item, without touching Condition (Condition stays a plain Good/Damaged
        // binary; "was repaired" lives entirely in RepairTicket, not in the shared Condition lookup).
        private void LoadLastRepairedInfo()
        {
            try
            {
                using (var con = new SqlConnection(_connectionString))
                {
                    con.Open();
                    const string sql = @"
                        SELECT TOP (1) TicketCode, CompletedAt
                        FROM dbo.RepairTicket
                        WHERE ItemId = @ItemId AND Status = 'Completed'
                        ORDER BY RepairTicketId DESC";
                    using (var cmd = new SqlCommand(sql, con))
                    {
                        cmd.Parameters.AddWithValue("@ItemId", _item.ItemId);
                        using (var reader = cmd.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                string ticketCode = reader.IsDBNull(0) ? null : reader.GetString(0);
                                DateTime? completedAt = reader.IsDBNull(1) ? (DateTime?)null : reader.GetDateTime(1);

                                string text = completedAt.HasValue
                                    ? $"Repaired {completedAt.Value:MMM d, yyyy}"
                                    : "Repaired";
                                if (!string.IsNullOrWhiteSpace(ticketCode))
                                    text += $" (Ticket {ticketCode})";

                                LblLastRepairedInfo.Text = text;
                                LblLastRepairedInfo.Visibility = Visibility.Visible;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[EditItem] Failed to load last-repaired info: {ex.Message}");
            }
        }

        // Non-blocking: only drives the "already has Set/Renewal history" warning label next to
        // the Fixed Asset checkbox. Does not disable or otherwise gate the checkbox itself.
        private bool CheckIfItemIsTracked(int itemId)
        {
            try
            {
                using (var con = new SqlConnection(_connectionString))
                {
                    con.Open();

                    string setQuery = "SELECT COUNT(*) FROM dbo.SetItem WHERE ItemId = @ItemId";
                    using (var cmd = new SqlCommand(setQuery, con))
                    {
                        cmd.Parameters.AddWithValue("@ItemId", itemId);
                        int setCount = (int)cmd.ExecuteScalar();
                        if (setCount > 0) return true;
                    }

                    // Every item gets a baseline Renewals row (RenewalStatus = 'Active') at
                    // creation time (see ItemRepository.InsertItemGraph) — that's bookkeeping,
                    // not evidence of real renewal history. A genuine renewal/archive event
                    // inserts a NEW row with RenewalStatus 'Renewed' or 'Archived', so only
                    // those count here.
                    string renewalQuery = "SELECT COUNT(*) FROM dbo.Renewals WHERE ItemId = @ItemId AND RenewalStatus <> 'Active'";
                    using (var cmd = new SqlCommand(renewalQuery, con))
                    {
                        cmd.Parameters.AddWithValue("@ItemId", itemId);
                        int renewalCount = (int)cmd.ExecuteScalar();
                        if (renewalCount > 0) return true;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error checking if item is tracked: {ex.Message}");
            }

            return false;
        }

        // Warranty Years is just a display/reporting convenience derived from Start/End —
        // Start and End remain the source of truth the user edits directly. Keep Years in
        // sync whenever either date changes, without feeding back into the dates themselves.
        private void WarrantyDates_ValueChanged(object sender, SelectionChangedEventArgs e)
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

        private int ParseWarrantyYears()
        {
            int.TryParse(TxtWarrantyYears.Text, out int years);
            return Math.Max(0, Math.Min(10, years));
        }

        private async void ChkActive_CheckedOrUncheckedChanged(object sender, RoutedEventArgs e)
        {
            // If trying to check Active (make item available again), check if item has requests
            if (ChkActive.IsChecked == true)
            {
                var itemRepo = new ItemRepository();
                var (hasDependencies, setItemCount, requestCount, inventoryCount) = await itemRepo.CheckItemDependencies(_item.ItemId);

                if (requestCount > 0)
                {
                    ChkActive.Checked -= ChkActive_CheckedOrUncheckedChanged;
                    ChkActive.Unchecked -= ChkActive_CheckedOrUncheckedChanged;
                    ChkActive.IsChecked = false;
                    ChkActive.Checked += ChkActive_CheckedOrUncheckedChanged;
                    ChkActive.Unchecked += ChkActive_CheckedOrUncheckedChanged;

                    WinMsgBox.Show(
                        $"Cannot mark this item as active!\n\n" +
                        $"This item has {requestCount} request(s) associated with it.\n\n" +
                        $"You must resolve or remove all requests before marking this item as active again.",
                        "Item Has Requests",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                }
            }
        }

        private void ChkIsArchived_Checked(object sender, RoutedEventArgs e)
        {
            // When item is archived, automatically set it to inactive
            if (ChkIsArchived.IsChecked == true)
                ChkActive.IsChecked = false;
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
                Text = "Add Cartridge Model",
                Width = 450,
                Height = 320,
                FormBorderStyle = WinForms.FormBorderStyle.FixedDialog,
                StartPosition = WinForms.FormStartPosition.CenterParent,
                MaximizeBox = false,
                MinimizeBox = false
            };

            var mainPanel = new WinForms.TableLayoutPanel
            {
                Dock = WinForms.DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 4,
                Padding = new WinForms.Padding(20)
            };
            mainPanel.ColumnStyles.Add(new WinForms.ColumnStyle(WinForms.SizeType.Absolute, 120F));
            mainPanel.ColumnStyles.Add(new WinForms.ColumnStyle(WinForms.SizeType.Percent, 100F));
            dialog.Controls.Add(mainPanel);

            var lblModelNumber = new WinForms.Label { Text = "Model Number *", Dock = WinForms.DockStyle.Fill };
            var txtModelNumber = new WinForms.TextBox { Dock = WinForms.DockStyle.Fill };
            var initial = TxtModelNumber?.Text?.Trim();
            if (!string.IsNullOrWhiteSpace(initial))
                txtModelNumber.Text = initial;
            mainPanel.Controls.Add(lblModelNumber, 0, 0);
            mainPanel.Controls.Add(txtModelNumber, 1, 0);

            var chkRequestable = new WinForms.CheckBox { Text = "Is Requestable", Checked = true, Dock = WinForms.DockStyle.Fill };
            mainPanel.Controls.Add(new WinForms.Label(), 0, 1);
            mainPanel.Controls.Add(chkRequestable, 1, 1);

            var chkRefillable = new WinForms.CheckBox { Text = "Is Refillable", Checked = true, Dock = WinForms.DockStyle.Fill };
            mainPanel.Controls.Add(new WinForms.Label(), 0, 2);
            mainPanel.Controls.Add(chkRefillable, 1, 2);

            var buttonPanel = new WinForms.FlowLayoutPanel
            {
                FlowDirection = WinForms.FlowDirection.RightToLeft,
                Dock = WinForms.DockStyle.Fill,
                AutoSize = true
            };

            var btnSave = new WinForms.Button { Text = "Save", Width = 80, DialogResult = WinForms.DialogResult.None };
            var btnCancel = new WinForms.Button { Text = "Cancel", Width = 80, DialogResult = WinForms.DialogResult.Cancel };

            btnSave.Click += async (s, e) =>
            {
                if (string.IsNullOrWhiteSpace(txtModelNumber.Text))
                {
                    WinForms.MessageBox.Show("Please enter a model number.", "Validation Error",
                        WinForms.MessageBoxButtons.OK, WinForms.MessageBoxIcon.Warning);
                    return;
                }

                try
                {
                    btnSave.Enabled = false;
                    btnSave.Text = "Saving...";

                    var repository = new CartridgeModelRepository();
                    var existing = await repository.FindByModelNumberAsync(txtModelNumber.Text.Trim());
                    if (existing != null)
                    {
                        WinForms.MessageBox.Show($"Model number '{txtModelNumber.Text.Trim()}' already exists.",
                            "Duplicate", WinForms.MessageBoxButtons.OK, WinForms.MessageBoxIcon.Warning);
                        btnSave.Enabled = true;
                        btnSave.Text = "Save";
                        return;
                    }

                    var model = new CartridgeModelDto
                    {
                        ModelNumber = txtModelNumber.Text.Trim(),
                        IsRequestable = chkRequestable.Checked,
                        IsRefillable = chkRefillable.Checked,
                        IsActive = true,
                        CreatedBy = AppSession.CurrentUserId,
                        CreatedAt = DateTime.Now
                    };

                    var newId = await repository.CreateAsync(model);
                    WinForms.MessageBox.Show("Cartridge model added successfully!", "Success",
                        WinForms.MessageBoxButtons.OK, WinForms.MessageBoxIcon.Information);

                    dialog.Tag = new QuickAddCartridgeModelResult
                    {
                        CartridgeModelId = newId,
                        ModelNumber = model.ModelNumber
                    };

                    dialog.DialogResult = WinForms.DialogResult.OK;
                }
                catch (Exception ex)
                {
                    WinForms.MessageBox.Show($"Error: {ex.Message}", "Error",
                        WinForms.MessageBoxButtons.OK, WinForms.MessageBoxIcon.Error);
                    btnSave.Enabled = true;
                    btnSave.Text = "Save";
                }
            };

            buttonPanel.Controls.Add(btnSave);
            buttonPanel.Controls.Add(btnCancel);
            mainPanel.Controls.Add(buttonPanel, 0, 3);
            mainPanel.SetColumnSpan(buttonPanel, 2);

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
                Text = "Add " + category + " Model",
                Width = 450,
                Height = 260,
                FormBorderStyle = WinForms.FormBorderStyle.FixedDialog,
                StartPosition = WinForms.FormStartPosition.CenterParent,
                MaximizeBox = false,
                MinimizeBox = false
            };

            var mainPanel = new WinForms.TableLayoutPanel
            {
                Dock = WinForms.DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 3,
                Padding = new WinForms.Padding(20)
            };
            mainPanel.ColumnStyles.Add(new WinForms.ColumnStyle(WinForms.SizeType.Absolute, 120F));
            mainPanel.ColumnStyles.Add(new WinForms.ColumnStyle(WinForms.SizeType.Percent, 100F));
            dialog.Controls.Add(mainPanel);

            var lblModelNumber = new WinForms.Label { Text = "Model Number *", Dock = WinForms.DockStyle.Fill };
            var txtModelNumberInput = new WinForms.TextBox { Dock = WinForms.DockStyle.Fill };
            var initial = TxtModelNumber?.Text?.Trim();
            if (!string.IsNullOrWhiteSpace(initial))
                txtModelNumberInput.Text = initial;
            mainPanel.Controls.Add(lblModelNumber, 0, 0);
            mainPanel.Controls.Add(txtModelNumberInput, 1, 0);

            var chkRequestable = new WinForms.CheckBox { Text = "Is Requestable", Checked = true, Dock = WinForms.DockStyle.Fill };
            mainPanel.Controls.Add(new WinForms.Label(), 0, 1);
            mainPanel.Controls.Add(chkRequestable, 1, 1);

            var buttonPanel = new WinForms.FlowLayoutPanel
            {
                FlowDirection = WinForms.FlowDirection.RightToLeft,
                Dock = WinForms.DockStyle.Fill,
                AutoSize = true
            };

            var btnSaveModel = new WinForms.Button { Text = "Save", Width = 80, DialogResult = WinForms.DialogResult.None };
            var btnCancelModel = new WinForms.Button { Text = "Cancel", Width = 80, DialogResult = WinForms.DialogResult.Cancel };

            btnSaveModel.Click += async (s, e) =>
            {
                if (string.IsNullOrWhiteSpace(txtModelNumberInput.Text))
                {
                    WinForms.MessageBox.Show("Please enter a model number.", "Validation Error",
                        WinForms.MessageBoxButtons.OK, WinForms.MessageBoxIcon.Warning);
                    return;
                }

                try
                {
                    btnSaveModel.Enabled = false;
                    btnSaveModel.Text = "Saving...";

                    var repository = new ConsumableModelRepository();
                    var existing = await repository.FindByModelNumberAsync(txtModelNumberInput.Text.Trim(), category);
                    if (existing != null)
                    {
                        WinForms.MessageBox.Show($"Model number '{txtModelNumberInput.Text.Trim()}' already exists for {category}.",
                            "Duplicate", WinForms.MessageBoxButtons.OK, WinForms.MessageBoxIcon.Warning);
                        btnSaveModel.Enabled = true;
                        btnSaveModel.Text = "Save";
                        return;
                    }

                    var model = new ConsumableModelDto
                    {
                        ModelNumber = txtModelNumberInput.Text.Trim(),
                        Category = category,
                        IsRequestable = chkRequestable.Checked,
                        IsActive = true,
                        CreatedBy = AppSession.CurrentUserId,
                        CreatedAt = DateTime.Now
                    };

                    var newId = await repository.CreateAsync(model);
                    WinForms.MessageBox.Show($"{category} model added successfully!", "Success",
                        WinForms.MessageBoxButtons.OK, WinForms.MessageBoxIcon.Information);

                    dialog.Tag = new QuickAddConsumableModelResult
                    {
                        ConsumableModelId = newId,
                        ModelNumber = model.ModelNumber
                    };

                    dialog.DialogResult = WinForms.DialogResult.OK;
                }
                catch (Exception ex)
                {
                    WinForms.MessageBox.Show($"Error: {ex.Message}", "Error",
                        WinForms.MessageBoxButtons.OK, WinForms.MessageBoxIcon.Error);
                    btnSaveModel.Enabled = true;
                    btnSaveModel.Text = "Save";
                }
            };

            buttonPanel.Controls.Add(btnSaveModel);
            buttonPanel.Controls.Add(btnCancelModel);
            mainPanel.Controls.Add(buttonPanel, 0, 2);
            mainPanel.SetColumnSpan(buttonPanel, 2);

            dialog.AcceptButton = btnSaveModel;
            dialog.CancelButton = btnCancelModel;

            return dialog;
        }

        private static bool IsCellPhoneCategory(string categoryName)
            => !string.IsNullOrWhiteSpace(categoryName)
               && string.Equals(categoryName.Replace(" ", ""), "CellPhone", StringComparison.OrdinalIgnoreCase);

        private void CmbCategory_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var selectedCategory = CmbCategory.SelectedItem as CategoryItem;
            bool isCartridge = selectedCategory != null &&
                string.Equals(selectedCategory.Name, "Cartridge", StringComparison.OrdinalIgnoreCase);

            RowCartridgeModel.Visibility = isCartridge ? Visibility.Visible : Visibility.Collapsed;
            RowCartridgeOrigin.Visibility = isCartridge ? Visibility.Visible : Visibility.Collapsed;

            if (isCartridge && CmbCartridgeModel.Items.Count == 0)
                LoadCartridgeModels();

            bool isConsumable = CanonicalConsumableCategory(selectedCategory?.Name) != null;
            RowConsumableModel.Visibility = isConsumable ? Visibility.Visible : Visibility.Collapsed;

            if (isConsumable)
                LoadConsumableModels();

            bool isCellPhone = IsCellPhoneCategory(selectedCategory?.Name);
            RowCellPhoneNumber.Visibility = isCellPhone ? Visibility.Visible : Visibility.Collapsed;
            RowImei1.Visibility = isCellPhone ? Visibility.Visible : Visibility.Collapsed;
            RowImei2.Visibility = isCellPhone ? Visibility.Visible : Visibility.Collapsed;
        }

        private void CmbItemType_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            string selectedItemType = CmbItemType.SelectedItem?.ToString();
            bool showLicenseNumber = selectedItemType == "Software/License" || selectedItemType == "Services" || selectedItemType == "Hardware";
            RowLicenseNumber.Visibility = showLicenseNumber ? Visibility.Visible : Visibility.Collapsed;

            bool isServices = string.Equals(selectedItemType, "Services", StringComparison.OrdinalIgnoreCase);
            bool isSoftwareLicense = string.Equals(selectedItemType, "Software/License", StringComparison.OrdinalIgnoreCase);
            bool isHardware = string.Equals(selectedItemType, "Hardware", StringComparison.OrdinalIgnoreCase);
            RowServiceContractToggle.Visibility = (isServices || isSoftwareLicense || isHardware) ? Visibility.Visible : Visibility.Collapsed;
            if (!isServices && !isSoftwareLicense && !isHardware)
                ChkServiceContractDates.IsChecked = false;

            // Repopulate Unit of Measure options based on ItemType
            string currentUom = CmbUnitOfMeasure.Text;
            CmbUnitOfMeasure.Items.Clear();
            if (isServices || isSoftwareLicense)
            {
                CmbUnitOfMeasure.Items.Add("Monthly");
                CmbUnitOfMeasure.Items.Add("Annually");
                CmbUnitOfMeasure.Items.Add("One Time");
            }
            else
            {
                CmbUnitOfMeasure.Items.Add("Unit");
                CmbUnitOfMeasure.Items.Add("Piece");
                CmbUnitOfMeasure.Items.Add("Cartridge");
                CmbUnitOfMeasure.Items.Add("Box");
                CmbUnitOfMeasure.Items.Add("Set");
            }
            CmbUnitOfMeasure.Text = currentUom;

            ApplyServiceContractDateMode();
        }

        private void ChkServiceContractDates_Changed(object sender, RoutedEventArgs e)
        {
            ApplyServiceContractDateMode();
        }

        private void ApplyServiceContractDateMode()
        {
            string selectedItemType = CmbItemType.SelectedItem?.ToString();
            bool isServices = string.Equals(selectedItemType, "Services", StringComparison.OrdinalIgnoreCase);
            bool isSoftwareLicense = string.Equals(selectedItemType, "Software/License", StringComparison.OrdinalIgnoreCase);
            bool isHardware = string.Equals(selectedItemType, "Hardware", StringComparison.OrdinalIgnoreCase);
            bool enableServiceDates = (isServices || isSoftwareLicense || isHardware) && ChkServiceContractDates.IsChecked == true;

            LblStartDate.Text = enableServiceDates ? "Start Date" : "Start Date (Auto)";

            DpStartDate.IsEnabled = enableServiceDates;
            RowSyncLinkedInvoiceDates.Visibility = enableServiceDates ? Visibility.Visible : Visibility.Collapsed;

            // When enabled, EndDate is required — lock the checkbox checked and non-interactive
            // (the date picker itself stays editable). When disabled, restore the optional
            // checkbox-driven behavior.
            if (enableServiceDates)
            {
                ChkEndDate.IsChecked = true;
                ChkEndDate.IsEnabled = false;

                DpStartDate.SelectedDateChanged -= ServiceContractDates_ValueChanged;
                DpEndDate.SelectedDateChanged -= ServiceContractDates_ValueChanged;
                DpStartDate.SelectedDateChanged += ServiceContractDates_ValueChanged;
                DpEndDate.SelectedDateChanged += ServiceContractDates_ValueChanged;

                EnsureServiceContractDateRange();
            }
            else
            {
                ChkEndDate.IsEnabled = true;

                DpStartDate.SelectedDateChanged -= ServiceContractDates_ValueChanged;
                DpEndDate.SelectedDateChanged -= ServiceContractDates_ValueChanged;
                ChkSyncLinkedInvoiceDates.IsChecked = true;
            }
        }

        private void ServiceContractDates_ValueChanged(object sender, EventArgs e)
        {
            EnsureServiceContractDateRange();
        }

        private void EnsureServiceContractDateRange()
        {
            if (DpStartDate.SelectedDate == null) return;
            if (DpEndDate.SelectedDate == null || DpEndDate.SelectedDate <= DpStartDate.SelectedDate)
                DpEndDate.SelectedDate = DpStartDate.SelectedDate.Value.AddDays(1);
        }

        // Snapshots the dialog's current field values (not the stored _item, so any unsaved
        // edits are carried over) into an ItemDto to pre-fill BatchAddItemDialog's "copy" mode.
        // Serial Number / Cell Phone Number / IMEI1 / IMEI2 are intentionally left blank —
        // BatchAddItemDialog enforces that these must be re-entered and pass uniqueness checks.
        private ItemDto BuildItemSnapshotForCopy()
        {
            var selectedCategory = CmbCategory.SelectedItem as CategoryItem;
            var selectedCondition = CmbCondition.SelectedItem as ConditionItem;
            var selectedVendor = CmbVendor.SelectedItem as VendorItem;
            var selectedCartridgeModel = CmbCartridgeModel.SelectedItem as CartridgeModelItem;
            var selectedConsumableModel = CmbConsumableModel.SelectedItem as ConsumableModelItem;

            bool isCartridge = selectedCategory != null &&
                string.Equals(selectedCategory.Name, "Cartridge", StringComparison.OrdinalIgnoreCase);

            decimal.TryParse(TxtAmount.Text, out decimal amount);

            return new ItemDto
            {
                Name = TxtName.Text?.Trim(),
                Description = TxtDescription.Text?.Trim(),
                Category = selectedCategory?.Name,
                CategoryId = selectedCategory?.CategoryId ?? 0,
                ItemType = CmbItemType.SelectedItem?.ToString(),
                SubType = CmbSubType.SelectedItem as string,
                ModelNumber = TxtModelNumber.Text?.Trim(),
                CartridgeModelId = isCartridge ? selectedCartridgeModel?.CartridgeModelId : null,
                ConsumableModelId = CanonicalConsumableCategory(selectedCategory?.Name) != null
                    ? selectedConsumableModel?.ConsumableModelId
                    : null,
                RefillStatus = isCartridge && string.Equals(CmbCartridgeOrigin.SelectedItem?.ToString(), "Refilled", StringComparison.OrdinalIgnoreCase)
                    ? "Available"
                    : null,
                UnitOfMeasure = CmbUnitOfMeasure.Text,
                Amount = amount,
                ConditionId = selectedCondition?.ConditionId ?? 0,
                ConditionName = selectedCondition?.ConditionName,
                VendorId = (selectedVendor != null && selectedVendor.VendorId > 0) ? (int?)selectedVendor.VendorId : null,
                VendorName = selectedVendor?.VendorName,
                Remarks = TxtRemarks.Text?.Trim(),
                LicenseNumber = TxtLicenseNumber.Text?.Trim(),
                PartNumber = TxtPartNumber.Text?.Trim(),
                WarrantyYears = ParseWarrantyYears(),
                DatePurchased = ChkDatePurchased.IsChecked == true ? DpDatePurchased.SelectedDate : null,
                IsTrackedAsset = ChkIsTrackedAsset.IsChecked == true,
                StartDate = ChkServiceContractDates.IsChecked == true ? DpStartDate.SelectedDate : null,
                EndDate = (ChkServiceContractDates.IsChecked == true && ChkEndDate.IsChecked == true) ? DpEndDate.SelectedDate : null,
                // Must never be copied — the user is forced to enter new values.
                SerialNumber = null,
                CellPhoneNumber = null,
                IMEI1 = null,
                IMEI2 = null
            };
        }

        private void BtnAddCopy_Click(object sender, RoutedEventArgs e)
        {
            var copySource = BuildItemSnapshotForCopy();

            using (var batchDialog = new BatchAddItemDialog(copySource) { Owner = this })
            {
                var result = batchDialog.ShowDialog();
                if (result == WinForms.DialogResult.OK)
                    _copyAdded = true;
            }
        }

        private void BtnAddCategory_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new QuickAddCategory();
            if (dialog.ShowDialog() == WinForms.DialogResult.OK)
            {
                LoadCategories();

                for (int i = 0; i < CmbCategory.Items.Count; i++)
                {
                    if (CmbCategory.Items[i] is CategoryItem item && item.Name == dialog.NewCategoryName)
                    {
                        CmbCategory.SelectedIndex = i;
                        break;
                    }
                }
            }
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }

        private async void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            // Validation
            if (string.IsNullOrWhiteSpace(TxtName.Text))
            {
                WinMsgBox.Show("Please enter an Item Name.", "Validation Error",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                TxtName.Focus();
                return;
            }

            // ModelNumber is required only for physical-asset item types (Hardware).
            // Services and Software/License items do not require a Part Number.
            string selectedItemType = CmbItemType.SelectedItem?.ToString() ?? "Hardware";
            bool isPhysicalAssetType = !string.Equals(selectedItemType, "Services", StringComparison.OrdinalIgnoreCase)
                                    && !string.Equals(selectedItemType, "Software/License", StringComparison.OrdinalIgnoreCase);
            if (isPhysicalAssetType && string.IsNullOrWhiteSpace(TxtModelNumber.Text))
            {
                WinMsgBox.Show("Please enter a Model Number.", "Validation Error",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                TxtModelNumber.Focus();
                return;
            }

            if (string.IsNullOrWhiteSpace(CmbUnitOfMeasure.Text))
            {
                WinMsgBox.Show("Please enter a Unit of Measure.", "Validation Error",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                CmbUnitOfMeasure.Focus();
                return;
            }

            if (CmbCategory.SelectedItem == null)
            {
                WinMsgBox.Show("Please select a Category.", "Validation Error",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                CmbCategory.Focus();
                return;
            }

            if (CmbCondition.SelectedItem == null)
            {
                WinMsgBox.Show("Please select a Condition.", "Validation Error",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                CmbCondition.Focus();
                return;
            }

            if (CmbItemType.SelectedItem == null)
            {
                WinMsgBox.Show("Please select an Item Type.", "Validation Error",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                CmbItemType.Focus();
                return;
            }

            // CARTRIDGE MODEL VALIDATION: CartridgeModelId is REQUIRED when Category = 'Cartridge'
            var validationCategory = CmbCategory.SelectedItem as CategoryItem;
            if (validationCategory != null &&
                string.Equals(validationCategory.Name, "Cartridge", StringComparison.OrdinalIgnoreCase))
            {
                var selectedModel = CmbCartridgeModel.SelectedItem as CartridgeModelItem;
                if (selectedModel == null || selectedModel.CartridgeModelId <= 0)
                {
                    WinMsgBox.Show("Cartridge Model is required for cartridge items.\n\n" +
                        "Please select a valid Cartridge Model from the dropdown.",
                        "Validation Error",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    CmbCartridgeModel.Focus();
                    return;
                }
            }

            try
            {
                BtnSave.IsEnabled = false;
                BtnCancel.IsEnabled = false;
                Mouse.OverrideCursor = Cursors.Wait;

                var selectedCategory = CmbCategory.SelectedItem as CategoryItem;

                // Parse Amount
                if (!decimal.TryParse(TxtAmount.Text, out decimal amount))
                {
                    WinMsgBox.Show("Please enter a valid Amount.", "Validation Error",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    TxtAmount.Focus();
                    return;
                }

                // Parse Stock On Hand
                if (!int.TryParse(TxtStockOnHand.Text.Trim(), out int stockOnHand))
                {
                    WinMsgBox.Show("Stock On Hand must be a valid whole number.", "Validation Error",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    TxtStockOnHand.Focus();
                    return;
                }

                using (var con = new SqlConnection(_connectionString))
                {
                    con.Open();
                    using (var transaction = con.BeginTransaction())
                    {
                        try
                        {
                            DateTime? oldStartDate = _item.StartDate;
                            DateTime? oldEndDate = _item.EndDate;

                            bool archiveNowChecked = ChkIsArchived.IsChecked == true;
                            bool archiveStatusChanged = archiveNowChecked != _originalIsArchived;

                            if (archiveStatusChanged)
                            {
                                if (archiveNowChecked)
                                {
                                    string insertArchiveSql = @"
                                        INSERT INTO ArchiveStatus (EntityType, EntityId, IsArchived, ArchivedAt, ArchivedBy, ArchiveReason)
                                        VALUES ('Item', @ItemId, 1, GETDATE(), @ArchivedBy, @ArchiveReason)";

                                    using (var cmd = new SqlCommand(insertArchiveSql, con, transaction))
                                    {
                                        cmd.Parameters.AddWithValue("@ItemId", _item.ItemId);
                                        cmd.Parameters.AddWithValue("@ArchivedBy", AppSession.CurrentUserName ?? "System");
                                        cmd.Parameters.AddWithValue("@ArchiveReason", "Archived from Edit Item Dialog");
                                        cmd.ExecuteNonQuery();
                                    }
                                }
                                else
                                {
                                    string deleteArchiveSql = @"
                                        DELETE FROM ArchiveStatus
                                        WHERE EntityType = 'Item' AND EntityId = @ItemId";

                                    using (var cmd = new SqlCommand(deleteArchiveSql, con, transaction))
                                    {
                                        cmd.Parameters.AddWithValue("@ItemId", _item.ItemId);
                                        cmd.ExecuteNonQuery();
                                    }
                                }
                            }

                            string sql = @"
                                UPDATE dbo.Item
                                SET Name = @Name,
                                    Description = @Description,
                                    CategoryId = @CategoryId,
                                    Category = @Category,
                                    ItemType = @ItemType,
                                    SerialNumber = @SerialNumber,
                                    ModelNumber = @ModelNumber,
                                    CartridgeModelId = @CartridgeModelId,
                                    ConsumableModelId = @ConsumableModelId,
                                    RefillStatus = @RefillStatus,
                                    CellPhoneNumber = @CellPhoneNumber,
                                    IMEI1 = @IMEI1,
                                    IMEI2 = @IMEI2,
                                    UnitOfMeasure = @UnitOfMeasure,
                                    StockOnHand = @StockOnHand,
                                    Amount = @Amount,
                                    Active = @Active,
                                    IsBorrowable = @IsBorrowable,
                                    ConditionId = @ConditionId,
                                    VendorId = @VendorId,
                                    StartDate = @StartDate,
                                    EndDate = @EndDate,
                                    Remarks = @Remarks,
                                    LicenseNumber = @LicenseNumber,
                                    WarrantyYears = @WarrantyYears,
                                    WarrantyStartDate = @WarrantyStartDate,
                                    WarrantyEndDate = @WarrantyEndDate,
                                    DatePurchased = @DatePurchased,
                                    AcquisitionType = @AcquisitionType,
                                    IsTrackedAsset = @IsTrackedAsset,
                                    DateModified = @DateModified,
                                    ModifiedBy = @ModifiedBy,
                                    SubType = @SubType
                                WHERE ItemId = @ItemId";

                            using (var cmd = new SqlCommand(sql, con, transaction))
                            {
                                var selectedCondition = CmbCondition.SelectedItem as ConditionItem;

                                cmd.Parameters.AddWithValue("@ItemId", _item.ItemId);
                                cmd.Parameters.AddWithValue("@Name", TxtName.Text.Trim());
                                cmd.Parameters.AddWithValue("@Description", (object)TxtDescription.Text?.Trim() ?? DBNull.Value);
                                cmd.Parameters.AddWithValue("@CategoryId", selectedCategory.CategoryId);
                                cmd.Parameters.AddWithValue("@Category", selectedCategory.Name);
                                cmd.Parameters.AddWithValue("@ItemType", CmbItemType.SelectedItem.ToString());
                                var selectedSubType = CmbSubType.SelectedItem as string;
                                cmd.Parameters.AddWithValue("@SubType", string.IsNullOrWhiteSpace(selectedSubType) ? (object)DBNull.Value : selectedSubType);
                                var serialNumber = string.IsNullOrWhiteSpace(TxtSerialNumber.Text) ? null : TxtSerialNumber.Text.Trim();
                                cmd.Parameters.AddWithValue("@SerialNumber", (object)serialNumber ?? DBNull.Value);
                                cmd.Parameters.AddWithValue("@ModelNumber", TxtModelNumber.Text.Trim());

                                bool isCategoryCartridge = string.Equals(selectedCategory.Name, "Cartridge", StringComparison.OrdinalIgnoreCase);
                                if (isCategoryCartridge)
                                {
                                    var cartridgeModelItem = CmbCartridgeModel.SelectedItem as CartridgeModelItem;
                                    cmd.Parameters.AddWithValue("@CartridgeModelId",
                                        (cartridgeModelItem != null && cartridgeModelItem.CartridgeModelId > 0)
                                            ? (object)cartridgeModelItem.CartridgeModelId
                                            : DBNull.Value);

                                    cmd.Parameters.AddWithValue("@RefillStatus",
                                        CmbCartridgeOrigin.SelectedIndex == 1 ? (object)"Available" : DBNull.Value);
                                }
                                else
                                {
                                    cmd.Parameters.AddWithValue("@CartridgeModelId", DBNull.Value);
                                    cmd.Parameters.AddWithValue("@RefillStatus", DBNull.Value);
                                }

                                if (CanonicalConsumableCategory(selectedCategory.Name) != null)
                                {
                                    var consumableModelItem = CmbConsumableModel.SelectedItem as ConsumableModelItem;
                                    cmd.Parameters.AddWithValue("@ConsumableModelId",
                                        (consumableModelItem != null && consumableModelItem.ConsumableModelId > 0)
                                            ? (object)consumableModelItem.ConsumableModelId
                                            : DBNull.Value);
                                }
                                else
                                {
                                    cmd.Parameters.AddWithValue("@ConsumableModelId", DBNull.Value);
                                }

                                if (IsCellPhoneCategory(selectedCategory.Name))
                                {
                                    cmd.Parameters.AddWithValue("@CellPhoneNumber",
                                        string.IsNullOrWhiteSpace(TxtCellPhoneNumber.Text) ? (object)DBNull.Value : TxtCellPhoneNumber.Text.Trim());
                                    cmd.Parameters.AddWithValue("@IMEI1",
                                        string.IsNullOrWhiteSpace(TxtImei1.Text) ? (object)DBNull.Value : TxtImei1.Text.Trim());
                                    cmd.Parameters.AddWithValue("@IMEI2",
                                        string.IsNullOrWhiteSpace(TxtImei2.Text) ? (object)DBNull.Value : TxtImei2.Text.Trim());
                                }
                                else
                                {
                                    cmd.Parameters.AddWithValue("@CellPhoneNumber", DBNull.Value);
                                    cmd.Parameters.AddWithValue("@IMEI1", DBNull.Value);
                                    cmd.Parameters.AddWithValue("@IMEI2", DBNull.Value);
                                }

                                cmd.Parameters.AddWithValue("@UnitOfMeasure", CmbUnitOfMeasure.Text.Trim());
                                cmd.Parameters.AddWithValue("@Amount", amount);
                                cmd.Parameters.AddWithValue("@StockOnHand", stockOnHand);
                                cmd.Parameters.AddWithValue("@Active", ChkActive.IsChecked == true);
                                cmd.Parameters.AddWithValue("@IsBorrowable", ChkIsBorrowable.IsChecked == true);

                                cmd.Parameters.AddWithValue("@ConditionId", selectedCondition.ConditionId);

                                var selectedVendor = CmbVendor.SelectedItem as VendorItem;
                                cmd.Parameters.AddWithValue("@VendorId",
                                    (selectedVendor != null && selectedVendor.VendorId > 0) ? (object)selectedVendor.VendorId : DBNull.Value);

                                bool isServices = string.Equals(CmbItemType.SelectedItem?.ToString(), "Services", StringComparison.OrdinalIgnoreCase);
                                bool isSoftwareLicense = string.Equals(CmbItemType.SelectedItem?.ToString(), "Software/License", StringComparison.OrdinalIgnoreCase);
                                bool isHardwareType = string.Equals(CmbItemType.SelectedItem?.ToString(), "Hardware", StringComparison.OrdinalIgnoreCase);
                                bool enableServiceDates = (isServices || isSoftwareLicense || isHardwareType) && ChkServiceContractDates.IsChecked == true;

                                DateTime serviceStartDate = (DpStartDate.SelectedDate ?? _item.DateCreated).Date.AddHours(8);
                                DateTime serviceEndDate = (DpEndDate.SelectedDate ?? serviceStartDate).Date.AddHours(8);

                                DateTime autoStartDate = _item.StartDate ?? _item.DateCreated;
                                cmd.Parameters.AddWithValue("@StartDate", enableServiceDates ? serviceStartDate : autoStartDate);

                                object endDateParam = enableServiceDates
                                    ? (object)serviceEndDate
                                    : (ChkEndDate.IsChecked == true && DpEndDate.SelectedDate.HasValue ? (object)DpEndDate.SelectedDate.Value : DBNull.Value);
                                cmd.Parameters.AddWithValue("@EndDate", endDateParam);
                                cmd.Parameters.AddWithValue("@Remarks",
                                    string.IsNullOrWhiteSpace(TxtRemarks.Text) ? (object)DBNull.Value : TxtRemarks.Text.Trim());

                                cmd.Parameters.AddWithValue("@LicenseNumber",
                                    string.IsNullOrWhiteSpace(TxtLicenseNumber.Text) ? (object)DBNull.Value : TxtLicenseNumber.Text.Trim());

                                // Warranty parameters — Start and End are independently user-editable
                                cmd.Parameters.AddWithValue("@WarrantyYears", ParseWarrantyYears());
                                cmd.Parameters.AddWithValue("@WarrantyStartDate",
                                    ChkWarrantyStart.IsChecked == true && DpWarrantyStart.SelectedDate.HasValue
                                        ? (object)DpWarrantyStart.SelectedDate.Value : DBNull.Value);
                                cmd.Parameters.AddWithValue("@WarrantyEndDate",
                                    ChkWarrantyEnd.IsChecked == true && DpWarrantyEnd.SelectedDate.HasValue
                                        ? (object)DpWarrantyEnd.SelectedDate.Value : DBNull.Value);

                                cmd.Parameters.AddWithValue("@DatePurchased",
                                    ChkDatePurchased.IsChecked == true && DpDatePurchased.SelectedDate.HasValue
                                        ? (object)DpDatePurchased.SelectedDate.Value : DBNull.Value);

                                string acquisitionType = CmbAcquisitionType.SelectedItem?.ToString();
                                if (acquisitionType == "(None)" || string.IsNullOrWhiteSpace(acquisitionType))
                                    cmd.Parameters.AddWithValue("@AcquisitionType", DBNull.Value);
                                else
                                    cmd.Parameters.AddWithValue("@AcquisitionType", acquisitionType);

                                cmd.Parameters.AddWithValue("@IsTrackedAsset", ChkIsTrackedAsset.IsChecked == true);

                                cmd.Parameters.AddWithValue("@DateModified", DateTime.Now);
                                cmd.Parameters.AddWithValue("@ModifiedBy", AppSession.CurrentUserId);

                                int rowsAffected = cmd.ExecuteNonQuery();

                                if (rowsAffected > 0)
                                {
                                    bool isServicesForSync = string.Equals(CmbItemType.SelectedItem?.ToString(), "Services", StringComparison.OrdinalIgnoreCase);
                                    bool isSoftwareLicenseForSync = string.Equals(CmbItemType.SelectedItem?.ToString(), "Software/License", StringComparison.OrdinalIgnoreCase);
                                    bool isHardwareForSync = string.Equals(CmbItemType.SelectedItem?.ToString(), "Hardware", StringComparison.OrdinalIgnoreCase);
                                    bool serviceDatesEnabledForSync = (isServicesForSync || isSoftwareLicenseForSync || isHardwareForSync) && ChkServiceContractDates.IsChecked == true;
                                    DateTime newStartDate = serviceDatesEnabledForSync ? serviceStartDate : (_item.StartDate ?? _item.DateCreated);
                                    DateTime? newEndDate = serviceDatesEnabledForSync
                                        ? serviceEndDate
                                        : (ChkEndDate.IsChecked == true ? DpEndDate.SelectedDate : null);

                                    const string renewalUpdateSql = @"
                                        UPDATE dbo.Renewals
                                        SET PartNumber = @PartNumber,
                                            ModifiedBy = @ModifiedBy,
                                            ModifiedAt = GETDATE()
                                        WHERE ItemId = @ItemId
                                          AND IsArchived = 0";
                                    using (var renewalCmd = new SqlCommand(renewalUpdateSql, con, transaction))
                                    {
                                        renewalCmd.Parameters.AddWithValue("@PartNumber",
                                            string.IsNullOrWhiteSpace(TxtPartNumber.Text)
                                                ? (object)DBNull.Value
                                                : TxtPartNumber.Text.Trim());
                                        renewalCmd.Parameters.AddWithValue("@ModifiedBy", AppSession.CurrentUserId);
                                        renewalCmd.Parameters.AddWithValue("@ItemId", _item.ItemId);
                                        renewalCmd.ExecuteNonQuery();
                                    }

                                    if (serviceDatesEnabledForSync
                                        && ChkSyncLinkedInvoiceDates.IsChecked == true
                                        && (oldStartDate != newStartDate || oldEndDate != newEndDate))
                                    {
                                        const string countSql = @"
SELECT
    COUNT(*) AS LineCount,
    COUNT(DISTINCT si.SetId) AS SetCount
FROM dbo.SetItem si
INNER JOIN dbo.[Set] s ON s.SetId = si.SetId
WHERE si.ItemId = @ItemId
  AND s.IsInvoice = 1
  AND s.SetType IN ('Software/License', 'Service', 'Services');";

                                        int lineCount = 0;
                                        int setCount = 0;
                                        using (var countCmd = new SqlCommand(countSql, con, transaction))
                                        {
                                            countCmd.Parameters.AddWithValue("@ItemId", _item.ItemId);
                                            using (var reader = await countCmd.ExecuteReaderAsync())
                                            {
                                                if (await reader.ReadAsync())
                                                {
                                                    lineCount = reader.IsDBNull(0) ? 0 : reader.GetInt32(0);
                                                    setCount = reader.IsDBNull(1) ? 0 : reader.GetInt32(1);
                                                }
                                            }
                                        }

                                        bool skipInvoiceDateSync = false;
                                        if (setCount > 1)
                                        {
                                            var confirm = WinMsgBox.Show(
                                                $"This item appears in {setCount} invoice set(s) ({lineCount} line(s)).\n\n" +
                                                "Update the invoice line dates for this item in ALL of them?",
                                                "Confirm invoice line update",
                                                MessageBoxButton.YesNo,
                                                MessageBoxImage.Question);

                                            if (confirm != MessageBoxResult.Yes)
                                                skipInvoiceDateSync = true;
                                        }

                                        if (!skipInvoiceDateSync)
                                        {
                                            const string syncSql = @"
UPDATE si
SET LineStartDate = @NewStartDate,
    LineEndDate   = @NewEndDate
FROM dbo.SetItem si
INNER JOIN dbo.[Set] s ON s.SetId = si.SetId
WHERE si.ItemId = @ItemId
  AND s.IsInvoice = 1
  AND s.SetType IN ('Software/License', 'Service', 'Services');";

                                            using (var syncCmd = new SqlCommand(syncSql, con, transaction))
                                            {
                                                syncCmd.Parameters.AddWithValue("@ItemId", _item.ItemId);
                                                syncCmd.Parameters.AddWithValue("@NewStartDate", newStartDate);
                                                syncCmd.Parameters.AddWithValue("@NewEndDate", (object)newEndDate ?? DBNull.Value);
                                                await syncCmd.ExecuteNonQueryAsync();
                                            }
                                        }
                                    }

                                    var selectedConditionForAudit = CmbCondition.SelectedItem as ConditionItem;

                                    var serialForAudit = string.IsNullOrWhiteSpace(TxtSerialNumber.Text)
                                        ? null
                                        : TxtSerialNumber.Text.Trim();

                                    var beforeName = _item?.Name;
                                    var beforeModel = _item?.ModelNumber;
                                    var beforeCondition = _item?.ConditionName;
                                    var beforeActive = _item != null && _item.Active;
                                    var beforeArchived = _originalIsArchived;

                                    var afterName = string.IsNullOrWhiteSpace(TxtName.Text) ? null : TxtName.Text.Trim();
                                    var afterModel = string.IsNullOrWhiteSpace(TxtModelNumber.Text) ? null : TxtModelNumber.Text.Trim();
                                    var afterCondition = selectedConditionForAudit?.ConditionName;
                                    var afterActive = ChkActive.IsChecked == true;
                                    var afterArchived = ChkIsArchived.IsChecked == true;

                                    var nameChanged = !string.Equals((beforeName ?? string.Empty).Trim(), (afterName ?? string.Empty).Trim(), StringComparison.OrdinalIgnoreCase);
                                    var modelChanged = !string.Equals((beforeModel ?? string.Empty).Trim(), (afterModel ?? string.Empty).Trim(), StringComparison.OrdinalIgnoreCase);
                                    var conditionChanged = !string.Equals((beforeCondition ?? string.Empty).Trim(), (afterCondition ?? string.Empty).Trim(), StringComparison.OrdinalIgnoreCase);
                                    var activeChanged = beforeActive != afterActive;
                                    var archivedChanged = archiveStatusChanged;

                                    if (nameChanged || modelChanged || conditionChanged || activeChanged || archivedChanged)
                                    {
                                        var changed = new List<string>();
                                        if (nameChanged) changed.Add("Name");
                                        if (modelChanged) changed.Add("Model");
                                        if (conditionChanged) changed.Add("Condition");
                                        if (activeChanged) changed.Add(afterActive ? "Active" : "Inactive");
                                        if (archivedChanged) changed.Add(afterArchived ? "Archived" : "Unarchived");

                                        var action = archivedChanged
                                            ? (afterArchived ? "Item Archived" : "Item Restored")
                                            : "Item Updated";

                                        string ComputeStatus(string condition, bool active, bool archived)
                                        {
                                            if (archived) return "Archived";
                                            if (!active) return "Inactive";
                                            if (!string.IsNullOrWhiteSpace(condition) &&
                                                (condition.Trim().Equals("Damaged", StringComparison.OrdinalIgnoreCase)
                                                 || condition.Trim().Equals("Broken", StringComparison.OrdinalIgnoreCase)))
                                                return condition.Trim();
                                            return "Active";
                                        }

                                        var beforeStatus = ComputeStatus(beforeCondition, beforeActive, beforeArchived);
                                        var afterStatusComputed = ComputeStatus(afterCondition, afterActive, afterArchived);

                                        var status = afterStatusComputed;

                                        var message = archivedChanged
                                            ? (afterArchived ? "Archived from Edit Item Dialog" : "Unarchived from Edit Item Dialog")
                                            : ("Updated from Edit Item Dialog: " + string.Join(", ", changed));

                                        var notesJson = JsonConvert.SerializeObject(new
                                        {
                                            message = message,
                                            changed = changed,
                                            before = new
                                            {
                                                status = beforeStatus,
                                                setCode = (string)null,
                                                branch = (string)null,
                                                department = (string)null,
                                                employee = (string)null,
                                                itemName = beforeName,
                                                modelNumber = beforeModel,
                                                condition = beforeCondition,
                                                active = beforeActive,
                                                archived = beforeArchived
                                            },
                                            after = new
                                            {
                                                status = afterStatusComputed,
                                                setCode = (string)null,
                                                branch = (string)null,
                                                department = (string)null,
                                                employee = (string)null,
                                                itemName = afterName,
                                                modelNumber = afterModel,
                                                condition = afterCondition,
                                                active = afterActive,
                                                archived = afterArchived
                                            }
                                        });

                                        ItemAuditTrailWriter.TryLog(con, transaction, new ItemAuditTrailDto
                                        {
                                            ItemId = _item.ItemId,
                                            SerialNumber = serialForAudit,
                                            Action = action,
                                            ActionTime = DateTime.Now,
                                            Status = status,
                                            ReferenceType = "Item",
                                            ReferenceId = _item.ItemId,
                                            Notes = notesJson,
                                            CreatedBy = AppSession.CurrentUserName ?? "System"
                                        });
                                    }

                                    transaction.Commit();

                                    string successMessage = archiveStatusChanged && afterArchived
                                        ? "Item archived successfully!"
                                        : "Item updated successfully!";

                                    WinMsgBox.Show(successMessage, "Success",
                                        MessageBoxButton.OK, MessageBoxImage.Information);
                                    DialogResult = true;
                                }
                                else
                                {
                                    transaction.Rollback();
                                    WinMsgBox.Show("Failed to update item. No rows were affected.", "Error",
                                        MessageBoxButton.OK, MessageBoxImage.Error);
                                }
                            }
                        }
                        catch
                        {
                            transaction.Rollback();
                            throw;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                WinMsgBox.Show($"Error updating item: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                BtnSave.IsEnabled = true;
                BtnCancel.IsEnabled = true;
                Mouse.OverrideCursor = null;
            }
        }

        // Helper classes for ComboBox items
        private class CategoryItem
        {
            public int CategoryId { get; set; }
            public string Name { get; set; }
            public override string ToString() => Name;
        }

        private class ConditionItem
        {
            public int ConditionId { get; set; }
            public string ConditionName { get; set; }
            public override string ToString() => ConditionName;
        }

        private class VendorItem
        {
            public int VendorId { get; set; }
            public string VendorName { get; set; }
            public override string ToString() => VendorName;
        }

        private class CartridgeModelItem
        {
            public int CartridgeModelId { get; set; }
            public string ModelNumber { get; set; }
            public string VendorName { get; set; }
            public int? VendorId { get; set; }

            public override string ToString() => string.IsNullOrWhiteSpace(VendorName)
                ? ModelNumber
                : $"{ModelNumber} - {VendorName}";
        }

        private sealed class QuickAddCartridgeModelResult
        {
            public int CartridgeModelId { get; set; }
            public string ModelNumber { get; set; }
        }

        private class ConsumableModelItem
        {
            public int ConsumableModelId { get; set; }
            public string ModelNumber { get; set; }

            public override string ToString() => ModelNumber;
        }

        private sealed class QuickAddConsumableModelResult
        {
            public int ConsumableModelId { get; set; }
            public string ModelNumber { get; set; }
        }
    }
}
