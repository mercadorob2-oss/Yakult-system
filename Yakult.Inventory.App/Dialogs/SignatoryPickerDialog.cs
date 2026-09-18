using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using Yakult.Inventory.App.Core;

namespace Yakult.Inventory.App.Dialogs
{
    /// <summary>
    /// Dialog that lets the user pick employees as signatories for a report.
    /// Supports 1–5 signatories per row and any number of rows.
    /// Output DataTable uses the same pair-based schema (LeftName/LeftTitle/RightName/RightTitle)
    /// so existing RDLC reports are not affected.
    /// </summary>
    public class SignatoryPickerDialog : Form
    {
        // ── Layout ────────────────────────────────────────────────────────────
        private Panel          _headerPanel;
        private Label          _titleLabel;
        private Label          _subtitleLabel;
        private Label          _roleLabel;

        private Panel          _controlBar;
        private NumericUpDown  _columnsUpDown;
        private Button         _addSlotButton;
        private Button         _removeSlotButton;

        private Panel          _colHeadersPanel;
        private Panel          _slotsContainer;

        private Panel          _footerPanel;
        private Button         _okButton;
        private Button         _cancelButton;

        // ── Data ─────────────────────────────────────────────────────────────
        private readonly List<Employee> _employees = new List<Employee>();
        private int _columnsPerRow = 2;

        // Multi-role mode: one labelled slot per role, all roles in a single dialog, instead of
        // opening this form once per role. The Sets report needs four (Prepared/Noted/Approved/
        // Received) and only ever reads the FIRST signatory of each, so one slot per role is
        // exactly the data that gets consumed.
        private readonly string[] _roles;
        private bool RoleMode => _roles != null && _roles.Length > 0;
        private const int RoleLabelW = 220;

        /// <summary>Shows the full role label/hint text on hover — needed now that hints can carry
        /// a full "for: Item Name (TICKET-CODE)" sentence that gets ellipsized at this column width.</summary>
        private readonly ToolTip _roleToolTip = new ToolTip { AutoPopDelay = 8000, InitialDelay = 300, ReshowDelay = 100 };

        // Remembering the previous pick is keyed by role, so "Noted By" doesn't inherit whoever
        // was last chosen for "Approved By".
        private readonly string _memoryKey;

        /// <summary>The application's standard accent blue (#3498DB) — matches the report dialogs.</summary>
        private static readonly Color AccentBlue = Color.FromArgb(52, 152, 219);

        private Label _validationLabel;

        /// <summary>
        /// False until the form is on screen at its final size/position. The autocomplete popup is
        /// a separate top-level window positioned in SCREEN coordinates, so opening one before
        /// then leaves it stranded wherever the dialog happened to be mid-layout.
        /// </summary>
        private bool _uiReady;

        /// <summary>Hides every open autocomplete popup — fired whenever the dialog moves or resizes.</summary>
        private Action _hideAllPopups = delegate { };
        private static readonly Color WarnBg = Color.FromArgb(254, 249, 231);
        private static readonly Color WarnFg = Color.FromArgb(161, 98, 7);

        private class Employee
        {
            public int    EmpId     { get; set; }
            public string Name      { get; set; }
            public string Position  { get; set; }
            public string TitleCode { get; set; }
            public int?   DeptId    { get; set; }
            // Shown in the autocomplete list. Position disambiguates people with similar names;
            // only the Name is written back into the textbox on selection.
            public override string ToString() =>
                string.IsNullOrWhiteSpace(Position) ? Name : Name + "   —   " + Position;
        }

        // ── Optional per-role scoping (role mode only) ────────────────────────
        // Both null by default so every other caller of this dialog (Sets/Renewals/Invoice/
        // Warranty reports) is completely unaffected — only a caller that explicitly passes these
        // gets department-restricted candidates / a hint under the role label.
        private readonly Dictionary<string, int?> _roleDeptIds;
        private readonly Dictionary<string, string> _roleHints;

        /// <summary>Optional: role name → item rows (Item, Category, Reported Problem, Status) shown
        /// as a real table above that role's Employee/Title picker, so it's unmistakable which
        /// item(s) that signature applies to. Only used by the Repair Report batch flow.</summary>
        private readonly Dictionary<string, List<string[]>> _roleItemRows;

        /// <summary>Optional: role names after which an obvious (thicker, darker) divider is drawn —
        /// e.g. the last role in a requester group's block, so different requesters' item sets are
        /// unmistakably separated instead of blending into one continuous scroll.</summary>
        private readonly HashSet<string> _roleSectionBreakAfter;

        private static bool Contains(string haystack, string needle) =>
            !string.IsNullOrEmpty(haystack) &&
            haystack.IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0;

        // Floating dropdown window that never steals keyboard focus from the parent
        private class DropdownWindow : Form
        {
            protected override bool ShowWithoutActivation => true;
        }

        // ── Slot controls per row ─────────────────────────────────────────────
        private readonly List<SlotRow> _slotRows = new List<SlotRow>();

        private class SlotControl
        {
            public TextBox  NameBox          { get; set; }
            public TextBox  TitleBox         { get; set; }
            public Employee SelectedEmployee { get; set; }
            /// <summary>Re-applies the free-text/duplicate tint on this slot's name box.</summary>
            public Action   RefreshValidity  { get; set; }
        }

        private class SlotRow
        {
            public Panel             Container { get; set; }
            public List<SlotControl> Slots     { get; set; } = new List<SlotControl>();
            /// <summary>Multi-role mode only — the role this row collects a signatory for.</summary>
            public string            Role      { get; set; }
            public Label             RoleLabel { get; set; }
            public Label             RoleHintLabel { get; set; }
            /// <summary>True when this row rendered an item table (see _roleItemRows) — changes the
            /// Employee/Title picker to sit BELOW the table (full width) instead of beside a left
            /// role-label column.</summary>
            public bool              HasItemTable { get; set; }
            /// <summary>Where the Employee/Title textboxes start — computed once when the row is
            /// built, since it differs between the plain role-label layout and the item-table layout.</summary>
            public int               PickerOriginX { get; set; }
            public int               PickerY       { get; set; } = 8;
        }

        // ── Result ────────────────────────────────────────────────────────────
        /// <summary>DataTable with columns: PairIndex, LeftName, LeftTitle, RightName, RightTitle.</summary>
        public DataTable SignatoryData { get; private set; }

        /// <summary>
        /// Multi-role mode only: one signatory DataTable per role, in the same pair-based schema
        /// as <see cref="SignatoryData"/>. Null in single-role mode.
        /// </summary>
        public Dictionary<string, DataTable> RoleSignatoryData { get; private set; }

