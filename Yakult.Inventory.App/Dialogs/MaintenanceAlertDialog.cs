using System;
using System.Drawing;
using System.Windows.Forms;
using Yakult.Inventory.App.Repositories;

namespace Yakult.Inventory.App.Dialogs
{
    /// <summary>
    /// Quiet "system under maintenance" notice: blue info card, no sound,
    /// single acknowledge button. Dismisses on click, Esc, or the button.
    /// </summary>
    public class MaintenanceAlertDialog : Form
    {
        private MaintenanceAlertDialog(AdminAlertDto alert)
        {
            var navy = ColorTranslator.FromHtml("#1F2B45");
            var blue = ColorTranslator.FromHtml("#2471A3");
            var dimText = ColorTranslator.FromHtml("#8A94A8");

            Text = "System Maintenance";
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(430, 400);
            BackColor = blue;
            Padding = new Padding(2);
            TopMost = true;
            ShowInTaskbar = false;
            KeyPreview = true;
            Font = new Font("Segoe UI", 10F);
            KeyDown += (_, e) => { if (e.KeyCode == Keys.Escape) Close(); };

            var body = new Panel { Dock = DockStyle.Fill, BackColor = Color.White, Padding = new Padding(28, 24, 28, 24) };
            Controls.Add(body);

            var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1 };
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            body.Controls.Add(layout);

            var titleRow = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 2 };
            titleRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            titleRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            var wrench = new Label { Text = "🔧", Font = new Font("Segoe UI", 30F), AutoSize = true, Margin = new Padding(0, 0, 12, 0) };
            var title = new Label
            {
                Text = "System Under Maintenance",
                Font = new Font("Segoe UI", 17F, FontStyle.Bold),
                ForeColor = navy, AutoSize = false, Height = 60,
                TextAlign = ContentAlignment.MiddleLeft, Dock = DockStyle.Fill
            };
            titleRow.Controls.Add(wrench, 0, 0);
            titleRow.Controls.Add(title, 1, 0);
            layout.Controls.Add(titleRow, 0, 0);

            string bodyText = !string.IsNullOrWhiteSpace(alert.Message)
                ? alert.Message
                : "This workstation has been flagged for scheduled maintenance by IT. Please save your work. No action is needed right now.";
            var message = new Label
            {
                Text = bodyText, Font = new Font("Segoe UI", 11F), ForeColor = Color.FromArgb(60, 60, 60),
                AutoSize = false, Height = 110, Dock = DockStyle.Top, Margin = new Padding(0, 14, 0, 0)
            };
            layout.Controls.Add(message, 0, 1);

            var details = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 2, Margin = new Padding(0, 6, 0, 0) };
            details.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 70));
            details.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            AddDetailRow(details, dimText, 0, "From:", string.IsNullOrWhiteSpace(alert.CreatedByName) ? "IT Department" : alert.CreatedByName);
            AddDetailRow(details, dimText, 1, "Sent:", alert.CreatedAt == DateTime.MinValue ? "just now" : alert.CreatedAt.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss"));
            layout.Controls.Add(details, 0, 2);

            var ok = new Button
            {
                Text = "Got it",
                Font = new Font("Segoe UI", 11F, FontStyle.Bold),
                ForeColor = Color.White, BackColor = blue, FlatStyle = FlatStyle.Flat,
                Height = 44, Dock = DockStyle.Bottom, Cursor = Cursors.Hand
            };
            ok.FlatAppearance.BorderSize = 0;
            ok.Click += (_, __) => Close();
            layout.Controls.Add(ok, 0, 3);

            MouseClick += (_, __) => Close();
            Shown += (_, __) => ok.Focus();
        }

        public static void ShowAlert(AdminAlertDto alert)
        {
            if (alert == null) return;
            try { new MaintenanceAlertDialog(alert).Show(); }
            catch { }
        }

        private static void AddDetailRow(TableLayoutPanel grid, Color labelColor, int row, string label, string value)
        {
            grid.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            grid.Controls.Add(new Label { Text = label, ForeColor = labelColor, Font = new Font("Segoe UI", 9.5F), AutoSize = true, Margin = new Padding(0, 3, 0, 3) }, 0, row);
            grid.Controls.Add(new Label { Text = value, ForeColor = Color.FromArgb(60, 60, 60), Font = new Font("Segoe UI", 9.5F), AutoSize = true, MaximumSize = new Size(280, 0), Margin = new Padding(0, 3, 0, 3) }, 1, row);
        }
    }
}
