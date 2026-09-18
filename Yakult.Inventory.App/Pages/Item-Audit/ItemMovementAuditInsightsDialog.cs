using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using Yakult.Inventory.App.Repositories;

namespace Yakult.Inventory.App.Pages.ItemAudit
{
    public class ItemMovementAuditInsightsDialog : Form
    {
        private readonly string _serial;
        private readonly ItemMovementAuditRepository _repo;

        private TabControl _tabs;
        private DataGridView _gridTimeline;
        private DataGridView _gridHeatmap;
        private DataGridView _gridTransitions;
        private DataGridView _gridLocation;
        private Label _lblHeader;
        private Button _btnClose;
        private bool _loading;

        public ItemMovementAuditInsightsDialog(string serial)
        {
            _serial = serial;
            _repo = new ItemMovementAuditRepository();

            KeyPreview = true;
            BuildUi();

            Shown += async (s, e) => await LoadAsync();
        }

        private void BuildUi()
        {
            Text = "Item Movement Insights";
            StartPosition = FormStartPosition.CenterParent;
            Size = new Size(980, 720);
            MinimumSize = new Size(860, 640);
            BackColor = Color.White;

            var header = new Panel { Dock = DockStyle.Top, Height = 56, BackColor = Color.White, Padding = new Padding(16, 10, 16, 10) };
            header.Paint += (s, e) =>
            {
                using (var p = new Pen(Color.FromArgb(235, 238, 240)))
                    e.Graphics.DrawLine(p, 0, header.Height - 1, header.Width, header.Height - 1);
            };

            _lblHeader = new Label
            {
                Text = $"Insights: {_serial}",
                AutoSize = true,
                Font = new Font("Segoe UI", 12F, FontStyle.Bold),
                ForeColor = Color.FromArgb(45, 55, 72),
                Location = new Point(16, 16)
            };

            _btnClose = new Button
            {
                Text = "Close",
                Width = 90,
                Height = 28,
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(52, 152, 219),
                ForeColor = Color.White,
                Location = new Point(Width - 120, 14)
            };
            _btnClose.FlatAppearance.BorderSize = 0;
            _btnClose.Click += (s, e) => Close();

            header.Controls.Add(_lblHeader);
            header.Controls.Add(_btnClose);
            Controls.Add(header);

            _tabs = new TabControl { Dock = DockStyle.Fill };
            Controls.Add(_tabs);

            _gridTimeline = CreateGrid();
            _gridTimeline.AutoGenerateColumns = false;
            _gridTimeline.Columns.Add(new DataGridViewTextBoxColumn { Name = "EventTime", DataPropertyName = "EventTime", HeaderText = "When", Width = 160, DefaultCellStyle = new DataGridViewCellStyle { Format = "yyyy-MM-dd HH:mm" } });
            _gridTimeline.Columns.Add(new DataGridViewTextBoxColumn { Name = "MovementType", DataPropertyName = "MovementType", HeaderText = "Type", Width = 140 });
            _gridTimeline.Columns.Add(new DataGridViewTextBoxColumn { Name = "MovementCategory", DataPropertyName = "MovementCategory", HeaderText = "Category", Width = 160 });
            _gridTimeline.Columns.Add(new DataGridViewTextBoxColumn { Name = "Direction", DataPropertyName = "Direction", HeaderText = "Dir", Width = 60 });
            _gridTimeline.Columns.Add(new DataGridViewTextBoxColumn { Name = "SetCode", DataPropertyName = "SetCode", HeaderText = "Set", Width = 120 });
            _gridTimeline.Columns.Add(new DataGridViewTextBoxColumn { Name = "BranchName", DataPropertyName = "BranchName", HeaderText = "Branch", Width = 140 });
            _gridTimeline.Columns.Add(new DataGridViewTextBoxColumn { Name = "DepartmentName", DataPropertyName = "DepartmentName", HeaderText = "Department", Width = 140 });
            _gridTimeline.Columns.Add(new DataGridViewTextBoxColumn { Name = "EmployeeName", DataPropertyName = "EmployeeName", HeaderText = "Employee", Width = 150 });
            _gridTimeline.Columns.Add(new DataGridViewTextBoxColumn { Name = "Notes", DataPropertyName = "Notes", HeaderText = "Notes", AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill });

            _gridHeatmap = CreateGrid();
            _gridTransitions = CreateGrid();
            _gridLocation = CreateGrid();

            _tabs.TabPages.Add(new TabPage("Timeline") { BackColor = Color.White });
            _tabs.TabPages.Add(new TabPage("Heatmap") { BackColor = Color.White });
            _tabs.TabPages.Add(new TabPage("Transitions") { BackColor = Color.White });
            _tabs.TabPages.Add(new TabPage("Location Flow") { BackColor = Color.White });

            _tabs.TabPages[0].Controls.Add(_gridTimeline);
            _tabs.TabPages[1].Controls.Add(_gridHeatmap);
            _tabs.TabPages[2].Controls.Add(_gridTransitions);
            _tabs.TabPages[3].Controls.Add(_gridLocation);

            Resize += (s, e) => { _btnClose.Left = Width - 120; };
        }

