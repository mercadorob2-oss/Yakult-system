using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using Yakult.Inventory.App.Helpers;
using Yakult.Inventory.App.Repositories;
using Yakult.Inventory.App.Session;
using Yakult.Inventory.App.Services;
using HopeButton = ReaLTaiizor.Controls.HopeButton;

namespace Yakult.Inventory.App.Pages.Admin.ReferenceData
{
    public partial class HolidayManagementPage : UserControl
    {
        // ── Color palette ─────────────────────────────────────────────
        private static readonly Color RegularFore    = Color.FromArgb(180, 30, 20);
        private static readonly Color RegularBack    = Color.FromArgb(255, 245, 244);
        private static readonly Color SpecialFore    = Color.FromArgb(25, 90, 160);
        private static readonly Color SpecialBack    = Color.FromArgb(243, 248, 255);
        private static readonly Color InactiveFore   = Color.FromArgb(160, 160, 160);
        private static readonly Color InactiveBack   = Color.FromArgb(250, 250, 250);
        private static readonly Color SidebarBack    = Color.White;
        private static readonly Color SidebarAccent  = Color.FromArgb(52, 152, 219);
        private static readonly Color CardBack       = Color.FromArgb(242, 246, 250);
        private static readonly Color BodyBack       = Color.FromArgb(245, 247, 250);

        private readonly HolidayRepository _repo = new HolidayRepository();

        // Controls
        private MonthCalendar _calendar;
        private DataGridView  _dgv;
        private TextBox       _txtSearch;
        private ComboBox      _cboFilter;
        private ComboBox      _cboYear;
        private HopeButton    _btnAdd, _btnEdit, _btnDelete, _btnRefresh;
        private Label         _lblCount;
        private Label         _lblEmptyState;
        private Label         _lblDetailName, _lblDetailMeta, _lblDetailNotes;
        private Label         _lblStatTotal, _lblStatRegular, _lblStatSpecial, _lblStatUpcoming;
        private Panel         _detailsPanel;

        // Data
        private List<HolidayDto> _all      = new List<HolidayDto>();
        private List<HolidayDto> _filtered = new List<HolidayDto>();
        private bool             _suppressCalendarSync;

        public HolidayManagementPage()
        {
            InitializeComponent();
            BuildUI();
            LoadData();
        }

        private void InitializeComponent() { }

        // ═══════════════════════════════════════════════════════════════
        //  UI BUILD
        // ═══════════════════════════════════════════════════════════════

        private void BuildUI()
        {
            SuspendLayout();
            Dock      = DockStyle.Fill;
            BackColor = BodyBack;

            // ── RIGHT PANEL ───────────────────────────────────────────
            var rightPanel = new Panel { Dock = DockStyle.Fill, BackColor = BodyBack, Padding = new Padding(0) };

            _detailsPanel = BuildDetailsPanel();
            rightPanel.Controls.Add(_detailsPanel);

            var gridWrapper = new Panel { Dock = DockStyle.Fill, BackColor = Color.White, Margin = new Padding(12, 0, 12, 0) };
            gridWrapper.Paint += PaintBorder;

            _lblEmptyState = new Label
            {
                Text      = "No holidays found for this selection.\nClick  + Add  to create a new holiday.",
                Font      = new Font("Segoe UI", 11F),
                ForeColor = Color.FromArgb(190, 190, 190),
                TextAlign = ContentAlignment.MiddleCenter,
                Dock      = DockStyle.Fill,
                Visible   = false
            };

            _dgv = BuildGrid();
            gridWrapper.Controls.Add(_dgv);
            gridWrapper.Controls.Add(_lblEmptyState);
            rightPanel.Controls.Add(gridWrapper);

            rightPanel.Controls.Add(BuildToolbar());
            rightPanel.Controls.Add(BuildFilterBar());

            // ── LEFT SIDEBAR ──────────────────────────────────────────
            var sidebar = new Panel { Dock = DockStyle.Left, Width = 340, BackColor = SidebarBack };

            var statsPanel = BuildStatsPanel();
            sidebar.Controls.Add(statsPanel);

            sidebar.Controls.Add(BuildLegendPanel());
            sidebar.Controls.Add(BuildCalendarSection());
            sidebar.Controls.Add(BuildSidebarHeader());

            // 1px divider
            var divider = new Panel { Dock = DockStyle.Left, Width = 1, BackColor = Color.FromArgb(218, 224, 230) };

            Controls.Add(rightPanel);
            Controls.Add(divider);
            Controls.Add(sidebar);

            ResumeLayout(false);
        }

