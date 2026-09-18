using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Linq;
using System.Windows.Forms;
using ReaLTaiizor.Controls;

namespace Yakult.Inventory.App.Helpers
{
    /// <summary>
    /// USAGE GUIDE: Fixing the Row Auto-Selection and Button Highlight Issues
    /// ===========================================================================
    ///
    /// PROBLEM:
    /// - First row is automatically highlighted (blue) when a page is opened or revisited
    /// - Action buttons appear highlighted even when NO row is selected
    /// - These issues stem from DataGridView's default selection behavior
    ///
    /// SOLUTION (4 steps):
    ///
    /// Step 1: Call SetupInitialPageFocus() at the END of BuildUI()
    /// -----------------------------------------------------------------------
    /// This sets initial focus to a neutral control (search box or grid) instead of buttons.
    /// CRITICAL: Call this AFTER all controls are created and added to the layout.
    ///
    /// Example:
    ///     private void BuildUiWithTemplate()
    ///     {
    ///         // ... create all controls ...
    ///         // ... add controls to layout ...
    ///
    ///         ResumeLayout(true);
    ///
    ///         // LAST THING: Set up initial focus (prevents buttons from being highlighted on load)
    ///         DefaultListPageTemplate.SetupInitialPageFocus(_layout.SearchBox, dgvCategories, _btnEdit, _btnDelete, _btnArchive);
    ///     }
    ///
    /// Step 2: Call DisableDefaultRowHighlight() after setting DataSource
    /// -----------------------------------------------------------------------
    /// This clears the auto-selection and attaches event handlers to maintain clean state.
    ///
    /// Example (basic usage without specific buttons):
    ///     dgvCategories.DataSource = categories;
    ///     DefaultListPageTemplate.DisableDefaultRowHighlight(dgvCategories);
    ///
    /// Example (with specific action buttons for targeted focus clearing):
    ///     dgvCategories.DataSource = categories;
    ///     DefaultListPageTemplate.DisableDefaultRowHighlight(dgvCategories, _btnEdit, _btnDelete, _btnArchive);
    ///
    /// Step 3: Use SyncActionButtonVisualState() in SelectionChanged event
    /// -----------------------------------------------------------------------
    /// This keeps button visual state synchronized with row selection.
    ///
    /// Example:
    ///     dgvCategories.SelectionChanged += (s, e) => {
    ///         // Sync visual state (NO Enabled changes - buttons stay clickable)
    ///         DefaultListPageTemplate.SyncActionButtonVisualState(dgvCategories, _btnEdit, _btnDelete, _btnArchive);
    ///
    ///         // Your existing logic (if you want enable/disable behavior)
    ///         bool hasSelection = dgvCategories.SelectedRows.Count > 0;
    ///         _btnEdit.Enabled = hasSelection;
    ///         _btnDelete.Enabled = hasSelection;
    ///         _btnArchive.Enabled = hasSelection;
    ///     };
    ///
    /// Step 4: DO NOT set Enabled = false in button initialization
    /// -----------------------------------------------------------------------
    /// WRONG (old approach):
    ///     _btnEdit = new HopeButton {
    ///         Text = "Edit",
    ///         Enabled = false  // ❌ DON'T DO THIS
    ///     };
    ///
    /// RIGHT (new approach):
    ///     _btnEdit = new HopeButton {
    ///         Text = "Edit"
    ///         // No Enabled = false - button starts enabled
    ///     };
    ///
    /// COMPLETE EXAMPLE (ViewCategoryPage pattern):
    /// -----------------------------------------------------------------------
    ///     private void BuildUiWithTemplate()
    ///     {
    ///         SuspendLayout();
    ///         Controls.Clear();
    ///
    ///         // Create layout
    ///         _layout = DefaultListPageTemplate.Create("View Categories", "Search...", OnSearchChanged, OnRefresh);
    ///
    ///         // Create buttons WITHOUT Enabled = false
    ///         _btnEdit = new HopeButton { Text = "Edit" };
    ///         _btnDelete = new HopeButton { Text = "Delete" };
    ///         _btnArchive = new HopeButton { Text = "Archive" };
    ///
    ///         // Create DataGridView
    ///         dgvCategories = new DataGridView();
    ///
    ///         // Attach SelectionChanged with visual state sync
    ///         dgvCategories.SelectionChanged += (s, e) => {
    ///             DefaultListPageTemplate.SyncActionButtonVisualState(dgvCategories, _btnEdit, _btnDelete, _btnArchive);
    ///             // Optional: your enable/disable logic
    ///             bool hasSelection = dgvCategories.SelectedRows.Count > 0;
    ///             _btnEdit.Enabled = hasSelection;
    ///             _btnDelete.Enabled = hasSelection;
    ///         };
    ///
    ///         // ... add controls to layout ...
    ///
    ///         ResumeLayout(true);
    ///
    ///         // CRITICAL: Set up initial focus at the END (prevents buttons from being highlighted on load)
    ///         DefaultListPageTemplate.SetupInitialPageFocus(_layout.SearchBox, dgvCategories, _btnEdit, _btnDelete, _btnArchive);
    ///     }
    ///
    ///     private void LoadCategories()
    ///     {
    ///         // Load data
    ///         var categories = GetCategories();
    ///
    ///         // Bind to grid
    ///         dgvCategories.DataSource = categories;
    ///
    ///         // Clear auto-selection and attach handlers
    ///         DefaultListPageTemplate.DisableDefaultRowHighlight(dgvCategories, _btnEdit, _btnDelete, _btnArchive);
    ///     }
    ///
    /// EXPECTED BEHAVIOR AFTER FIX:
    /// -----------------------------------------------------------------------
    /// ✓ When page opens: NO row selected, NO button highlighted
    /// ✓ User selects row: Row highlighted, buttons may appear active
    /// ✓ User clears selection: NO row selected, NO button highlighted
    /// ✓ Buttons always remain enabled and clickable
    /// ✓ Visual state always matches selection state
    /// </summary>
    /// <summary>
    /// Represents a sort option in the Sort By dropdown.
    /// Maps display text to column key and sort direction.
    /// </summary>
    internal class SortOption
    {
        /// <summary>
        /// Display text shown in dropdown (e.g., "Name (ASC)", "Active (Checked)")
        /// </summary>
        public string DisplayText { get; set; }

        /// <summary>
        /// Column DataPropertyName or Name to sort by
        /// </summary>
        public string ColumnKey { get; set; }

        /// <summary>
        /// Sort direction (Ascending or Descending)
        /// </summary>
        public System.Windows.Forms.SortOrder SortDirection { get; set; }

        /// <summary>
        /// For boolean/checkbox columns: the target boolean value (true for "Checked", false for "Unchecked")
        /// Null for non-boolean columns
        /// </summary>
        public bool? BooleanValue { get; set; }

        public override string ToString() => DisplayText;
    }

    internal sealed class DefaultListPageLayout
    {
        internal System.Windows.Forms.Panel HeaderPanel { get; set; }
        internal System.Windows.Forms.Panel ButtonBarPanel { get; set; }
        internal System.Windows.Forms.Panel SummaryPanel { get; set; }
        internal System.Windows.Forms.Panel PaginationPanel { get; set; }
        internal ReaLTaiizor.Controls.Panel BodyPanel { get; set; }
        internal MaterialCard GridCard { get; set; }

        internal Label TitleLabel { get; set; }
        internal HopeTextBox SearchBox { get; set; }
        internal ComboBox FilterByComboBox { get; set; }
        internal ComboBox SortByComboBox { get; set; }
        internal Label SortByLabel { get; set; }
        internal HopeButton RefreshButton { get; set; }

        internal FlowLayoutPanel ButtonLeftFlow { get; set; }
        internal FlowLayoutPanel SummaryFlow { get; set; }
    }

    internal static class DefaultListPageTemplate
    {
        private sealed class SortByDropdownState
        {
            public bool IsUpdating;
            public EventHandler Handler;
            public Action<string, System.Windows.Forms.SortOrder> OnSortChanged;
        }