        private static DataGridView CreateGrid()
        {
            var dgv = new DataGridView
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                AllowUserToResizeRows = false,
                AllowUserToResizeColumns = true,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect = false,
                RowHeadersVisible = false,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None,
                BackgroundColor = Color.White,
                BorderStyle = BorderStyle.None
            };

            dgv.EnableHeadersVisualStyles = false;
            dgv.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(52, 152, 219);
            dgv.ColumnHeadersDefaultCellStyle.ForeColor = Color.White;
            dgv.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            dgv.DefaultCellStyle.Font = new Font("Segoe UI", 9F);
            dgv.DefaultCellStyle.SelectionBackColor = Color.FromArgb(41, 128, 185);
            dgv.DefaultCellStyle.SelectionForeColor = Color.White;
            dgv.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(245, 247, 250);
            dgv.RowTemplate.Height = 34;

            return dgv;
        }

        private async Task LoadAsync()
        {
            if (_loading)
                return;

            _loading = true;
            try
            {
                var data = await _repo.GetTimelineAsync(
                    fromDate: null,
                    toDate: null,
                    direction: "All",
                    serial: _serial,
                    setCode: null,
                    source: "All",
                    userName: null,
                    top: 5000);

                var enriched = EnrichMovements(data);

                _gridTimeline.DataSource = enriched
                    .OrderByDescending(x => x.EventTime)
                    .ToList();

                BuildHeatmap(enriched);
                BuildTransitions(enriched);
                BuildLocationFlow(enriched);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Insights load failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                _loading = false;
            }
        }

        private void BuildHeatmap(List<ItemMovementAuditDto> enriched)
        {
            var table = new DataTable();
            table.Columns.Add("Day");
            table.Columns.Add("00-03", typeof(int));
            table.Columns.Add("04-07", typeof(int));
            table.Columns.Add("08-11", typeof(int));
            table.Columns.Add("12-15", typeof(int));
            table.Columns.Add("16-19", typeof(int));
            table.Columns.Add("20-23", typeof(int));

            var dayOrder = new[]
            {
                DayOfWeek.Monday,
                DayOfWeek.Tuesday,
                DayOfWeek.Wednesday,
                DayOfWeek.Thursday,
                DayOfWeek.Friday,
                DayOfWeek.Saturday,
                DayOfWeek.Sunday
            };

            foreach (var day in dayOrder)
            {
                var row = table.NewRow();
                row["Day"] = day.ToString();

                for (int bucket = 0; bucket < 6; bucket++)
                    row[bucket + 1] = 0;

                var dayEvents = enriched.Where(x => x.EventTime != DateTime.MinValue && x.EventTime.DayOfWeek == day);
                foreach (var ev in dayEvents)
                {
                    var hour = ev.EventTime.Hour;
                    var b = hour / 4;
                    if (b < 0) b = 0;
                    if (b > 5) b = 5;
                    row[b + 1] = (int)row[b + 1] + 1;
                }

                table.Rows.Add(row);
            }

            _gridHeatmap.DataSource = table;
            _gridHeatmap.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        }

        private void BuildTransitions(List<ItemMovementAuditDto> enriched)
        {
            var table = new DataTable();
            table.Columns.Add("From");
            table.Columns.Add("To");
            table.Columns.Add("Count", typeof(int));

            var transitions = enriched
                .Where(x => !string.IsNullOrWhiteSpace(x.StatusBefore) || !string.IsNullOrWhiteSpace(x.StatusAfter))
                .Select(x => new { From = string.IsNullOrWhiteSpace(x.StatusBefore) ? "(none)" : x.StatusBefore, To = string.IsNullOrWhiteSpace(x.StatusAfter) ? "(none)" : x.StatusAfter })
                .GroupBy(x => new { x.From, x.To })
                .OrderByDescending(g => g.Count())
                .ToList();

            foreach (var t in transitions)
            {
                var row = table.NewRow();
                row["From"] = t.Key.From;
                row["To"] = t.Key.To;
                row["Count"] = t.Count();
                table.Rows.Add(row);
            }

            _gridTransitions.DataSource = table;
            _gridTransitions.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        }

