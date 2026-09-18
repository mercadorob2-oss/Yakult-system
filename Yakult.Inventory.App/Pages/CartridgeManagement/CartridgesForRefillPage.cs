using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using Yakult.Inventory.App.Core;
using Yakult.Inventory.App.Helpers;
using Yakult.Inventory.App.Models;
using Yakult.Inventory.App.Services;
using Yakult.Inventory.App.Session;

namespace Yakult.Inventory.App.Pages.CartridgeManagement
{
    /// <summary>
    /// Page for viewing cartridges eligible for vendor refill
    /// Shows batches with return progress and enables sending to vendor when threshold is met
    /// </summary>
    public class CartridgesForRefillPage : UserControl
    {
        private Panel _headerPanel, _gridPanel;
        private Label lblTitle;
        private DataGridView dgvRefillBatches;
        private ReaLTaiizor.Controls.HopeButton btnRefresh;
        private ReaLTaiizor.Controls.MaterialCard _gridCard;
        private Button btnSendToVendor;

        private readonly CartridgeRefillService _refillService;
        private List<RefillEligibilityResult> _batches;

        public CartridgesForRefillPage()
        {
            _refillService = new CartridgeRefillService();
            _batches = new List<RefillEligibilityResult>();

            InitializeComponent();
            _ = LoadDataAsync();
        }

        private void InitializeComponent()
        {
            SuspendLayout();
            Controls.Clear();

            Dock = DockStyle.Fill;
            BackColor = Color.White;

            // Header Panel
            _headerPanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 84,
                BackColor = Color.FromArgb(245, 247, 250),
                Padding = new Padding(15, 10, 15, 10)
            };

            lblTitle = new Label
            {
                Text = "Cartridges For Refill",
                AutoSize = true,
                Font = new Font("Segoe UI", 14F, FontStyle.Bold),
                ForeColor = Color.FromArgb(40, 40, 40),
                Location = new Point(15, 10)
            };

            var lblSubtitle = new Label
            {
                Text = "Track returned cartridges and send eligible batches to vendors for refill",
                AutoSize = true,
                Font = new Font("Segoe UI", 9F),
                ForeColor = Color.FromArgb(100, 100, 100),
                Location = new Point(15, 38)
            };

            btnRefresh = new ReaLTaiizor.Controls.HopeButton
            {
                Text = "⟳",
                Font = new Font("Segoe UI", 18F, FontStyle.Bold),
                Size = new Size(58, 58),
                MinimumSize = new Size(58, 58),
                MaximumSize = new Size(58, 58),
                Location = new Point(_headerPanel.Width - 73, 13),
                Anchor = AnchorStyles.Top | AnchorStyles.Right
            };
            ConfigurePillHopeButton(btnRefresh, Color.FromArgb(52, 152, 219), Color.FromArgb(41, 128, 185));
            btnRefresh.Click += async (s, e) => await LoadDataAsync();
            btnRefresh.Resize += (s, e) =>
            {
                using (var path = new System.Drawing.Drawing2D.GraphicsPath())
                {
                    int w = Math.Max(1, btnRefresh.Width - 1);
                    int h = Math.Max(1, btnRefresh.Height - 1);
                    path.AddEllipse(0, 0, w, h);
                    btnRefresh.Region = new Region(path);
                }
            };

            _headerPanel.Controls.Add(lblTitle);
            _headerPanel.Controls.Add(lblSubtitle);
            _headerPanel.Controls.Add(btnRefresh);

            // Grid Panel
            _gridPanel = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.White,
                Padding = new Padding(15, 12, 15, 15)
            };

            _gridCard = new ReaLTaiizor.Controls.MaterialCard
            {
                Dock = DockStyle.Fill,
                BackColor = Color.White,
                Padding = new Padding(10)
            };