        internal static DefaultListPageLayout Create(string title, string searchHint, EventHandler onSearchChanged, Action onRefresh)
        {
            var layout = new DefaultListPageLayout();

            layout.HeaderPanel = new System.Windows.Forms.Panel
            {
                Dock = DockStyle.Top,
                Height = UiTheme.Sizes.HeaderHeight,
                BackColor = UiTheme.Colors.HeaderBack,
                Padding = UiTheme.Padding.Header
            };

            layout.TitleLabel = new Label
            {
                AutoSize = true,
                Text = title,
                Font = UiTheme.Fonts.Title,
                ForeColor = UiTheme.Colors.TextDark,
                Location = new Point(15, 10)
            };

            layout.SearchBox = new HopeTextBox
            {
                BackColor = Color.White,
                BaseColor = Color.White,
                BorderColorA = Color.FromArgb(220, 220, 220),
                BorderColorB = Color.FromArgb(220, 220, 220),
                ForeColor = Color.FromArgb(60, 60, 60),
                Font = UiTheme.Fonts.Search,
                Hint = searchHint,
                Location = new Point(18, 64),
                Size = new Size(360, 32),
                MaxLength = 32767,
                Multiline = false,
                UseSystemPasswordChar = false
            };

            if (onSearchChanged != null)
                layout.SearchBox.TextChanged += onSearchChanged;

            // Filter By ComboBox
            var lblFilterBy = new Label
            {
                Text = "Filter By:",
                AutoSize = false,
                Width = 62,
                Font = new Font("Segoe UI", 9F),
                ForeColor = UiTheme.Colors.TextMuted,
                Location = new Point(400, 67) // Align with SearchBox
            };

            layout.FilterByComboBox = new ComboBox
            {
                Location = new Point(468, 64),
                Width = 160,
                Font = new Font("Segoe UI", 9F),
                DropDownStyle = ComboBoxStyle.DropDownList,
                BackColor = Color.White,
                ForeColor = Color.Black,
                FlatStyle = FlatStyle.Flat
            };
            layout.FilterByComboBox.Items.AddRange(new object[] { "Default", "Most Recently Added", "Oldest Added" });
            layout.FilterByComboBox.SelectedIndex = 0; // Default

            // Sort By ComboBox
            layout.SortByLabel = new Label
            {
                Text = "Sort By:",
                AutoSize = false,
                Width = 58,
                Font = new Font("Segoe UI", 9F),
                ForeColor = UiTheme.Colors.TextMuted,
                Location = new Point(640, 67) // Positioned after Filter By
            };

            layout.SortByComboBox = new ComboBox
            {
                Location = new Point(704, 64),
                Width = 200,
                Font = new Font("Segoe UI", 9F),
                DropDownStyle = ComboBoxStyle.DropDownList,
                BackColor = Color.White,
                ForeColor = Color.Black,
                FlatStyle = FlatStyle.Flat
            };
            // Note: Sort By options will be populated dynamically via SetupSortByDropdown()

            layout.HeaderPanel.Controls.Add(layout.TitleLabel);
            layout.HeaderPanel.Controls.Add(layout.SearchBox);
            layout.HeaderPanel.Controls.Add(lblFilterBy);
            layout.HeaderPanel.Controls.Add(layout.FilterByComboBox);
            layout.HeaderPanel.Controls.Add(layout.SortByLabel);
            layout.HeaderPanel.Controls.Add(layout.SortByComboBox);

            layout.ButtonBarPanel = new System.Windows.Forms.Panel
            {
                Dock = DockStyle.Top,
                Height = UiTheme.Sizes.ButtonBarHeight,
                BackColor = UiTheme.Colors.HeaderBack,
                Padding = UiTheme.Padding.ButtonBar
            };

            var buttonCard = new MaterialCard
            {
                Dock = DockStyle.Fill,
                BackColor = UiTheme.Colors.CardBack,
                Padding = UiTheme.Padding.CardInner
            };

            var buttonTable = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 1,
                Margin = new Padding(0),
                Padding = new Padding(0),
                BackColor = Color.Transparent
            };
            buttonTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            buttonTable.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

            layout.ButtonLeftFlow = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                AutoScroll = true,
                BackColor = Color.Transparent,
                Padding = new Padding(0),
                Margin = new Padding(0)
            };

            var rightFlow = new FlowLayoutPanel
            {
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                BackColor = Color.Transparent,
                Padding = new Padding(0),
                Margin = new Padding(0)
            };

            layout.RefreshButton = UiFactory.CreateCircularRefreshButton(onRefresh);
            rightFlow.Controls.Add(layout.RefreshButton);

            buttonTable.Controls.Add(layout.ButtonLeftFlow, 0, 0);
            buttonTable.Controls.Add(rightFlow, 1, 0);
            buttonCard.Controls.Add(buttonTable);
            layout.ButtonBarPanel.Controls.Add(buttonCard);

            layout.SummaryPanel = new System.Windows.Forms.Panel
            {
                Dock = DockStyle.Top,
                Height = UiTheme.Sizes.SummaryRowHeight,
                Padding = UiTheme.Padding.SummaryPanel,
                BackColor = UiTheme.Colors.CardBack
            };