        // ─────────────────────────────────────────────────────────────────────
        public SignatoryPickerDialog(IEnumerable<string> reportNames = null)
        {
            var labels = reportNames == null ? null : new List<string>(reportNames);
            _memoryKey = labels != null && labels.Count > 0 ? string.Join(", ", labels) : "(default)";

            BuildLayout();
            if (labels != null && labels.Count > 0)
            {
                var list = string.Join(", ", labels);
                _subtitleLabel.Text = "Applying signatories to:";
                _roleLabel.Text     = list.ToUpper();
                _roleLabel.Visible  = true;

                // Place role label inline — immediately after the subtitle text
                var subtitleSize = TextRenderer.MeasureText(_subtitleLabel.Text, _subtitleLabel.Font);
                _roleLabel.Location = new Point(_subtitleLabel.Left + subtitleSize.Width - 4, _subtitleLabel.Top);

                ApplyTheme(list);
            }
            LoadEmployees();

            // Pre-fill from the last time this role was used; fall back to one empty slot.
            var remembered = ReportPreferences.GetSignatories(_memoryKey);
            if (remembered.Count > 0)
                foreach (var r in remembered) AddSlot(r.Name, r.Title);
            else
                AddSlot();

            this.Load  += (s, e) => { RebuildColHeaders(); ResizeSlotRows(); };
            // Only after Shown is the dialog at its real size/position, so only then may an
            // autocomplete popup be anchored to a textbox.
            this.Shown += (s, e) => _uiReady = true;
            this.Move   += (s, e) => _hideAllPopups();
            this.Resize += (s, e) => _hideAllPopups();
        }

        /// <summary>
        /// Multi-role constructor: renders one labelled row per role in a single dialog, replacing
        /// the previous flow of opening this form once per role. Results land in
        /// <see cref="RoleSignatoryData"/>, keyed by role name.
        /// </summary>
        /// <param name="roleDeptIds">Optional: role name → DeptId. When a role has an entry with a
        /// non-null DeptId, that role's autocomplete only offers employees in that department
        /// (falls back to the full employee list if the filter would leave zero candidates).</param>
        /// <param name="roleHints">Optional: role name → short hint text shown under the role label
        /// (e.g. the department name), so the picker communicates the scoping without the caller
        /// needing to change the role label text itself.</param>
        /// <param name="roleItemRows">Optional: role name → item rows (each a 4-cell array: Item,
        /// Category, Reported Problem, Status) rendered as a real table above that role's picker.</param>
        /// <param name="roleSectionBreakAfter">Optional: role names after which an obvious divider
        /// is drawn, separating one requester's block of roles from the next.</param>
        public SignatoryPickerDialog(string[] roles, bool multiRole,
            Dictionary<string, int?> roleDeptIds = null, Dictionary<string, string> roleHints = null,
            Dictionary<string, List<string[]>> roleItemRows = null,
            HashSet<string> roleSectionBreakAfter = null)
        {
            if (roles == null || roles.Length == 0)
                throw new ArgumentException("At least one role is required.", nameof(roles));

            _roles      = roles;
            _memoryKey  = null;   // each role remembers itself independently
            _columnsPerRow = 1;
            _roleDeptIds = roleDeptIds;
            _roleHints = roleHints;
            _roleItemRows = roleItemRows;
            _roleSectionBreakAfter = roleSectionBreakAfter;

            BuildLayout();

            _titleLabel.Text    = "Select Signatories";
            _subtitleLabel.Text = roleHints != null && roleHints.Count > 0
                ? "Choose the people who will sign — each role below shows exactly which item(s) it's for."
                : "One signatory per role — all roles for this report on one screen.";
            _controlBar.Visible = false;   // no columns spinner / add / remove in role mode

            LoadEmployees();

            foreach (var role in _roles)
            {
                var remembered = ReportPreferences.GetSignatories(role);
                var first = remembered.Count > 0 ? remembered[0] : null;
                AddSlot(first?.Name, first?.Title, role);
            }

            this.Load  += (s, e) => { RebuildColHeaders(); ResizeSlotRows(); };
            // Only after Shown is the dialog at its real size/position, so only then may an
            // autocomplete popup be anchored to a textbox.
            this.Shown += (s, e) => _uiReady = true;
            this.Move   += (s, e) => _hideAllPopups();
            this.Resize += (s, e) => _hideAllPopups();
        }

        // ─────────────────────────────────────────────────────────────────────
        private void ApplyTheme(string label)
        {
            string lower = label.ToLowerInvariant();

            Color headerBg, titleFg, subtitleFg, roleFg;
            // The primary action is always the app's accent blue, whatever the role — only the
            // light header tint varies, so the role stays distinguishable at a glance.
            Color okBg = AccentBlue;

            if (lower.Contains("issued"))
            {
                // Light blue
                headerBg   = Color.FromArgb(219, 234, 254);
                titleFg    = Color.FromArgb(29,  78,  216);
                subtitleFg = Color.FromArgb(59,  130, 246);
                roleFg     = Color.FromArgb(29,  78,  216);
            }
            else if (lower.Contains("noted"))
            {
                // Light green
                headerBg   = Color.FromArgb(209, 250, 229);
                titleFg    = Color.FromArgb(6,   95,  70);
                subtitleFg = Color.FromArgb(4,   120, 87);
                roleFg     = Color.FromArgb(6,   95,  70);
            }
            else
            {
                // Neutral slate (fallback for any other label)
                headerBg   = Color.FromArgb(248, 250, 252);
                titleFg    = Color.FromArgb(30,  41,  59);
                subtitleFg = Color.FromArgb(100, 116, 139);
                roleFg     = Color.FromArgb(30,  41,  59);
            }

            _headerPanel.BackColor   = headerBg;
            _titleLabel.ForeColor    = titleFg;
            _subtitleLabel.ForeColor = subtitleFg;
            _roleLabel.ForeColor     = roleFg;
            _okButton.BackColor      = okBg;
        }

