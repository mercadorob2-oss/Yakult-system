using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Yakult.Inventory.App.Models;
using Yakult.Inventory.App.Repositories;
using ReaLTaiizor.Util;

namespace Yakult.Inventory.App.Pages
{
    public partial class MarkAsProcessedDialog : Form
    {
        [DllImport("Gdi32.dll", EntryPoint = "CreateRoundRectRgn")]
        private static extern IntPtr CreateRoundRectRgn(int nLeftRect, int nTopRect, int nRightRect, int nBottomRect, int nWidthEllipse, int nHeightEllipse);

        [DllImport("user32.dll")]
        public static extern bool ReleaseCapture();
        [DllImport("user32.dll")]
        public static extern IntPtr SendMessage(IntPtr hWnd, int msg, int wParam, int lParam);

        private const int WM_NCLBUTTONDOWN = 0xA1;
        private const int HTCAPTION = 0x2;

        private readonly List<SetItemUpdateDto> _updates;
        private readonly List<SetItemUpdateDto> _validUpdates;
        private readonly List<SetItemUpdateDto> _skippedUpdates;
        private readonly List<SetItemUpdateDto> _alreadyProcessedUpdates;
        private readonly List<UpdatePreviewRow> _previewRows;

        private DataGridView dgvItems;
        private ComboBox cmbCondition;
        private ComboBox cmbRepairAction;
        private Label lblSummary, lblWarning, lblConditionWarning;
        private LinkLabel lnkSkippedDetails;
        private ReaLTaiizor.Controls.HopeButton btnConfirm, btnCancel;
        private ProgressBar progressBar;
        private Panel pnlProgress;

        private readonly Color PrimaryBlue = Color.FromArgb(41, 128, 185);
        private readonly Color WarningRed = Color.FromArgb(220, 53, 69);
        private readonly Color SuccessGreen = Color.FromArgb(39, 174, 96);
        private readonly Color TextMain = Color.FromArgb(44, 62, 80);
        private readonly Color TextMuted = Color.FromArgb(127, 140, 141);

        public int SelectedConditionId { get; private set; }
        public string SelectedConditionName { get; private set; }
        public string SelectedRepairAction { get; private set; }
        public List<SetItemUpdateDto> UpdatesToProcess { get; private set; }

        private sealed class UpdatePreviewRow
        {
            public int UpdateId { get; set; }
            public DateTime CreatedAt { get; set; }
            public string SetCode { get; set; }
            public string ItemType { get; set; }
            public string SerialNumber { get; set; }
            public string BranchName { get; set; }
            public string DepartmentName { get; set; }
            public string Status { get; set; }

            public bool IsValid { get; set; }
            public bool IsSkippedMissingArchived { get; set; }
            public bool IsAlreadyProcessed { get; set; }
        }

        public MarkAsProcessedDialog(List<SetItemUpdateDto> updates)
        {
            _updates = updates ?? throw new ArgumentNullException(nameof(updates));
            _alreadyProcessedUpdates = updates.Where(u => u.Processed).ToList();
            _validUpdates = updates.Where(u => !u.Processed && !u.IsMissing && !u.IsArchived).ToList();
            _skippedUpdates = updates.Where(u => !u.Processed && (u.IsMissing || u.IsArchived)).ToList();
            UpdatesToProcess = new List<SetItemUpdateDto>(_validUpdates);
            _previewRows = BuildPreviewRows(updates);
            InitializeComponent();
            SetupForm();
            SetupControls();
            LoadConditions();
            UpdateSummary();
        }

        private void SetupForm()
        {
            this.FormBorderStyle = FormBorderStyle.None;
            this.Size = new Size(680, 580);
            this.StartPosition = FormStartPosition.CenterParent;
            this.BackColor = Color.White;
            this.Padding = new Padding(1);
            this.Region = Region.FromHrgn(CreateRoundRectRgn(0, 0, Width, Height, 24, 24));

            this.Paint += (s, e) =>
            {
                using (var pen = new Pen(Color.FromArgb(220, 220, 220), 2))
                {
                    e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                    e.Graphics.DrawPath(pen, GetRoundedRectPath(new Rectangle(0, 0, Width - 1, Height - 1), 24));
                }
            };
        }

        private GraphicsPath GetRoundedRectPath(Rectangle r, int radius)
        {
            var path = new GraphicsPath();
            path.AddArc(r.X, r.Y, radius, radius, 180, 90);
            path.AddArc(r.Right - radius, r.Y, radius, radius, 270, 90);
            path.AddArc(r.Right - radius, r.Bottom - radius, radius, radius, 0, 90);
            path.AddArc(r.X, r.Bottom - radius, radius, radius, 90, 90);
            path.CloseFigure();
            return path;
        }