            layout.SummaryFlow = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                AutoScroll = true,
                BackColor = Color.Transparent,
                Padding = new Padding(0),
                Margin = new Padding(0)
            };
            layout.SummaryPanel.Controls.Add(layout.SummaryFlow);

            layout.PaginationPanel = new System.Windows.Forms.Panel
            {
                Dock = DockStyle.Bottom,
                Height = 40,
                BackColor = UiTheme.Colors.CardBack,
                Padding = UiTheme.Padding.Pagination
            };

            layout.BodyPanel = new ReaLTaiizor.Controls.Panel
            {
                Dock = DockStyle.Fill,
                BackColor = UiTheme.Colors.CardBack,
                Padding = UiTheme.Padding.BodyPanel
            };

            layout.GridCard = new MaterialCard
            {
                Dock = DockStyle.Fill,
                BackColor = UiTheme.Colors.CardBack,
                Padding = UiTheme.Padding.GridCard
            };
            layout.BodyPanel.Controls.Add(layout.GridCard);

            return layout;
        }

        internal static void AttachVendorsPillCellPainting(DataGridView grid, Func<DataGridViewColumn, bool> isCenteredDataColumn)
        {
            if (grid == null)
                return;

            // Configure grid borders globally - White separators between columns and rows
            grid.CellBorderStyle = DataGridViewCellBorderStyle.Single;
            grid.GridColor = Color.White;
            grid.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.Single;
            grid.RowHeadersBorderStyle = DataGridViewHeaderBorderStyle.Single;

            // Cache visible column boundaries — recomputed only when columns are added, removed, or hidden,
            // not on every single cell paint (which was causing scroll flicker).
            int cachedFirstVisible = -1;
            int cachedLastVisible = -1;

            void RebuildVisibleColumnCache()
            {
                cachedFirstVisible = -1;
                cachedLastVisible = -1;
                for (int i = 0; i < grid.Columns.Count; i++)
                {
                    if (!grid.Columns[i].Visible) continue;
                    if (cachedFirstVisible < 0) cachedFirstVisible = i;
                    cachedLastVisible = i;
                }
            }

            grid.ColumnAdded += (s, e2) => RebuildVisibleColumnCache();
            grid.ColumnRemoved += (s, e2) => RebuildVisibleColumnCache();
            grid.ColumnStateChanged += (s, e2) =>
            {
                if (e2.StateChanged == DataGridViewElementStates.Visible)
                    RebuildVisibleColumnCache();
            };
            RebuildVisibleColumnCache();

            // Cache reflection PropertyInfo per DTO type — reflection is slow; don't call GetProperty on every row paint.
            Type cachedDataItemType = null;
            System.Reflection.PropertyInfo cachedDbActiveProp = null;
            System.Reflection.PropertyInfo cachedActiveProp = null;

            grid.CellPainting += (s, e) =>
            {
                var g = s as DataGridView;
                if (g == null)
                    return;

                if (e.ColumnIndex < 0)
                    return;

                var column = g.Columns[e.ColumnIndex];
                if (column == null)
                    return;

                if (!column.Visible)
                    return;

                e.Graphics.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

                int firstVisibleIndex = cachedFirstVisible;
                int lastVisibleIndex = cachedLastVisible;

                if (firstVisibleIndex < 0 || lastVisibleIndex < 0)
                    return;

                if (e.RowIndex < 0)
                {
                    e.Handled = true;

                    // Fill background with white for border separation
                    e.Graphics.FillRectangle(Brushes.White, e.CellBounds);

                    // Fill header cell with header color (leaving 1px border)
                    Rectangle r = new Rectangle(
                        e.CellBounds.X,
                        e.CellBounds.Y,
                        e.CellBounds.Width - 1,  // Leave 1px for right border
                        e.CellBounds.Height - 1  // Leave 1px for bottom border
                    );

                    using (var brush = new SolidBrush(UiTheme.Colors.GridHeaderBack))
                    {
                        e.Graphics.FillRectangle(brush, r);
                    }

                    // Draw white vertical border on the right edge
                    using (var borderPen = new Pen(Color.White, 1))
                    {
                        e.Graphics.DrawLine(borderPen,
                            e.CellBounds.Right - 1, e.CellBounds.Top,
                            e.CellBounds.Right - 1, e.CellBounds.Bottom - 1);
                    }

                    // Draw white horizontal border on the bottom edge
                    using (var borderPen = new Pen(Color.White, 1))
                    {
                        e.Graphics.DrawLine(borderPen,
                            e.CellBounds.Left, e.CellBounds.Bottom - 1,
                            e.CellBounds.Right, e.CellBounds.Bottom - 1);
                    }

                    // Draw header text
                    using (var textBrush = new SolidBrush(Color.White))
                    using (var format = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                    {
                        // Adjust text rectangle to make room for sort glyph
                        var textRect = e.CellBounds;
                        
                        // Draw Sort Glyph on ALL sortable columns (not just active sort)
                        bool isSortable = column.SortMode == DataGridViewColumnSortMode.Programmatic;
                        
                        if (isSortable)
                        {
                            // Reduce text width to avoid overlap
                            textRect.Width -= 16;
                            
                            var glyphX = e.CellBounds.Right - 18;
                            var glyphY = e.CellBounds.Y + (e.CellBounds.Height - 8) / 2;
                            
                            // Determine arrow color - bright white for active sort, dimmed for inactive
                            Color arrowColor = column.HeaderCell.SortGlyphDirection != SortOrder.None 
                                ? Color.White 
                                : Color.FromArgb(180, 255, 255, 255); // Semi-transparent white
                            
                            // Draw arrow
                            using (var pen = new Pen(arrowColor, 2))
                            {
                                if (column.HeaderCell.SortGlyphDirection == SortOrder.Ascending)
                                {
                                    // Up arrow (active sort)
                                    e.Graphics.DrawLine(pen, glyphX, glyphY + 6, glyphX + 5, glyphY);
                                    e.Graphics.DrawLine(pen, glyphX + 5, glyphY, glyphX + 10, glyphY + 6);
                                }
                                else if (column.HeaderCell.SortGlyphDirection == SortOrder.Descending)
                                {
                                    // Down arrow (active sort)
                                    e.Graphics.DrawLine(pen, glyphX, glyphY, glyphX + 5, glyphY + 6);
                                    e.Graphics.DrawLine(pen, glyphX + 5, glyphY + 6, glyphX + 10, glyphY);
                                }
                                else
                                {
                                    // Default: show both arrows (inactive state)
                                    // Up arrow
                                    e.Graphics.DrawLine(pen, glyphX + 2, glyphY + 2, glyphX + 5, glyphY - 1);
                                    e.Graphics.DrawLine(pen, glyphX + 5, glyphY - 1, glyphX + 8, glyphY + 2);
                                    // Down arrow
                                    e.Graphics.DrawLine(pen, glyphX + 2, glyphY + 4, glyphX + 5, glyphY + 7);
                                    e.Graphics.DrawLine(pen, glyphX + 5, glyphY + 7, glyphX + 8, glyphY + 4);
                                }
                            }
                        }

                        e.Graphics.DrawString(column.HeaderText, e.CellStyle.Font ?? g.Font, textBrush, textRect, format);
                    }

                    return;
                }

                bool isSelected = g.Rows[e.RowIndex].Selected;
                bool isFirstColumn = e.ColumnIndex == firstVisibleIndex;
                bool isLastColumn = e.ColumnIndex == lastVisibleIndex;

                bool center = isCenteredDataColumn != null && isCenteredDataColumn(column);

                // Check if the row represents an inactive item or item in transaction
                bool isInactive = false;
                bool hasTransaction = false;
                if (g.Rows[e.RowIndex].DataBoundItem != null)
                {
                    var dataItem = g.Rows[e.RowIndex].DataBoundItem;
                    var dataType = dataItem.GetType();

                    // Rebuild PropertyInfo cache only when the DTO type changes (not on every row paint).
                    // GetType().GetProperty() is slow; caching it eliminates the cost during scroll.
                    if (dataType != cachedDataItemType)
                    {
                        cachedDataItemType = dataType;
                        cachedDbActiveProp = dataType.GetProperty("DbActive");
                        cachedActiveProp = dataType.GetProperty("Active") ?? dataType.GetProperty("IsActive");
                    }

                    if (cachedDbActiveProp != null && cachedActiveProp != null &&
                        cachedDbActiveProp.PropertyType == typeof(bool) && cachedActiveProp.PropertyType == typeof(bool))
                    {
                        // ItemDto case: has both DbActive and Active (EffectiveActive)
                        bool dbActive = (bool)cachedDbActiveProp.GetValue(dataItem);
                        bool effectiveActive = (bool)cachedActiveProp.GetValue(dataItem);

                        if (!dbActive)
                        {
                            // Manually set to inactive
                            isInactive = true;
                        }
                        else if (dbActive && !effectiveActive)
                        {
                            // Active in DB but inactive due to transaction
                            hasTransaction = true;
                        }
                    }
                    else if (cachedActiveProp != null && cachedActiveProp.PropertyType == typeof(bool))
                    {
                        // Standard case: only has Active/IsActive property
                        isInactive = !(bool)cachedActiveProp.GetValue(dataItem);
                    }
                }

                Color pillColor;
                if (hasTransaction)
                {
                    // Use light purple shades for items in transaction
                    pillColor = (e.RowIndex % 2 == 0)
                        ? Color.FromArgb(230, 220, 240)  // Light purple for even rows
                        : Color.FromArgb(220, 210, 235); // Slightly darker purple for odd rows
                }
                else if (isInactive)
                {
                    // Use gray shades for manually inactive items
                    pillColor = (e.RowIndex % 2 == 0)
                        ? Color.FromArgb(240, 240, 240)  // Light gray for even rows
                        : Color.FromArgb(230, 230, 230); // Slightly darker gray for odd rows
                }
                else
                {
                    // Use normal blue/green shades for active items
                    pillColor = (e.RowIndex % 2 == 0)
                        ? Color.FromArgb(227, 242, 253)  // Light blue
                        : Color.FromArgb(232, 245, 233); // Light green
                }

                if (isSelected)
                    pillColor = UiTheme.Colors.GridSelectionBack;

                // Fill background with white for border separation
                e.Graphics.FillRectangle(Brushes.White, e.CellBounds);

                int topPadding = 6;
                int bottomPadding = 6;

                // Fill cell with color (leaving space for borders)
                Rectangle cellBounds = new Rectangle(
                    e.CellBounds.X,
                    e.CellBounds.Y + topPadding,
                    e.CellBounds.Width - 1,  // Leave 1px for right border
                    e.CellBounds.Height - topPadding - bottomPadding - 1  // Leave 1px for bottom border
                );

                using (var brush = new SolidBrush(pillColor))
                {
                    e.Graphics.FillRectangle(brush, cellBounds);
                }

                Color textColor = isSelected
                    ? Color.White
                    : (e.CellStyle.ForeColor.IsEmpty ? UiTheme.Colors.TextDark : e.CellStyle.ForeColor);

                if (column is DataGridViewCheckBoxColumn)
                {
                    // Paint checkbox content within the styled cell bounds
                    var checkBoxSize = 18;
                    var checkBoxX = e.CellBounds.X + (e.CellBounds.Width - checkBoxSize) / 2;
                    var checkBoxY = e.CellBounds.Y + (e.CellBounds.Height - checkBoxSize) / 2;
                    var checkBoxRect = new Rectangle(checkBoxX, checkBoxY, checkBoxSize, checkBoxSize);

                    System.Windows.Forms.VisualStyles.CheckBoxState state = System.Windows.Forms.VisualStyles.CheckBoxState.UncheckedNormal;

                    bool isChecked = false;
                    if (e.Value != null && e.Value is bool)
                    {
                        isChecked = (bool)e.Value;
                    }

                    if (isChecked)
                        state = System.Windows.Forms.VisualStyles.CheckBoxState.CheckedNormal;
                    else
                        state = System.Windows.Forms.VisualStyles.CheckBoxState.UncheckedNormal;

                    CheckBoxRenderer.DrawCheckBox(e.Graphics, checkBoxRect.Location, state);
                }
                else if (e.Value != null)
                {
                    using (var textBrush = new SolidBrush(textColor))
                    {
                        var format = new StringFormat
                        {
                            Alignment = center ? StringAlignment.Center : StringAlignment.Near,
                            LineAlignment = StringAlignment.Center,
                            Trimming = StringTrimming.EllipsisCharacter
                        };

                        var textRect = new Rectangle(
                            e.CellBounds.X + (center ? 0 : 10),
                            e.CellBounds.Y + topPadding,
                            e.CellBounds.Width - (center ? 0 : 20),
                            e.CellBounds.Height - topPadding - bottomPadding
                        );

                        e.Graphics.DrawString(e.FormattedValue?.ToString() ?? string.Empty,
                            e.CellStyle.Font ?? g.Font,
                            textBrush,
                            textRect,
                            format);
                    }
                }

                // Draw white vertical border on the right edge of the cell
                using (var borderPen = new Pen(Color.White, 1))
                {
                    e.Graphics.DrawLine(borderPen,
                        e.CellBounds.Right - 1, e.CellBounds.Top,
                        e.CellBounds.Right - 1, e.CellBounds.Bottom - 1);
                }

                // Draw white horizontal border on the bottom edge of the cell
                using (var borderPen = new Pen(Color.White, 1))
                {
                    e.Graphics.DrawLine(borderPen,
                        e.CellBounds.Left, e.CellBounds.Bottom - 1,
                        e.CellBounds.Right, e.CellBounds.Bottom - 1);
                }

                e.Handled = true;
            };
        }

        // --- SORTING HELPERS ---

        private static readonly string[] CreatedDateCandidates =
        {
            "DateCreated",
            "CreatedAt",
            "CreatedDate",
            "DatePosted",
            "AuditDate",
            "Timestamp"
        };

        internal static DataGridViewColumn ResolveCreatedDateColumn(DataGridView grid)
        {
            if (grid == null) return null;
            return grid.Columns
                .Cast<DataGridViewColumn>()
                .FirstOrDefault(c =>
                    CreatedDateCandidates.Any(n =>
                        string.Equals(c.DataPropertyName, n, StringComparison.OrdinalIgnoreCase)));
        }

        internal static string ResolveDefaultSortColumnKey(DataGridView grid)
        {
            if (grid == null || grid.Columns.Count == 0)
                return null;

            var sortableColumns = grid.Columns
                .Cast<DataGridViewColumn>()
                .OrderBy(c => c.DisplayIndex)
                .Where(c => c != null)
                .Where(c => c.Visible)
                .Where(c => c.SortMode != DataGridViewColumnSortMode.NotSortable)
                .Where(c => !(c is DataGridViewButtonColumn) && !(c is DataGridViewImageColumn))
                // Exclude selector checkbox columns (typically have no DataPropertyName)
                .Where(c => !(c is DataGridViewCheckBoxColumn) || !string.IsNullOrEmpty(c.DataPropertyName))
                .ToList();

            string GetKey(DataGridViewColumn c) => !string.IsNullOrEmpty(c.DataPropertyName) ? c.DataPropertyName : c.Name;

            // Prefer "ID" header or *Id property names when present.
            var idCol = sortableColumns.FirstOrDefault(c =>
            {
                var key = GetKey(c) ?? string.Empty;
                var header = c.HeaderText ?? string.Empty;

                if (string.Equals(header.Trim(), "ID", StringComparison.OrdinalIgnoreCase))
                    return true;

                return key.EndsWith("Id", StringComparison.OrdinalIgnoreCase) ||
                       key.EndsWith("ID", StringComparison.OrdinalIgnoreCase) ||
                       string.Equals(key, "Id", StringComparison.OrdinalIgnoreCase) ||
                       string.Equals(key, "ID", StringComparison.OrdinalIgnoreCase);
            });

            return idCol != null ? GetKey(idCol) : GetKey(sortableColumns.FirstOrDefault());
        }

        internal static void EnableSortingGlyphs(DataGridView grid)
        {
            if (grid == null) return;

            foreach (DataGridViewColumn col in grid.Columns)
            {
                col.SortMode =
                    col is DataGridViewCheckBoxColumn ||
                    col is DataGridViewButtonColumn ||
                    col is DataGridViewImageColumn
                        ? DataGridViewColumnSortMode.NotSortable
                        : DataGridViewColumnSortMode.Programmatic;
            }

            grid.EnableHeadersVisualStyles = false;
        }

        internal static void AddButtons(FlowLayoutPanel leftFlow, IEnumerable<HopeButton> buttons)
        {
            if (leftFlow == null || buttons == null)
                return;

            foreach (var b in buttons.Where(x => x != null))
                leftFlow.Controls.Add(b);
        }

        internal static void AddSummaryCards(FlowLayoutPanel summaryFlow, IEnumerable<MaterialCard> cards)
        {
            if (summaryFlow == null || cards == null)
                return;

            foreach (var c in cards.Where(x => x != null))
                summaryFlow.Controls.Add(c);
        }

        /// <summary>
        /// Adds a Select All checkbox to the header of a checkbox column in a DataGridView.
        /// The checkbox will automatically check/uncheck all selectable rows and update its state based on row selections.
        /// </summary>
        /// <param name="grid">The DataGridView to add the Select All checkbox to</param>
        /// <param name="checkboxColumnName">The name of the checkbox column (default: "colSelect")</param>
        /// <param name="excludeNewRow">Whether to exclude the new row placeholder from selection (default: true)</param>
        /// <returns>The created CheckBox control for further customization if needed</returns>
        internal static System.Windows.Forms.CheckBox AddSelectAllCheckBox(DataGridView grid, string checkboxColumnName = "colSelect", bool excludeNewRow = true, Func<(int total, int selected)> stateProvider = null)
        {
            if (grid == null || !grid.Columns.Contains(checkboxColumnName))
                return null;

            // Ensure the selection column is consistently styled/fixed, even if the caller invoked
            // StyleSelectionCheckBoxColumn before adding the column.
            StyleSelectionCheckBoxColumn(grid, checkboxColumnName);

            var selectAllCheckBox = new System.Windows.Forms.CheckBox
            {
                Size = new Size(15, 15),
                BackColor = Color.Transparent,
                ThreeState = true,
                AutoCheck = false,  // Prevent automatic toggling - we manage state manually in MouseDown
                CheckState = CheckState.Unchecked,
                Tag = new SelectAllCheckBoxTag { ColumnName = checkboxColumnName, ExcludeNewRow = excludeNewRow, UserClicked = false }
            };

            bool isUpdating = false;
            bool toggledByCellClick = false;
            bool userClickedCheckbox = false;

            // Handler for direct clicks on Select All checkbox - force two-state toggle behavior
            selectAllCheckBox.MouseDown += (s, e) =>
            {
                if (isUpdating) return;
                userClickedCheckbox = true;

                if (selectAllCheckBox.Tag is SelectAllCheckBoxTag tag)
                    tag.UserClicked = true;

                // When user clicks directly, force toggle between Checked and Unchecked only
                // Skip Indeterminate state for user clicks
                if (selectAllCheckBox.CheckState == CheckState.Indeterminate)
                {
                    // If currently indeterminate, clicking should check all
                    selectAllCheckBox.CheckState = CheckState.Checked;
                }
                else if (selectAllCheckBox.CheckState == CheckState.Checked)
                {
                    // If currently checked, clicking should uncheck all
                    selectAllCheckBox.CheckState = CheckState.Unchecked;
                }
                else
                {
                    // Unchecked -> Checked
                    selectAllCheckBox.CheckState = CheckState.Checked;
                }
            };

            // Handler for Select All checkbox state change
            selectAllCheckBox.CheckStateChanged += (s, e) =>
            {
                if (isUpdating) return;

                // Set flag to prevent UpdateSelectAllState from running immediately after user click
                if (userClickedCheckbox)
                {
                    isUpdating = true;
                    userClickedCheckbox = false;
                }

                bool newCheckState = selectAllCheckBox.CheckState == CheckState.Checked;

                // End any pending edit on the current cell so the edit buffer
                // does not shadow the value we are about to set programmatically.
                try { grid.EndEdit(); } catch { }

                for (int i = 0; i < grid.Rows.Count; i++)
                {
                    var row = grid.Rows[i];
                    if (excludeNewRow && row.IsNewRow) continue;

                    if (grid.Columns.Contains(checkboxColumnName))
                    {
                        row.Cells[checkboxColumnName].Value = newCheckState;
                    }
                }

                grid.Refresh();
                
                // Reset isUpdating flag after all row updates are complete
                if (isUpdating)
                {
                    isUpdating = false;
                }

                // Defer clearing the flag until after this event dispatch finishes, so any
                // other CheckStateChanged subscriber added by the host page (e.g. one that
                // bulk-selects across all filtered/paginated rows, not just the ones bound
                // to this grid) still sees UserClicked == true when it runs right after this
                // handler in the same multicast delegate invocation.
                if (selectAllCheckBox.Tag is SelectAllCheckBoxTag tag)
                {
                    if (grid.IsHandleCreated)
                        grid.BeginInvoke(new Action(() => tag.UserClicked = false));
                    else
                        tag.UserClicked = false;
                }
            };

            // Handler for individual row checkbox clicks - update Select All state
            void UpdateSelectAllState()
            {
                if (selectAllCheckBox == null) return;

                isUpdating = true;

                try
                {
                    int totalRows;
                    int checkedRows;

                    // When the caller supplies a stateProvider, the header checkbox reflects
                    // selection across the caller's full dataset (e.g. all pages of a paginated
                    // grid) rather than just the rows currently rendered in this page's DataGridView.
                    if (stateProvider != null)
                    {
                        var state = stateProvider();
                        totalRows = state.total;
                        checkedRows = state.selected;
                    }
                    else
                    {
                        totalRows = 0;
                        checkedRows = 0;

                        for (int i = 0; i < grid.Rows.Count; i++)
                        {
                            var row = grid.Rows[i];
                            if (excludeNewRow && row.IsNewRow) continue;

                            totalRows++;

                            var checkValue = row.Cells[checkboxColumnName].Value;
                            bool isChecked = checkValue != null && (bool)checkValue == true;

                            if (isChecked)
                                checkedRows++;
                        }
                    }

                    if (totalRows == 0 || checkedRows == 0)
                    {
                        selectAllCheckBox.CheckState = CheckState.Unchecked;
                    }
                    else if (checkedRows == totalRows)
                    {
                        selectAllCheckBox.CheckState = CheckState.Checked;
                    }
                    else
                    {
                        selectAllCheckBox.CheckState = CheckState.Indeterminate;
                    }
                }
                finally
                {
                    isUpdating = false;
                }
            }

            // Enable single-click checkbox toggling (only for the selection column, not other checkbox columns)
            grid.CellClick += (s, e) =>
            {
                if (e.RowIndex >= 0 && e.ColumnIndex >= 0 && grid.Columns[e.ColumnIndex].Name == checkboxColumnName)
                {
                    if (!grid.Rows[e.RowIndex].IsNewRow)
                    {
                        var cell = grid.Rows[e.RowIndex].Cells[e.ColumnIndex];
                        var currentValue = cell.Value;

                        // Toggle the checkbox value
                        if (currentValue == null || (bool)currentValue == false)
                        {
                            cell.Value = true;
                        }
                        else
                        {
                            cell.Value = false;
                        }

                        toggledByCellClick = true;
                        grid.InvalidateCell(e.ColumnIndex, e.RowIndex);

                        if (grid.IsHandleCreated)
                        {
                            grid.BeginInvoke(new Action(() => UpdateSelectAllState()));
                        }
                        else
                        {
                            UpdateSelectAllState();
                        }
                    }
                }
            };

            // Attach event handler for cell content clicks (for keyboard navigation)
            grid.CellContentClick += (s, e) =>
            {
                if (e.RowIndex >= 0 && e.ColumnIndex >= 0 && grid.Columns[e.ColumnIndex].Name == checkboxColumnName)
                {
                    // Some grids (especially with custom painting) may fire only CellContentClick
                    // or may not toggle the value automatically. Ensure the value is toggled.
                    if (!toggledByCellClick)
                    {
                        if (!grid.Rows[e.RowIndex].IsNewRow)
                        {
                            var cell = grid.Rows[e.RowIndex].Cells[e.ColumnIndex];
                            var currentValue = cell.Value;

                            if (currentValue == null || (bool)currentValue == false)
                                cell.Value = true;
                            else
                                cell.Value = false;
                        }
                    }

                    toggledByCellClick = false;
                    grid.CommitEdit(DataGridViewDataErrorContexts.Commit);
                    grid.InvalidateCell(e.ColumnIndex, e.RowIndex);
                    if (grid.IsHandleCreated)
                    {
                        grid.BeginInvoke(new Action(() => UpdateSelectAllState()));
                    }
                    else
                    {
                        UpdateSelectAllState();
                    }
                }
            };

            // Attach event handler for rows added/removed
            grid.RowsAdded += (s, e) =>
            {
                // Initialize checkbox cells with false value for newly added rows
                for (int i = e.RowIndex; i < e.RowIndex + e.RowCount; i++)
                {
                    if (i >= 0 && i < grid.Rows.Count && grid.Columns.Contains(checkboxColumnName))
                    {
                        var row = grid.Rows[i];
                        if (!row.IsNewRow && row.Cells[checkboxColumnName].Value == null)
                        {
                            row.Cells[checkboxColumnName].Value = false;
                        }
                    }
                }

                if (grid.IsHandleCreated)
                {
                    grid.BeginInvoke(new Action(() => UpdateSelectAllState()));
                }
                else
                {
                    UpdateSelectAllState();
                }
            };
            grid.RowsRemoved += (s, e) =>
            {
                if (grid.IsHandleCreated)
                {
                    grid.BeginInvoke(new Action(() => UpdateSelectAllState()));
                }
                else
                {
                    UpdateSelectAllState();
                }
            };

            // Add checkbox to grid and position it
            if (grid.Controls.Contains(selectAllCheckBox))
            {
                grid.Controls.Remove(selectAllCheckBox);
            }

            grid.Controls.Add(selectAllCheckBox);

            // Position the checkbox in the column header
            void PositionCheckBox()
            {
                if (grid.Columns.Contains(checkboxColumnName))
                {
                    var col = grid.Columns[checkboxColumnName];
                    if (!col.Visible)
                    {
                        selectAllCheckBox.Visible = false;
                        return;
                    }

                    // Prefer the visible/clipped header rect, but fall back to the full rect (can be negative)
                    // so we don't mistakenly treat the cell as "empty" during initial layout.
                    var rect = grid.GetCellDisplayRectangle(col.Index, -1, true);
                    if (rect.Width <= 0 || rect.Height <= 0)
                    {
                        rect = grid.GetCellDisplayRectangle(col.Index, -1, false);
                    }

                    if (rect.Width <= 0 || rect.Height <= 0)
                    {
                        // Layout not ready yet; don't relocate to a bad default.
                        // A later Layout/Scroll/Size event will re-run this.
                        return;
                    }

                    int visibleLeft = grid.RowHeadersVisible ? grid.RowHeadersWidth : 0;
                    int visibleRight = grid.ClientSize.Width;

                    // Hide only when the header cell is completely off-screen horizontally.
                    if (rect.Right <= visibleLeft || rect.Left >= visibleRight)
                    {
                        selectAllCheckBox.Visible = false;
                        return;
                    }

                    selectAllCheckBox.Visible = true;

                    var headerVisibleRect = Rectangle.Intersect(
                        rect,
                        new Rectangle(visibleLeft, rect.Top, Math.Max(0, visibleRight - visibleLeft), rect.Height)
                    );

                    int desiredX = rect.Left + (rect.Width - selectAllCheckBox.Width) / 2;
                    int desiredY = rect.Top + (rect.Height - selectAllCheckBox.Height) / 2;

                    // Clamp to the visible part of the header cell so it can't drift out of the cell
                    // and remains clickable even when the column is partially visible.
                    int clampedX = Math.Max(headerVisibleRect.Left, Math.Min(headerVisibleRect.Right - selectAllCheckBox.Width, desiredX));
                    int clampedY = Math.Max(rect.Top, Math.Min(rect.Bottom - selectAllCheckBox.Height, desiredY));

                    selectAllCheckBox.Location = new Point(clampedX, clampedY);
                    selectAllCheckBox.BringToFront();
                }
            }

            if (grid.IsHandleCreated)
            {
                grid.BeginInvoke(new Action(() => PositionCheckBox()));
            }
            else
            {
                // Defer positioning until handle is created
                EventHandler handleCreated = null;
                handleCreated = (s, e) =>
                {
                    PositionCheckBox();
                    grid.HandleCreated -= handleCreated;
                };
                grid.HandleCreated += handleCreated;
            }

            // Reposition on scroll or resize
            grid.Scroll += (s, e) =>
            {
                PositionCheckBox();
            };

            grid.ColumnWidthChanged += (s, e) =>
            {
                if (e.Column.Name == checkboxColumnName)
                {
                    PositionCheckBox();
                }
            };

            grid.SizeChanged += (s, e) => PositionCheckBox();
            grid.ColumnHeadersHeightChanged += (s, e) => PositionCheckBox();
            grid.ColumnDisplayIndexChanged += (s, e) => PositionCheckBox();
            grid.Layout += (s, e) => PositionCheckBox();
            grid.VisibleChanged += (s, e) => { if (grid.Visible) PositionCheckBox(); };

            // Store update method in checkbox Tag for external access
            selectAllCheckBox.Click += (s, e) =>
            {
                // Prevent column header click from sorting
                if (grid.SortedColumn?.Name == checkboxColumnName)
                {
                    grid.Sort(grid.SortedColumn, grid.SortOrder == SortOrder.Ascending ? System.ComponentModel.ListSortDirection.Ascending : System.ComponentModel.ListSortDirection.Descending);
                }
            };

            return selectAllCheckBox;
        }

        private sealed class SelectAllCheckBoxTag
        {
            public string ColumnName { get; set; }
            public bool ExcludeNewRow { get; set; }
            public bool UserClicked { get; set; }
        }

        /// <summary>
        /// Normalizes the list page state by clearing row selection, button focus, and visual highlights.
        /// This ensures that when a page loads or data refreshes:
        /// - No DataGridView row is selected or highlighted
        /// - No action button appears focused or highlighted
        /// - Button visual state is synchronized with row selection (none selected = neutral state)
        ///
        /// This method solves both connected issues:
        /// 1. Auto-selected first row on data binding
        /// 2. Action buttons appearing highlighted when no row is actually selected
        ///
        /// IMPORTANT: This method does NOT disable buttons. Buttons remain enabled and clickable.
        /// It only manages the visual focus/highlight state to ensure proper UX.
        ///
        /// Call this method after setting the DataSource on your DataGridView.
        /// </summary>
        /// <param name="dgv">Target DataGridView.</param>
        /// <param name="actionButtons">Optional: Array of action buttons to sync visual state with row selection.
        /// If provided, these buttons will have their focus cleared when no row is selected.</param>
        internal static void DisableDefaultRowHighlight(DataGridView dgv, params HopeButton[] actionButtons)
        {
            if (dgv == null)
                return;

            // Track whether we've already attached the event to avoid duplicates
            bool eventsAttached = dgv.Tag is string tag && tag.Contains("DisableDefaultRowHighlight_Attached");

            void NormalizeListPageState()
            {
                // Step 1: Clear DataGridView selection and current cell
                // This removes the blue row highlight
                dgv.ClearSelection();
                dgv.CurrentCell = null;

                // Step 2: Clear focus from all action buttons
                // This removes button highlight/focus visual state
                var form = dgv.FindForm();
                if (form != null)
                {
                    // Clear ActiveControl if it's a HopeButton
                    if (form.ActiveControl is HopeButton)
                    {
                        form.ActiveControl = null;
                    }

                    // Recursively clear focus state from all buttons in the form
                    ClearButtonFocusInForm(form);

                    // If specific action buttons were provided, clear their focus state explicitly
                    if (actionButtons != null && actionButtons.Length > 0)
                    {
                        ClearActionButtonFocus(form, actionButtons);
                    }
                }

                // Step 3: Force buttons to refresh their visual state
                // This ensures button appearance reflects "no selection" state
                RefreshActionButtonStates(form);
            }

            // Execute immediately to clear any initial selection
            NormalizeListPageState();

            // Attach event handlers only once to avoid duplicate subscriptions
            if (!eventsAttached)
            {
                // CRITICAL: Clear button focus when the parent control becomes visible
                // This handles the initial page load case where buttons get focus by default
                var parentControl = dgv.Parent;
                while (parentControl != null && !(parentControl is UserControl || parentControl is Form))
                {
                    parentControl = parentControl.Parent;
                }

                if (parentControl != null)
                {
                    EventHandler visibleChanged = null;
                    visibleChanged = (s, e) =>
                    {
                        if (parentControl.Visible)
                        {
                            // Use BeginInvoke to ensure this runs after the control is fully loaded
                            if (dgv.IsHandleCreated)
                            {
                                dgv.BeginInvoke(new Action(() =>
                                {
                                    NormalizeListPageState();
                                }));
                            }
                        }
                    };

                    parentControl.VisibleChanged += visibleChanged;

                    // Also handle the case where the control is already visible
                    if (parentControl.Visible)
                    {
                        if (dgv.IsHandleCreated)
                        {
                            dgv.BeginInvoke(new Action(() =>
                            {
                                NormalizeListPageState();
                            }));
                        }
                    }
                }

                // Clear selection after every data binding operation
                dgv.DataBindingComplete += (s, e) =>
                {
                    // Use BeginInvoke to ensure this runs after all binding operations complete
                    if (dgv.IsHandleCreated)
                    {
                        dgv.BeginInvoke(new Action(() =>
                        {
                            NormalizeListPageState();
                        }));
                    }
                    else
                    {
                        NormalizeListPageState();
                    }
                };

                // CRITICAL: Handle SelectionChanged to keep button visual state in sync
                // This ensures buttons don't appear highlighted when no row is selected
                dgv.SelectionChanged += (s, e) =>
                {
                    var form = dgv.FindForm();
                    if (form == null) return;

                    // If no rows are selected, clear button focus/highlight
                    if (dgv.SelectedRows.Count == 0 && dgv.CurrentCell == null)
                    {
                        // Clear ActiveControl if it's a HopeButton
                        if (form.ActiveControl is HopeButton)
                        {
                            form.ActiveControl = null;
                        }

                        // Clear focus from action buttons
                        if (actionButtons != null && actionButtons.Length > 0)
                        {
                            ClearActionButtonFocus(form, actionButtons);
                        }
                        else
                        {
                            // Fallback: clear focus from all buttons
                            ClearButtonFocusInForm(form);
                        }

                        // Refresh button visual states
                        RefreshActionButtonStates(form);
                    }
                };

                // Mark that events are attached
                dgv.Tag = (dgv.Tag?.ToString() ?? "") + "DisableDefaultRowHighlight_Attached";
            }
        }

        /// <summary>
        /// Clears focus from specific action buttons.
        /// This is a targeted version that only affects the provided buttons.
        /// Buttons remain enabled and clickable - only the visual focus state is cleared.
        /// </summary>
        /// <param name="form">The parent form containing the buttons.</param>
        /// <param name="buttons">The action buttons to clear focus from.</param>
        private static void ClearActionButtonFocus(Form form, params HopeButton[] buttons)
        {
            if (form == null || buttons == null || buttons.Length == 0)
                return;

            foreach (var btn in buttons)
            {
                if (btn == null)
                    continue;

                // If this button currently has focus, clear it
                if (form.ActiveControl == btn)
                {
                    form.ActiveControl = null;
                }

                // Reset TabStop to clear any lingering focus state without affecting functionality
                bool originalTabStop = btn.TabStop;
                btn.TabStop = false;
                btn.TabStop = originalTabStop;

                // Force the button to repaint itself to reflect neutral state
                btn.Invalidate();
                btn.Update();
            }
        }

        /// <summary>
        /// Recursively clears focus from all HopeButton controls in a form.
        /// Buttons remain enabled and clickable - only the visual focus state is cleared.
        /// </summary>
        private static void ClearButtonFocusInForm(Control parent)
        {
            if (parent == null)
                return;

            var form = parent.FindForm();

            foreach (Control control in parent.Controls)
            {
                if (control is HopeButton btn)
                {
                    // If this button currently has focus, clear it
                    if (form != null && form.ActiveControl == btn)
                    {
                        form.ActiveControl = null;
                    }

                    // Reset TabStop to clear any lingering focus state without affecting functionality
                    bool originalTabStop = btn.TabStop;
                    btn.TabStop = false;
                    btn.TabStop = originalTabStop;

                    // Force the button to repaint itself
                    btn.Invalidate();
                }

                // Recursively process child controls
                if (control.HasChildren)
                {
                    ClearButtonFocusInForm(control);
                }
            }
        }

        /// <summary>
        /// Forces all action buttons on a form to refresh their visual state.
        /// This ensures buttons don't retain "focused" or "highlighted" appearance
        /// when they shouldn't have it (e.g., when no row is selected).
        /// </summary>
        private static void RefreshActionButtonStates(Form form)
        {
            if (form == null)
                return;

            RefreshButtonStatesRecursive(form);
        }

        /// <summary>
        /// Recursively refreshes button states throughout the control hierarchy.
        /// </summary>
        private static void RefreshButtonStatesRecursive(Control parent)
        {
            if (parent == null)
                return;

            foreach (Control control in parent.Controls)
            {
                if (control is HopeButton btn)
                {
                    // Force visual refresh by invalidating and updating the button
                    btn.Invalidate();
                    btn.Update();
                }

                // Recursively process child controls
                if (control.HasChildren)
                {
                    RefreshButtonStatesRecursive(control);
                }
            }
        }

        /// <summary>
        /// Sets up initial page state to ensure no buttons appear highlighted on first load.
        /// Call this method at the END of your BuildUI() or constructor, AFTER all controls are created.
        ///
        /// This method:
        /// - Sets initial focus to a neutral control (search box or DataGridView)
        /// - Clears focus from all action buttons
        /// - Ensures clean visual state on page load
        ///
        /// Example:
        ///     private void BuildUiWithTemplate()
        ///     {
        ///         // ... create all controls ...
        ///         // ... add controls to layout ...
        ///
        ///         // At the END, set up initial focus
        ///         DefaultListPageTemplate.SetupInitialPageFocus(_layout.SearchBox, dgvCategories, _btnEdit, _btnDelete, _btnArchive);
        ///     }
        /// </summary>
        /// <param name="searchBox">The search box control (will receive initial focus if available).</param>
        /// <param name="dgv">The DataGridView (fallback focus if search box is null).</param>
        /// <param name="actionButtons">Action buttons to clear focus from.</param>
        internal static void SetupInitialPageFocus(Control searchBox, DataGridView dgv, params HopeButton[] actionButtons)
        {
            // Schedule focus setup to run after the control is fully loaded and visible
            void SetupFocus()
            {
                var form = dgv?.FindForm();
                if (form == null)
                    return;

                // Clear focus from all action buttons first
                if (actionButtons != null && actionButtons.Length > 0)
                {
                    ClearActionButtonFocus(form, actionButtons);
                }

                // Set focus to a neutral control (search box preferred, DataGridView as fallback)
                if (searchBox != null && searchBox.CanFocus && searchBox.Visible)
                {
                    searchBox.Focus();
                }
                else if (dgv != null && dgv.CanFocus && dgv.Visible)
                {
                    dgv.Focus();
                }
                else
                {
                    // Last resort: clear ActiveControl
                    form.ActiveControl = null;
                }

                // Force refresh of button states
                RefreshActionButtonStates(form);
            }

            // Execute immediately
            SetupFocus();

            // Also execute when the page becomes visible (handles tab switching)
            if (dgv != null)
            {
                var parentControl = dgv.Parent;
                while (parentControl != null && !(parentControl is UserControl || parentControl is Form))
                {
                    parentControl = parentControl.Parent;
                }

                if (parentControl != null)
                {
                    EventHandler visibleChanged = null;
                    visibleChanged = (s, e) =>
                    {
                        if (parentControl.Visible && dgv.SelectedRows.Count == 0)
                        {
                            if (dgv.IsHandleCreated)
                            {
                                dgv.BeginInvoke(new Action(() => SetupFocus()));
                            }
                        }
                    };
                    parentControl.VisibleChanged += visibleChanged;
                }
            }
        }

        /// <summary>
        /// Hides the Sort By dropdown and its label from the header panel.
        /// Call this on pages that use per-column sort/filter instead.
        /// </summary>
        internal static void HideSortDropdown(DefaultListPageLayout layout)
        {
            if (layout?.SortByComboBox == null) return;

            layout.SortByComboBox.Visible = false;

            // The label ("Sort By:") is a local var in Create(), so find it by text in the same panel.
            var panel = layout.SortByComboBox.Parent;
            if (panel == null) return;
            foreach (Control ctrl in panel.Controls)
            {
                if (ctrl is Label lbl && lbl.Text.StartsWith("Sort", StringComparison.OrdinalIgnoreCase))
                {
                    lbl.Visible = false;
                    break;
                }
            }
        }

        /// <summary>
        /// Synchronizes action button visual state with DataGridView row selection.
        ///
        /// This method ensures that buttons don't appear highlighted/focused when no row is selected,
        /// while keeping them enabled and clickable at all times.
        ///
        /// IMPORTANT: This method does NOT modify the Enabled property. Buttons remain enabled.
        /// It only manages the visual focus state to match the selection state.
        ///
        /// Usage: Call this from your DataGridView's SelectionChanged event handler.
        ///
        /// Example:
        ///     dgvItems.SelectionChanged += (s, e) => {
        ///         DefaultListPageTemplate.SyncActionButtonVisualState(dgvItems, btnEdit, btnDelete, btnArchive);
        ///         // Your other selection logic here (enable/disable based on selection count)
        ///     };
        /// </summary>
        /// <param name="dgv">The DataGridView to check for selected rows.</param>
        /// <param name="actionButtons">The action buttons to synchronize visual state.</param>
        internal static void SyncActionButtonVisualState(DataGridView dgv, params HopeButton[] actionButtons)
        {
            if (dgv == null || actionButtons == null || actionButtons.Length == 0)
                return;

            var form = dgv.FindForm();
            if (form == null)
                return;

            // If no rows are selected, clear button focus/highlight
            if (dgv.SelectedRows.Count == 0 && dgv.CurrentCell == null)
            {
                // Clear ActiveControl if it's one of our action buttons
                if (form.ActiveControl is HopeButton activeBtn && actionButtons.Contains(activeBtn))
                {
                    form.ActiveControl = null;
                }

                // Clear focus from all action buttons
                ClearActionButtonFocus(form, actionButtons);

                // Refresh their visual states
                foreach (var btn in actionButtons)
                {
                    if (btn != null)
                    {
                        btn.Invalidate();
                        btn.Update();
                    }
                }
            }
        }

        /// <summary>
        /// Helper method to programmatically update the Select All checkbox state.
        /// Call this after operations like delete, archive, or filter changes.
        /// Note: The checkbox state is automatically updated through event handlers.
        /// This method is provided for compatibility but is no longer needed.
        /// </summary>
        /// <param name="selectAllCheckBox">The Select All checkbox returned from AddSelectAllCheckBox</param>
        internal static void UpdateSelectAllCheckBoxState(System.Windows.Forms.CheckBox selectAllCheckBox)
        {
            // No-op: The checkbox state is automatically updated through the event handlers
            // set up in AddSelectAllCheckBox (CellContentClick, RowsAdded, RowsRemoved)
        }

        /// <summary>
        /// Apply sizing and alignment for the dedicated selection checkbox column only.
        /// Other checkbox columns (e.g., Active) are left untouched.
        /// </summary>
        /// <param name="grid">Target DataGridView</param>
        /// <param name="columnName">Selection checkbox column name (default: \"colSelect\")</param>
        internal static void StyleSelectionCheckBoxColumn(DataGridView grid, string columnName = "colSelect")
        {
            if (grid == null || !grid.Columns.Contains(columnName))
                return;

            if (grid.Columns[columnName] is DataGridViewCheckBoxColumn col)
            {
                col.AutoSizeMode = DataGridViewAutoSizeColumnMode.None;
                col.Width = 40;
                col.MinimumWidth = 40;
                col.Resizable = DataGridViewTriState.False;
                col.SortMode = DataGridViewColumnSortMode.NotSortable;
                col.Frozen = true;
                col.DisplayIndex = 0;
                col.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
                col.DefaultCellStyle.Padding = new Padding(0);
                col.HeaderCell.Style.Alignment = DataGridViewContentAlignment.MiddleCenter;
                col.HeaderCell.Style.Padding = new Padding(0);
            }
        }

        /// <summary>
        /// Preserves column widths when updating DataGridView data source.
        /// Prevents columns from resizing when rapidly toggling filters like "Show Inactive".
        /// </summary>
        /// <param name="grid">Target DataGridView</param>
        /// <param name="updateAction">Action that updates the data source</param>
        public static void PreserveColumnWidthsOnUpdate(DataGridView grid, Action updateAction)
        {
            if (grid == null || updateAction == null)
                return;

            // Check if this is the initial load (columns haven't been properly sized yet)
            bool isInitialLoad = grid.Columns.Count == 0 ||
                                 grid.Columns.Cast<DataGridViewColumn>().All(c => c.Width <= 0);

            // Save current column widths
            var columnWidths = new Dictionary<string, int>();
            var originalGridAutoSizeMode = grid.AutoSizeColumnsMode;

            foreach (DataGridViewColumn col in grid.Columns)
            {
                if (col.Visible && col.Width > 0)
                    columnWidths[col.Name] = col.Width;
            }

            // If this is not the initial load and we have saved widths, disable auto-sizing permanently
            if (!isInitialLoad && columnWidths.Count > 0)
            {
                grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None;
                foreach (DataGridViewColumn col in grid.Columns)
                {
                    col.AutoSizeMode = DataGridViewAutoSizeColumnMode.None;
                }
            }

            // Execute the update action (e.g., set DataSource)
            updateAction();

            // Restore column widths only if we had valid widths saved
            if (columnWidths.Count > 0)
            {
                foreach (DataGridViewColumn col in grid.Columns)
                {
                    if (columnWidths.ContainsKey(col.Name))
                        col.Width = columnWidths[col.Name];
                }
            }
        }

        // ==================================================================================
        // SORT BY DROPDOWN HELPERS
        // ==================================================================================

        /// <summary>
        /// Sets up the Sort By dropdown for a DataGridView.
        /// Populates dropdown options from sortable columns and wires up event handlers.
        ///
        /// HOW IT WORKS:
        /// 1. Scans visible columns in the DataGridView
        /// 2. Excludes checkbox/button/image columns and non-sortable columns
        /// 3. For each sortable column, generates two options: ASC and DESC
        /// 4. Special handling for boolean columns: generates "Checked" and "Unchecked" options
        /// 5. When user selects an option, calls the onSortChanged callback with column and direction
        ///
        /// INTEGRATION WITH EXISTING SORTING:
        /// The callback receives the column name and sort direction, which the page should use
        /// to update its internal _sortColumn and _sortOrder state, then call ApplyFilters()
        /// to trigger the existing sorting logic.
        ///
        /// USAGE EXAMPLE:
        ///     DefaultListPageTemplate.SetupSortByDropdown(
        ///         _layout.SortByComboBox,
        ///         dgvItems,
        ///         (columnKey, direction) => {
        ///             // Find the column by DataPropertyName or Name
        ///             var col = dgvItems.Columns.Cast<DataGridViewColumn>()
        ///                 .FirstOrDefault(c => c.DataPropertyName == columnKey || c.Name == columnKey);
        ///
        ///             if (col != null)
        ///             {
        ///                 _sortColumn = col;
        ///                 _sortOrder = direction;
        ///                 ApplyFilters(); // This applies the sort using existing logic
        ///             }
        ///         }
        ///     );
        /// </summary>
        /// <param name="sortByComboBox">The Sort By ComboBox from the layout</param>
        /// <param name="grid">The DataGridView to generate sort options from</param>
        /// <param name="onSortChanged">Callback invoked when user selects a sort option (columnKey, sortDirection)</param>
        /// <param name="defaultColumnKey">Optional: Column key to select by default (null = first option)</param>
        /// <param name="defaultDirection">Optional: Default sort direction (default = Ascending)</param>
        internal static void SetupSortByDropdown(
            ComboBox sortByComboBox,
            DataGridView grid,
            Action<string, System.Windows.Forms.SortOrder> onSortChanged,
            string defaultColumnKey = null,
            System.Windows.Forms.SortOrder defaultDirection = System.Windows.Forms.SortOrder.Ascending)
        {
            if (sortByComboBox == null || grid == null || onSortChanged == null)
                return;

            // Support rebinding the same ComboBox to different grids by keeping the current
            // callback + handler in Tag.
            var state = sortByComboBox.Tag as SortByDropdownState;
            if (state == null)
            {
                state = new SortByDropdownState();
                sortByComboBox.Tag = state;
            }

            // Replace any previous handler (important for rebinding to a different grid)
            if (state.Handler != null)
            {
                sortByComboBox.SelectedIndexChanged -= state.Handler;
                state.Handler = null;
            }

            state.IsUpdating = true;
            state.OnSortChanged = onSortChanged;

            // Populate sort options from grid columns
            var sortOptions = PopulateSortByOptions(grid);

            // Clear and populate the dropdown
            sortByComboBox.Items.Clear();
            foreach (var option in sortOptions)
            {
                sortByComboBox.Items.Add(option);
            }

            // Set default selection
            if (sortOptions.Count > 0)
            {
                int defaultIndex = 0;

                if (!string.IsNullOrEmpty(defaultColumnKey))
                {
                    // Find the option matching default column and direction
                    for (int i = 0; i < sortOptions.Count; i++)
                    {
                        var opt = sortOptions[i];
                        if (opt.ColumnKey == defaultColumnKey && opt.SortDirection == defaultDirection)
                        {
                            defaultIndex = i;
                            break;
                        }
                    }
                }

                sortByComboBox.SelectedIndex = defaultIndex;
            }

            state.IsUpdating = false;

            // Wire up a handler tied to the latest callback
            state.Handler = (s, e) =>
            {
                if (state.IsUpdating)
                    return;

                if (sortByComboBox.SelectedItem is SortOption selectedOption)
                {
                    state.OnSortChanged?.Invoke(selectedOption.ColumnKey, selectedOption.SortDirection);
                }
            };

            sortByComboBox.SelectedIndexChanged += state.Handler;
        }

        /// <summary>
        /// Synchronizes the Sort By dropdown selection with the current grid sort state.
        /// Call this method when the user clicks a column header to keep the dropdown in sync.
        ///
        /// USAGE EXAMPLE (in column header click handler):
        ///     private void DgvItems_ColumnHeaderMouseClick(object sender, DataGridViewCellMouseEventArgs e)
        ///     {
        ///         var col = dgvItems.Columns[e.ColumnIndex];
        ///         // ... existing sort logic ...
        ///
        ///         // Sync the dropdown
        ///         DefaultListPageTemplate.SyncSortByDropdown(_layout.SortByComboBox, col, _sortOrder);
        ///
        ///         ApplyFilters();
        ///     }
        /// </summary>
        /// <param name="sortByComboBox">The Sort By ComboBox from the layout</param>
        /// <param name="sortColumn">The currently sorted column</param>
        /// <param name="sortOrder">The current sort order</param>
        internal static void SyncSortByDropdown(
            ComboBox sortByComboBox,
            DataGridViewColumn sortColumn,
            System.Windows.Forms.SortOrder sortOrder)
        {
            if (sortByComboBox == null || sortColumn == null)
                return;

            var state = sortByComboBox.Tag as SortByDropdownState;

            // Find matching sort option in dropdown
            string columnKey = !string.IsNullOrEmpty(sortColumn.DataPropertyName)
                ? sortColumn.DataPropertyName
                : sortColumn.Name;

            for (int i = 0; i < sortByComboBox.Items.Count; i++)
            {
                if (sortByComboBox.Items[i] is SortOption option)
                {
                    if (option.ColumnKey == columnKey && option.SortDirection == sortOrder)
                    {
                        if (state != null)
                            state.IsUpdating = true;

                        sortByComboBox.SelectedIndex = i;

                        if (state != null)
                            state.IsUpdating = false;
                        break;
                    }
                }
            }
        }

        /// <summary>
        /// Populates sort options from a DataGridView's columns.
        ///
        /// HOW OPTIONS ARE GENERATED:
        /// - Scans all visible columns in the grid
        /// - Excludes checkbox columns (unless they represent boolean data fields)
        /// - Excludes button and image columns
        /// - Excludes columns with SortMode = NotSortable
        /// - For each sortable column, creates two options: ASC and DESC
        /// - Special case for boolean columns: creates "Checked" and "Unchecked" options
        ///
        /// BOOLEAN COLUMN HANDLING:
        /// Boolean/checkbox columns are detected by checking if the column is a
        /// DataGridViewCheckBoxColumn with a valid DataPropertyName (not selection checkboxes).
        /// These get special labels like "Active (Checked)" and "Active (Unchecked)".
        /// </summary>
        /// <param name="grid">The DataGridView to scan</param>
        /// <returns>List of SortOption objects</returns>
        private static List<SortOption> PopulateSortByOptions(DataGridView grid)
        {
            var options = new List<SortOption>();

            if (grid == null || grid.Columns.Count == 0)
                return options;

            foreach (DataGridViewColumn col in grid.Columns)
            {
                // Skip invisible columns
                if (!col.Visible)
                    continue;

                // Skip columns that don't support sorting
                if (col.SortMode == DataGridViewColumnSortMode.NotSortable)
                    continue;

                // Skip button and image columns
                if (col is DataGridViewButtonColumn || col is DataGridViewImageColumn)
                    continue;

                // Determine column key (prefer DataPropertyName, fallback to Name)
                string columnKey = !string.IsNullOrEmpty(col.DataPropertyName)
                    ? col.DataPropertyName
                    : col.Name;

                // Skip columns without a valid key
                if (string.IsNullOrEmpty(columnKey))
                    continue;

                // Get display name from HeaderText
                string displayName = col.HeaderText;
                if (string.IsNullOrEmpty(displayName))
                    displayName = columnKey;

                // Check if this is a boolean/checkbox column with data binding
                // (excludes selection checkboxes which typically don't have DataPropertyName)
                bool isBooleanColumn = col is DataGridViewCheckBoxColumn &&
                                       !string.IsNullOrEmpty(col.DataPropertyName);

                if (isBooleanColumn)
                {
                    // Special handling for boolean columns
                    // Generate "Checked" and "Unchecked" options
                    options.Add(new SortOption
                    {
                        DisplayText = $"{displayName} (Checked)",
                        ColumnKey = columnKey,
                        SortDirection = System.Windows.Forms.SortOrder.Descending, // Checked = true sorts to top
                        BooleanValue = true
                    });

                    options.Add(new SortOption
                    {
                        DisplayText = $"{displayName} (Unchecked)",
                        ColumnKey = columnKey,
                        SortDirection = System.Windows.Forms.SortOrder.Ascending, // Unchecked = false sorts to top
                        BooleanValue = false
                    });
                }
                else
                {
                    // Standard column: generate ASC and DESC options
                    options.Add(new SortOption
                    {
                        DisplayText = $"{displayName} (ASC)",
                        ColumnKey = columnKey,
                        SortDirection = System.Windows.Forms.SortOrder.Ascending,
                        BooleanValue = null
                    });

                    options.Add(new SortOption
                    {
                        DisplayText = $"{displayName} (DESC)",
                        ColumnKey = columnKey,
                        SortDirection = System.Windows.Forms.SortOrder.Descending,
                        BooleanValue = null
                    });
                }
            }

            return options;
        }
    }
}
