using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Data.SqlClient;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Yakult.Inventory.App.Core;
using Yakult.Inventory.App.Pages;
using Yakult.Inventory.App.Pages.Software;
using Yakult.Inventory.App.Repositories;
using Yakult.Inventory.App.Session;

namespace Yakult.Inventory.App.Pages.Item
{
    /// <summary>
    /// One-page invoice builder — the on-screen counterpart to Import Invoice CSV.
    ///
    /// Instead of walking the multi-step flow (add the items, then build a set, then tag sub-types,
    /// then price it), everything is entered on one page: the invoice header, the Sub-Type groups
    /// with their reference code and coverage dates, the line items under each group, and the
    /// financial percents. The multi-step flow is untouched — this is an additional route.
    ///
    /// Saving goes through <see cref="InvoiceCsvImportDialog.RunImport"/>, the exact same
    /// transaction the CSV import uses: the Items, the Set, its SetItems, the Sub-Type groups and
    /// the financial totals all commit together or roll back together. Nothing here re-implements
    /// that logic, so the two entry points can never drift apart.
    /// </summary>
    public partial class InvoiceBuilderDialog : Window
    {
        // Bound by the DataGrid's dropdown columns via x:Static, so they must be static and must
        // keep their identity — populate in place rather than reassigning.
        public static ObservableCollection<string> CategoryOptions { get; } = new ObservableCollection<string>();
        public static ObservableCollection<string> ItemTypeOptions { get; } =
            new ObservableCollection<string> { "Hardware", "Software/License", "Services" };
        public static ObservableCollection<string> VendorOptions { get; } = new ObservableCollection<string>();

        // ── Model catalogues, one per family ─────────────────────────────────
        // A line's Model dropdown shows whichever of these matches its Category: Cartridge draws
        // from dbo.CartridgeModel, Ink / Toner / Print Head from dbo.ConsumableModel filtered to
        // that family. Every other category has no model at all.
        public static ObservableCollection<string> CartridgeModelOptions { get; } = new ObservableCollection<string>();
        public static ObservableCollection<string> InkModelOptions { get; } = new ObservableCollection<string>();
        public static ObservableCollection<string> TonerModelOptions { get; } = new ObservableCollection<string>();
        public static ObservableCollection<string> PrintHeadModelOptions { get; } = new ObservableCollection<string>();

        /// <summary>Shown for categories that have no model catalogue.</summary>
        public static ObservableCollection<string> NoModelOptions { get; } = new ObservableCollection<string>();

        /// <summary>The "no model" entry — a blank combo item reads as a rendering fault, so the
        /// absence is spelled out.</summary>
        internal const string NoneOption = "(none)";

        /// <summary>
        /// The last entry in every lookup dropdown. Choosing it does not select a value — it opens
        /// the matching quick-add dialog, then selects whatever was created. This is what puts
        /// "add it on the spot" inside the dropdown itself rather than only on a toolbar button.
        /// </summary>
        internal const string AddNewOption = "➕  Add new…";

        /// <summary>Canonicalises a category name to its consumable family, or null when the
        /// category is not a consumable. Mirrors EditItemDialog.CanonicalConsumableCategory.</summary>
        internal static string ConsumableFamily(string categoryName)
        {
            if (string.IsNullOrWhiteSpace(categoryName)) return null;
            var normalized = categoryName.Replace(" ", string.Empty).ToLowerInvariant();
            if (normalized.Contains("ink")) return "Ink";
            if (normalized.Contains("toner")) return "Toner";
            if (normalized.Contains("printhead")) return "Print Head";
            return null;
        }

        internal static bool IsCartridgeCategory(string categoryName) =>
            string.Equals(categoryName, "Cartridge", StringComparison.OrdinalIgnoreCase);

        /// <summary>The dropdown a line should show, based on its category.</summary>
        internal static ObservableCollection<string> ModelOptionsFor(string categoryName)
        {
            if (IsCartridgeCategory(categoryName)) return CartridgeModelOptions;

            switch (ConsumableFamily(categoryName))
            {
                case "Ink": return InkModelOptions;
                case "Toner": return TonerModelOptions;
                case "Print Head": return PrintHeadModelOptions;
                default: return NoModelOptions;
            }
        }

        private readonly ObservableCollection<BuilderGroup> _groups = new ObservableCollection<BuilderGroup>();

        /// <summary>
        /// The invoice header fields, now edited in InvoiceHeaderDialog rather than inline —
        /// plain data instead of named controls so BuildRows, the compact summary card and the
        /// modal can all read/write the same values without depending on which one is currently
        /// on screen.
        /// </summary>
        public sealed class InvoiceHeaderValues
        {
            public string DocumentNumber { get; set; }
            public string ReferenceNumber { get; set; }
            public DateTime? DocumentDate { get; set; }
            public string Company { get; set; }
            public string Distributor { get; set; }
            public string SiteCompany { get; set; }
            public string SiteBranch { get; set; }
            public string SiteDepartment { get; set; }
            public DateTime? StartDate { get; set; }
            public DateTime? EndDate { get; set; }

            /// <summary>The Receipt Set picked in Edit Invoice Details, before the invoice (and
            /// therefore its Set row) exists. Attached for real in BtnCreate_Click once the new
            /// SetId is known. Null means no receipt was chosen.</summary>
            public int? ReceiptSetId { get; set; }

            /// <summary>Display text for the picked receipt set (Supplier / SI / DR / PO), so the
            /// modal can show what's selected without re-querying the repository.</summary>
            public string ReceiptSetLabel { get; set; }

            /// <summary>A copy handed to the modal, so a Cancel there cannot mutate the values
            /// still shown on the compact summary card.</summary>
            public InvoiceHeaderValues Clone() => new InvoiceHeaderValues
            {
                DocumentNumber = DocumentNumber,
                ReferenceNumber = ReferenceNumber,
                DocumentDate = DocumentDate,
                Company = Company,
                Distributor = Distributor,
                SiteCompany = SiteCompany,
                SiteBranch = SiteBranch,
                SiteDepartment = SiteDepartment,
                StartDate = StartDate,
                EndDate = EndDate,
                ReceiptSetId = ReceiptSetId,
                ReceiptSetLabel = ReceiptSetLabel
            };
        }

        private InvoiceHeaderValues _header = new InvoiceHeaderValues { DocumentDate = DateTime.Today };

        // Populated once in LoadLookups and handed to InvoiceHeaderDialog each time it opens,
        // rather than owning live ComboBox.Items collections the way this window used to.
        private List<string> _companyNames = new List<string>();
        private List<string> _distributorNames = new List<string>();
        private List<string> _branchNames = new List<string>();
        private List<string> _departmentNames = new List<string>();

        /// <summary>Category name -> CategoryId, for stamping rows before handing them to the importer.</summary>
        private readonly Dictionary<string, int> _categoryIds =
            new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        /// <summary>Cartridge model number -> CartridgeModelId, resolved on save.</summary>
        private readonly Dictionary<string, int> _cartridgeModelIds =
            new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        /// <summary>"family|model number" -> ConsumableModelId. Keyed by family too, because the
        /// same model number may legitimately exist under Ink and under Toner.</summary>
        private readonly Dictionary<string, int> _consumableModelIds =
            new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        private static string ConsumableKey(string family, string modelNumber) => family + "|" + modelNumber;

        /// <summary>True when at least one invoice was created, so the caller knows to refresh.</summary>
        public bool CreatedInvoice { get; private set; }

        public InvoiceBuilderDialog()
        {
            InitializeComponent();
            UpdateMaximizeIcon();

            TabsHost.ItemsSource = _groups;

            // WindowStyle=None + AllowsTransparency means WPF maximises to the full screen
            // rectangle, which sits on top of the taskbar. Clamping to the work area keeps the
            // taskbar visible. Re-read on every state change because the work area moves when
            // the taskbar is repositioned or the window is dragged to another monitor.
            SourceInitialized += (s, e) => ClampToWorkArea();
            StateChanged += (s, e) => { ClampToWorkArea(); UpdateMaximizeIcon(); };

            Loaded += (s, e) =>
            {
                LoadLookups();
                RefreshHeaderSummary();
                // Start with the ungrouped bucket plus one real group, so the shape of the page is
                // obvious without the user having to click anything first.
                var ungrouped = AddGroup(isUngrouped: true);
                var firstGroup = AddGroup(isUngrouped: false);
                // Land on the real group, not the caution nav item — Ungrouped is the fallback,
                // not where invoice-building normally starts.
                SelectGroup(firstGroup);
                Recalculate();
            };
        }

        /// <summary>
        /// Shows exactly one group's editor at a time. Only one group is ever selected — every
        /// other call site (add, remove, nav click) routes through this rather than touching
        /// BuilderGroup.IsSelected directly.
        ///
        /// Unlike the previous pill-tab version, the editor is not an ItemsControl with a
        /// Visibility-per-item trick — GroupEditorRoot is one static panel whose DataContext is
        /// swapped here to whichever group is current, so exactly one editor ever exists in the
        /// visual tree and a plain Grid.Row="*" sizes its DataGrid correctly.
        /// </summary>
        private void SelectGroup(BuilderGroup group)
        {
            if (group == null) return;
            foreach (var g in _groups) g.IsSelected = ReferenceEquals(g, group);
            GroupEditorRoot.DataContext = group;
        }

        /// <summary>Keeps the compact Invoice Details card in sync with <see cref="_header"/> —
        /// called after the initial load and every time InvoiceHeaderDialog returns via Save.</summary>
        private void RefreshHeaderSummary()
        {
            LblSummaryDocNumber.Text = string.IsNullOrWhiteSpace(_header.DocumentNumber) ? "(not set)" : _header.DocumentNumber;

            string subject = !string.IsNullOrWhiteSpace(_header.Company) ? _header.Company
                : !string.IsNullOrWhiteSpace(_header.Distributor) ? _header.Distributor
                : "(not set)";
            LblSummaryCompany.Text = subject;

            LblSummaryDate.Text = _header.DocumentDate.HasValue
                ? _header.DocumentDate.Value.ToString("MM/dd/yyyy")
                : "(not set)";
        }