        private void SetupControls()
        {
            // Root panel
            var pnlRoot = new Panel { Dock = DockStyle.Fill, BackColor = Color.White, Padding = new Padding(24) };
            this.Controls.Add(pnlRoot);

            // Header
            var pnlHeader = new Panel { Dock = DockStyle.Top, Height = 65, BackColor = Color.Transparent };
            var lblTitle = new Label
            {
                Text = "Mark as Processed",
                Font = new Font("Segoe UI", 16F, FontStyle.Bold),
                ForeColor = TextMain,
                Dock = DockStyle.Left,
                AutoSize = true
            };
            var btnCloseX = new Label
            {
                Text = "✕",
                Font = new Font("Segoe UI", 12F, FontStyle.Bold),
                ForeColor = TextMuted,
                Size = new Size(30, 30),
                Dock = DockStyle.Right,
                TextAlign = ContentAlignment.MiddleCenter,
                Cursor = Cursors.Hand
            };
            btnCloseX.MouseEnter += (s, e) => btnCloseX.ForeColor = Color.IndianRed;
            btnCloseX.MouseLeave += (s, e) => btnCloseX.ForeColor = TextMuted;
            btnCloseX.Click += (s, e) => this.Close();
            pnlHeader.Controls.Add(lblTitle);
            pnlHeader.Controls.Add(btnCloseX);
            pnlHeader.MouseDown += Title_MouseDown;
            pnlRoot.Controls.Add(pnlHeader);

            // Summary label
            lblSummary = new Label
            {
                Font = new Font("Segoe UI", 10F),
                ForeColor = TextMain,
                Dock = DockStyle.Top,
                Height = 40,
                Padding = new Padding(0, 8, 0, 0)
            };
            pnlRoot.Controls.Add(lblSummary);

            // Warning label for skipped items
            lblWarning = new Label
            {
                Font = new Font("Segoe UI", 9F, FontStyle.Regular),
                ForeColor = WarningRed,
                Dock = DockStyle.Top,
                Height = 44,
                Padding = new Padding(0, 4, 0, 0),
                Visible = false
            };
            pnlRoot.Controls.Add(lblWarning);

            lnkSkippedDetails = new LinkLabel
            {
                Text = "View skipped details",
                Dock = DockStyle.Top,
                Height = 20,
                LinkColor = PrimaryBlue,
                ActiveLinkColor = PrimaryBlue,
                Visible = false
            };
            lnkSkippedDetails.Click += (s, e) => ShowSkippedDetailsDialog();
            pnlRoot.Controls.Add(lnkSkippedDetails);

            lblConditionWarning = new Label
            {
                Font = new Font("Segoe UI", 9F, FontStyle.Regular),
                ForeColor = WarningRed,
                Dock = DockStyle.Top,
                Height = 26,
                Visible = false
            };
            pnlRoot.Controls.Add(lblConditionWarning);

            // Items grid (contained in a fixed-height panel)
            var pnlList = new Panel { Dock = DockStyle.Top, Height = 220, BackColor = Color.White };
            dgvItems = new DataGridView
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
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                AutoGenerateColumns = false,
                BorderStyle = BorderStyle.None,
                BackgroundColor = Color.White,
                RowTemplate = { Height = 32 }
            };
            dgvItems.Columns.Add(new DataGridViewTextBoxColumn { Name = "SetCode", DataPropertyName = "SetCode", HeaderText = "Set Code", FillWeight = 15, MinimumWidth = 80 });
            dgvItems.Columns.Add(new DataGridViewTextBoxColumn { Name = "ItemType", DataPropertyName = "ItemType", HeaderText = "Item", FillWeight = 25, MinimumWidth = 120 });
            dgvItems.Columns.Add(new DataGridViewTextBoxColumn { Name = "SerialNumber", DataPropertyName = "SerialNumber", HeaderText = "Serial", FillWeight = 20, MinimumWidth = 100 });
            dgvItems.Columns.Add(new DataGridViewTextBoxColumn { Name = "BranchName", DataPropertyName = "BranchName", HeaderText = "Branch", FillWeight = 20, MinimumWidth = 100 });
            dgvItems.Columns.Add(new DataGridViewTextBoxColumn { Name = "DepartmentName", DataPropertyName = "DepartmentName", HeaderText = "Department", FillWeight = 20, MinimumWidth = 100 });
            dgvItems.Columns.Add(new DataGridViewTextBoxColumn { Name = "Status", DataPropertyName = "Status", HeaderText = "Status", FillWeight = 12, MinimumWidth = 110 });
            dgvItems.DataSource = null;
            dgvItems.DataSource = _previewRows;
            dgvItems.CellFormatting += DgvItems_CellFormatting;
            pnlList.Controls.Add(dgvItems);
            pnlRoot.Controls.Add(pnlList);