            // Action button
            var actionPanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 50,
                Padding = new Padding(0, 8, 0, 8)
            };

            btnSendToVendor = new Button
            {
                Text = "Send to Vendor for Refill",
                Width = 200,
                Height = 34,
                BackColor = Color.FromArgb(46, 204, 113),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand,
                Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
                Enabled = false,
                Margin = new Padding(0, 8, 0, 0),
                Padding = new Padding(10, 0, 10, 0)
            };
            btnSendToVendor.FlatAppearance.BorderSize = 0;
            btnSendToVendor.FlatAppearance.MouseOverBackColor = Color.FromArgb(39, 174, 96);
            btnSendToVendor.FlatAppearance.MouseDownBackColor = Color.FromArgb(34, 153, 84);
            btnSendToVendor.Click += BtnSendToVendor_Click;

            actionPanel.Controls.Add(btnSendToVendor);

            dgvRefillBatches = new DataGridView
            {
                Dock = DockStyle.Fill,
                AutoGenerateColumns = false,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                ReadOnly = true,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect = false,
                ScrollBars = ScrollBars.Vertical
            };

            UiFactory.StyleGrid(dgvRefillBatches);
            dgvRefillBatches.CellBorderStyle = DataGridViewCellBorderStyle.SingleVertical;
            dgvRefillBatches.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.Single;
            dgvRefillBatches.GridColor = Color.FromArgb(200, 200, 200);
            dgvRefillBatches.ColumnHeadersDefaultCellStyle.Padding = new Padding(8, 0, 8, 0);

            dgvRefillBatches.Columns.Add(new DataGridViewTextBoxColumn
            {
                DataPropertyName = "BatchId",
                HeaderText = "Batch ID",
                Width = 80,
                MinimumWidth = 80,
                AutoSizeMode = DataGridViewAutoSizeColumnMode.None,
                HeaderCell = { Style = { Alignment = DataGridViewContentAlignment.MiddleCenter } },
                DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleCenter }
            });

            dgvRefillBatches.Columns.Add(new DataGridViewTextBoxColumn
            {
                DataPropertyName = "VendorName",
                HeaderText = "Vendor",
                FillWeight = 20,
                MinimumWidth = 150,
                HeaderCell = { Style = { Alignment = DataGridViewContentAlignment.MiddleLeft } },
                DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleLeft }
            });

            dgvRefillBatches.Columns.Add(new DataGridViewTextBoxColumn
            {
                DataPropertyName = "CartridgeModel",
                HeaderText = "Cartridge Model",
                FillWeight = 25,
                MinimumWidth = 180,
                HeaderCell = { Style = { Alignment = DataGridViewContentAlignment.MiddleLeft } },
                DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleLeft }
            });

            dgvRefillBatches.Columns.Add(new DataGridViewTextBoxColumn
            {
                DataPropertyName = "OriginalQty",
                HeaderText = "Original Qty",
                Width = 100,
                HeaderCell = { Style = { Alignment = DataGridViewContentAlignment.MiddleCenter } },
                DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleCenter }
            });

            dgvRefillBatches.Columns.Add(new DataGridViewTextBoxColumn
            {
                DataPropertyName = "ReturnedQty",
                HeaderText = "Returned",
                Width = 100,
                HeaderCell = { Style = { Alignment = DataGridViewContentAlignment.MiddleCenter } },
                DefaultCellStyle = new DataGridViewCellStyle
                {
                    Alignment = DataGridViewContentAlignment.MiddleCenter,
                    Font = new Font("Segoe UI", 9F, FontStyle.Bold)
                }
            });

            dgvRefillBatches.Columns.Add(new DataGridViewTextBoxColumn
            {
                DataPropertyName = "Status",
                HeaderText = "Status",
                Width = 130,
                HeaderCell = { Style = { Alignment = DataGridViewContentAlignment.MiddleCenter } },
                DefaultCellStyle = new DataGridViewCellStyle
                {
                    Alignment = DataGridViewContentAlignment.MiddleCenter,
                    Font = new Font("Segoe UI", 9F, FontStyle.Bold)
                }
            });

            dgvRefillBatches.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            dgvRefillBatches.SelectionChanged += DgvRefillBatches_SelectionChanged;
            dgvRefillBatches.CellFormatting += DgvRefillBatches_CellFormatting;

            _gridCard.Controls.Add(dgvRefillBatches);
            _gridCard.Controls.Add(actionPanel);
            _gridPanel.Controls.Add(_gridCard);

            Controls.Add(_gridPanel);
            Controls.Add(_headerPanel);

            ResumeLayout(true);
        }

        private void ConfigurePillHopeButton(ReaLTaiizor.Controls.HopeButton button, Color baseColor, Color hoverColor)
        {
            if (button == null)
                return;

            button.ButtonType = ReaLTaiizor.Util.HopeButtonType.Primary;
            button.PrimaryColor = baseColor;
            button.DefaultColor = baseColor;
            button.BorderColor = baseColor;
            button.TextColor = Color.White;
            button.HoverTextColor = Color.White;
            button.Cursor = Cursors.Hand;

            button.MouseEnter += (s, e) =>
            {
                button.PrimaryColor = hoverColor;
                button.DefaultColor = hoverColor;
                button.BorderColor = hoverColor;
                button.Invalidate();
            };

            button.MouseLeave += (s, e) =>
            {
                button.PrimaryColor = baseColor;
                button.DefaultColor = baseColor;
                button.BorderColor = baseColor;
                button.Invalidate();
            };
        }

        private async Task LoadDataAsync()
        {
            try
            {
                btnRefresh.Enabled = false;

                // Update eligible batches first
                await _refillService.ProcessEligibleBatchesAsync();

                // Load refill eligibility data
                _batches = await _refillService.GetRefillEligibilityAsync();
                dgvRefillBatches.DataSource = _batches;

                // Clear selection
                if (dgvRefillBatches.Rows.Count > 0)
                {
                    dgvRefillBatches.ClearSelection();
                }

                btnSendToVendor.Enabled = false;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading refill data: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                Logger.LogError("CartridgesForRefillPage.LoadDataAsync failed", ex);
            }
            finally
            {
                btnRefresh.Enabled = true;
            }
        }

        private void DgvRefillBatches_SelectionChanged(object sender, EventArgs e)
        {
            if (dgvRefillBatches.SelectedRows.Count > 0)
            {
                var batch = dgvRefillBatches.SelectedRows[0].DataBoundItem as RefillEligibilityResult;
                btnSendToVendor.Enabled = batch != null && batch.Status == "Active";
            }
            else
            {
                btnSendToVendor.Enabled = false;
            }
        }

        private void DgvRefillBatches_CellFormatting(object sender, DataGridViewCellFormattingEventArgs e)
        {
            if (dgvRefillBatches.Columns[e.ColumnIndex].DataPropertyName == "Status" && e.Value != null)
            {
                var status = e.Value.ToString();
                switch (status)
                {
                    case "Active":
                        e.CellStyle.ForeColor = Color.FromArgb(52, 152, 219); // Blue
                        break;
                    case "SentForRefill":
                        e.CellStyle.ForeColor = Color.FromArgb(230, 126, 34); // Orange
                        break;
                    case "Completed":
                        e.CellStyle.ForeColor = Color.FromArgb(149, 165, 166); // Gray
                        break;
                }
            }
        }

        private async void BtnSendToVendor_Click(object sender, EventArgs e)
        {
            if (dgvRefillBatches.SelectedRows.Count == 0)
                return;

            var batch = dgvRefillBatches.SelectedRows[0].DataBoundItem as RefillEligibilityResult;
            if (batch == null || batch.Status != "Active")
                return;

            var result = MessageBox.Show(
                $"Send {batch.ReturnedQty} cartridges from batch {batch.BatchId} to {batch.VendorName} for refill?\n\n" +
                $"Model: {batch.CartridgeModel}\n" +
                $"This will mark the batch as 'SentForRefill' and create a refill transaction.",
                "Confirm Send to Vendor",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);

            if (result != DialogResult.Yes)
                return;

            try
            {
                btnSendToVendor.Enabled = false;

                // Get full batch details
                var batchDetails = await _refillService.GetBatchByIdAsync(batch.BatchId);
                if (batchDetails == null)
                {
                    MessageBox.Show("Batch not found.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                // Send to vendor
                await _refillService.SendBatchForRefillAsync(
                    batch.BatchId,
                    batchDetails.VendorId,
                    batch.ReturnedQty,
                    AppSession.CurrentUserId,
                    $"Sent {batch.ReturnedQty} empty cartridges for refill");

                MessageBox.Show(
                    $"Successfully sent {batch.ReturnedQty} cartridges to {batch.VendorName} for refill.",
                    "Success",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);

                // Reload data
                await LoadDataAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error sending to vendor: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                Logger.LogError("CartridgesForRefillPage.BtnSendToVendor_Click failed", ex);
            }
            finally
            {
                btnSendToVendor.Enabled = true;
            }
        }
    }
}
