using System;
using System.Drawing;
using System.Windows.Forms;
using Yakult.Inventory.App.Repositories;

namespace Yakult.Inventory.App.Pages.RequestPortal
{
    /// <summary>
    /// Displays the authorization history for a specific employee.
    /// Shown on the Authorization History tab of the Requester Portal.
    /// </summary>
    public class AuthorizationHistoryPage : UserControl
    {
        private readonly int _employeeId;
        private readonly CartridgeAuthorizationRepository _repo;
        private DataGridView _grid;
        private Button       _btnRefresh;
        private Label        _lblStatus;

        public AuthorizationHistoryPage(int employeeId)
        {
            _employeeId = employeeId;
            _repo       = new CartridgeAuthorizationRepository();
            Dock        = DockStyle.Fill;
            BuildUi();
            _ = LoadDataAsync();
        }

        private void BuildUi()
        {
            BackColor = Color.FromArgb(248, 250, 252);

            // ── Header ────────────────────────────────────────────────────────
            var header = new Panel
            {
                Dock      = DockStyle.Top,
                Height    = 56,
                BackColor = Color.White,
                Padding   = new Padding(16, 0, 16, 0)
            };
            header.Paint += (s, e) =>
            {
                using (var pen = new Pen(Color.FromArgb(226, 232, 240)))
                    e.Graphics.DrawLine(pen, 0, header.Height - 1, header.Width, header.Height - 1);
            };

            var lblTitle = new Label
            {
                Text      = "Authorization History",
                Font      = new Font("Segoe UI", 13F, FontStyle.Bold),
                ForeColor = Color.FromArgb(30, 41, 59),
                AutoSize  = true,
                Location  = new Point(16, 16)
            };

            _btnRefresh = new Button
            {
                Text      = "⟳  Refresh",
                Font      = new Font("Segoe UI", 9F),
                Size      = new Size(100, 30),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(8, 145, 178),
                ForeColor = Color.White,
                Anchor    = AnchorStyles.Right | AnchorStyles.Top,
                Location  = new Point(header.Width - 120, 13)
            };
            _btnRefresh.FlatAppearance.BorderSize = 0;
            _btnRefresh.Click += async (s, e) => await LoadDataAsync();

            header.Controls.AddRange(new Control[] { lblTitle, _btnRefresh });
            header.Resize += (s, e) => _btnRefresh.Left = header.Width - 120;

            // ── Status label ──────────────────────────────────────────────────
            _lblStatus = new Label
            {
                Dock      = DockStyle.Bottom,
                Height    = 24,
                Text      = string.Empty,
                Font      = new Font("Segoe UI", 8F),
                ForeColor = Color.FromArgb(100, 116, 139),
                TextAlign = ContentAlignment.MiddleLeft,
                Padding   = new Padding(16, 0, 0, 0)
            };

            // ── Grid ──────────────────────────────────────────────────────────
            _grid = new DataGridView
            {
                Dock                  = DockStyle.Fill,
                ReadOnly              = true,
                AllowUserToAddRows    = false,
                AllowUserToDeleteRows = false,
                SelectionMode         = DataGridViewSelectionMode.FullRowSelect,
                AutoSizeColumnsMode   = DataGridViewAutoSizeColumnsMode.Fill,
                RowHeadersVisible     = false,
                BackgroundColor       = Color.FromArgb(248, 250, 252),
                BorderStyle           = BorderStyle.None,
                Font                  = new Font("Segoe UI", 9F),
                ColumnHeadersHeight   = 36,
            };
            _grid.RowTemplate.Height = 32;
            _grid.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(241, 245, 249);
            _grid.ColumnHeadersDefaultCellStyle.Font      = new Font("Segoe UI", 9F, FontStyle.Bold);
            _grid.EnableHeadersVisualStyles               = false;
            _grid.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(248, 250, 252);

            _grid.Columns.AddRange(new DataGridViewColumn[]
            {
                new DataGridViewTextBoxColumn { Name = "AuthorizationId", HeaderText = "ID",              FillWeight = 6  },
                new DataGridViewTextBoxColumn { Name = "Status",          HeaderText = "Status",           FillWeight = 10 },
                new DataGridViewTextBoxColumn { Name = "DepartmentName",  HeaderText = "Department",       FillWeight = 18 },
                new DataGridViewTextBoxColumn { Name = "RequestedModels", HeaderText = "Cartridge Models", FillWeight = 22 },
                new DataGridViewTextBoxColumn { Name = "SignedByName",    HeaderText = "Signed By",        FillWeight = 18 },
                new DataGridViewTextBoxColumn { Name = "SignedDate",      HeaderText = "Date Signed",      FillWeight = 14 },
                new DataGridViewTextBoxColumn { Name = "CreatedDate",     HeaderText = "Requested On",     FillWeight = 12 }
            });

            _grid.CellFormatting += Grid_CellFormatting;

            Controls.AddRange(new Control[] { _grid, _lblStatus, header });
        }

        private async System.Threading.Tasks.Task LoadDataAsync()
        {
            _btnRefresh.Enabled = false;
            _lblStatus.Text     = "Loading…";
            _grid.Rows.Clear();

            try
            {
                var items = await _repo.GetHistoryByEmployeeAsync(_employeeId);

                foreach (var a in items)
                {
                    _grid.Rows.Add(
                        a.AuthorizationId,
                        a.Status,
                        a.DepartmentName ?? string.Format("DeptId {0}", a.DepartmentId),
                        a.RequestedModels ?? "—",
                        a.SignedByName    ?? "—",
                        a.SignedDate.HasValue ? a.SignedDate.Value.ToString("yyyy-MM-dd HH:mm") : "—",
                        a.CreatedDate.ToString("yyyy-MM-dd HH:mm")
                    );
                }

                _lblStatus.Text = string.Format("{0} record(s) — last refreshed {1:HH:mm:ss}", items.Count, DateTime.Now);
            }
            catch (Exception ex)
            {
                _lblStatus.Text = "Error: " + ex.Message;
            }
            finally
            {
                _btnRefresh.Enabled = true;
            }
        }

        private void Grid_CellFormatting(object sender, DataGridViewCellFormattingEventArgs e)
        {
            if (_grid.Columns[e.ColumnIndex].Name != "Status" || e.Value == null) return;

            Color color;
            switch (e.Value.ToString())
            {
                case "Approved": color = Color.FromArgb(21, 128, 61);   break;
                case "Pending":  color = Color.FromArgb(146, 64, 14);   break;
                case "Rejected": color = Color.FromArgb(185, 28, 28);   break;
                default:         color = Color.FromArgb(100, 116, 139); break;
            }

            e.CellStyle.ForeColor = color;
            e.CellStyle.Font      = new Font("Segoe UI", 9F, FontStyle.Bold);
        }
    }
}