        private void BuildLocationFlow(List<ItemMovementAuditDto> enriched)
        {
            var table = new DataTable();
            table.Columns.Add("Change");
            table.Columns.Add("From");
            table.Columns.Add("To");
            table.Columns.Add("When");
            table.Columns.Add("Type");

            foreach (var ev in enriched.OrderBy(x => x.EventTime))
            {
                AddLocationRowIfChanged(table, "Set", ev.PrevSetCode, ev.NewSetCode, ev);
                AddLocationRowIfChanged(table, "Branch", ev.PrevBranchName, ev.NewBranchName, ev);
                AddLocationRowIfChanged(table, "Department", ev.PrevDepartmentName, ev.NewDepartmentName, ev);
                AddLocationRowIfChanged(table, "Employee", ev.PrevEmployeeName, ev.NewEmployeeName, ev);
            }

            _gridLocation.DataSource = table;
            _gridLocation.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        }

        private static void AddLocationRowIfChanged(DataTable table, string kind, string from, string to, ItemMovementAuditDto ev)
        {
            var a = string.IsNullOrWhiteSpace(from) ? "(none)" : from;
            var b = string.IsNullOrWhiteSpace(to) ? "(none)" : to;

            if (string.Equals(a, b, StringComparison.OrdinalIgnoreCase))
                return;

            var row = table.NewRow();
            row["Change"] = kind;
            row["From"] = a;
            row["To"] = b;
            row["When"] = ev.EventTime == DateTime.MinValue ? "" : ev.EventTime.ToString("yyyy-MM-dd HH:mm");
            row["Type"] = ev.MovementType;
            table.Rows.Add(row);
        }

        private static List<ItemMovementAuditDto> EnrichMovements(List<ItemMovementAuditDto> movements)
        {
            if (movements == null || movements.Count == 0)
                return movements ?? new List<ItemMovementAuditDto>();

            var enriched = movements.ToList();

            var groups = enriched
                .GroupBy(m => string.IsNullOrWhiteSpace(m.SerialNumber)
                    ? (m.ItemId.HasValue ? $"ItemId:{m.ItemId.Value}" : "(unknown)")
                    : $"Serial:{m.SerialNumber.Trim()}")
                .ToList();

            foreach (var g in groups)
            {
                var ordered = g.OrderBy(x => x.EventTime).ToList();

                string prevStatus = null;
                string prevSetCode = null;
                string prevBranch = null;
                string prevDept = null;
                string prevEmp = null;

                foreach (var m in ordered)
                {
                    m.MovementCategory = ClassifyCategory(m);
                    m.MovementType = ClassifyType(m);
                    m.MovementPriority = ClassifyPriority(m);

                    m.PrevSetCode = prevSetCode;
                    m.PrevBranchName = prevBranch;
                    m.PrevDepartmentName = prevDept;
                    m.PrevEmployeeName = prevEmp;

                    m.NewSetCode = string.IsNullOrWhiteSpace(m.SetCode) ? prevSetCode : m.SetCode;
                    m.NewBranchName = string.IsNullOrWhiteSpace(m.BranchName) ? prevBranch : m.BranchName;
                    m.NewDepartmentName = string.IsNullOrWhiteSpace(m.DepartmentName) ? prevDept : m.DepartmentName;
                    m.NewEmployeeName = string.IsNullOrWhiteSpace(m.EmployeeName) ? prevEmp : m.EmployeeName;

                    m.StatusBefore = prevStatus;
                    m.StatusAfter = DetermineStatusAfter(m, prevStatus);

                    prevSetCode = m.NewSetCode;
                    prevBranch = m.NewBranchName;
                    prevDept = m.NewDepartmentName;
                    prevEmp = m.NewEmployeeName;
                    prevStatus = m.StatusAfter;
                }

            }

            return enriched;
        }