        // ─────────────────────────────────────────────────────────────────────
        private void BuildLayout()
        {
            Text            = "Select Signatories";
            Size            = new Size(1100, 660);
            MinimumSize     = new Size(760, 460);
            StartPosition   = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.Sizable;
            BackColor       = Color.White;
            Font            = new Font("Segoe UI", 9f);

            // ── Header ──────────────────────────────────────────────────────
            _headerPanel = new Panel
            {
                Dock      = DockStyle.Top,
                Height    = 60,
                BackColor = Color.FromArgb(248, 250, 252),
                Padding   = new Padding(16, 0, 16, 0)
            };
            _titleLabel = new Label
            {
                Text      = "Select Signatories",
                ForeColor = Color.FromArgb(30, 41, 59),
                Font      = new Font("Segoe UI", 13f, FontStyle.Bold),
                AutoSize  = true,
                Location  = new Point(16, 4)
            };
            _subtitleLabel = new Label
            {
                Text      = "Choose employees who will sign this report.",
                ForeColor = Color.FromArgb(100, 116, 139),
                Font      = new Font("Segoe UI", 8.5f),
                AutoSize  = true,
                Location  = new Point(18, 38)
            };
            _roleLabel = new Label
            {
                Text      = "",
                ForeColor = Color.FromArgb(30, 41, 59),
                Font      = new Font("Segoe UI", 8.5f, FontStyle.Bold),
                AutoSize  = true,
                Location  = new Point(18, 38),  // overridden inline by constructor
                Visible   = false
            };
            _headerPanel.Controls.Add(_titleLabel);
            _headerPanel.Controls.Add(_subtitleLabel);
            _headerPanel.Controls.Add(_roleLabel);

            // ── Footer ──────────────────────────────────────────────────────
            _footerPanel = new Panel
            {
                Dock      = DockStyle.Bottom,
                Height    = 52,
                BackColor = Color.FromArgb(248, 250, 252)
            };
            _cancelButton = new Button
            {
                Text      = "Cancel",
                Size      = new Size(88, 34),
                Location  = new Point(12, 9),
                FlatStyle = FlatStyle.Flat
            };
            _cancelButton.FlatAppearance.BorderColor = Color.FromArgb(203, 213, 225);
            _cancelButton.Click += (s, e) => { DialogResult = DialogResult.Cancel; Close(); };

            _okButton = new Button
            {
                Text      = "Add to Report",
                Size      = new Size(112, 34),
                FlatStyle = FlatStyle.Flat,
                BackColor = AccentBlue,
                ForeColor = Color.White
            };
            _okButton.FlatAppearance.BorderSize = 0;
            _okButton.Click += OkButton_Click;
            _footerPanel.SizeChanged += (s, e) =>
                _okButton.Location = new Point(_footerPanel.ClientSize.Width - _okButton.Width - 12, 9);
            _okButton.Location = new Point(1100 - 112 - 12, 9);

            // Live warning strip: unknown names / duplicates are surfaced here as you type rather
            // than only at OK time, so there is no surprise dialog at the end.
            _validationLabel = new Label
            {
                Text      = "",
                AutoSize  = false,
                Location  = new Point(108, 9),
                Size      = new Size(420, 34),
                Font      = new Font("Segoe UI", 8f),
                ForeColor = WarnFg,
                TextAlign = ContentAlignment.MiddleLeft
            };

            _footerPanel.Controls.Add(_cancelButton);
            _footerPanel.Controls.Add(_validationLabel);
            _footerPanel.Controls.Add(_okButton);

            // ── Control bar: columns spinner + add/remove buttons ────────────
            _controlBar = new Panel
            {
                Dock      = DockStyle.Top,
                Height    = 48,
                BackColor = Color.FromArgb(248, 250, 252)
            };

            var colsLabel = new Label
            {
                Text      = "Columns per row:",
                AutoSize  = true,
                Font      = new Font("Segoe UI", 9f),
                Location  = new Point(14, 15),
                TextAlign = ContentAlignment.MiddleLeft
            };

            _columnsUpDown = new NumericUpDown
            {
                Minimum   = 1,
                Maximum   = 5,
                Value     = 2,
                Width     = 52,
                Location  = new Point(170, 11),
                Font      = new Font("Segoe UI", 9f),
                TextAlign = HorizontalAlignment.Center
            };
            _columnsUpDown.ValueChanged += (s, e) =>
            {
                _columnsPerRow = (int)_columnsUpDown.Value;
                RebuildAllRows();
            };

            _addSlotButton = new Button
            {
                Text      = "+ Add",
                Size      = new Size(84, 32),
                Location  = new Point(232, 8),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(241, 245, 249),
                TextAlign = ContentAlignment.MiddleCenter,
                Font      = new Font("Segoe UI", 9f)
            };
            _addSlotButton.FlatAppearance.BorderColor = Color.FromArgb(203, 213, 225);
            _addSlotButton.Click += (s, e) => AddSlot();

            _removeSlotButton = new Button
            {
                Text      = "- Remove",
                Size      = new Size(100, 32),
                Location  = new Point(324, 8),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(241, 245, 249),
                TextAlign = ContentAlignment.MiddleCenter,
                Font      = new Font("Segoe UI", 9f)
            };
            _removeSlotButton.FlatAppearance.BorderColor = Color.FromArgb(203, 213, 225);
            _removeSlotButton.Click += (s, e) => RemoveLastSlot();

            _controlBar.Controls.Add(colsLabel);
            _controlBar.Controls.Add(_columnsUpDown);
            _controlBar.Controls.Add(_addSlotButton);
            _controlBar.Controls.Add(_removeSlotButton);

            // ── Column headers (rebuilt when column count or width changes) ──
            _colHeadersPanel = new Panel
            {
                Dock      = DockStyle.Top,
                Height    = 26,
                BackColor = Color.FromArgb(241, 245, 249)
            };

            // ── Slots container (scrollable body) ────────────────────────────
            _slotsContainer = new Panel
            {
                Dock       = DockStyle.Fill,
                AutoScroll = true
            };
            _slotsContainer.SizeChanged += (s, e) =>
            {
                RebuildColHeaders();
                ResizeSlotRows();
            };

            // Docking order: last added DockStyle.Top = topmost.
            Controls.Add(_slotsContainer);      // Fill — added first
            Controls.Add(_colHeadersPanel);     // Top — 3rd from top
            Controls.Add(_controlBar);          // Top — 2nd from top
            Controls.Add(_footerPanel);         // Bottom
            Controls.Add(_headerPanel);         // Top — topmost
        }