            // Progress panel (initially hidden, above the list)
            pnlProgress = new Panel { Dock = DockStyle.Top, Height = 28, BackColor = Color.Transparent, Padding = new Padding(0, 4, 0, 4), Visible = false };
            progressBar = new ProgressBar { Dock = DockStyle.Fill, Height = 8, Style = ProgressBarStyle.Continuous };
            pnlProgress.Controls.Add(progressBar);
            pnlRoot.Controls.Add(pnlProgress);

            // Condition/Repair panel (top of bottom stack)
            var pnlCondition = new Panel { Dock = DockStyle.Top, Height = 110, BackColor = Color.Transparent, Padding = new Padding(0, 8, 0, 0) };
            int labelWidth = 110;
            int inputLeft = labelWidth + 12;
            var lblCondition = new Label { Text = "Condition:", Font = new Font("Segoe UI", 9F, FontStyle.Bold), ForeColor = TextMain, AutoSize = true, Left = 0, Top = 6 };
            cmbCondition = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 220, Left = inputLeft, Top = 2 };
            cmbCondition.SelectedIndexChanged += (s, e) => UpdateConditionWarning();
            var lblRepair = new Label { Text = "Repair Action:", Font = new Font("Segoe UI", 9F, FontStyle.Bold), ForeColor = TextMain, AutoSize = true, Left = 0, Top = 46 };
            cmbRepairAction = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 300, Left = inputLeft, Top = 42 };
            cmbRepairAction.Items.AddRange(new object[] {
                "None (not a repair)",
                "Repaired - Spare inventory",
                "Repaired - Returned to requester"
            });
            if (cmbRepairAction.Items.Count > 0) cmbRepairAction.SelectedIndex = 0;
            pnlCondition.Controls.AddRange(new Control[] { lblCondition, cmbCondition, lblRepair, cmbRepairAction });
            pnlRoot.Controls.Add(pnlCondition);

            // Footer buttons
            var pnlFooter = new Panel { Dock = DockStyle.Bottom, Height = 70, BackColor = Color.Transparent, Padding = new Padding(0, 20, 0, 0) };
            pnlFooter.Paint += (s, e) =>
            {
                using (var pen = new Pen(Color.FromArgb(240, 240, 240), 1))
                {
                    e.Graphics.DrawLine(pen, 0, 5, pnlFooter.Width, 5);
                }
            };
            btnConfirm = new ReaLTaiizor.Controls.HopeButton
            {
                Text = "Confirm",
                Size = new Size(120, 38),
                Anchor = AnchorStyles.Right | AnchorStyles.Top,
                ButtonType = HopeButtonType.Primary,
                PrimaryColor = PrimaryBlue,
                Cursor = Cursors.Hand
            };
            btnConfirm.Location = new Point(pnlFooter.Width - btnConfirm.Width - 24, 20);
            btnConfirm.Click += BtnConfirm_Click;
            btnCancel = new ReaLTaiizor.Controls.HopeButton
            {
                Text = "Cancel",
                Size = new Size(100, 38),
                Anchor = AnchorStyles.Right | AnchorStyles.Top,
                ButtonType = HopeButtonType.Primary,
                PrimaryColor = Color.FromArgb(108, 117, 125),
                TextColor = Color.White,
                Cursor = Cursors.Hand
            };
            btnCancel.Location = new Point(btnConfirm.Left - btnCancel.Width - 12, 20);
            btnCancel.Click += (s, e) => this.Close();
            pnlFooter.Controls.AddRange(new Control[] { btnConfirm, btnCancel });
            pnlRoot.Controls.Add(pnlFooter);

            // Z-order
            pnlHeader.SendToBack();
            pnlFooter.SendToBack();
            pnlCondition.BringToFront();
            dgvItems.BringToFront();
            lblSummary.BringToFront();
            lblWarning.BringToFront();
            lnkSkippedDetails.BringToFront();
            lblConditionWarning.BringToFront();
            btnCloseX.BringToFront();
        }

        private static List<UpdatePreviewRow> BuildPreviewRows(List<SetItemUpdateDto> updates)
        {
            var rows = new List<UpdatePreviewRow>();
            if (updates == null)
                return rows;

            foreach (var u in updates.OrderByDescending(x => x.CreatedAt).ThenByDescending(x => x.UpdateId))
            {
                var reason = GetSkipReason(u);
                var isValid = string.IsNullOrWhiteSpace(reason);
                rows.Add(new UpdatePreviewRow
                {
                    UpdateId = u.UpdateId,
                    CreatedAt = u.CreatedAt,
                    SetCode = u.SetCode,
                    ItemType = u.ItemType,
                    SerialNumber = u.SerialNumber,
                    BranchName = u.BranchName,
                    DepartmentName = u.DepartmentName,
                    Status = isValid ? "Will process" : reason,
                    IsValid = isValid,
                    IsAlreadyProcessed = string.Equals(reason, "Already processed", StringComparison.OrdinalIgnoreCase),
                    IsSkippedMissingArchived = !isValid && !string.Equals(reason, "Already processed", StringComparison.OrdinalIgnoreCase),
                });
            }

            return rows;
        }

        private static string GetSkipReason(SetItemUpdateDto u)
        {
            if (u == null)
                return "Unknown";
            if (u.Processed)
                return "Already processed";
            if (u.IsMissing)
                return "Missing item";
            if (u.IsArchived)
                return "Archived item";
            return null;
        }

        private void DgvItems_CellFormatting(object sender, DataGridViewCellFormattingEventArgs e)
        {
            if (dgvItems == null || e.RowIndex < 0 || e.RowIndex >= dgvItems.Rows.Count)
                return;

            var row = dgvItems.Rows[e.RowIndex];
            if (!(row.DataBoundItem is UpdatePreviewRow preview))
                return;

            if (preview.IsAlreadyProcessed)
            {
                row.DefaultCellStyle.ForeColor = TextMuted;
                row.DefaultCellStyle.BackColor = Color.FromArgb(248, 249, 250);
            }
            else if (preview.IsSkippedMissingArchived)
            {
                row.DefaultCellStyle.ForeColor = TextMuted;
                row.DefaultCellStyle.BackColor = Color.FromArgb(255, 245, 245);
            }
            else
            {
                row.DefaultCellStyle.ForeColor = TextMain;
                row.DefaultCellStyle.BackColor = Color.White;
            }
        }

        private static bool IsRiskyCondition(string conditionName)
        {
            if (string.IsNullOrWhiteSpace(conditionName))
                return false;

            var c = conditionName.Trim();
            return c.IndexOf("damag", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   c.IndexOf("broken", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   c.IndexOf("missing", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   c.IndexOf("lost", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private void UpdateConditionWarning()
        {
            var name = (cmbCondition?.Text ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(name))
            {
                lblConditionWarning.Visible = false;
                UpdateSummary();
                return;
            }

            if (IsRiskyCondition(name))
            {
                lblConditionWarning.Text = $"⚠ Condition '{name}' is a high-impact status. Please verify the selected updates.";
                lblConditionWarning.Visible = true;
            }
            else
            {
                lblConditionWarning.Visible = false;
            }

            UpdateSummary();
        }

        private void ShowSkippedDetailsDialog()
        {
            var rows = _previewRows?.Where(r => !r.IsValid).ToList() ?? new List<UpdatePreviewRow>();
            if (rows.Count == 0)
                return;

            using (var dlg = new Form())
            {
                dlg.Text = "Skipped Details";
                dlg.StartPosition = FormStartPosition.CenterParent;
                dlg.Size = new Size(820, 420);
                dlg.FormBorderStyle = FormBorderStyle.FixedDialog;
                dlg.MaximizeBox = false;
                dlg.MinimizeBox = false;

                var root = new Panel { Dock = DockStyle.Fill, Padding = new Padding(12) };
                dlg.Controls.Add(root);

                var grid = new DataGridView
                {
                    Dock = DockStyle.Fill,
                    ReadOnly = true,
                    AllowUserToAddRows = false,
                    AllowUserToDeleteRows = false,
                    RowHeadersVisible = false,
                    AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                    AutoGenerateColumns = false,
                    BackgroundColor = Color.White,
                    BorderStyle = BorderStyle.FixedSingle
                };
                grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "SetCode", HeaderText = "Set Code", FillWeight = 14 });
                grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "ItemType", HeaderText = "Item", FillWeight = 22 });
                grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "SerialNumber", HeaderText = "Serial", FillWeight = 18 });
                grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "BranchName", HeaderText = "Branch", FillWeight = 18 });
                grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "DepartmentName", HeaderText = "Department", FillWeight = 18 });
                grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "Status", HeaderText = "Reason", FillWeight = 10 });
                grid.DataSource = rows;

                var footer = new Panel { Dock = DockStyle.Bottom, Height = 46 };
                var btnCopy = new Button { Text = "Copy", Width = 90, Height = 30, Anchor = AnchorStyles.Right | AnchorStyles.Top, Top = 8 };
                var btnClose = new Button { Text = "Close", Width = 90, Height = 30, Anchor = AnchorStyles.Right | AnchorStyles.Top, Top = 8 };
                footer.Resize += (s, e) =>
                {
                    btnClose.Left = footer.Width - btnClose.Width;
                    btnCopy.Left = btnClose.Left - btnCopy.Width - 8;
                };
                btnClose.Click += (s, e) => dlg.Close();
                btnCopy.Click += (s, e) =>
                {
                    var text = string.Join(Environment.NewLine, rows.Select(r =>
                        $"{r.SetCode}\t{r.ItemType}\t{r.SerialNumber}\t{r.BranchName}\t{r.DepartmentName}\t{r.Status}"));
                    Clipboard.SetText(text);
                };
                footer.Controls.Add(btnCopy);
                footer.Controls.Add(btnClose);

                root.Controls.Add(grid);
                root.Controls.Add(footer);

                dlg.ShowDialog(this);
            }
        }

        private void LoadConditions()
        {
            try
            {
                var repo = new ItemConditionRepository();
                var conditions = repo.GetAll();
                cmbCondition.DisplayMember = "ConditionName";
                cmbCondition.ValueMember = "ConditionId";
                cmbCondition.DataSource = conditions;
                if (cmbCondition.Items.Count > 0) cmbCondition.SelectedIndex = 0;
                UpdateConditionWarning();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to load conditions: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void UpdateSummary()
        {
            var total = _updates?.Count ?? 0;
            var willProcess = _validUpdates?.Count ?? 0;
            var skippedMissingArchived = _skippedUpdates?.Count ?? 0;
            var alreadyProcessed = _alreadyProcessedUpdates?.Count ?? 0;

            if (total == 0)
            {
                lblSummary.Text = "No updates selected. Please select at least one update to mark as processed.";
            }
            else
            {
                lblSummary.Text = $"Selected: {total}  •  Will process: {willProcess}";
            }

            var warningParts = new List<string>();
            if (skippedMissingArchived > 0)
                warningParts.Add($"{skippedMissingArchived} skipped (missing/archived)");
            if (alreadyProcessed > 0)
                warningParts.Add($"{alreadyProcessed} skipped (already processed)");

            lblWarning.Visible = warningParts.Count > 0;
            lblWarning.Text = warningParts.Count > 0 ? $"⚠ {string.Join(" • ", warningParts)}" : string.Empty;
            lnkSkippedDetails.Visible = warningParts.Count > 0;

            if (btnConfirm != null)
                btnConfirm.Enabled = willProcess > 0 && cmbCondition?.SelectedItem != null;
        }

        private void Title_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                ReleaseCapture();
                SendMessage(Handle, WM_NCLBUTTONDOWN, HTCAPTION, 0);
            }
        }

        private void BtnConfirm_Click(object sender, EventArgs e)
        {
            if (_validUpdates == null || _validUpdates.Count == 0)
            {
                MessageBox.Show("No valid updates to process.\n\nAlready processed items, missing items, and archived items cannot be marked again.",
                    "Nothing to Process", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            if (cmbCondition != null && IsRiskyCondition(cmbCondition.Text) && _validUpdates.Count > 0)
            {
                var confirm = MessageBox.Show(
                    $"You are about to mark {_validUpdates.Count} update(s) with condition '{cmbCondition.Text}'.\n\nProceed?",
                    "Confirm high-impact change",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Warning);
                if (confirm != DialogResult.Yes)
                    return;
            }

            if (cmbCondition.SelectedItem is ConditionDto condition)
            {
                SelectedConditionId = condition.ConditionId;
                SelectedConditionName = condition.ConditionName;
                var repairText = cmbRepairAction.SelectedItem?.ToString();
                SelectedRepairAction = string.IsNullOrWhiteSpace(repairText) ? null : repairText.Trim();
                this.DialogResult = DialogResult.OK;
                this.Close();
            }
            else
            {
                MessageBox.Show("Please select a condition.", "Validation", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        public void ShowProgress(int current, int total, string status = null)
        {
            if (!pnlProgress.Visible)
            {
                pnlProgress.Visible = true;
                progressBar.Maximum = total;
                progressBar.Value = 0;
            }
            progressBar.Value = current;
            if (!string.IsNullOrEmpty(status))
            {
                lblSummary.Text = status;
            }
            Application.DoEvents();
        }

        private void InitializeComponent()
        {
            this.SuspendLayout();
            this.Name = "MarkAsProcessedDialog";
            this.Text = "Mark as Processed";
            this.ResumeLayout(false);
        }
    }
}