        private Panel BuildSidebarHeader()
        {
            var pnl = new Panel { Dock = DockStyle.Top, Height = 58, BackColor = SidebarAccent };

            pnl.Controls.Add(new Label
            {
                Text      = "Company Holidays",
                Font      = new Font("Segoe UI", 13F, FontStyle.Bold),
                ForeColor = Color.White,
                Dock      = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding   = new Padding(18, 0, 0, 0)
            });
            return pnl;
        }

        private Panel BuildCalendarSection()
        {
            var outer = new Panel { Dock = DockStyle.Top, BackColor = SidebarBack, Padding = new Padding(8, 6, 8, 4) };

            var sectionLabel = new Label
            {
                Text      = "CALENDAR",
                Font      = new Font("Segoe UI", 7.5F, FontStyle.Bold),
                ForeColor = UiTheme.Colors.TextMuted,
                Dock      = DockStyle.Top,
                Height    = 22
            };

            _calendar = new MonthCalendar
            {
                MaxSelectionCount  = 1,
                ShowTodayCircle    = true,
                ShowWeekNumbers    = false,
                CalendarDimensions = new Size(1, 2),
                Dock               = DockStyle.Top,
                TitleBackColor     = SidebarAccent,
                TitleForeColor     = Color.White
            };
            _calendar.DateSelected    += Calendar_DateSelected;
            _calendar.MouseDoubleClick += (s, e) => BtnAdd_Click(s, e);

            outer.Controls.Add(_calendar);
            outer.Controls.Add(sectionLabel);

            // Fit panel height to calendar after layout
            outer.HandleCreated += (s, e) =>
                BeginInvoke(new Action(() => outer.Height = _calendar.Height + 34));

            return outer;
        }

        private Panel BuildLegendPanel()
        {
            var pnl = new Panel { Dock = DockStyle.Top, Height = 64, BackColor = CardBack, Padding = new Padding(18, 10, 18, 10) };

            MakeLegendRow(pnl, "Regular Holiday",        Color.FromArgb(231, 76, 60),  new Point(18, 12));
            MakeLegendRow(pnl, "Special Non-Working",    Color.FromArgb(52, 152, 219), new Point(18, 36));
            return pnl;
        }

        private static void MakeLegendRow(Panel parent, string text, Color dot, Point loc)
        {
            var dotLabel = new Label
            {
                Text      = "⬤",
                Font      = new Font("Segoe UI", 8F),
                ForeColor = dot,
                AutoSize  = true,
                Location  = loc
            };
            var textLabel = new Label
            {
                Text      = text,
                Font      = new Font("Segoe UI", 8.5F),
                ForeColor = UiTheme.Colors.TextDark,
                AutoSize  = true,
                Location  = new Point(loc.X + 20, loc.Y + 1)
            };
            parent.Controls.Add(dotLabel);
            parent.Controls.Add(textLabel);
        }

        private Panel BuildStatsPanel()
        {
            var pnl = new Panel { Dock = DockStyle.Fill, BackColor = SidebarBack, Padding = new Padding(18, 14, 18, 14) };

            var sectionLabel = new Label
            {
                Text      = "QUICK STATS",
                Font      = new Font("Segoe UI", 7.5F, FontStyle.Bold),
                ForeColor = UiTheme.Colors.TextMuted,
                AutoSize  = true,
                Location  = new Point(0, 0)
            };
            pnl.Controls.Add(sectionLabel);

            _lblStatTotal    = MakeStatLabel(pnl, "Total",          new Point(0, 24));
            _lblStatRegular  = MakeStatLabel(pnl, "Regular",        new Point(0, 48));
            _lblStatSpecial  = MakeStatLabel(pnl, "Special",        new Point(0, 72));
            _lblStatUpcoming = MakeStatLabel(pnl, "Next 30 Days",   new Point(0, 96));
            return pnl;
        }

        private static Label MakeStatLabel(Panel parent, string caption, Point loc)
        {
            parent.Controls.Add(new Label
            {
                Text      = caption,
                Font      = new Font("Segoe UI", 8.5F),
                ForeColor = UiTheme.Colors.TextMuted,
                AutoSize  = true,
                Location  = loc
            });
            var val = new Label
            {
                Text      = "—",
                Font      = new Font("Segoe UI", 11F, FontStyle.Bold),
                ForeColor = UiTheme.Colors.TextDark,
                AutoSize  = true,
                Location  = new Point(loc.X + 120, loc.Y - 1)
            };
            parent.Controls.Add(val);
            return val;
        }