        // ── Column headers ────────────────────────────────────────────────────
        private void RebuildColHeaders()
        {
            _colHeadersPanel.Controls.Clear();

            // Item-table rows carry their own inline "Employee"/"Title / Designation" headers
            // (added per-row in AddSlot) because each row's picker starts at a different X than a
            // plain role row's does — one shared global header row can't align with both layouts.
            if (_roleItemRows != null && _roleItemRows.Count > 0) return;

            int totalW = _slotsContainer.ClientSize.Width - SystemInformation.VerticalScrollBarWidth - 4;
            if (totalW < 80) return;

            int originX = 0;
            if (RoleMode)
            {
                _colHeadersPanel.Controls.Add(new Label
                {
                    Text      = "Role",
                    Font      = new Font("Segoe UI", 7.5f, FontStyle.Bold),
                    ForeColor = Color.FromArgb(100, 116, 139),
                    Location  = new Point(0, 4),
                    Size      = new Size(RoleLabelW - 6, 20)
                });
                originX = RoleLabelW;
                totalW -= RoleLabelW;
                if (totalW < 80) return;
            }

            int slotW  = totalW / _columnsPerRow;
            int nameW  = (int)(slotW * 0.48);
            int titleW = slotW - nameW - 6;

            for (int c = 0; c < _columnsPerRow; c++)
            {
                string sigLabel = RoleMode ? "Employee"
                                : _columnsPerRow == 1 ? "Signature"
                                : _columnsPerRow == 2 ? (c == 0 ? "Left Signature" : "Right Signature")
                                : $"Signature {c + 1}";

                int x = originX + c * slotW;
                _colHeadersPanel.Controls.Add(new Label
                {
                    Text      = sigLabel,
                    Font      = new Font("Segoe UI", 7.5f, FontStyle.Bold),
                    ForeColor = Color.FromArgb(100, 116, 139),
                    Location  = new Point(x, 4),
                    Size      = new Size(nameW, 20)
                });
                _colHeadersPanel.Controls.Add(new Label
                {
                    Text      = "Title / Designation",
                    Font      = new Font("Segoe UI", 7.5f, FontStyle.Bold),
                    ForeColor = Color.FromArgb(100, 116, 139),
                    Location  = new Point(x + nameW + 6, 4),
                    Size      = new Size(titleW, 20)
                });
            }
        }

        private void ResizeSlotRows()
        {
            int fullW = _slotsContainer.ClientSize.Width - SystemInformation.VerticalScrollBarWidth - 4;
            if (fullW < 80) return;

            foreach (var row in _slotRows)
            {
                row.Container.Width = fullW;
                // Role/hint label heights, and the item table's own column layout (if any), are
                // computed once in AddSlot from their actual content extent at that width — only the
                // Employee/Title picker below them re-flows here, using this row's own origin (a
                // table row starts near the left edge; a plain role row starts after the role-label
                // column), since different rows can use different layouts.
                int originX = row.PickerOriginX;
                int totalW  = row.Container.Width - originX;
                if (totalW < 80) continue;

                int slotW  = totalW / _columnsPerRow;
                int nameW  = (int)(slotW * 0.48);
                int titleW = slotW - nameW - 6;

                for (int c = 0; c < row.Slots.Count && c < _columnsPerRow; c++)
                {
                    int x = originX + c * slotW;
                    row.Slots[c].NameBox.Location  = new Point(x, row.PickerY);
                    row.Slots[c].NameBox.Width     = nameW;
                    row.Slots[c].TitleBox.Location = new Point(x + nameW + 6, row.PickerY);
                    row.Slots[c].TitleBox.Width    = titleW;
                }
            }
        }