        /// <summary>Opens the "Edit Invoice Details" modal with a copy of the current values, and
        /// only commits them back (and refreshes the summary card) if the user actually saves.</summary>
        private void BtnEditInvoiceDetails_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new InvoiceHeaderDialog(
                _header.Clone(), _companyNames, _distributorNames, _branchNames, _departmentNames)
            { Owner = this };

            if (dialog.ShowDialog() != true) return;

            _header = dialog.Result;
            RefreshHeaderSummary();
        }

        // ─────────────────────────────────────────────────────────────────────
        // Window chrome
        // ─────────────────────────────────────────────────────────────────────
        private void OnHeaderDrag(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton != MouseButton.Left || e.ButtonState != MouseButtonState.Pressed) return;

            // Double-click toggles maximise, like a normal title bar. DragMove would otherwise
            // swallow the second click.
            if (e.ClickCount == 2)
            {
                WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
                return;
            }

            DragMove();
        }

        /// <summary>
        /// A DataGrid's internal ScrollViewer swallows the wheel, so hovering a line-item grid
        /// would stop the page scrolling. The grids here are unbounded inside the page scroller
        /// and never scroll internally, so the wheel is forwarded to the page.
        /// Mirrors ViewInvoiceDetailPage.DgvItems_PreviewMouseWheel.
        ///
        /// It must NOT forward when the pointer is over something that scrolls on its own — an
        /// open dropdown above all. A popup is positioned relative to its placement target, so
        /// scrolling the page underneath drags the whole dropdown across the screen instead of
        /// moving its list.
        /// </summary>
        private void OnGridPreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (e.Handled) return;
            if (HandlesItsOwnScrolling(e.OriginalSource as DependencyObject, sender as DependencyObject, e.Delta))
                return;

            e.Handled = true;

            var args = new MouseWheelEventArgs(e.MouseDevice, e.Timestamp, e.Delta)
            {
                RoutedEvent = UIElement.MouseWheelEvent,
                Source = sender
            };
            ((UIElement)((FrameworkElement)sender).Parent)?.RaiseEvent(args);
        }

        /// <summary>
        /// True when something between <paramref name="start"/> and <paramref name="stopAt"/> owns
        /// this wheel event: either an open popup (a dropdown list — always let it keep the wheel,
        /// scrollable or not, because moving the page would drag the popup), or a ScrollViewer
        /// that still has room to move in this direction.
        /// </summary>
        private static bool HandlesItsOwnScrolling(DependencyObject start, DependencyObject stopAt, int delta)
        {
            var node = start;

            while (node != null && !ReferenceEquals(node, stopAt))
            {
                if (node is System.Windows.Controls.Primitives.Popup) return true;

                if (node is ScrollViewer viewer && viewer.ScrollableHeight > 0)
                {
                    bool roomUp = delta > 0 && viewer.VerticalOffset > 0;
                    bool roomDown = delta < 0 && viewer.VerticalOffset < viewer.ScrollableHeight;
                    if (roomUp || roomDown) return true;
                }

                node = VisualOrLogicalParent(node);
            }

            return false;
        }

        /// <summary>
        /// Walks up the visual tree, falling back to the logical parent. Popup content lives in
        /// its own visual tree whose root has no visual parent; the logical link is what leads
        /// back out to the Popup element itself.
        /// </summary>
        private static DependencyObject VisualOrLogicalParent(DependencyObject node)
        {
            if (node is System.Windows.Media.Visual || node is System.Windows.Media.Media3D.Visual3D)
            {
                var visualParent = System.Windows.Media.VisualTreeHelper.GetParent(node);
                if (visualParent != null) return visualParent;
            }

            return LogicalTreeHelper.GetParent(node);
        }

        private void OnCloseClick(object sender, MouseButtonEventArgs e) => Close();

        /// <summary>Toggles fullscreen — same WindowState flip as the header's own
        /// double-click (OnHeaderDrag), just reachable as a single click on its own icon.</summary>
        private void OnMaximizeRestoreClick(object sender, MouseButtonEventArgs e)
        {
            WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
        }

        /// <summary>Keeps the title bar icon/tooltip describing the action it would perform
        /// NEXT, matching how the icon itself is meant to read either way.</summary>
        private void UpdateMaximizeIcon()
        {
            if (TxtMaximizeIcon == null) return;
            bool isMaximized = WindowState == WindowState.Maximized;
            TxtMaximizeIcon.Text = isMaximized ? "🗗" : "🗖";
            TxtMaximizeIcon.ToolTip = isMaximized ? "Restore" : "Maximize";
        }

        /// <summary>
        /// Caps the window at the monitor's work area (screen minus taskbar). Without this a
        /// borderless maximised window covers the taskbar entirely.
        /// </summary>
        private void ClampToWorkArea()
        {
            try
            {
                var handle = new System.Windows.Interop.WindowInteropHelper(this).Handle;
                var screen = handle != IntPtr.Zero
                    ? System.Windows.Forms.Screen.FromHandle(handle)
                    : System.Windows.Forms.Screen.PrimaryScreen;

                // Screen bounds are device pixels; WPF sizes are DIPs. Convert via the window's
                // own transform so this stays correct at non-100% display scaling.
                var source = PresentationSource.FromVisual(this);
                double scaleX = source?.CompositionTarget?.TransformToDevice.M11 ?? 1.0;
                double scaleY = source?.CompositionTarget?.TransformToDevice.M22 ?? 1.0;
                if (scaleX <= 0) scaleX = 1.0;
                if (scaleY <= 0) scaleY = 1.0;

                MaxWidth = screen.WorkingArea.Width / scaleX;
                MaxHeight = screen.WorkingArea.Height / scaleY;
            }
            catch (Exception ex)
            {
                // Never let a sizing quirk stop the dialog from opening.
                Logger.LogError("InvoiceBuilderDialog: could not clamp to work area", ex);
            }
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e) => Close();

        // ─────────────────────────────────────────────────────────────────────
        // Lookups
        // ─────────────────────────────────────────────────────────────────────
        private void LoadLookups()
        {
            try
            {
                LoadCategoryOptions();

                _departmentNames = new List<string>();
                using (var con = new SqlConnection(DatabaseConfig.ConnectionString))
                {
                    con.Open();
                    using (var cmd = new SqlCommand("SELECT Name FROM dbo.Department WHERE Active = 1 ORDER BY Name", con))
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                            if (!reader.IsDBNull(0)) _departmentNames.Add(reader.GetString(0));
                    }
                }

                // Company / Distributor / Branch come from the repository rather than hand-written
                // SQL so their active filters and column names stay correct in one place. These
                // feed InvoiceHeaderDialog each time it opens rather than living ComboBox.Items
                // collections in this window, since the fields themselves moved into that dialog.
                var setRepo = new ServiceSetRepository();
                _companyNames = setRepo.GetAllCompanies().Select(c => c.CompanyName).ToList();
                _distributorNames = setRepo.GetAllDistributors().Select(d => d.Name).ToList();
                _branchNames = setRepo.GetAllBranches().Select(b => b.Name).ToList();

                LoadVendorOptions();
                LoadCartridgeModelOptions();
                LoadConsumableModelOptions();

                // Categories with no model catalogue still need a one-entry list so the dropdown
                // renders something explicit rather than looking broken.
                NoModelOptions.Clear();
                NoModelOptions.Add(NoneOption);
            }
            catch (Exception ex)
            {
                Logger.LogError("InvoiceBuilderDialog: failed to load lookups", ex);
                LblStatus.Text = "Some dropdowns could not be loaded — you can still type values in manually.";
            }
        }

        /// <summary>
        /// Reloads the Category dropdown, keeping the collection instance (the DataGrid column
        /// binds to it once, via x:Static) and the name -> id map in step.
        /// </summary>
        private void LoadCategoryOptions()
        {
            CategoryOptions.Clear();
            _categoryIds.Clear();

            using (var con = new SqlConnection(DatabaseConfig.ConnectionString))
            {
                con.Open();
                using (var cmd = new SqlCommand(
                    "SELECT CategoryId, Name FROM dbo.ItemCategory WHERE Active = 1 ORDER BY Name", con))
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        string name = reader.GetString(1);
                        CategoryOptions.Add(name);
                        _categoryIds[name] = reader.GetInt32(0);
                    }
                }
            }

            CategoryOptions.Add(AddNewOption);
        }

        private void LoadVendorOptions()
        {
            VendorOptions.Clear();
            try
            {
                // The importer resolves a vendor by name, so the dropdown carries names rather
                // than ids — picking from the list guarantees the name will resolve.
                using (var con = new SqlConnection(DatabaseConfig.ConnectionString))
                {
                    con.Open();
                    using (var cmd = new SqlCommand(
                        @"SELECT v.VendorName
                            FROM dbo.Vendor v
                            LEFT JOIN dbo.ArchiveStatus arc
                                   ON arc.EntityType = 'Vendor' AND arc.EntityId = v.VendorID AND arc.IsArchived = 1
                           WHERE v.IsActive = 1 AND arc.ArchiveId IS NULL
                           ORDER BY v.VendorName", con))
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                            if (!reader.IsDBNull(0)) VendorOptions.Add(reader.GetString(0));
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("InvoiceBuilderDialog: failed to load vendors", ex);
            }

            VendorOptions.Add(AddNewOption);
        }

        private void LoadCartridgeModelOptions()
        {
            CartridgeModelOptions.Clear();
            CartridgeModelOptions.Add(NoneOption);
            _cartridgeModelIds.Clear();

            try
            {
                using (var con = new SqlConnection(DatabaseConfig.ConnectionString))
                {
                    con.Open();
                    using (var cmd = new SqlCommand(
                        "SELECT CartridgeModelId, ModelNumber FROM dbo.CartridgeModel WHERE IsActive = 1 ORDER BY ModelNumber", con))
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            string modelNumber = reader.GetString(1);
                            CartridgeModelOptions.Add(modelNumber);
                            _cartridgeModelIds[modelNumber] = reader.GetInt32(0);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("InvoiceBuilderDialog: failed to load cartridge models", ex);
            }

            CartridgeModelOptions.Add(AddNewOption);
        }

        /// <summary>
        /// Loads dbo.ConsumableModel into one list per family, so an Ink line never offers a Toner
        /// model. Rebuilt wholesale rather than patched, so a model added on the spot lands in the
        /// right list without any special casing.
        /// </summary>
        private void LoadConsumableModelOptions()
        {
            var lists = new Dictionary<string, ObservableCollection<string>>(StringComparer.OrdinalIgnoreCase)
            {
                { "Ink", InkModelOptions },
                { "Toner", TonerModelOptions },
                { "Print Head", PrintHeadModelOptions }
            };

            foreach (var list in lists.Values)
            {
                list.Clear();
                list.Add(NoneOption);
            }

            // Only drop the consumable half of the map; cartridge ids live in their own dictionary.
            _consumableModelIds.Clear();

            try
            {
                using (var con = new SqlConnection(DatabaseConfig.ConnectionString))
                {
                    con.Open();
                    using (var cmd = new SqlCommand(
                        "SELECT ConsumableModelId, ModelNumber, Category FROM dbo.ConsumableModel WHERE IsActive = 1 ORDER BY ModelNumber", con))
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            string modelNumber = reader.GetString(1);
                            string family = ConsumableFamily(reader.IsDBNull(2) ? null : reader.GetString(2));
                            if (family == null || !lists.ContainsKey(family)) continue;

                            lists[family].Add(modelNumber);
                            _consumableModelIds[ConsumableKey(family, modelNumber)] = reader.GetInt32(0);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("InvoiceBuilderDialog: failed to load consumable models", ex);
            }

            foreach (var list in lists.Values) list.Add(AddNewOption);
        }

        // ─────────────────────────────────────────────────────────────────────
        // On-the-spot lookup creation — mirrors the "+ Add" buttons on
        // BatchAddItemDialog so a missing category/vendor/model does not force the
        // user to abandon a half-built invoice.
        // ─────────────────────────────────────────────────────────────────────
        private void BtnNewCategory_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new Category.QuickAddCategory();
            if (dialog.ShowDialog(GetWin32Owner()) != System.Windows.Forms.DialogResult.OK) return;

            try
            {
                LoadCategoryOptions();
                LblStatus.Text = string.IsNullOrWhiteSpace(dialog.NewCategoryName)
                    ? "Category list refreshed."
                    : $"Category '{dialog.NewCategoryName}' added — pick it in any line's Category column.";
            }
            catch (Exception ex)
            {
                Logger.LogError("InvoiceBuilderDialog: failed to refresh categories", ex);
            }
        }

        private void BtnNewVendor_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new Vendor.QuickAddVendorDialog();
            if (dialog.ShowDialog(GetWin32Owner()) != System.Windows.Forms.DialogResult.OK) return;

            LoadVendorOptions();
            LblStatus.Text = "Vendor added — pick it in any line's Vendor column.";
        }

        private void BtnNewCartridgeModel_Click(object sender, RoutedEventArgs e)
        {
            using (var dialog = new Cartridge.QuickAddCartridgeModelDialog())
            {
                if (dialog.ShowDialog(GetWin32Owner()) != System.Windows.Forms.DialogResult.OK) return;

                LoadCartridgeModelOptions();
                LblStatus.Text = string.IsNullOrWhiteSpace(dialog.NewModelNumber)
                    ? "Cartridge model list refreshed."
                    : $"Cartridge model '{dialog.NewModelNumber}' added — pick it on a Cartridge line.";
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        // "Add new…" chosen from inside a dropdown
        //
        // Each lookup list ends with an AddNewOption entry. Picking it is not a value — it opens
        // the matching quick-add dialog and then selects whatever was created, so a missing
        // category / vendor / model can be created without leaving the cell being filled in.
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>Guards against re-entering the handler while a value is programmatically restored.</summary>
        private bool _suppressAddNew;

        private void OnCategorySelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_suppressAddNew) return;
            var combo = sender as ComboBox;
            if (!IsAddNew(combo)) return;

            var line = combo.DataContext as BuilderLine;
            RevertSelection(combo, line == null ? null : line.Category);

            var dialog = new Category.QuickAddCategory();
            if (dialog.ShowDialog(GetWin32Owner()) != System.Windows.Forms.DialogResult.OK) return;

            LoadCategoryOptions();
            if (line != null && !string.IsNullOrWhiteSpace(dialog.NewCategoryName))
                SelectAfterReload(combo, line, () => line.Category = dialog.NewCategoryName);
        }

        private void OnVendorSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_suppressAddNew) return;
            var combo = sender as ComboBox;
            if (!IsAddNew(combo)) return;

            var line = combo.DataContext as BuilderLine;
            RevertSelection(combo, line == null ? null : line.Vendor);

            var dialog = new Vendor.QuickAddVendorDialog();
            if (dialog.ShowDialog(GetWin32Owner()) != System.Windows.Forms.DialogResult.OK) return;

            string created = dialog.NewVendorName;
            LoadVendorOptions();
            if (line != null && !string.IsNullOrWhiteSpace(created))
                SelectAfterReload(combo, line, () => line.Vendor = created);
        }

        private void OnModelSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_suppressAddNew) return;
            var combo = sender as ComboBox;
            if (!IsAddNew(combo)) return;

            var line = combo.DataContext as BuilderLine;
            RevertSelection(combo, line == null ? null : line.Model);
            if (line == null) return;

            // Which catalogue to add to follows the line's own category.
            if (IsCartridgeCategory(line.Category))
            {
                using (var dialog = new Cartridge.QuickAddCartridgeModelDialog(line.ModelNumber))
                {
                    if (dialog.ShowDialog(GetWin32Owner()) != System.Windows.Forms.DialogResult.OK) return;

                    string created = dialog.NewModelNumber;
                    LoadCartridgeModelOptions();
                    if (!string.IsNullOrWhiteSpace(created))
                        SelectAfterReload(combo, line, () => line.Model = created);
                }
                return;
            }

            string family = ConsumableFamily(line.Category);
            if (family == null)
            {
                MessageBox.Show(this,
                    "This line's Category has no model list. Models apply to Cartridge, Ink, Toner and Print Head items.",
                    "Add Model", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            using (var dialog = new Consumable.QuickAddConsumableModelDialog(family, line.ModelNumber))
            {
                if (dialog.ShowDialog(GetWin32Owner()) != System.Windows.Forms.DialogResult.OK) return;

                string created = dialog.NewModelNumber;
                LoadConsumableModelOptions();
                if (!string.IsNullOrWhiteSpace(created))
                    SelectAfterReload(combo, line, () => line.Model = created);
            }
        }

        private static bool IsAddNew(ComboBox combo) =>
            combo != null && string.Equals(combo.SelectedItem as string, AddNewOption, StringComparison.Ordinal);

        /// <summary>
        /// Puts the cell back to the value it held before "Add new…" was picked, so a cancelled
        /// dialog leaves no trace of the sentinel.
        /// </summary>
        private void RevertSelection(ComboBox combo, string previous)
        {
            _suppressAddNew = true;
            try { combo.SelectedItem = previous; }
            finally { _suppressAddNew = false; }
        }

        /// <summary>
        /// Applies the newly created value once its list has been rebuilt. Deferred to Background
        /// priority because the combo has to re-bind to the refreshed collection before a
        /// SelectedItem outside the old list will stick.
        /// </summary>
        private void SelectAfterReload(ComboBox combo, BuilderLine line, Action apply)
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                _suppressAddNew = true;
                // Setting the model property is enough: the column's SelectedItemBinding pushes
                // the new value into the combo once the refreshed list contains it.
                try { apply(); }
                finally { _suppressAddNew = false; }
                Recalculate();
            }), System.Windows.Threading.DispatcherPriority.Background);
        }

        /// <summary>
        /// The quick-add dialogs are WinForms, so they need a Win32 owner to sit above this
        /// window rather than behind it.
        /// </summary>
        private System.Windows.Forms.IWin32Window GetWin32Owner()
        {
            var handle = new System.Windows.Interop.WindowInteropHelper(this).Handle;
            return handle == IntPtr.Zero ? null : new Win32Window(handle);
        }

        private sealed class Win32Window : System.Windows.Forms.IWin32Window
        {
            public Win32Window(IntPtr handle) { Handle = handle; }
            public IntPtr Handle { get; }
        }

        // ─────────────────────────────────────────────────────────────────────
        // Groups and lines
        // ─────────────────────────────────────────────────────────────────────
        private BuilderGroup AddGroup(bool isUngrouped)
        {
            var group = new BuilderGroup(isUngrouped);

            if (!isUngrouped)
            {
                // A new group starts priced, not blank — the user should not have to remember to
                // retype the same three defaults into every group. Existing groups are never
                // touched by this (there is no "load an existing invoice" path here — this dialog
                // only ever builds new ones — so every real group that ever exists in this window
                // passes through here exactly once, at creation).
                group.VatPercentText = "12";
                group.WhtPercentText = "2";
                group.DiscountPercentText = "0";
            }

            // Any edit anywhere in the group feeds the invoice-level totals.
            group.Changed += Recalculate;
            group.LineModelCleared += OnLineModelCleared;
            group.LineExistingItemToggleRequested += OnLineExistingItemToggleRequested;
            group.Lines.CollectionChanged += OnLinesChanged;

            group.Lines.Add(new BuilderLine());
            _groups.Add(group);
            return group;
        }

        private void OnLinesChanged(object sender, NotifyCollectionChangedEventArgs e) => Recalculate();

        /// <summary>
        /// A model the user had picked was discarded because the line's Category changed and the
        /// two catalogues do not overlap. Say so plainly — a value silently vanishing from a cell
        /// reads as the app losing the edit.
        ///
        /// Deliberately a banner rather than a modal: changing the category of several lines in a
        /// row is normal, and a dialog per row would be worse than the problem it reports.
        /// </summary>
        private void OnLineModelCleared(BuilderLine line, string previousCategory, string discardedModel)
        {
            string where = string.IsNullOrWhiteSpace(line?.ItemName) ? "A line" : $"'{line.ItemName}'";
            string from = string.IsNullOrWhiteSpace(previousCategory) ? "its previous category" : previousCategory;
            string to = string.IsNullOrWhiteSpace(line?.Category) ? "the new category" : line.Category;

            // Only suggest picking a replacement when the new category actually has models —
            // telling someone to choose a "Battery model" would send them looking for a list
            // that does not exist.
            string advice = line != null && line.SupportsModel
                ? $"which does not share a model list with {to} — pick a {to} model instead."
                : $"and {to} has no model list, so this line will be saved without a model.";

            LblModelWarning.Text = $"⚠  {where}: model '{discardedModel}' was cleared. It belongs to {from}, {advice}";
            BannerModelWarning.Visibility = Visibility.Visible;
        }

        private void BtnDismissModelWarning_Click(object sender, RoutedEventArgs e) =>
            BannerModelWarning.Visibility = Visibility.Collapsed;

        /// <summary>
        /// A row's own "Existing?" checkbox was just ticked on — a deliberate, per-row action
        /// (see the column's XAML comment), never triggered by "+ Add line"/"+ 5 lines"
        /// themselves. Deferred one dispatcher cycle: opening a modal synchronously from
        /// inside the same click that commits the checkbox's value pre-empts the DataGrid's
        /// own render pass, so the box can visually appear still unchecked until the modal
        /// closes — posting the actual picker call to run after that render pass fixes it.
        /// </summary>
        private void OnLineExistingItemToggleRequested(BuilderLine line)
        {
            Dispatcher.BeginInvoke(new Action(() => OpenExistingItemPickerForLine(line)),
                System.Windows.Threading.DispatcherPriority.Background);
        }

        /// <summary>
        /// Opens the same searchable, multi-select item picker SoftwareServiceSetDialog's "Add
        /// Item" button uses (SoftwareItemPickerDialog), already excluding items picked by
        /// other lines on this invoice so the same existing item can't be added twice. The
        /// first checked item populates the row whose checkbox triggered this; any further
        /// checked items each get their own new row auto-added right after it in the same
        /// group, so checking several items in one picker session adds all of them instead of
        /// silently discarding everything past the first. Cancelling, or a picker that returns
        /// no selection, unchecks the triggering row's box again rather than leaving a
        /// half-set row with nothing behind it.
        /// </summary>
        private void OpenExistingItemPickerForLine(BuilderLine line)
        {
            var excludedIds = _groups
                .SelectMany(g => g.Lines)
                .Where(l => l != line && l.ExistingItemId.HasValue)
                .Select(l => l.ExistingItemId.Value)
                .ToList();

            using (var picker = new SoftwareItemPickerDialog(excludedIds, isForInvoiceSet: true))
            {
                if (picker.ShowDialog(GetWin32Owner()) != System.Windows.Forms.DialogResult.OK
                    || picker.SelectedItems.Count == 0)
                {
                    line.IsExistingItem = false;
                    return;
                }

                var picked = picker.SelectedItems[0];
                line.ExistingItemId = picked.ItemId;
                line.ItemName = picked.Name;
                line.Description = picked.Description;
                line.Category = picked.Category;
                line.ItemType = picked.ItemType;
                line.ModelNumber = picked.ModelNumber;

                if (picker.SelectedItems.Count > 1)
                {
                    var group = _groups.FirstOrDefault(g => g.Lines.Contains(line));
                    if (group != null)
                    {
                        foreach (var extra in picker.SelectedItems.Skip(1))
                        {
                            var newLine = new BuilderLine();
                            group.Lines.Add(newLine);
                            newLine.SetExistingItemDirect(extra.ItemId, extra.Name, extra.Description, extra.Category, extra.ItemType, extra.ModelNumber);
                        }
                    }
                }
            }
        }

        /// <summary>Blocks editing the new-item-only columns on a line once it's switched to
        /// an existing item — DataGridColumn.IsReadOnly is column-wide, not per-row, so this is
        /// the standard WPF way to make a subset of ROWS read-only instead. Paired with the
        /// ExistingItemLockedCell style in XAML, which greys those same cells out visually.</summary>
        private static readonly HashSet<string> ExistingItemLockedHeaders = new HashSet<string>(StringComparer.Ordinal)
        {
            "Item Name *", "Description", "Category *", "Item Type", "Serial #s", "Model #",
            "Vendor", "Model", "License #", "Part #", "Warranty (Yrs)", "Remarks",
            "Date Purchased", "Warranty Start", "Warranty End"
        };

        private void LinesGrid_BeginningEdit(object sender, DataGridBeginningEditEventArgs e)
        {
            if (e.Row.Item is BuilderLine line && line.IsExistingItem
                && e.Column.Header is string header && ExistingItemLockedHeaders.Contains(header))
            {
                e.Cancel = true;
            }
        }

        private void BtnAddGroup_Click(object sender, RoutedEventArgs e)
        {
            var group = AddGroup(isUngrouped: false);
            // Inherit the invoice coverage dates — the common case is a group that runs for the
            // same period as the invoice, and they are still editable per group.
            group.BeginDate = _header.StartDate;
            group.EndDate = _header.EndDate;
            SelectGroup(group);
            Recalculate();
        }

        /// <summary>Switches which group's card is shown. The tab itself carries the group as its
        /// DataContext, same as every other per-row control on this page.</summary>
        private void BtnSelectGroupTab_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as FrameworkElement)?.DataContext is BuilderGroup group)
                SelectGroup(group);
        }

        private void BtnRemoveGroup_Click(object sender, RoutedEventArgs e)
        {
            if (!((sender as FrameworkElement)?.Tag is BuilderGroup group)) return;
            if (group.IsUngrouped) return;   // the ungrouped bucket is permanent

            if (group.Lines.Any(l => !l.IsBlank))
            {
                var confirm = MessageBox.Show(
                    $"Remove this group and its {group.Lines.Count} line(s)?",
                    "Remove Sub-Type Group", MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (confirm != MessageBoxResult.Yes) return;
            }

            bool wasSelected = group.IsSelected;

            group.Changed -= Recalculate;
            group.LineModelCleared -= OnLineModelCleared;
            group.LineExistingItemToggleRequested -= OnLineExistingItemToggleRequested;
            group.Lines.CollectionChanged -= OnLinesChanged;
            _groups.Remove(group);

            // The card behind a removed tab can't stay on screen — land on whatever's left,
            // preferring a real group over the Ungrouped bucket, same reasoning as the initial
            // Loaded selection.
            if (wasSelected)
                SelectGroup(_groups.FirstOrDefault(g => !g.IsUngrouped) ?? _groups.FirstOrDefault());

            Recalculate();
        }

        // Always plain blank rows — never opens the item picker. Marking a specific row as an
        // existing item happens afterward, per-row, via its own "Existing?" checkbox (see
        // OnLineExistingItemToggleRequested) — so the full line count is visible first, and
        // which rows become existing items is a separate, deliberate choice per row.
        private void BtnAddLine_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as FrameworkElement)?.Tag is BuilderGroup group)
                group.Lines.Add(new BuilderLine());
        }

        private void BtnAddFiveLines_Click(object sender, RoutedEventArgs e)
        {
            if (!((sender as FrameworkElement)?.Tag is BuilderGroup group)) return;
            for (int i = 0; i < 5; i++) group.Lines.Add(new BuilderLine());
        }


        /// <summary>
        /// Opens the "one serial per line" popup for the clicked row, mirroring Batch Add Items'
        /// Serial Numbers box. The button's DataContext is the row's own BuilderLine (standard
        /// DataGridTemplateColumn cell binding), so no Tag plumbing is needed to find it.
        /// </summary>
        private void BtnEditSerialNumbers_Click(object sender, RoutedEventArgs e)
        {
            var line = (sender as FrameworkElement)?.DataContext as BuilderLine;
            if (line == null) return;

            var dialog = new SerialNumbersDialog(line.ItemName, line.SerialNumbers) { Owner = this };
            if (dialog.ShowDialog() == true)
                line.SetSerialNumbers(dialog.Result);
        }

        private void BtnRemoveLine_Click(object sender, RoutedEventArgs e)
        {
            if (!((sender as FrameworkElement)?.Tag is BuilderGroup group)) return;

            // Read the grid's current selection directly rather than trusting a snapshot taken
            // earlier by a SelectionChanged handler — that snapshot could go stale (e.g. a
            // pending cell edit commits and reshapes selection) between the last click and this
            // button's click, which was silently dropping all but one selected row.
            var selected = LinesGrid.SelectedItems.OfType<BuilderLine>().ToList();
            if (selected.Count == 0)
            {
                MessageBox.Show(this,
                    "Select the line(s) you want to remove first — click anywhere on a row to select it.",
                    "Remove Line", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            foreach (var line in selected) group.Lines.Remove(line);

            // Never leave a group with no row to type into.
            if (group.Lines.Count == 0) group.Lines.Add(new BuilderLine());

            Recalculate();
        }

        // ─────────────────────────────────────────────────────────────────────
        // Financials
        // ─────────────────────────────────────────────────────────────────────
        private void OnFinancialInputChanged(object sender, TextChangedEventArgs e) => Recalculate();

        private static decimal ParsePercent(TextBox box)
        {
            if (box == null) return 0m;
            return decimal.TryParse((box.Text ?? string.Empty).Trim(),
                NumberStyles.Any, CultureInfo.InvariantCulture, out var value) ? value : 0m;
        }

        /// <summary>
        /// Recomputes every group card and then the invoice totals.
        ///
        /// Two independent layers, exactly as ViewInvoiceDetailPage works:
        ///   1. Each Sub-Type group nets out its OWN discount, VAT and WHT (see
        ///      BuilderGroup.Total) — a group's percentages never touch another group's.
        ///   2. The invoice Subtotal is the sum of those group totals plus any ungrouped
        ///      lines (matching UpdateHeaderSubtotalFromGroups), and the header's own
        ///      percentages then apply on top of that combined figure.
        ///
        /// Both layers use one chain: discount comes off first, then VAT and WHT are figured
        /// on the discounted net — identical to the importer's ComputeFinancials, which is
        /// what actually gets persisted.
        /// </summary>
        private void Recalculate()
        {
            if (LblSubtotal == null) return;   // still constructing

            foreach (var group in _groups) group.RefreshSubtotal();

            decimal subtotal = _groups.Sum(g => g.ContributionToInvoice);
            decimal vatPct = ParsePercent(TxtVatPercent);
            decimal discPct = ParsePercent(TxtDiscountPercent);
            decimal whtPct = ParsePercent(TxtWhtPercent);

            decimal discount = Math.Round(subtotal * discPct / 100m, 2);
            decimal netAfterDiscount = subtotal - discount;
            decimal vat = Math.Round(netAfterDiscount * vatPct / 100m, 2);
            decimal wht = Math.Round(netAfterDiscount * whtPct / 100m, 2);
            decimal total = netAfterDiscount + vat - wht;

            LblSubtotal.Text = subtotal.ToString("N2");
            LblVat.Text = vat.ToString("N2");
            LblDiscount.Text = discount.ToString("N2");
            LblWht.Text = wht.ToString("N2");
            LblTotalDue.Text = total.ToString("N2");

            int lines = _groups.Sum(g => g.Lines.Count(l => !l.IsBlank));
            int tagged = _groups.Where(g => !g.IsUngrouped).Sum(g => g.Lines.Count(l => !l.IsBlank));
            LblStatus.Text = lines == 0
                ? "Add at least one line item."
                : $"{lines} line item(s) — {tagged} in a Sub-Type group, {lines - tagged} ungrouped.";
        }

        // ─────────────────────────────────────────────────────────────────────
        // Create
        // ─────────────────────────────────────────────────────────────────────
        private async void BtnCreate_Click(object sender, RoutedEventArgs e)
        {
            var rows = BuildRows(out var errors);
            if (errors.Count > 0)
            {
                MessageBox.Show(
                    "Fix the following before creating the invoice:\n\n   " +
                    string.Join("\n   ", errors.Take(12)) +
                    (errors.Count > 12 ? $"\n   … and {errors.Count - 12} more" : string.Empty),
                    "Incomplete Invoice", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var confirm = MessageBox.Show(
                $"Create invoice '{rows[0].DocumentNumber}' with {rows.Count} line item(s)?\n\n" +
                $"{rows.Count} new Item(s) will be created and bundled into one Sales Invoice Set.",
                "Create Invoice", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (confirm != MessageBoxResult.Yes) return;

            BtnCreate.IsEnabled = false;
            LblStatus.Text = "Creating invoice…";

            try
            {
                var groups = rows
                    .GroupBy(r => r.DocumentNumber, StringComparer.OrdinalIgnoreCase)
                    .ToList();

                var (invoicesCreated, itemsCreated, importErrors, createdSetIds) =
                    await Task.Run(() => InvoiceCsvImportDialog.RunImport(groups));

                if (importErrors.Count > 0)
                {
                    LblStatus.Text = "Invoice was not created.";
                    BtnCreate.IsEnabled = true;
                    MessageBox.Show(
                        "The invoice could not be created — nothing was saved:\n\n   " +
                        string.Join("\n   ", importErrors),
                        "Create Invoice", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                CreatedInvoice = invoicesCreated > 0;

                // BuildRows() stamps _header.DocumentNumber onto every row, so there is always
                // exactly one Document # — and therefore one Set — per Build Invoice session.
                bool receiptLinked = false;
                if (_header.ReceiptSetId.HasValue &&
                    createdSetIds.TryGetValue(_header.DocumentNumber ?? string.Empty, out var newSetId))
                {
                    try
                    {
                        new ReceiptSetRepository().AttachReceiptSetToSet(
                            _header.ReceiptSetId.Value, newSetId, AppSession.CurrentUserId);
                        receiptLinked = true;
                    }
                    catch (Exception ex)
                    {
                        Logger.LogError("InvoiceBuilderDialog: receipt attach failed", ex);
                    }
                }

                string message = $"Created {invoicesCreated} invoice(s) and {itemsCreated} item(s).";
                if (_header.ReceiptSetId.HasValue)
                    message += receiptLinked ? "\nReceipt set linked." : "\nReceipt set could NOT be linked — link it manually from the invoice.";

                MessageBox.Show(message, "Create Invoice", MessageBoxButton.OK, MessageBoxImage.Information);
                DialogResult = true;
                Close();
            }
            catch (Exception ex)
            {
                Logger.LogError("InvoiceBuilderDialog: create failed", ex);
                LblStatus.Text = "Invoice was not created.";
                BtnCreate.IsEnabled = true;
                MessageBox.Show("The invoice could not be created:\n\n" + ex.Message,
                    "Create Invoice", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// Flattens the page into the importer's row model — one row per line item, with the
        /// invoice header repeated on each (the importer reads header values from the first row of
        /// a Document # group) and the parent group's Sub-Type details stamped on.
        /// </summary>
        private List<InvoiceCsvImportDialog.InvoiceCsvRow> BuildRows(out List<string> errors)
        {
            errors = new List<string>();
            var rows = new List<InvoiceCsvImportDialog.InvoiceCsvRow>();

            string documentNumber = (_header.DocumentNumber ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(documentNumber))
                errors.Add("Document # is required.");

            // Company and Distributor are two alternative classifications for the same
            // "subject" of the invoice — the live hint under the fields warns as soon as both
            // are non-blank, but this is the hard stop that actually keeps it from saving.
            if (!string.IsNullOrWhiteSpace(_header.Company) && !string.IsNullOrWhiteSpace(_header.Distributor))
                errors.Add("Company and Distributor cannot both be set — choose only one.");

            decimal vatPct = ParsePercent(TxtVatPercent);
            decimal discPct = ParsePercent(TxtDiscountPercent);
            decimal whtPct = ParsePercent(TxtWhtPercent);

            int lineNumber = 0;

            foreach (var group in _groups)
            {
                // "(none)" is a deliberate, valid choice — same as the Ungrouped bucket, its
                // lines save with no Sub-Type at all — so only a truly untouched group (SubType
                // still blank, never chosen either way) is an error.
                bool isEffectivelyUngrouped = group.IsUngrouped || group.HasNoSubType;

                if (!group.IsUngrouped && !group.HasNoSubType)
                {
                    bool hasContent = group.Lines.Any(l => !l.IsBlank);
                    if (hasContent && string.IsNullOrWhiteSpace(group.SubType))
                        errors.Add($"A group with {group.Lines.Count(l => !l.IsBlank)} line(s) has no Sub-Type selected.");
                }

                foreach (var line in group.Lines)
                {
                    if (line.IsBlank) continue;   // untouched spare rows are ignored, not errors
                    lineNumber++;

                    if (line.IsExistingItem)
                    {
                        if (!line.ExistingItemId.HasValue)
                            errors.Add($"Line {lineNumber}: 'Existing?' is checked but no item was picked.");
                    }
                    else
                    {
                        if (string.IsNullOrWhiteSpace(line.ItemName))
                            errors.Add($"Line {lineNumber}: Item Name is required.");

                        if (string.IsNullOrWhiteSpace(line.Category) || !_categoryIds.ContainsKey(line.Category))
                            errors.Add($"Line {lineNumber} ('{line.ItemName}'): pick a Category.");
                    }

                    // One dbo.Item per serial when any are entered (Quantity is then ignored,
                    // matching Batch Add Items); otherwise a single row using the typed Quantity,
                    // exactly as before Serial #s existed. An existing-item line always resolves
                    // to exactly one row — it references one already-created dbo.Item, so there
                    // is nothing to fan out per serial.
                    var unitsForThisLine = line.IsExistingItem
                        ? new List<(string Serial, int Quantity)> { (null, Math.Max(1, line.Quantity)) }
                        : line.SerialNumbers.Count > 0
                            ? line.SerialNumbers.Select(s => (Serial: (string)s, Quantity: 1)).ToList()
                            : new List<(string Serial, int Quantity)> { (null, Math.Max(1, line.Quantity)) };

                    foreach (var unit in unitsForThisLine)
                    {
                    rows.Add(new InvoiceCsvImportDialog.InvoiceCsvRow
                    {
                        LineNumber = lineNumber,

                        DocumentNumber = documentNumber,
                        ReferenceNumber = (_header.ReferenceNumber ?? string.Empty).Trim(),
                        DocumentDate = _header.DocumentDate,
                        Company = _header.Company?.Trim(),
                        Distributor = _header.Distributor?.Trim(),
                        SiteCompany = (_header.SiteCompany ?? string.Empty).Trim(),
                        SiteBranch = _header.SiteBranch?.Trim(),
                        SiteDepartment = _header.SiteDepartment?.Trim(),
                        StartDate = _header.StartDate,
                        EndDate = _header.EndDate,

                        // Subtotal is left computed — the importer sums the line amounts, which is
                        // exactly what the on-screen Subtotal shows.
                        HasSubtotal = false,
                        VatPercent = vatPct,
                        DiscountPercent = discPct,
                        WhtPercent = whtPct,

                        // Set only for a line whose "Existing?" checkbox is on — RunImport skips
                        // creating a new dbo.Item entirely and reuses this id instead.
                        ExistingItemId = line.IsExistingItem ? line.ExistingItemId : null,

                        ItemName = line.ItemName?.Trim(),
                        Description = line.Description?.Trim(),
                        ModelNumber = line.ModelNumber?.Trim(),
                        SerialNumber = unit.Serial,
                        ItemType = line.ItemType,
                        Category = line.Category,
                        Vendor = line.Vendor?.Trim(),
                        UnitOfMeasure = line.UnitOfMeasure?.Trim(),
                        UnitPrice = line.UnitPrice,
                        Quantity = unit.Quantity,
                        LicenseNumber = line.LicenseNumber?.Trim(),
                        PartNumber = line.PartNumber?.Trim(),
                        WarrantyYears = Math.Max(0, line.WarrantyYears),
                        WarrantyStartDate = line.WarrantyStartDate,
                        WarrantyEndDate = line.WarrantyEndDate,
                        Remarks = line.Remarks?.Trim(),
                        DatePurchased = line.DatePurchased,
                        LineStartDate = line.LineStartDate,
                        LineEndDate = line.LineEndDate,

                        // The Model column feeds whichever FK matches the line's category.
                        // "(none)", the "Add new…" sentinel and anything not in the catalogue all
                        // resolve to null rather than a bogus FK.
                        CartridgeModelId = ResolveCartridgeModelId(line),
                        ConsumableModelId = ResolveConsumableModelId(line),

                        // Sub-Type group. Blank on the ungrouped bucket, which the importer
                        // reads as "no group" — those lines print without a banner.
                        SubType = isEffectivelyUngrouped ? null : group.SubType,
                        ReferenceCode = isEffectivelyUngrouped ? null : group.ReferenceCode?.Trim(),
                        GroupBeginDate = isEffectivelyUngrouped ? null : group.BeginDate,
                        GroupEndDate = isEffectivelyUngrouped ? null : group.EndDate,

                        // The group's own financials. Left null when the user did not type any,
                        // so the group keeps falling back to the header percentages and its
                        // computed line sum rather than being pinned to a zero.
                        GroupVatPercent = isEffectivelyUngrouped ? null : group.VatPercent,
                        GroupWhtPercent = isEffectivelyUngrouped ? null : group.WhtPercent,
                        GroupDiscountPercent = isEffectivelyUngrouped ? null : group.DiscountPercent,
                        GroupSubtotalOverride = isEffectivelyUngrouped ? null : group.SubtotalOverride,

                        // Parent Tag — independent of, and not gated by, Sub-Type grouping above;
                        // a group can be Sub-Type-ungrouped and still carry its own Parent Tag.
                        ParentTagLabel = string.IsNullOrWhiteSpace(group.ParentTagLabel) ? null : group.ParentTagLabel.Trim(),

                        // Categories are picked from a dropdown here, so they are valid by
                        // construction — stamp the FK the importer would otherwise resolve.
                        CategoryValid = !string.IsNullOrWhiteSpace(line.Category) && _categoryIds.ContainsKey(line.Category),
                        CategoryId = !string.IsNullOrWhiteSpace(line.Category) && _categoryIds.ContainsKey(line.Category)
                            ? _categoryIds[line.Category]
                            : 0
                    });
                    }   // foreach unit
                }
            }

            // Cross-check every serial entered anywhere on this invoice — the same guard Batch
            // Add Items runs in-memory before ever touching the database. A collision here is
            // reported once, by name, instead of surfacing as a raw SQL error deep inside the
            // save transaction.
            var duplicateSerials = rows
                .Where(r => !string.IsNullOrWhiteSpace(r.SerialNumber))
                .GroupBy(r => r.SerialNumber, StringComparer.OrdinalIgnoreCase)
                .Where(g => g.Count() > 1)
                .Select(g => g.Key)
                .ToList();
            if (duplicateSerials.Count > 0)
                errors.Add("Duplicate serial number(s) on this invoice: " + string.Join(", ", duplicateSerials));

            if (rows.Count == 0) errors.Add("Add at least one line item.");
            return rows;
        }

        /// <summary>Cartridge lines only; every other category resolves to null.</summary>
        private int? ResolveCartridgeModelId(BuilderLine line)
        {
            if (line == null || !IsCartridgeCategory(line.Category)) return null;
            if (string.IsNullOrWhiteSpace(line.Model)) return null;

            int id;
            return _cartridgeModelIds.TryGetValue(line.Model, out id) ? id : (int?)null;
        }

        /// <summary>Ink / Toner / Print Head lines only; keyed by family so an Ink model can never
        /// be saved against a Toner line.</summary>
        private int? ResolveConsumableModelId(BuilderLine line)
        {
            if (line == null || string.IsNullOrWhiteSpace(line.Model)) return null;

            string family = ConsumableFamily(line.Category);
            if (family == null) return null;

            int id;
            return _consumableModelIds.TryGetValue(ConsumableKey(family, line.Model), out id) ? id : (int?)null;
        }

        // ─────────────────────────────────────────────────────────────────────
        // Row models
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// One Sub-Type group on the page — becomes one dbo.SetItemSubTypeGroup row, which is what
        /// the Invoice and Renewals reports print as a banner with its own subtotal underneath.
        /// The <see cref="IsUngrouped"/> instance is the bucket for lines that carry no sub-type.
        /// </summary>
        public sealed class BuilderGroup : INotifyPropertyChanged
        {
            public event PropertyChangedEventHandler PropertyChanged;
            /// <summary>Raised on any edit that can move a total.</summary>
            public event Action Changed;

            /// <summary>Forwards <see cref="BuilderLine.ModelCleared"/> from any line in this
            /// group, so the dialog can subscribe once per group instead of once per row.</summary>
            public event Action<BuilderLine, string, string> LineModelCleared;

            /// <summary>Forwards <see cref="BuilderLine.ExistingItemToggleRequested"/>, same
            /// once-per-group rationale as LineModelCleared.</summary>
            public event Action<BuilderLine> LineExistingItemToggleRequested;

            public BuilderGroup(bool isUngrouped)
            {
                IsUngrouped = isUngrouped;
                Lines = new ObservableCollection<BuilderLine>();
                Lines.CollectionChanged += (s, e) =>
                {
                    if (e.NewItems != null)
                        foreach (BuilderLine line in e.NewItems)
                        {
                            line.Changed += OnLineChanged;
                            line.ModelCleared += OnLineModelCleared;
                            line.ExistingItemToggleRequested += OnLineExistingItemToggleRequested;
                        }
                    if (e.OldItems != null)
                        foreach (BuilderLine line in e.OldItems)
                        {
                            line.Changed -= OnLineChanged;
                            line.ModelCleared -= OnLineModelCleared;
                            line.ExistingItemToggleRequested -= OnLineExistingItemToggleRequested;
                        }
                    OnLineChanged();
                };
            }

            private void OnLineModelCleared(BuilderLine line, string previousCategory, string discardedModel) =>
                LineModelCleared?.Invoke(line, previousCategory, discardedModel);

            private void OnLineExistingItemToggleRequested(BuilderLine line) =>
                LineExistingItemToggleRequested?.Invoke(line);

            private void OnLineChanged()
            {
                RefreshSubtotal();
                Raise(nameof(LineCount));
                Raise(nameof(HasLines));
                Changed?.Invoke();
            }

            public bool IsUngrouped { get; }
            public ObservableCollection<BuilderLine> Lines { get; }

            /// <summary>
            /// Whether this group's card is the one currently shown below the tab strip. Only one
            /// group is ever selected at a time — InvoiceBuilderDialog.SelectGroup() is the single
            /// place that enforces that, this property just reacts to it.
            /// </summary>
            private bool _isSelected;
            public bool IsSelected
            {
                get => _isSelected;
                set
                {
                    if (_isSelected == value) return;
                    _isSelected = value;
                    Raise();
                    Raise(nameof(TabBackground));
                    Raise(nameof(TabForeground));
                }
            }

            /// <summary>Whether the Financial Details expander is open. Defaults to expanded so a
            /// newly created group is immediately usable without an extra click; each group keeps
            /// its own state afterwards, since the shared editor swaps its DataContext to a
            /// different BuilderGroup instance rather than resetting this one.</summary>
            private bool _isFinancialExpanded = true;
            public bool IsFinancialExpanded
            {
                get => _isFinancialExpanded;
                set { _isFinancialExpanded = value; Raise(); }
            }

            /// <summary>Real (non-blank) lines only — shown as a count badge on the tab so the
            /// user can tell how full a group is without opening it.</summary>
            public int LineCount => Lines.Count(l => !l.IsBlank);

            /// <summary>Whether the tab's count badge should render at all — an empty group's tab
            /// stays uncluttered rather than showing a "0".</summary>
            public bool HasLines => LineCount > 0;

            /// <summary>What the nav row reads. The Ungrouped bucket carries a warning glyph —
            /// it holds lines with no Sub-Type, which is a valid but easy-to-miss choice now that
            /// its editor is hidden behind a nav row like every other group.</summary>
            public string TabLabel
            {
                get
                {
                    if (IsUngrouped) return "⚠ Ungrouped";
                    string label = string.IsNullOrWhiteSpace(SubType) ? "New Group"
                        : SubType == NoneOption ? "No Sub-Type"
                        : SubType;
                    return string.IsNullOrWhiteSpace(ReferenceCode) ? label : $"{label} · {ReferenceCode}";
                }
            }

            // Ungrouped uses an amber caution palette instead of the app's usual accent blue, so
            // the tab itself signals "no Sub-Type" before the user even opens its card.
            private static readonly SolidColorBrush AccentSelectedBg = new SolidColorBrush(Color.FromRgb(0x3A, 0x8E, 0xF6));
            private static readonly SolidColorBrush AccentUnselectedBg = new SolidColorBrush(Color.FromRgb(0xEE, 0xF4, 0xFF));
            private static readonly SolidColorBrush AccentUnselectedFg = new SolidColorBrush(Color.FromRgb(0x3A, 0x8E, 0xF6));
            private static readonly SolidColorBrush AccentBorder = new SolidColorBrush(Color.FromRgb(0xBD, 0xD0, 0xF8));

            private static readonly SolidColorBrush CautionSelectedBg = new SolidColorBrush(Color.FromRgb(0xE6, 0x51, 0x00));
            private static readonly SolidColorBrush CautionUnselectedBg = new SolidColorBrush(Color.FromRgb(0xFF, 0xF8, 0xE6));
            private static readonly SolidColorBrush CautionUnselectedFg = new SolidColorBrush(Color.FromRgb(0x8A, 0x5A, 0x00));
            private static readonly SolidColorBrush CautionBorder = new SolidColorBrush(Color.FromRgb(0xF5, 0xC8, 0x42));

            public Brush TabBackground => IsSelected
                ? (IsUngrouped ? (Brush)CautionSelectedBg : AccentSelectedBg)
                : (IsUngrouped ? (Brush)CautionUnselectedBg : AccentUnselectedBg);

            public Brush TabForeground => IsSelected
                ? Brushes.White
                : (IsUngrouped ? (Brush)CautionUnselectedFg : AccentUnselectedFg);

            public Brush TabBorderBrush => IsUngrouped ? (Brush)CautionBorder : AccentBorder;

            /// <summary>"(none)" first, then the four real Sub-Types. Picking "(none)" is a
            /// deliberate choice for a group whose items should stay as this group's own
            /// container — keeping its own line items, Reference Code/dates fields and Financial
            /// Details together — without being tagged with a Sub-Type on save. It is not the
            /// same as putting the lines in the shared Ungrouped bucket: this stays its own named
            /// nav entry, just untagged.</summary>
            public IReadOnlyList<string> SubTypeOptions { get; } =
                new[] { NoneOption }.Concat(ItemSubTypeCatalog.ValidSubTypes).ToList();

            private string _subType;
            public string SubType
            {
                get => _subType;
                set
                {
                    _subType = value;
                    Raise();
                    Raise(nameof(TabLabel));
                    Raise(nameof(HasNoSubType));
                    Changed?.Invoke();
                }
            }

            /// <summary>True once "(none)" is explicitly picked — not the same as SubType simply
            /// being blank on an untouched new group, which shows "New Group" instead of this
            /// notice.</summary>
            public bool HasNoSubType => SubType == NoneOption;

            private string _referenceCode;
            public string ReferenceCode
            {
                get => _referenceCode;
                set { _referenceCode = value; Raise(); Raise(nameof(TabLabel)); }
            }

            /// <summary>Free-text Parent Tag label (e.g. "Cisco") for this group's items — an
            /// independent, orthogonal grouping from Sub-Type, with no financial semantics of
            /// its own. Unlike SubType, this is plain user-typed text, not a fixed enum.</summary>
            private string _parentTagLabel;
            public string ParentTagLabel
            {
                get => _parentTagLabel;
                set { _parentTagLabel = value; Raise(); }
            }

            private DateTime? _beginDate;
            public DateTime? BeginDate
            {
                get => _beginDate;
                set { _beginDate = value; Raise(); }
            }

            private DateTime? _endDate;
            public DateTime? EndDate
            {
                get => _endDate;
                set { _endDate = value; Raise(); }
            }

            /// <summary>Sum of this group's line amounts — the computed subtotal.</summary>
            public decimal LineSum => Lines.Where(l => !l.IsBlank).Sum(l => l.Amount);

            // ── This group's OWN financials ───────────────────────────────────
            // A Sub-Type group is priced independently of the invoice header, exactly like the
            // group cards on ViewInvoiceDetailPage. Percentages left blank fall through to the
            // invoice header's; a blank Subtotal means "use the line sum".

            private string _subtotalOverrideText;
            /// <summary>Blank = use <see cref="LineSum"/>. Typing a figure persists as
            /// dbo.SetItemSubTypeGroup.SubtotalOverride, which the reports and the invoice
            /// detail page prefer over the computed sum.</summary>
            public string SubtotalOverrideText
            {
                get => _subtotalOverrideText;
                set { _subtotalOverrideText = value; Raise(); Recalc(); }
            }

            private string _vatPercentText;
            public string VatPercentText
            {
                get => _vatPercentText;
                set { _vatPercentText = value; Raise(); Recalc(); }
            }

            private string _whtPercentText;
            public string WhtPercentText
            {
                get => _whtPercentText;
                set { _whtPercentText = value; Raise(); Recalc(); }
            }

            private string _discountPercentText;
            public string DiscountPercentText
            {
                get => _discountPercentText;
                set { _discountPercentText = value; Raise(); Recalc(); }
            }

            /// <summary>Parses an optional numeric input; blank or unparseable reads as "not set".</summary>
            private static decimal? ParseOptional(string text)
            {
                if (string.IsNullOrWhiteSpace(text)) return null;
                return decimal.TryParse(text.Trim(), NumberStyles.Any, CultureInfo.InvariantCulture, out var v)
                    ? v
                    : (decimal?)null;
            }

            public decimal? SubtotalOverride => ParseOptional(SubtotalOverrideText);
            public decimal? VatPercent => ParseOptional(VatPercentText);
            public decimal? WhtPercent => ParseOptional(WhtPercentText);
            public decimal? DiscountPercent => ParseOptional(DiscountPercentText);

            /// <summary>The subtotal actually used — the override when set, else the line sum.</summary>
            public decimal Subtotal => SubtotalOverride ?? LineSum;

            /// <summary>
            /// This group's own totals, using the identical chain as
            /// ViewInvoiceDetailPage.RecalculateGroupCardTotal and the CSV importer's
            /// ComputeFinancials: discount comes off the subtotal first, then VAT and WHT are
            /// figured on the discounted net.
            /// </summary>
            public decimal DiscountAmount => Subtotal * (DiscountPercent.GetValueOrDefault() / 100m);
            public decimal NetAfterDiscount => Subtotal - DiscountAmount;
            public decimal VatAmount => NetAfterDiscount * (VatPercent.GetValueOrDefault() / 100m);
            public decimal WhtAmount => NetAfterDiscount * (WhtPercent.GetValueOrDefault() / 100m);
            public decimal Total => NetAfterDiscount + VatAmount - WhtAmount;

            public string LineSumDisplay => LineSum.ToString("N2");
            public string SubtotalDisplay => Subtotal.ToString("N2");
            public string VatDisplay => VatAmount.ToString("N2");
            public string WhtDisplay => WhtAmount.ToString("N2");
            public string DiscountDisplay => DiscountAmount.ToString("N2");
            public string TotalDisplay => Total.ToString("N2");

            public string SubtotalCaption => IsUngrouped ? "Ungrouped lines" : "Group total";

            /// <summary>Reminds the user the typed figure is replacing the computed sum.</summary>
            public string SubtotalHint => SubtotalOverride.HasValue
                ? "overriding line sum " + LineSumDisplay
                : "= sum of lines";

            // The ungrouped bucket is permanent: it has no Sub-Type header to fill in, and no
            // financials of its own — its lines roll straight into the invoice subtotal.
            public Visibility RemoveButtonVisibility => IsUngrouped ? Visibility.Collapsed : Visibility.Visible;
            public Visibility UngroupedNoticeVisibility => IsUngrouped ? Visibility.Visible : Visibility.Collapsed;
            public Visibility GroupHeaderVisibility => IsUngrouped ? Visibility.Collapsed : Visibility.Visible;
            public Visibility FinancialsVisibility => IsUngrouped ? Visibility.Collapsed : Visibility.Visible;

            /// <summary>What this group contributes to the invoice subtotal: its own Total when
            /// it is a real group (already net of its own VAT/WHT/discount, matching
            /// UpdateHeaderSubtotalFromGroups), or just the raw line sum when ungrouped.</summary>
            public decimal ContributionToInvoice => IsUngrouped ? LineSum : Total;

            /// <summary>
            /// Refreshes this card's displayed figures WITHOUT re-raising <see cref="Changed"/>.
            /// The window calls this from its own recalculation pass, which Changed triggers —
            /// raising Changed again from here would recurse forever.
            /// </summary>
            public void RefreshSubtotal() => RaiseComputed();

            private void RaiseComputed()
            {
                Raise(nameof(LineSum));
                Raise(nameof(LineSumDisplay));
                Raise(nameof(Subtotal));
                Raise(nameof(SubtotalDisplay));
                Raise(nameof(SubtotalHint));
                Raise(nameof(VatDisplay));
                Raise(nameof(WhtDisplay));
                Raise(nameof(DiscountDisplay));
                Raise(nameof(TotalDisplay));
            }

            /// <summary>An edit on this card: refresh it, then let the window re-total.</summary>
            private void Recalc()
            {
                RaiseComputed();
                Changed?.Invoke();
            }

            private void Raise([CallerMemberName] string name = null) =>
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }

        /// <summary>One invoice line — becomes one new dbo.Item plus one dbo.SetItem.</summary>
        public sealed class BuilderLine : INotifyPropertyChanged
        {
            public event PropertyChangedEventHandler PropertyChanged;
            /// <summary>Raised when a value that feeds Amount changes.</summary>
            public event Action Changed;

            /// <summary>
            /// Raised when changing Category discarded a model the user had already picked.
            /// Arguments: the line, the category it was picked under, and the discarded model.
            /// The dialog turns this into a visible warning — silently dropping a chosen value
            /// would look like the app losing the edit.
            /// </summary>
            public event Action<BuilderLine, string, string> ModelCleared;

            /// <summary>Raised the moment this row's own "Existing?" checkbox is ticked on —
            /// the dialog responds by opening SoftwareItemPickerDialog for just this row, either
            /// populating it from the pick or, on cancel/no pick, unchecking the box again.</summary>
            public event Action<BuilderLine> ExistingItemToggleRequested;

            /// <summary>True for an actual model choice, as opposed to "(none)" or the sentinel.</summary>
            private static bool IsRealModelPick(string model) =>
                !string.IsNullOrWhiteSpace(model) &&
                !string.Equals(model, NoneOption, StringComparison.Ordinal) &&
                !string.Equals(model, AddNewOption, StringComparison.Ordinal);

            public BuilderLine()
            {
                Quantity = 1;
                ItemType = "Software/License";
                UnitOfMeasure = "Unit";
            }

            /// <summary>True when this line references an already-existing dbo.Item (picked
            /// via SoftwareItemPickerDialog, one row at a time — checking THIS row's own
            /// "Existing?" box is what triggers the picker for it) instead of creating a new
            /// one. The new-item-only fields (Description, Category, Item Type, Serial #s,
            /// Model #, Model, License #, Warranty, Remarks, Date Purchased) become read-only
            /// echoes of the picked item — see InvoiceBuilderDialog.LinesGrid_BeginningEdit —
            /// and the row itself is highlighted orange (DataGrid.RowStyle) so which lines are
            /// which is obvious without checking any one cell.</summary>
            private bool _isExistingItem;
            public bool IsExistingItem
            {
                get => _isExistingItem;
                set
                {
                    if (_isExistingItem == value) return;
                    _isExistingItem = value;
                    if (!value)
                    {
                        // Unchecked: revert to a blank, fully-editable new-item row rather than
                        // leaving the picked item's values behind under a different meaning.
                        ExistingItemId = null;
                        ItemName = null;
                        Description = null;
                        Category = null;
                        ModelNumber = null;
                    }
                    Raise();
                    if (value) ExistingItemToggleRequested?.Invoke(this);
                }
            }

            /// <summary>FK to the picked dbo.Item — set only via the checkbox's picker flow.</summary>
            public int? ExistingItemId { get; set; }

            /// <summary>Populates a brand-new line as an existing-item row directly, without
            /// going through IsExistingItem's own setter — used for the extra rows a multi-item
            /// picker session auto-adds (see InvoiceBuilderDialog.OpenExistingItemPickerForLine),
            /// where the item's data is already in hand and re-opening the picker for each one
            /// would be wrong.</summary>
            public void SetExistingItemDirect(int itemId, string name, string description, string category, string itemType, string modelNumber)
            {
                ExistingItemId = itemId;
                ItemName = name;
                Description = description;
                Category = category;
                ItemType = itemType;
                ModelNumber = modelNumber;
                _isExistingItem = true;
                Raise(nameof(IsExistingItem));
            }

            private string _itemName;
            public string ItemName
            {
                get => _itemName;
                set { _itemName = value; Raise(); Changed?.Invoke(); }
            }

            public string Description { get; set; }

            private string _category;
            public string Category
            {
                get => _category;
                set
                {
                    if (string.Equals(_category, value, StringComparison.Ordinal)) return;

                    string previousCategory = _category;
                    _category = value;

                    // The model catalogue follows the category, so a model picked under the old
                    // category must not survive the switch — an Ink model on a Toner line would
                    // otherwise resolve to nothing (or worse, the wrong family) on save.
                    // Only a real pick counts as lost work worth warning about; "(none)" and the
                    // Add-new sentinel are not values the user would miss.
                    string discarded = IsRealModelPick(_model) ? _model : null;
                    _model = null;

                    Raise();
                    Raise(nameof(Model));
                    Raise(nameof(ModelOptions));
                    Raise(nameof(SupportsModel));
                    Changed?.Invoke();

                    // Raised last, so the warning is not immediately overwritten by the
                    // recalculation that Changed triggers.
                    if (discarded != null)
                        ModelCleared?.Invoke(this, previousCategory, discarded);
                }
            }

            private string _model;
            /// <summary>
            /// The model picked from this line's own catalogue — a dbo.CartridgeModel entry on a
            /// Cartridge line, a dbo.ConsumableModel entry on Ink / Toner / Print Head. Resolved
            /// to the matching FK on save.
            /// </summary>
            public string Model
            {
                get => _model;
                set { _model = value; Raise(); }
            }

            /// <summary>The dropdown this row shows, chosen by its Category.</summary>
            public ObservableCollection<string> ModelOptions => ModelOptionsFor(Category);

            /// <summary>False for categories with no model catalogue, which greys the cell out.</summary>
            public bool SupportsModel =>
                IsCartridgeCategory(Category) || ConsumableFamily(Category) != null;

            public string ItemType { get; set; }
            public string ModelNumber { get; set; }

            /// <summary>
            /// One dbo.Item is created per serial number, exactly like Batch Add Items' Serial
            /// Numbers box: enter one per line there, and Quantity is ignored once any are given.
            /// Empty means "not serialized" — the typed Quantity governs a single non-serialized
            /// item's StockOnHand instead. Set only through EditSerialNumbers, which owns the
            /// parse/trim/dedupe rules, so this list is never touched with raw, unnormalised text.
            /// </summary>
            private List<string> _serialNumbers = new List<string>();
            public IReadOnlyList<string> SerialNumbers => _serialNumbers;

            public void SetSerialNumbers(IEnumerable<string> serials)
            {
                // Uppercased to match Batch Add Items, which stores serials the same way — a
                // serial typed here and one typed there should collide as the same value.
                _serialNumbers = (serials ?? Enumerable.Empty<string>())
                    .Select(s => (s ?? string.Empty).Trim().ToUpperInvariant())
                    .Where(s => s.Length > 0)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();

                Raise(nameof(SerialNumbers));
                Raise(nameof(SerialNumberSummary));
                Raise(nameof(EffectiveQuantity));
                RaiseAmount();
            }

            /// <summary>Placeholder shown on the Serial #s button when no serials are entered yet.</summary>
            public const string NoSerialsPlaceholder = "+ Add serial #s";

            /// <summary>What the Serial #s cell shows — the placeholder, the single serial, or a
            /// count plus a short preview so a long list doesn't blow out the row.</summary>
            public string SerialNumberSummary
            {
                get
                {
                    if (_serialNumbers.Count == 0) return NoSerialsPlaceholder;
                    if (_serialNumbers.Count == 1) return _serialNumbers[0];
                    if (_serialNumbers.Count <= 3) return string.Join(", ", _serialNumbers);
                    return string.Join(", ", _serialNumbers.Take(2)) + $", +{_serialNumbers.Count - 2} more";
                }
            }

            /// <summary>The quantity actually used to build the invoice: the serial count when any
            /// are entered (Quantity is then ignored, same as Batch Add Items), otherwise the
            /// typed Quantity.</summary>
            public int EffectiveQuantity => _serialNumbers.Count > 0 ? _serialNumbers.Count : Math.Max(1, Quantity);

            /// <summary>Greys out and disables the Qty cell once serials are entered, so it reads
            /// as "ignored" rather than as an editable value the user might expect to matter.</summary>
            public bool QuantityEditable => _serialNumbers.Count == 0;
            private string _vendor;
            public string Vendor
            {
                get => _vendor;
                set { _vendor = value; Raise(); }
            }
            public string UnitOfMeasure { get; set; }
            public string LicenseNumber { get; set; }
            public string PartNumber { get; set; }

            /// <summary>Matches Batch Add Items' "Warranty (Years)" field. 0 means no warranty.
            /// WarrantyStartDate is set automatically to the moment the item is created (not to
            /// Date Purchased) when this is greater than zero — the same rule Batch Add Items
            /// uses — and WarrantyEndDate is computed from it by ItemRepository.AddItem.
            /// Kept in sync with WarrantyStartDate/WarrantyEndDate below whenever both are set —
            /// see RecalculateWarrantyYears — but editing this field directly never feeds back
            /// into the dates, exactly like Edit Item and Batch Add Items.</summary>
            private int _warrantyYears;
            public int WarrantyYears
            {
                get => _warrantyYears;
                set { _warrantyYears = value; Raise(); }
            }

            private DateTime? _warrantyStartDate;
            /// <summary>Manual override of the auto rule (Start = item creation time when
            /// WarrantyYears > 0, End = Start + Years) — blank means "use the auto rule", same as
            /// Edit Item's and Batch Add Items' checkbox-gated Warranty Start/End.</summary>
            public DateTime? WarrantyStartDate
            {
                get => _warrantyStartDate;
                set { _warrantyStartDate = value; Raise(); RecalculateWarrantyYears(); }
            }

            private DateTime? _warrantyEndDate;
            public DateTime? WarrantyEndDate
            {
                get => _warrantyEndDate;
                set { _warrantyEndDate = value; Raise(); RecalculateWarrantyYears(); }
            }

            /// <summary>
            /// Keeps Warranty (Yrs) in sync with Start/End whenever both are set — the display
            /// convenience Edit Item's WarrantyDates_ValueChanged and Batch Add Items' equivalent
            /// both provide. Only runs when BOTH dates are present and End is after Start; one
            /// date alone says nothing about a span, so Years is left as whatever it already was.
            /// </summary>
            private void RecalculateWarrantyYears()
            {
                if (!_warrantyStartDate.HasValue || !_warrantyEndDate.HasValue) return;

                var start = _warrantyStartDate.Value;
                var end = _warrantyEndDate.Value;
                if (end <= start) return;

                int years = end.Year - start.Year;
                if (end.Month < start.Month || (end.Month == start.Month && end.Day < start.Day))
                    years--;

                WarrantyYears = Math.Max(0, Math.Min(10, years));
            }

            public string Remarks { get; set; }
            public DateTime? DatePurchased { get; set; }

            /// <summary>When the item itself was actually put into (or taken out of) use — a
            /// different, usually-blank field from the warranty period, and no longer exposed as
            /// its own grid column. Left null here, a line simply inherits the invoice's own
            /// Coverage Start/End (see BuildRows), exactly as before this had columns of its own.</summary>
            public DateTime? LineStartDate { get; set; }
            public DateTime? LineEndDate { get; set; }

            private int _quantity;
            public int Quantity
            {
                get => _quantity;
                set { _quantity = value; Raise(); Raise(nameof(EffectiveQuantity)); RaiseAmount(); }
            }

            private decimal _unitPrice;
            public decimal UnitPrice
            {
                get => _unitPrice;
                set { _unitPrice = value; Raise(); RaiseAmount(); }
            }

            public decimal Amount => UnitPrice * EffectiveQuantity;

            /// <summary>
            /// A spare row the user never touched. Blank rows are skipped on save rather than
            /// reported as errors, so "+ 5 lines" never forces the user to delete what they did
            /// not fill in.
            /// </summary>
            public bool IsBlank =>
                string.IsNullOrWhiteSpace(ItemName) &&
                string.IsNullOrWhiteSpace(Description) &&
                _serialNumbers.Count == 0 &&
                string.IsNullOrWhiteSpace(ModelNumber) &&
                UnitPrice == 0m;

            private void RaiseAmount()
            {
                Raise(nameof(Amount));
                Changed?.Invoke();
            }

            private void Raise([CallerMemberName] string name = null) =>
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}