        private Panel BuildFilterBar()
        {
            var bar = new Panel { Dock = DockStyle.Top, Height = 46, BackColor = Color.White };
            bar.Paint += (s, e) =>
            {
                using (var p = new Pen(Color.FromArgb(222, 226, 230)))
                    e.Graphics.DrawLine(p, 0, bar.Height - 1, bar.Width, bar.Height - 1);
            };

            // FlowLayoutPanel prevents absolute-position overlap at any window width / DPI
            var flow = new FlowLayoutPanel
            {
                Dock          = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents  = false,
                BackColor     = Color.Transparent,
                Padding       = new Padding(12, 10, 12, 0)
            };

            var lblSearch = MakeFilterLabel("Search:");
            _txtSearch = new TextBox
            {
                Font        = new Font("Segoe UI", 9.5F),
                Width       = 190,
                Height      = 24,
                BorderStyle = BorderStyle.FixedSingle,
                Margin      = new Padding(4, 0, 16, 0)
            };
            _txtSearch.TextChanged += (s, e) => ApplyFilters();

            var lblShow = MakeFilterLabel("Show:");
            _cboFilter = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Font          = new Font("Segoe UI", 9.5F),
                Width         = 162,
                Margin        = new Padding(4, 0, 16, 0)
            };
            _cboFilter.Items.AddRange(new object[]
                { "Active Only", "All", "Regular Holidays", "Special Non-Working", "Inactive Only" });
            _cboFilter.SelectedIndex         = 0;
            _cboFilter.SelectedIndexChanged += (s, e) => ApplyFilters();

            var lblYear = MakeFilterLabel("Year:");
            _cboYear = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Font          = new Font("Segoe UI", 9.5F),
                Width         = 80,
                Margin        = new Padding(4, 0, 16, 0)
            };
            int cy = DateTime.Today.Year;
            for (int y = cy - 3; y <= cy + 3; y++) _cboYear.Items.Add(y);
            _cboYear.SelectedItem          = cy;
            _cboYear.SelectedIndexChanged += (s, e) => { ApplyFilters(); SyncCalendarYear(); };

            _lblCount = new Label
            {
                Font      = new Font("Segoe UI", 8.5F),
                ForeColor = UiTheme.Colors.TextMuted,
                AutoSize  = true,
                Margin    = new Padding(0, 4, 0, 0)
            };