        // ── Load employees ────────────────────────────────────────────────────
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
                            _employees.Add(new Employee
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
                Logger.LogError("SignatoryPickerDialog: failed to load employees", ex);
                MessageBox.Show("Could not load employee list.\n" + ex.Message,
                    "Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        // ── Slot / row management ─────────────────────────────────────────────

        /// <summary>
        /// Rebuilds all slots when the column count changes, preserving entered data.
        /// Each existing slot is placed into the new column layout in order.
        /// </summary>
        private void RebuildAllRows()
        {
            // Flatten all existing slot data
            var saved = new List<(string name, string title)>();
            foreach (var row in _slotRows)
                foreach (var slot in row.Slots)
                {
                    string n = slot.NameBox.Text.Trim();
                    saved.Add((n, slot.TitleBox.Text.Trim()));
                }

            _slotsContainer.Controls.Clear();
            _slotRows.Clear();

            // Re-add one slot per saved entry; at least one slot
            int count = Math.Max(1, saved.Count);
            for (int i = 0; i < count; i++)
                AddSlot(i < saved.Count ? saved[i].name  : null,
                        i < saved.Count ? saved[i].title : null);

            RebuildColHeaders();
        }

        /// <summary>
        /// Adds one signatory slot. Fills the current row until it reaches
        /// _columnsPerRow, then starts a new row automatically.
        /// </summary>
        private void AddSlot(string existingName = null, string existingTitle = null, string role = null)
        {
            // Determine which row to append to. In role mode every row is its own role, so a new
            // row is always started rather than packing slots side by side.
            SlotRow targetRow;
            if (RoleMode || _slotRows.Count == 0 || _slotRows[_slotRows.Count - 1].Slots.Count >= _columnsPerRow)
            {
                // Need a new row
                int totalW = _slotsContainer.ClientSize.Width - SystemInformation.VerticalScrollBarWidth - 4;
                if (totalW < 80) totalW = 860;

                // In role mode, rows are variable-height — a hint can be a multi-line "which item(s)
                // this role is for" list, and a role can carry a full item table, so the row must
                // grow to fit whichever it has rather than clipping to a fixed 46px. Grid mode
                // (non-role) has neither and keeps the original fixed height.
                int rowY = RoleMode ? _slotRows.Sum(r => r.Container.Height + 4) : _slotRows.Count * 50;
                int rowHeight = 46;

                string hint = null;
                if (RoleMode) _roleHints?.TryGetValue(role ?? "", out hint);

                List<string[]> tableRows = null;
                bool hasTable = RoleMode && _roleItemRows != null
                    && _roleItemRows.TryGetValue(role ?? "", out tableRows) && tableRows.Count > 0;

                var hintFont = new Font("Segoe UI", 7.5f, FontStyle.Italic);

                var container = new Panel
                {
                    Location  = new Point(0, rowY),
                    BackColor = _slotRows.Count % 2 == 0 ? Color.White : Color.FromArgb(248, 250, 252)
                };
                targetRow = new SlotRow { Container = container, Role = role, HasItemTable = hasTable };

                if (RoleMode && hasTable)
                {
                    // ── Item-table layout: role/hint on top, a real Item/Category/Problem/Status
                    // table below it (so it's unambiguous which item(s) this signature covers), then
                    // the Employee/Title picker full-width underneath the table. ──
                    int y = 6;
                    int fullW = totalW - 8;

                    targetRow.RoleLabel = new Label
                    {
                        Text = role, Font = new Font("Segoe UI", 9f, FontStyle.Bold),
                        ForeColor = Color.FromArgb(30, 41, 59),
                        Location = new Point(4, y), AutoSize = true,
                        MaximumSize = new Size(fullW, 0)
                    };
                    container.Controls.Add(targetRow.RoleLabel);
                    _roleToolTip.SetToolTip(targetRow.RoleLabel, role);
                    y += 22;

                    if (!string.IsNullOrWhiteSpace(hint))
                    {
                        targetRow.RoleHintLabel = new Label
                        {
                            Text = hint, Font = hintFont, ForeColor = Color.FromArgb(100, 116, 139),
                            Location = new Point(4, y), Size = new Size(fullW, 15),
                            TextAlign = ContentAlignment.MiddleLeft, AutoEllipsis = true
                        };
                        container.Controls.Add(targetRow.RoleHintLabel);
                        _roleToolTip.SetToolTip(targetRow.RoleHintLabel, hint);
                        y += 17;
                    }
                    y += 4;

                    // Columns: Item | Category | Reported Problem | Status — rendered as a real
                    // table: the app's standard blue column-header band (same #3498DB used by every
                    // DataGridView header in this app, e.g. DashboardView's grids) plus bordered
                    // cells and row/column grid lines, instead of floating unbordered text.
                    int[] colW = { (int)(fullW * 0.32), (int)(fullW * 0.16), (int)(fullW * 0.34), 0 };
                    colW[3] = fullW - colW[0] - colW[1] - colW[2];
                    string[] headers = { "Item", "Category", "Reported Problem", "Status" };
                    var headerFont = new Font("Segoe UI", 7.5f, FontStyle.Bold);
                    var tableHeaderFont = new Font("Segoe UI", 8.5f, FontStyle.Bold);
                    var cellFont   = new Font("Segoe UI", 8f);

                    var tableTop = y;
                    var headerBand = new Panel
                    {
                        Location = new Point(4, y), Size = new Size(fullW, 22),
                        // Matches the Repair Reports list window's own DataGrid header exactly
                        // (ListDataGridColumnHeader style, PrimaryBrush #4E9AFC) — same blue the
                        // user pointed at, not the dialog's own AccentBlue action-button color.
                        BackColor = Color.FromArgb(0x4E, 0x9A, 0xFC)
                    };
                    container.Controls.Add(headerBand);
                    int cx = 0;
                    for (int c = 0; c < 4; c++)
                    {
                        headerBand.Controls.Add(new Label
                        {
                            Text = headers[c], Font = tableHeaderFont, ForeColor = Color.White,
                            BackColor = Color.Transparent,
                            Location = new Point(cx + 6, 0), Size = new Size(colW[c] - 6, 22),
                            TextAlign = ContentAlignment.MiddleLeft
                        });
                        cx += colW[c];
                    }
                    y += 22;

                    for (int ri = 0; ri < tableRows.Count; ri++)
                    {
                        var row = tableRows[ri];
                        int rh = 18;
                        cx = 0;
                        var cellLabels = new Label[4];
                        for (int c = 0; c < 4; c++)
                        {
                            string text = c < row.Length ? row[c] ?? "" : "";
                            var measured = TextRenderer.MeasureText(text, cellFont, new Size(colW[c] - 12, 0),
                                TextFormatFlags.WordBreak | TextFormatFlags.TextBoxControl);
                            rh = Math.Max(rh, measured.Height + 8);
                            cellLabels[c] = new Label
                            {
                                Text = text, Font = cellFont, ForeColor = Color.FromArgb(0x1A, 0x23, 0x33),
                                Location = new Point(4 + cx + 6, y), Size = new Size(colW[c] - 12, 0),
                                TextAlign = ContentAlignment.TopLeft, AutoSize = false
                            };
                            cx += colW[c];
                        }
                        // Row background band (alternating). SendToBack() makes the stacking order
                        // explicit rather than relying on WinForms' add-order z-index behavior — this
                        // panel MUST render behind the cell labels or it blanks the text out entirely.
                        var rowBand = new Panel
                        {
                            Location = new Point(4, y - 4), Size = new Size(fullW, rh + 4),
                            BackColor = (ri % 2 == 0) ? Color.White : Color.FromArgb(0xFA, 0xFB, 0xFC)
                        };
                        container.Controls.Add(rowBand);
                        rowBand.SendToBack();
                        foreach (var lbl in cellLabels) { lbl.Height = rh; container.Controls.Add(lbl); }
                        y += rh + 4;
                        container.Controls.Add(new Panel
                        {
                            Location = new Point(4, y - 1), Size = new Size(fullW, 1),
                            BackColor = Color.FromArgb(226, 232, 240)
                        });
                    }

                    // Vertical column separators spanning the whole table (header + all rows).
                    int tableBottom = y;
                    cx = 0;
                    for (int c = 0; c < 3; c++)
                    {
                        cx += colW[c];
                        container.Controls.Add(new Panel
                        {
                            Location = new Point(4 + cx, tableTop), Size = new Size(1, tableBottom - tableTop),
                            BackColor = Color.FromArgb(226, 232, 240)
                        });
                    }
                    y += 6;

                    // Inline picker headers — the global _colHeadersPanel is hidden in table mode
                    // (see RebuildColHeaders) since each row's picker starts at a different X.
                    // Height/TextAlign matter here: 14px clipped these labels' descenders under the
                    // default Segoe UI line metrics — 18px with MiddleLeft renders them cleanly.
                    int pickerSlotW = fullW / _columnsPerRow;
                    int pickerNameW = (int)(pickerSlotW * 0.48);
                    container.Controls.Add(new Label
                    {
                        Text = "Employee", Font = headerFont, ForeColor = Color.FromArgb(100, 116, 139),
                        Location = new Point(4, y), Size = new Size(pickerNameW, 18),
                        TextAlign = ContentAlignment.MiddleLeft
                    });
                    container.Controls.Add(new Label
                    {
                        Text = "Title / Designation", Font = headerFont, ForeColor = Color.FromArgb(100, 116, 139),
                        Location = new Point(4 + pickerNameW + 6, y), Size = new Size(pickerSlotW - pickerNameW - 6, 18),
                        TextAlign = ContentAlignment.MiddleLeft
                    });
                    y += 19;

                    targetRow.PickerOriginX = 4;
                    targetRow.PickerY       = y;
                    rowHeight = y + 26 + 10;   // picker row height (26) + bottom margin
                }
                else if (RoleMode)
                {
                    int hintHeight = 0;
                    if (!string.IsNullOrWhiteSpace(hint))
                    {
                        var measured = TextRenderer.MeasureText(hint, hintFont, new Size(RoleLabelW - 8, 0),
                            TextFormatFlags.WordBreak | TextFormatFlags.TextBoxControl);
                        hintHeight = measured.Height + 4;
                        rowHeight = Math.Max(rowHeight, 24 + hintHeight + 6);
                    }

                    targetRow.RoleLabel = new Label
                    {
                        Text        = role,
                        Font        = new Font("Segoe UI", 9f, FontStyle.Bold),
                        ForeColor   = Color.FromArgb(30, 41, 59),
                        Location    = new Point(4, string.IsNullOrWhiteSpace(hint) ? 10 : 2),
                        // AutoSize (not a fixed pixel height) so the label always fits the text at
                        // the current DPI — a fixed height clipped the "y" descender in "Prepared By"
                        // etc. once Windows display scaling pushed the bold line metrics past it.
                        AutoSize    = true,
                        MaximumSize = new Size(RoleLabelW - 8, 0)
                    };
                    container.Controls.Add(targetRow.RoleLabel);
                    _roleToolTip.SetToolTip(targetRow.RoleLabel, role);

                    if (!string.IsNullOrWhiteSpace(hint))
                    {
                        targetRow.RoleHintLabel = new Label
                        {
                            Text      = hint,
                            Font      = hintFont,
                            ForeColor = Color.FromArgb(100, 116, 139),
                            Location  = new Point(4, 24),
                            Size      = new Size(RoleLabelW - 8, hintHeight),
                            TextAlign = ContentAlignment.TopLeft,
                            AutoSize  = false
                        };
                        container.Controls.Add(targetRow.RoleHintLabel);
                        _roleToolTip.SetToolTip(targetRow.RoleHintLabel, hint);
                    }

                    targetRow.PickerOriginX = RoleLabelW;
                    targetRow.PickerY       = 8;
                }
                else
                {
                    targetRow.PickerOriginX = 0;
                    targetRow.PickerY       = 8;
                }

                // Divider under this row — a thin line normally, or an obvious thicker/darker bar
                // when this role ends a requester's section (roleSectionBreakAfter), so one item
                // set's details don't visually blend into the next requester's.
                bool obviousBreak = RoleMode && _roleSectionBreakAfter != null && _roleSectionBreakAfter.Contains(role ?? "");
                int dividerH  = obviousBreak ? 3 : 1;
                int dividerPad = obviousBreak ? 10 : 4;
                rowHeight += dividerPad + dividerH;
                container.Size = new Size(totalW, rowHeight);
                container.Controls.Add(new Panel
                {
                    Location  = new Point(0, rowHeight - dividerH),
                    Size      = new Size(totalW, dividerH),
                    BackColor = obviousBreak ? Color.FromArgb(30, 41, 59) : Color.FromArgb(226, 232, 240)
                });

                _slotRows.Add(targetRow);
                _slotsContainer.Controls.Add(container);
            }
            else
            {
                targetRow = _slotRows[_slotRows.Count - 1];
            }

            // Build slot dimensions
            int originX    = targetRow.PickerOriginX;
            int totalWidth = targetRow.Container.Width - originX;
            int slotW      = totalWidth / _columnsPerRow;
            int nameW      = (int)(slotW * 0.48);
            int titleW     = slotW - nameW - 6;
            int colIdx     = targetRow.Slots.Count;
            int x          = originX + colIdx * slotW;
            int pickerY    = targetRow.PickerY;

            var nameBox = new TextBox
            {
                Location    = new Point(x, pickerY),
                Size        = new Size(nameW, 26),
                Font        = new Font("Segoe UI", 9f)
            };

            var titleBox = new TextBox
            {
                Location = new Point(x + nameW + 6, pickerY),
                Size     = new Size(titleW, 26),
                Font     = new Font("Segoe UI", 9f),
                Text     = existingTitle ?? ""
            };

            var slotCtrl = new SlotControl { NameBox = nameBox, TitleBox = titleBox };

            // This slot's candidate pool — the full employee list, unless a DeptId filter was
            // supplied for this role, in which case only that department's employees are offered
            // (falling back to everyone if the filter would otherwise leave zero candidates, so a
            // misconfigured/empty department never produces a dead-end picker).
            List<Employee> candidates = _employees;
            if (RoleMode && _roleDeptIds != null && _roleDeptIds.TryGetValue(role ?? "", out var filterDeptId) && filterDeptId.HasValue)
            {
                var filtered = _employees.Where(emp => emp.DeptId == filterDeptId.Value).ToList();
                if (filtered.Count > 0) candidates = filtered;
            }

            // Borderless floating window — always positioned below the nameBox,
            // completely independent of the parent form's layout so it never
            // causes the dialog to resize or blink.
            var listBox = new ListBox
            {
                Dock           = DockStyle.Fill,
                BorderStyle    = BorderStyle.None,
                Font           = new Font("Segoe UI", 9f),
                ItemHeight     = 22,
                IntegralHeight = false
            };
            var dropWin = new DropdownWindow
            {
                FormBorderStyle = FormBorderStyle.None,
                ShowInTaskbar   = false,
                StartPosition   = FormStartPosition.Manual,
                AutoSize        = false,
                BackColor       = Color.White
            };
            dropWin.Controls.Add(listBox);
            // Dispose the floating window when the dialog closes
            this.FormClosed += (s, e) => { if (!dropWin.IsDisposed) dropWin.Dispose(); };

            // Set initial name without triggering the filter popup
            if (!string.IsNullOrWhiteSpace(existingName))
            {
                foreach (var emp in candidates)
                {
                    if (emp.Name == existingName)
                    {
                        slotCtrl.SelectedEmployee = emp;
                        nameBox.Text = emp.Name;
                        if (string.IsNullOrWhiteSpace(titleBox.Text))
                            titleBox.Text = emp.Position;
                        break;
                    }
                }
                if (slotCtrl.SelectedEmployee == null)
                    nameBox.Text = existingName;
            }

            bool settingFromList = false;

            void HidePopup() { if (!dropWin.IsDisposed && dropWin.Visible) dropWin.Hide(); }

            // A popup is a top-level window in screen coordinates, so it does not travel with the
            // dialog. Close it on any move/resize rather than letting it float free.
            _hideAllPopups += HidePopup;

            // Amber tint = a name that isn't a known employee. Free text is still allowed (the
            // report renders whatever is typed), but it silently loses the Title-code prefix, so
            // the box makes that visible instead of letting it pass unnoticed.
            void RefreshValidity()
            {
                bool typedFreeText = slotCtrl.SelectedEmployee == null
                                     && !string.IsNullOrWhiteSpace(nameBox.Text);
                nameBox.BackColor = typedFreeText ? WarnBg : Color.White;
            }
            slotCtrl.RefreshValidity = RefreshValidity;

            void ShowPopup(string typed)
            {
                // Never open while the form is still being laid out. The first textbox receives
                // focus during Load, when the dialog has not been positioned/maximized yet — the
                // popup would be placed against stale screen coordinates and stranded there.
                if (!_uiReady || nameBox.IsDisposed || !nameBox.IsHandleCreated) return;

                listBox.Items.Clear();

                if (string.IsNullOrEmpty(typed))
                {
                    // Empty box: browse instead of forcing the user to guess a first letter.
                    // Recently-used signatories float to the top, then the full employee list.
                    var recentNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    foreach (var r in ReportPreferences.GetRecentSignatories())
                    {
                        var match = candidates.Find(emp =>
                            string.Equals(emp.Name, r.Name, StringComparison.OrdinalIgnoreCase));
                        if (match != null && recentNames.Add(match.Name))
                            listBox.Items.Add(match);
                    }
                    foreach (var emp in candidates)
                        if (!recentNames.Contains(emp.Name))
                            listBox.Items.Add(emp);
                }
                else
                {
                    // Match on name, position and title code — searching "Manager" or "ENGR"
                    // is often how people actually remember who signs a report.
                    foreach (var emp in candidates)
                        if (Contains(emp.Name, typed) || Contains(emp.Position, typed) || Contains(emp.TitleCode, typed))
                            listBox.Items.Add(emp);
                }

                if (listBox.Items.Count == 0) { HidePopup(); return; }

                int visible  = Math.Min(listBox.Items.Count, 10);
                int listH    = visible * listBox.ItemHeight + 2;
                // Size from the box's CURRENT width, not the width captured when the slot was
                // built — the dialog is resizable, so that snapshot goes stale on the first resize
                // and clipped the employee names.
                int listW    = Math.Max(nameBox.Width, 260);
                dropWin.Size = new Size(listW, listH);

                // Anchor under the box, then nudge back on-screen if it would fall off the bottom
                // or right edge of the monitor the dialog is actually on.
                var screenPt = nameBox.PointToScreen(new Point(0, nameBox.Height + 1));
                var wa       = Screen.FromControl(nameBox).WorkingArea;
                if (screenPt.Y + listH > wa.Bottom)
                    screenPt.Y = nameBox.PointToScreen(Point.Empty).Y - listH - 1;
                if (screenPt.X + listW > wa.Right)
                    screenPt.X = Math.Max(wa.Left, wa.Right - listW);
                dropWin.Location = screenPt;

                if (!dropWin.Visible)
                    dropWin.Show(this);   // owned by the dialog — correct z-order, no taskbar entry
            }

            void CommitSelection(Employee emp)
            {
                settingFromList           = true;
                slotCtrl.SelectedEmployee = emp;
                nameBox.Text              = emp.Name;
                settingFromList           = false;
                if (string.IsNullOrWhiteSpace(titleBox.Text))
                    titleBox.Text = emp.Position;
                HidePopup();
                RefreshValidity();
                UpdateValidationSummary();
                nameBox.Focus();
                nameBox.SelectionStart = nameBox.Text.Length;
            }

            nameBox.TextChanged += (s, e) =>
            {
                if (settingFromList) return;
                // Re-resolve against the employee list so typing a name in full (or restoring a
                // remembered one) still counts as a real selection rather than free text.
                slotCtrl.SelectedEmployee = candidates.Find(emp =>
                    string.Equals(emp.Name, nameBox.Text.Trim(), StringComparison.OrdinalIgnoreCase));
                RefreshValidity();
                UpdateValidationSummary();
                ShowPopup(nameBox.Text);
            };

            // Clicking or tabbing into an empty box opens the browsable list.
            nameBox.Enter += (s, e) => ShowPopup(nameBox.Text);
            nameBox.Click += (s, e) => { if (!dropWin.Visible) ShowPopup(nameBox.Text); };
            titleBox.TextChanged += (s, e) => UpdateValidationSummary();

            nameBox.Leave += (s, e) =>
            {
                // Short delay so listBox.Click fires before we hide
                var t = new System.Windows.Forms.Timer { Interval = 150 };
                t.Tick += (s2, e2) => { t.Stop(); t.Dispose(); if (!listBox.Focused) HidePopup(); };
                t.Start();
            };

            nameBox.KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.Down && dropWin.Visible && listBox.Items.Count > 0)
                {
                    listBox.Focus();
                    listBox.SelectedIndex = 0;
                    e.Handled = true;
                }
                else if (e.KeyCode == Keys.Escape && dropWin.Visible)
                {
                    HidePopup();
                    e.Handled = true;
                }
            };

            listBox.Click += (s, e) =>
            {
                if (listBox.SelectedItem is Employee emp) CommitSelection(emp);
            };

            listBox.KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.Enter && listBox.SelectedItem is Employee emp)
                { CommitSelection(emp); e.Handled = true; }
                else if (e.KeyCode == Keys.Escape)
                { HidePopup(); nameBox.Focus(); e.Handled = true; }
            };