        private static string ClassifyCategory(ItemMovementAuditDto m)
        {
            if (m == null)
                return null;

            if (string.Equals(m.ReferenceType, "ItemAuditTrail", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(m.Source, "AuditTrail", StringComparison.OrdinalIgnoreCase))
                return "Audit Trail";

            var notes = m.Notes ?? string.Empty;

            if (ContainsAny(notes, "repair", "repaired", "service", "serviced", "calibrat"))
                return "Maintenance";

            if (ContainsAny(notes, "archive", "archived", "inactive", "active", "lost", "missing", "damage", "damaged", "broken"))
                return "Status Changes";

            if (string.Equals(m.ReferenceType, "Request", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(m.Source, "Request", StringComparison.OrdinalIgnoreCase))
                return "Request Lifecycle";

            if (string.Equals(m.ReferenceType, "Inventory", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(m.Source, "Inventory", StringComparison.OrdinalIgnoreCase))
                return "Inventory Operations";

            if (ContainsAny(m.Source ?? string.Empty, "Mobile"))
                return "Inventory Operations";

            return "Other";
        }

        private static string ClassifyType(ItemMovementAuditDto m)
        {
            if (m == null)
                return null;

            if (string.Equals(m.ReferenceType, "ItemAuditTrail", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(m.Source, "AuditTrail", StringComparison.OrdinalIgnoreCase))
            {
                if (!string.IsNullOrWhiteSpace(m.AuditAction))
                    return m.AuditAction;

                return "Audit";
            }

            var notes = m.Notes ?? string.Empty;

            if (ContainsAny(notes, "lost", "missing"))
                return "Lost";
            if (ContainsAny(notes, "broken"))
                return "Broken";
            if (ContainsAny(notes, "damage", "damaged"))
                return "Damaged";
            if (ContainsAny(notes, "archive", "archived"))
                return "Archived";
            if (ContainsAny(notes, "inactive"))
                return "Inactive";
            if (ContainsAny(notes, " active ", "activate", "activated"))
                return "Active";
            if (ContainsAny(notes, "repair", "repaired"))
                return "Repair";
            if (ContainsAny(notes, "service", "serviced"))
                return "Service";
            if (ContainsAny(notes, "calibrat"))
                return "Calibration";

            if (string.Equals(m.ReferenceType, "Request", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(m.Source, "Request", StringComparison.OrdinalIgnoreCase))
            {
                var status = m.RequestStatus ?? string.Empty;
                if (ContainsAny(status, "approve", "approved"))
                    return "Approved";
                if (ContainsAny(status, "return", "returned"))
                    return "Returned";
                if (ContainsAny(status, "issue", "issued", "release", "released"))
                    return "Issued";
                if (ContainsAny(status, "submit", "submitted"))
                    return "Requested";
                return "Requested";
            }

            if (!string.IsNullOrWhiteSpace(m.InventoryEntryType))
            {
                if (string.Equals(m.InventoryEntryType, "Positive", StringComparison.OrdinalIgnoreCase))
                    return "Stock In";
                if (string.Equals(m.InventoryEntryType, "Negative", StringComparison.OrdinalIgnoreCase))
                    return "Stock Out";
            }

            if (string.Equals(m.Direction, "IN", StringComparison.OrdinalIgnoreCase))
                return "Stock In";
            if (string.Equals(m.Direction, "OUT", StringComparison.OrdinalIgnoreCase))
                return "Stock Out";

            return "Update";
        }

        private static string ClassifyPriority(ItemMovementAuditDto m)
        {
            if (m == null)
                return null;

            var notes = m.Notes ?? string.Empty;

            if (ContainsAny(notes, "lost", "missing", "broken", "damage", "damaged"))
                return "High";

            if (ContainsAny(notes, "unprocessed"))
                return "Medium";

            return "Low";
        }

        private static string DetermineStatusAfter(ItemMovementAuditDto m, string prevStatus)
        {
            if (m == null)
                return prevStatus;

            if (!string.IsNullOrWhiteSpace(m.AuditStatus) &&
                (string.Equals(m.ReferenceType, "ItemAuditTrail", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(m.Source, "AuditTrail", StringComparison.OrdinalIgnoreCase)))
                return m.AuditStatus;

            var type = m.MovementType ?? string.Empty;
            if (type == "Lost" || type == "Broken" || type == "Damaged" || type == "Archived" || type == "Inactive" || type == "Active")
                return type;

            if (string.Equals(type, "Repair", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(type, "Service", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(type, "Calibration", StringComparison.OrdinalIgnoreCase))
                return "Maintenance";

            if (string.Equals(type, "Approved", StringComparison.OrdinalIgnoreCase))
                return "Approved";
            if (string.Equals(type, "Requested", StringComparison.OrdinalIgnoreCase))
                return "Requested";
            if (string.Equals(type, "Returned", StringComparison.OrdinalIgnoreCase))
                return "Returned";

            if (string.Equals(m.Direction, "OUT", StringComparison.OrdinalIgnoreCase))
                return "Issued";
            if (string.Equals(m.Direction, "IN", StringComparison.OrdinalIgnoreCase))
                return "In Stock";

            return prevStatus;
        }

        private static bool ContainsAny(string value, params string[] needles)
        {
            if (string.IsNullOrEmpty(value) || needles == null || needles.Length == 0)
                return false;

            foreach (var n in needles)
            {
                if (string.IsNullOrEmpty(n))
                    continue;
                if (value.IndexOf(n, StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;
            }

            return false;
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == Keys.Escape)
            {
                Close();
                return true;
            }

            return base.ProcessCmdKey(ref msg, keyData);
        }
    }
}
