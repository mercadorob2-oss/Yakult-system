using System;
using System.Drawing;
using System.Windows.Forms;

namespace Yakult.Inventory.App.Dialogs
{
    /// <summary>
    /// Fake fullscreen blue screen prank. Looks like a Windows crash with a
    /// climbing fake progress counter. Always escapable: Esc or click closes
    /// it, and it auto-closes after two minutes so no PC is ever stuck.
    /// </summary>
    public class FakeBsodForm : Form
    {
        private readonly Timer _progressTimer;
        private readonly Timer _autoCloseTimer;
        private readonly Label _percentLabel;
        private int _percent = 12;

        private FakeBsodForm()
        {
            Text = "";
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.Manual;
            Location = new Point(0, 0);
            Size = Screen.PrimaryScreen.Bounds.Size;
            WindowState = FormWindowState.Maximized;
            BackColor = Color.FromArgb(0, 120, 215);
            TopMost = true;
            ShowInTaskbar = false;
            Cursor = Cursors.Default;
            Font = new Font("Segoe UI", 12F);
            KeyPreview = true;

            var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1 };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 62));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 38));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            Controls.Add(layout);

            var left = new Panel { Dock = DockStyle.Fill, Padding = new Padding(90, 70, 40, 40), BackColor = BackColor };
            var face = new Label { Text = ":(", Font = new Font("Segoe UI", 120F), ForeColor = Color.White, AutoSize = true };
            var msg = new Label
            {
                Text = "Your PC ran into a problem and needs to restart. We're just collecting some error info, and then we'll restart for you.",
                Font = new Font("Segoe UI", 20F), ForeColor = Color.White, AutoSize = false,
                Width = 640, Height = 130, Margin = new Padding(0, 20, 0, 0)
            };
            _percentLabel = new Label { Text = _percent + "% complete", Font = new Font("Segoe UI", 20F), ForeColor = Color.White, AutoSize = true, Margin = new Padding(0, 25, 0, 0) };
            var more = new Label
            {
                Text = "For more information about this issue and possible fixes, visit https://www.windows.com/stopcode\r\n\r\nIf you call a support person, give them this info:\r\nStop code: CRITICAL PROCESS DIED",
                Font = new Font("Segoe UI", 10F), ForeColor = Color.White, AutoSize = false,
                Width = 640, Height = 110, Margin = new Padding(0, 30, 0, 0)
            };
            var hint = new Label
            {
                Text = "IT prank — press ESC to dismiss",
                Font = new Font("Segoe UI", 9F, FontStyle.Italic), ForeColor = Color.FromArgb(160, 200, 240),
                AutoSize = true, Margin = new Padding(0, 40, 0, 0)
            };
            left.Controls.Add(face);
            left.Controls.Add(msg);
            left.Controls.Add(_percentLabel);
            left.Controls.Add(more);
            left.Controls.Add(hint);
            face.Dock = DockStyle.Top;
            msg.Dock = DockStyle.Top;
            _percentLabel.Dock = DockStyle.Top;
            more.Dock = DockStyle.Top;
            hint.Dock = DockStyle.Top;
            layout.Controls.Add(left, 0, 0);

            var right = new Panel { Dock = DockStyle.Fill, BackColor = BackColor };
            var logo = new Label
            {
                Text = "⊞", Font = new Font("Segoe UI", 150F), ForeColor = Color.White,
                AutoSize = false, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleCenter
            };
            right.Controls.Add(logo);
            layout.Controls.Add(right, 1, 0);

            _progressTimer = new Timer { Interval = 2000 };
            _progressTimer.Tick += (_, __) =>
            {
                if (_percent < 99) _percent = Math.Min(99, _percent + 7);
                _percentLabel.Text = _percent + "% complete";
            };
            _progressTimer.Start();

            _autoCloseTimer = new Timer { Interval = 120000 };
            _autoCloseTimer.Tick += (_, __) => Close();
            _autoCloseTimer.Start();

            KeyDown += (_, e) => { if (e.KeyCode == Keys.Escape) Close(); };
            MouseClick += (_, __) => Close();
            FormClosed += (_, __) =>
            {
                try { _progressTimer.Stop(); } catch { }
                try { _autoCloseTimer.Stop(); } catch { }
            };
        }

        public static void ShowAlert()
        {
            try { new FakeBsodForm().Show(); }
            catch { }
        }
    }
}
