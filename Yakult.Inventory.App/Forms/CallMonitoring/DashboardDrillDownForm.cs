using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using Yakult.Inventory.App.Helpers;
using Yakult.Inventory.App.Models.CallMonitoring;

namespace Yakult.Inventory.App.Forms.CallMonitoring
{
    public class DashboardDrillDownForm : Form
    {
        private DataGridView dgvList;
        private Label lblTitle;
        private Panel pnlHeader;
        private Button btnClose;

        public DashboardDrillDownForm(string title, List<CallTicketListItem> tickets)
        {
            InitializeComponent();
            this.lblTitle.Text = title;
            PopulateGrid(tickets);
        }

        private void InitializeComponent()
        {
            this.Size = new Size(1000, 700);
            this.StartPosition = FormStartPosition.CenterParent;
            this.FormBorderStyle = FormBorderStyle.None;
            this.BackColor = Color.WhiteSmoke;
            this.Padding = new Padding(1); // Border effect via padding

            // 1. Header
            this.pnlHeader = new Panel
            {
                Dock = DockStyle.Top,
                Height = 70,
                BackColor = Color.White,
                Padding = new Padding(24, 0, 24, 0)
            };
            // Bottom border for header
            this.pnlHeader.Paint += (s, e) =>
            {
                using (var pen = new Pen(Color.FromArgb(230, 230, 230)))
                {
                    e.Graphics.DrawLine(pen, 0, pnlHeader.Height - 1, pnlHeader.Width, pnlHeader.Height - 1);
                }
            };

            this.lblTitle = new Label
            {
                Text = "Drill Down",
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                Font = new Font("Segoe UI", 16F, FontStyle.Bold),
                ForeColor = ModernUiHelper.ColorTextPrimary
            };

            this.btnClose = new Button
            {
                Text = "Done",
                Dock = DockStyle.Right,
                Width = 100,
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.White,
                ForeColor = ModernUiHelper.ColorTextSecondary,
                Font = new Font("Segoe UI", 9.5F, FontStyle.Regular),
                Cursor = Cursors.Hand
            };
            this.btnClose.FlatAppearance.BorderSize = 0;
            this.btnClose.Click += (s, e) => this.Close(); 
            // Center the button vertically in the header
            var btnContainer = new Panel { Dock = DockStyle.Right, Width = 110, Padding = new Padding(0, 18, 0, 18) };
            btnContainer.Controls.Add(this.btnClose);
            this.btnClose.Dock = DockStyle.Fill;


            this.pnlHeader.Controls.Add(this.lblTitle);
            this.pnlHeader.Controls.Add(btnContainer);

            // 2. Grid Container
            var pnlGridContainer = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(24),
                BackColor = ModernUiHelper.ColorBackground
            };

            // 3. Grid
            this.dgvList = new DataGridView();
            ConfigureGrid(this.dgvList);
            
            // Wrap grid in a card
            var gridCard = ModernUiHelper.CreateStyledPanel();
            gridCard.Dock = DockStyle.Fill;
            gridCard.Padding = new Padding(1); // Inner border setup
            gridCard.Controls.Add(this.dgvList);

            pnlGridContainer.Controls.Add(gridCard);

            this.Controls.Add(pnlGridContainer);
            this.Controls.Add(this.pnlHeader);
        }

        private void ConfigureGrid(DataGridView grid)
        {
            ModernUiHelper.ConfigureModernGrid(grid);
            grid.Dock = DockStyle.Fill;

            // Columns
            grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "ID", HeaderText = "TICKET ID", FillWeight = 15 });
            grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Priority", HeaderText = "PRIORITY", FillWeight = 15 });
            grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Subject", HeaderText = "ISSUE", FillWeight = 40 });
            grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Status", HeaderText = "STATUS", FillWeight = 15 });
            grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Param1", HeaderText = "ASSIGNED TO", FillWeight = 15 });
        }

        private void PopulateGrid(List<CallTicketListItem> tickets)
        {
            this.dgvList.Rows.Clear();
            if (tickets == null) return;

            foreach (var t in tickets)
            {
                var idx = this.dgvList.Rows.Add(
                    t.TicketCode ?? t.TicketId.ToString(),
                    t.Priority,
                    t.Issue,
                    t.Status,
                    t.ResponsiblePerson
                );
                
                var row = this.dgvList.Rows[idx];
                
                // Priority specific styling
                if (t.Priority == "Critical")
                {
                   row.Cells[1].Style.ForeColor = ModernUiHelper.ColorDanger;
                   row.Cells[1].Style.Font = new Font("Segoe UI", 9.5F, FontStyle.Bold);
                }
                else if (t.Priority == "High")
                {
                   row.Cells[1].Style.ForeColor = Color.DarkOrange;
                }

                // Status styling
                if (t.Status == "Solved" || t.Status == "Closed")
                {
                    row.Cells[3].Style.ForeColor = ModernUiHelper.ColorSuccess;
                }
            }
        }
        
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            // Draw a subtle border around the entire modal
            ControlPaint.DrawBorder(e.Graphics, this.ClientRectangle, Color.DarkGray, ButtonBorderStyle.Solid);
        }
    }
}