            flow.Controls.AddRange(new Control[] { lblSearch, _txtSearch, lblShow, _cboFilter, lblYear, _cboYear, _lblCount });
            bar.Controls.Add(flow);
            return bar;
        }

        private static Label MakeFilterLabel(string text) => new Label
        {
            Text      = text,
            Font      = new Font("Segoe UI", 9F),
            ForeColor = UiTheme.Colors.TextMuted,
            AutoSize  = true,
            Margin    = new Padding(0, 4, 4, 0)
        };

        private Panel BuildToolbar()
        {
            var bar = new FlowLayoutPanel
            {
                Dock          = DockStyle.Top,
                Height        = 50,
                BackColor     = BodyBack,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents  = false,
                Padding       = new Padding(12, 8, 12, 8)
            };

            _btnAdd = MakeBtn("+ Add",      UiTheme.Colors.Primary,         UiTheme.Colors.PrimaryHover,          120);
            _btnEdit   = MakeBtn("✎  Edit",  Color.FromArgb(52, 73, 94),    Color.FromArgb(44, 62, 80),           100);
            _btnDelete = MakeBtn("✕  Delete", UiTheme.Colors.Danger,         Color.FromArgb(185, 40, 30),          105);
            _btnRefresh = MakeBtn("↺  Refresh", Color.FromArgb(39, 174, 96), Color.FromArgb(32, 145, 80),         110);

            _btnEdit.Enabled   = false;
            _btnDelete.Enabled = false;

            _btnAdd.Click     += BtnAdd_Click;
            _btnEdit.Click    += BtnEdit_Click;
            _btnDelete.Click  += BtnDelete_Click;
            _btnRefresh.Click += (s, e) => LoadData();

            bar.Controls.AddRange(new Control[] { _btnAdd, _btnEdit, _btnDelete, _btnRefresh });
            return bar;
        }

        private static HopeButton MakeBtn(string text, Color back, Color hover, int width)
        {
            var btn = new HopeButton
            {
                Text   = text,
                Font   = new Font("Segoe UI", 9F, FontStyle.Bold),
                Size   = new Size(width, 34),
                Margin = new Padding(0, 0, 6, 0)
            };
            UiFactory.ConfigurePillHopeButton(btn, back, hover);
            return btn;
        }

        private DataGridView BuildGrid()
        {
            var dgv = new DataGridView
            {
                Dock                  = DockStyle.Fill,
                AutoGenerateColumns   = false,
                AllowUserToAddRows    = false,
                AllowUserToDeleteRows = false,
                ReadOnly              = true,
                SelectionMode         = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect           = false,
                RowHeadersVisible     = false,
                AutoSizeColumnsMode   = DataGridViewAutoSizeColumnsMode.Fill,
                BorderStyle           = BorderStyle.None,
                CellBorderStyle       = DataGridViewCellBorderStyle.SingleHorizontal
            };
            dgv.RowTemplate.Height = 36;
            UiFactory.StyleGrid(dgv);

            dgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "HolidayId",   Visible = false,  DataPropertyName = "HolidayId"   });
            dgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "HolidayDate", HeaderText = "Date",          DataPropertyName = "HolidayDate", FillWeight = 17 });
            dgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "HolidayName", HeaderText = "Holiday Name",  DataPropertyName = "HolidayName", FillWeight = 34 });
            dgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "HolidayType", HeaderText = "Type",          DataPropertyName = "HolidayType", FillWeight = 24 });
            dgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "Recurring",   HeaderText = "Recurring",     DataPropertyName = "Recurring",   FillWeight = 12 });
            dgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "Status",      HeaderText = "Active",        DataPropertyName = "Status",      FillWeight = 11 });

            dgv.CellDoubleClick  += (s, e) => { if (e.RowIndex >= 0) BtnEdit_Click(s, e); };
            dgv.SelectionChanged += Dgv_SelectionChanged;
            dgv.CellFormatting   += Dgv_CellFormatting;

            return dgv;
        }

        private Panel BuildDetailsPanel()
        {
            var pnl = new Panel { Dock = DockStyle.Bottom, Height = 74, BackColor = Color.White, Padding = new Padding(12, 0, 12, 0) };
            pnl.Paint += (s, e) =>
            {
                using (var p = new Pen(Color.FromArgb(222, 226, 230)))
                    e.Graphics.DrawLine(p, 0, 0, pnl.Width, 0);
            };

            _lblDetailName = new Label
            {
                Font      = new Font("Segoe UI", 11F, FontStyle.Bold),
                ForeColor = UiTheme.Colors.TextDark,
                AutoSize  = true,
                Location  = new Point(0, 12)
            };
            _lblDetailMeta = new Label
            {
                Font      = new Font("Segoe UI", 9F),
                ForeColor = UiTheme.Colors.TextMuted,
                AutoSize  = true,
                Location  = new Point(0, 36)
            };
            _lblDetailNotes = new Label
            {
                Font      = new Font("Segoe UI", 8.5F, FontStyle.Italic),
                ForeColor = UiTheme.Colors.TextMuted,
                AutoSize  = true,
                Location  = new Point(0, 54)
            };

            SetEmptyDetail();
            pnl.Controls.AddRange(new Control[] { _lblDetailName, _lblDetailMeta, _lblDetailNotes });
            return pnl;
        }

        private static void PaintBorder(object sender, PaintEventArgs e)
        {
            var c = (Control)sender;
            using (var p = new Pen(Color.FromArgb(220, 224, 229)))
                e.Graphics.DrawRectangle(p, 0, 0, c.Width - 1, c.Height - 1);
        }

        // ═══════════════════════════════════════════════════════════════
        //  DATA
        // ═══════════════════════════════════════════════════════════════

        private void LoadData()
        {
            try
            {
                _all = _repo.GetAll();
                ApplyFilters();
                UpdateStats();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to load holidays:\n{ex.Message}", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void ApplyFilters()
        {
            if (_all == null) { _filtered = new List<HolidayDto>(); UpdateGrid(); return; }

            int selectedYear = GetSelectedYear();
            var filter       = _cboFilter.SelectedItem?.ToString() ?? "Active Only";
            var q            = (_txtSearch?.Text ?? "").Trim().ToLower();

            _filtered = _all.Where(h =>
            {
                // Recurring holidays appear in every year
                if (!h.IsRecurring && h.HolidayDate.Year != selectedYear) return false;

                if (filter == "Active Only"         && !h.IsActive)                          return false;
                if (filter == "Inactive Only"       &&  h.IsActive)                          return false;
                if (filter == "Regular Holidays"    && h.HolidayType != "Regular Holiday")   return false;
                if (filter == "Special Non-Working" && h.HolidayType != "Special Non-Working") return false;

                if (!string.IsNullOrEmpty(q) &&
                    !h.HolidayName.ToLower().Contains(q) &&
                    !h.HolidayType.ToLower().Contains(q)) return false;

                return true;
            })
            .OrderBy(h => DisplayDate(h, selectedYear))
            .ToList();

            UpdateGrid();
            RefreshBoldedDates();
        }

        private void UpdateGrid()
        {
            int y = GetSelectedYear();
            _dgv.DataSource = _filtered.Select(h => new
            {
                h.HolidayId,
                HolidayDate = DisplayDate(h, y).ToString("MMM dd, yyyy"),
                h.HolidayName,
                h.HolidayType,
                Recurring = h.IsRecurring ? "Yes" : "—",
                Status    = h.IsActive    ? "Active" : "Inactive"
            }).ToList();

            bool empty             = _filtered.Count == 0;
            _lblEmptyState.Visible = empty;
            _dgv.Visible           = !empty;
            _lblCount.Text         = $"{_filtered.Count} record{(_filtered.Count == 1 ? "" : "s")}";

            _btnEdit.Enabled   = false;
            _btnDelete.Enabled = false;
            SetEmptyDetail();
        }

        private void UpdateStats()
        {
            if (_all == null) return;
            var active   = _all.Where(h => h.IsActive).ToList();
            int today    = DateTime.Today.DayOfYear;
            int in30     = DateTime.Today.AddDays(30).DayOfYear;
            int upcoming = active.Count(h =>
            {
                int doy = DisplayDate(h, DateTime.Today.Year).DayOfYear;
                return doy >= today && doy <= in30;
            });

            _lblStatTotal.Text    = active.Count.ToString();
            _lblStatRegular.Text  = active.Count(h => h.HolidayType == "Regular Holiday").ToString();
            _lblStatSpecial.Text  = active.Count(h => h.HolidayType == "Special Non-Working").ToString();
            _lblStatUpcoming.Text = upcoming.ToString();
        }

        private void RefreshBoldedDates()
        {
            _calendar.RemoveAllBoldedDates();
            int y        = GetSelectedYear();
            var boldDates = _all
                .Where(h => h.IsActive)
                .Select(h => DisplayDate(h, y))
                .Where(d => d.Year == y)
                .Distinct()
                .ToArray();

            if (boldDates.Length > 0) _calendar.BoldedDates = boldDates;
            _calendar.UpdateBoldedDates();
        }

        private void SyncCalendarYear()
        {
            int y = GetSelectedYear();
            try { _calendar.SetDate(new DateTime(y, 1, 1)); } catch { }
        }

        private int GetSelectedYear() =>
            _cboYear?.SelectedItem is int y ? y : DateTime.Today.Year;

        private static DateTime DisplayDate(HolidayDto h, int year)
        {
            if (!h.IsRecurring) return h.HolidayDate;
            try { return new DateTime(year, h.HolidayDate.Month, h.HolidayDate.Day); }
            catch { return h.HolidayDate; }
        }

        // ═══════════════════════════════════════════════════════════════
        //  EVENTS
        // ═══════════════════════════════════════════════════════════════

        private void Dgv_SelectionChanged(object sender, EventArgs e)
        {
            bool has           = _dgv.SelectedRows.Count > 0;
            _btnEdit.Enabled   = has;
            _btnDelete.Enabled = has;

            if (!has) { SetEmptyDetail(); return; }

            int id      = (int)_dgv.SelectedRows[0].Cells["HolidayId"].Value;
            var holiday = _all.FirstOrDefault(h => h.HolidayId == id);
            if (holiday == null) return;

            ShowDetail(holiday);

            // Sync calendar without triggering Dgv_SelectionChanged again
            _suppressCalendarSync = true;
            try
            {
                var d = DisplayDate(holiday, GetSelectedYear());
                _calendar.SetDate(d);
                _calendar.SelectionStart = d;
            }
            catch { }
            finally { _suppressCalendarSync = false; }
        }

        private void Calendar_DateSelected(object sender, DateRangeEventArgs e)
        {
            if (_suppressCalendarSync) return;

            var clicked = e.Start.Date;
            int y       = GetSelectedYear();

            for (int i = 0; i < _dgv.Rows.Count; i++)
            {
                int id      = (int)_dgv.Rows[i].Cells["HolidayId"].Value;
                var holiday = _filtered.FirstOrDefault(h => h.HolidayId == id);
                if (holiday == null) continue;
                if (DisplayDate(holiday, y).Date != clicked) continue;

                _dgv.ClearSelection();
                _dgv.Rows[i].Selected = true;
                _dgv.FirstDisplayedScrollingRowIndex = i;
                return;
            }
        }

        private void Dgv_CellFormatting(object sender, DataGridViewCellFormattingEventArgs e)
        {
            if (e.RowIndex < 0 || e.RowIndex >= _filtered.Count) return;
            var h = _filtered[e.RowIndex];

            if (!h.IsActive)
            {
                e.CellStyle.ForeColor = InactiveFore;
                e.CellStyle.BackColor = InactiveBack;
            }
            else if (h.HolidayType == "Regular Holiday")
            {
                e.CellStyle.ForeColor = RegularFore;
                e.CellStyle.BackColor = RegularBack;
            }
            else
            {
                e.CellStyle.ForeColor = SpecialFore;
                e.CellStyle.BackColor = SpecialBack;
            }
        }

        // ── Detail panel helpers ──────────────────────────────────────

        private void SetEmptyDetail()
        {
            _lblDetailName.Text  = "No holiday selected";
            _lblDetailName.ForeColor = UiTheme.Colors.TextMuted;
            _lblDetailMeta.Text  = "Select a row or click a date on the calendar";
            _lblDetailNotes.Text = "";
        }

        private void ShowDetail(HolidayDto h)
        {
            _lblDetailName.Text      = h.HolidayName;
            _lblDetailName.ForeColor = h.HolidayType == "Regular Holiday" ? RegularFore : SpecialFore;
            string recurring         = h.IsRecurring ? " · Recurring annually" : "";
            string status            = h.IsActive ? "" : " · Inactive";
            _lblDetailMeta.Text      = $"{DisplayDate(h, GetSelectedYear()):dddd, MMMM dd, yyyy}  ·  {h.HolidayType}{recurring}{status}";
            _lblDetailNotes.Text     = string.IsNullOrWhiteSpace(h.Notes) ? "" : $"Notes: {h.Notes}";
        }

        // ═══════════════════════════════════════════════════════════════
        //  CRUD
        // ═══════════════════════════════════════════════════════════════

        private void BtnAdd_Click(object sender, EventArgs e)
        {
            var defaultDate = _calendar.SelectionStart.Year == GetSelectedYear()
                ? _calendar.SelectionStart
                : new DateTime(GetSelectedYear(), 1, 1);

            using (var dlg = new HolidayEditDialog(null, defaultDate, _all))
            {
                if (dlg.ShowDialog(this.FindForm()) != DialogResult.OK) return;
                try
                {
                    dlg.Result.CreatedBy = AppSession.CurrentUserId;
                    _repo.Insert(dlg.Result);
                    ActivityLogger.Log("Create", "CompanyHoliday", 0,
                        $"Holiday added: {dlg.Result.HolidayName} ({dlg.Result.HolidayDate:yyyy-MM-dd})");
                    LoadData();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Failed to add holiday:\n{ex.Message}", "Error",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private void BtnEdit_Click(object sender, EventArgs e)
        {
            if (_dgv.SelectedRows.Count == 0)
            {
                MessageBox.Show("Please select a holiday to edit.", "No Selection",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            int id      = (int)_dgv.SelectedRows[0].Cells["HolidayId"].Value;
            var holiday = _all.FirstOrDefault(h => h.HolidayId == id);
            if (holiday == null) return;

            using (var dlg = new HolidayEditDialog(holiday, holiday.HolidayDate, _all))
            {
                if (dlg.ShowDialog(this.FindForm()) != DialogResult.OK) return;
                try
                {
                    dlg.Result.HolidayId = id;
                    _repo.Update(dlg.Result);
                    ActivityLogger.Log("Update", "CompanyHoliday", id,
                        $"Holiday updated: {dlg.Result.HolidayName}");
                    LoadData();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Failed to update holiday:\n{ex.Message}", "Error",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private void BtnDelete_Click(object sender, EventArgs e)
        {
            if (_dgv.SelectedRows.Count == 0)
            {
                MessageBox.Show("Please select a holiday to delete.", "No Selection",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            int id      = (int)_dgv.SelectedRows[0].Cells["HolidayId"].Value;
            var holiday = _all.FirstOrDefault(h => h.HolidayId == id);
            if (holiday == null) return;

            if (MessageBox.Show(
                    $"Delete \"{holiday.HolidayName}\" ({holiday.HolidayDate:MMM dd, yyyy})?",
                    "Confirm Delete", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
                return;

            try
            {
                _repo.Delete(id);
                ActivityLogger.Log("Delete", "CompanyHoliday", id,
                    $"Holiday deleted: {holiday.HolidayName}");
                LoadData();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to delete holiday:\n{ex.Message}", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }

    // ═══════════════════════════════════════════════════════════════════
    //  Add / Edit Dialog
    // ═══════════════════════════════════════════════════════════════════

    internal sealed class HolidayEditDialog : Form
    {
        public HolidayDto Result { get; private set; }

        private readonly List<HolidayDto> _existing;
        private readonly int?             _editingId;

        private TextBox        _txtName;
        private DateTimePicker _dtpDate;
        private ComboBox       _cboType;
        private CheckBox       _chkRecurring;
        private CheckBox       _chkActive;
        private TextBox        _txtNotes;
        private Label          _lblDupeWarning;

        // Layout constants
        private const int Lx = 24;   // left margin
        private const int Fx = 170;  // field start x  (label area = 146px)
        private const int Fw = 310;  // field width
        private const int RowH = 38; // row height

        public HolidayEditDialog(HolidayDto editing, DateTime defaultDate, List<HolidayDto> allHolidays)
        {
            _existing  = allHolidays ?? new List<HolidayDto>();
            _editingId = editing?.HolidayId;

            bool isNew = editing == null;
            Text            = isNew ? "Add Holiday" : "Edit Holiday";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition   = FormStartPosition.CenterParent;
            MaximizeBox     = false;
            MinimizeBox     = false;
            BackColor       = Color.White;
            Font            = new Font("Segoe UI", 10F);

            // ── Coloured header strip ─────────────────────────────────
            var header = new Panel
            {
                Dock      = DockStyle.Top,
                Height    = 48,
                BackColor = Color.FromArgb(52, 152, 219)
            };
            header.Controls.Add(new Label
            {
                Text      = isNew ? "Add New Holiday" : "Edit Holiday",
                Font      = new Font("Segoe UI", 12F, FontStyle.Bold),
                ForeColor = Color.White,
                Dock      = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding   = new Padding(16, 0, 0, 0)
            });
            Controls.Add(header);

            // ── Form fields (absolute-positioned below header) ────────
            int y = 64;  // start below header

            // Holiday Name
            Row("Holiday Name *", y);
            _txtName = new TextBox { Location = new Point(Fx, y - 2), Width = Fw, Font = Font };
            _txtName.Text = editing?.HolidayName ?? "";
            Controls.Add(_txtName);
            y += RowH;

            // Date
            Row("Date *", y);
            _dtpDate = new DateTimePicker
            {
                Location = new Point(Fx, y - 2),
                Width    = Fw,
                Format   = DateTimePickerFormat.Long,
                Font     = Font,
                Value    = editing?.HolidayDate ?? defaultDate
            };
            _dtpDate.ValueChanged += (s, e) => CheckDuplicate();
            Controls.Add(_dtpDate);
            y += RowH;

            // Type
            Row("Type *", y);
            _cboType = new ComboBox
            {
                Location      = new Point(Fx, y - 2),
                Width         = Fw,
                DropDownStyle = ComboBoxStyle.DropDownList,
                Font          = Font
            };
            _cboType.Items.AddRange(new object[] { "Regular Holiday", "Special Non-Working" });
            _cboType.SelectedItem = editing?.HolidayType ?? "Regular Holiday";
            if (_cboType.SelectedIndex < 0) _cboType.SelectedIndex = 0;
            Controls.Add(_cboType);
            y += RowH;

            // Recurring
            Row("Recurring", y);
            _chkRecurring = new CheckBox
            {
                Location = new Point(Fx, y + 3),
                Text     = "Repeat every year (ignore year in date)",
                Checked  = editing?.IsRecurring ?? false,
                Font     = new Font("Segoe UI", 9.5F),
                Width    = Fw
            };
            _chkRecurring.CheckedChanged += (s, e) => CheckDuplicate();
            Controls.Add(_chkRecurring);
            y += RowH;

            // Status
            Row("Status", y);
            _chkActive = new CheckBox
            {
                Location = new Point(Fx, y + 3),
                Text     = "Active",
                Checked  = editing?.IsActive ?? true,
                Font     = Font,
                AutoSize = true
            };
            Controls.Add(_chkActive);
            y += RowH;

            // Notes
            Row("Notes", y);
            _txtNotes = new TextBox
            {
                Location   = new Point(Fx, y - 2),
                Width      = Fw,
                Height     = 64,
                Multiline  = true,
                ScrollBars = ScrollBars.Vertical,
                Font       = Font,
                Text       = editing?.Notes ?? ""
            };
            Controls.Add(_txtNotes);
            y += 78;

            // ── Separator ─────────────────────────────────────────────
            var sep = new Panel
            {
                Location  = new Point(0, y),
                Size      = new Size(Fx + Fw + Lx + 20, 1),
                BackColor = Color.FromArgb(220, 225, 230)
            };
            Controls.Add(sep);
            y += 10;

            // ── Duplicate warning ──────────────────────────────────────
            _lblDupeWarning = new Label
            {
                Text      = "",
                Font      = new Font("Segoe UI", 8.5F),
                ForeColor = Color.FromArgb(192, 57, 43),
                Location  = new Point(Lx, y),
                Size      = new Size(Fw + Fx, 18)
            };
            Controls.Add(_lblDupeWarning);
            y += 22;

            // ── Buttons ───────────────────────────────────────────────
            int totalW   = Fx + Fw + Lx;
            var btnSave = new Button
            {
                Text         = "Save",
                Location     = new Point(totalW - 184, y),
                Size         = new Size(88, 34),
                BackColor    = Color.FromArgb(52, 152, 219),
                ForeColor    = Color.White,
                FlatStyle    = FlatStyle.Flat,
                Font         = new Font("Segoe UI", 10F, FontStyle.Bold),
                DialogResult = DialogResult.None,
                Cursor       = Cursors.Hand
            };
            btnSave.FlatAppearance.BorderSize = 0;
            btnSave.Click += BtnSave_Click;

            var btnCancel = new Button
            {
                Text         = "Cancel",
                Location     = new Point(totalW - 90, y),
                Size         = new Size(88, 34),
                FlatStyle    = FlatStyle.Flat,
                Font         = new Font("Segoe UI", 10F),
                DialogResult = DialogResult.Cancel,
                Cursor       = Cursors.Hand
            };
            btnCancel.FlatAppearance.BorderColor = Color.FromArgb(200, 205, 210);

            Controls.Add(btnSave);
            Controls.Add(btnCancel);
            AcceptButton = btnSave;
            CancelButton = btnCancel;

            ClientSize = new Size(totalW + Lx, y + 56);
            CheckDuplicate();
        }

        private void Row(string text, int y)
        {
            Controls.Add(new Label
            {
                Text      = text,
                Location  = new Point(Lx, y + 3),
                Width     = Fx - Lx - 4,
                Font      = new Font("Segoe UI", 9.5F),
                ForeColor = Color.FromArgb(55, 65, 80)
            });
        }

        private void CheckDuplicate()
        {
            var picked  = _dtpDate.Value.Date;
            bool isDupe = _existing.Any(h =>
                h.HolidayId != (_editingId ?? -1) &&
                h.HolidayDate.Month == picked.Month &&
                h.HolidayDate.Day   == picked.Day   &&
                (h.IsRecurring || _chkRecurring.Checked || h.HolidayDate.Year == picked.Year));

            _lblDupeWarning.Text = isDupe
                ? $"⚠  A holiday already exists on {picked:MMM dd}."
                : "";
        }

        private void BtnSave_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(_txtName.Text))
            {
                MessageBox.Show("Holiday name is required.", "Validation",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                _txtName.Focus();
                return;
            }

            var picked  = _dtpDate.Value.Date;
            bool isDupe = _existing.Any(h =>
                h.HolidayId != (_editingId ?? -1) &&
                h.HolidayDate.Month == picked.Month &&
                h.HolidayDate.Day   == picked.Day   &&
                (h.IsRecurring || _chkRecurring.Checked || h.HolidayDate.Year == picked.Year));

            if (isDupe)
            {
                MessageBox.Show($"A holiday already exists on {picked:MMM dd}. Choose a different date.",
                    "Duplicate Date", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                _dtpDate.Focus();
                return;
            }

            Result = new HolidayDto
            {
                HolidayName = _txtName.Text.Trim(),
                HolidayDate = picked,
                HolidayType = _cboType.SelectedItem?.ToString() ?? "Regular Holiday",
                IsRecurring = _chkRecurring.Checked,
                IsActive    = _chkActive.Checked,
                Notes       = string.IsNullOrWhiteSpace(_txtNotes.Text) ? null : _txtNotes.Text.Trim()
            };
            DialogResult = DialogResult.OK;
        }
    }
}