            targetRow.Container.Controls.Add(nameBox);
            targetRow.Container.Controls.Add(titleBox);
            targetRow.Slots.Add(slotCtrl);

            // The pre-fill above ran before the handlers were wired, so paint the initial state now.
            RefreshValidity();
            UpdateValidationSummary();

            _slotsContainer.AutoScrollMinSize = RoleMode
                ? new Size(0, _slotRows.Sum(r => r.Container.Height + 4))
                : new Size(0, _slotRows.Count * 50);
        }

        // ── Validation ────────────────────────────────────────────────────────

        private IEnumerable<SlotControl> AllSlots()
        {
            foreach (var row in _slotRows)
                foreach (var slot in row.Slots)
                    yield return slot;
        }

        /// <summary>
        /// Recomputes the footer warning strip and the per-box duplicate tint. Warnings only —
        /// free-typed names and repeats are still allowed, because the report has always accepted
        /// them; this just stops them going out unnoticed.
        /// </summary>
        private void UpdateValidationSummary()
        {
            if (_validationLabel == null) return;

            var filled = new List<SlotControl>();
            foreach (var s in AllSlots())
                if (!string.IsNullOrWhiteSpace(s.NameBox.Text)) filled.Add(s);

            var counts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (var s in filled)
            {
                string n = s.NameBox.Text.Trim();
                counts[n] = counts.ContainsKey(n) ? counts[n] + 1 : 1;
            }

            var unknown = new List<string>();
            var dupes   = new List<string>();
            foreach (var s in filled)
            {
                string n = s.NameBox.Text.Trim();
                bool isDupe = counts[n] > 1;
                // In role mode the same person legitimately signs more than one role, so a repeat
                // there is not a mistake — only flag repeats within a single role's slot list.
                if (isDupe && !RoleMode && !dupes.Contains(n)) dupes.Add(n);
                if (s.SelectedEmployee == null && !unknown.Contains(n)) unknown.Add(n);
                s.NameBox.BackColor = (isDupe && !RoleMode) ? Color.FromArgb(254, 235, 235)
                                    : s.SelectedEmployee == null ? WarnBg
                                    : Color.White;
            }

            var parts = new List<string>();
            if (dupes.Count > 0)
                parts.Add("Repeated: " + string.Join(", ", dupes.ToArray()));
            if (unknown.Count > 0)
                parts.Add((unknown.Count == 1 ? "Not a listed employee: " : "Not listed employees: ")
                          + string.Join(", ", unknown.ToArray()) + " (no title prefix will print)");

            _validationLabel.Text = parts.Count == 0 ? "" : string.Join("   •   ", parts.ToArray());
        }

        /// <summary>
        /// Removes the last signatory slot. If that empties a row, the row is also removed.
        /// At least one slot is always kept.
        /// </summary>
        private void RemoveLastSlot()
        {
            // Count total slots
            int total = 0;
            foreach (var r in _slotRows) total += r.Slots.Count;
            if (total <= 1) return;

            var lastRow  = _slotRows[_slotRows.Count - 1];
            var lastSlot = lastRow.Slots[lastRow.Slots.Count - 1];

            lastRow.Container.Controls.Remove(lastSlot.NameBox);
            lastRow.Container.Controls.Remove(lastSlot.TitleBox);
            lastRow.Slots.RemoveAt(lastRow.Slots.Count - 1);

            if (lastRow.Slots.Count == 0)
            {
                _slotsContainer.Controls.Remove(lastRow.Container);
                _slotRows.RemoveAt(_slotRows.Count - 1);
            }

            _slotsContainer.AutoScrollMinSize = new Size(0, _slotRows.Count * 50);
        }

        // ── OK ────────────────────────────────────────────────────────────────
        private void OkButton_Click(object sender, EventArgs e)
        {
            UpdateValidationSummary();
            if (!string.IsNullOrEmpty(_validationLabel.Text))
            {
                var answer = MessageBox.Show(
                    _validationLabel.Text.Replace("   •   ", Environment.NewLine + Environment.NewLine)
                        + Environment.NewLine + Environment.NewLine + "Generate the report anyway?",
                    "Check signatories",
                    MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2);
                if (answer != DialogResult.Yes) return;
            }

            if (RoleMode)
            {
                RoleSignatoryData = new Dictionary<string, DataTable>(StringComparer.OrdinalIgnoreCase);
                foreach (var row in _slotRows)
                {
                    RoleSignatoryData[row.Role] = BuildSignatoryTable(row.Slots);
                    ReportPreferences.SetSignatories(row.Role, ToEntries(row.Slots));
                }
                // Keep the flat property populated too, so a caller that only reads SignatoryData
                // still gets the first role rather than null.
                SignatoryData = _slotRows.Count > 0 ? RoleSignatoryData[_slotRows[0].Role] : BuildSignatoryTable();
            }
            else
            {
                SignatoryData = BuildSignatoryTable();
                ReportPreferences.SetSignatories(_memoryKey, ToEntries(AllSlots()));
            }

            DialogResult = DialogResult.OK;
            Close();
        }

        private static List<ReportPreferences.SignatoryEntry> ToEntries(IEnumerable<SlotControl> slots)
        {
            var list = new List<ReportPreferences.SignatoryEntry>();
            foreach (var s in slots)
            {
                string name = s.SelectedEmployee != null ? s.SelectedEmployee.Name : s.NameBox.Text.Trim();
                if (string.IsNullOrWhiteSpace(name)) continue;
                list.Add(new ReportPreferences.SignatoryEntry { Name = name, Title = s.TitleBox.Text.Trim() });
            }
            return list;
        }

        /// <summary>
        /// Flattens all signatory slots (across all rows and columns) into the
        /// established pair-based DataTable schema so existing RDLC reports keep working.
        /// </summary>
        private DataTable BuildSignatoryTable() => BuildSignatoryTable(AllSlots());

        private DataTable BuildSignatoryTable(IEnumerable<SlotControl> slots)
        {
            var dt = new DataTable("SignatoryData");
            dt.Columns.Add("PairIndex",   typeof(int));
            dt.Columns.Add("LeftName",    typeof(string));
            dt.Columns.Add("LeftTitle",   typeof(string));
            dt.Columns.Add("RightName",   typeof(string));
            dt.Columns.Add("RightTitle",  typeof(string));
            dt.Columns.Add("LeftPrefix",  typeof(string));
            dt.Columns.Add("RightPrefix", typeof(string));

            // Collect the supplied slots in order (row-major)
            var all = new List<(string name, string title, string prefix)>();
            foreach (var slot in slots)
            {
                string name   = "";
                string prefix = "";
                if (slot.SelectedEmployee != null)
                {
                    name   = slot.SelectedEmployee.Name;
                    prefix = slot.SelectedEmployee.TitleCode ?? "";
                }
                else
                {
                    name = slot.NameBox.Text.Trim();
                }
                all.Add((name, slot.TitleBox.Text.Trim(), prefix));
            }

            // Pair consecutive entries (Left = even index, Right = odd index)
            int pairIdx = 0;
            for (int i = 0; i < all.Count; i += 2)
            {
                string leftName   = all[i].name;
                string leftTitle  = all[i].title;
                string leftPrefix = all[i].prefix;
                string rightName   = i + 1 < all.Count ? all[i + 1].name   : "";
                string rightTitle  = i + 1 < all.Count ? all[i + 1].title  : "";
                string rightPrefix = i + 1 < all.Count ? all[i + 1].prefix : "";

                // Skip entirely blank pairs
                if (string.IsNullOrWhiteSpace(leftName) && string.IsNullOrWhiteSpace(rightName))
                    continue;

                dt.Rows.Add(pairIdx++, leftName, leftTitle, rightName, rightTitle, leftPrefix, rightPrefix);
            }

            return dt;
        }
    }
}
