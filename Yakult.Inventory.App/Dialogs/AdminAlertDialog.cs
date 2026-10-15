using System;
using System.Drawing;
using System.IO;
using System.Media;
using System.Threading.Tasks;
using System.Windows.Forms;
using Yakult.Inventory.App.Repositories;

namespace Yakult.Inventory.App.Dialogs
{
    /// <summary>
    /// Standalone WinForms twin of the workspace STOP alert: dark-navy popup
    /// with flashing sirens, blinking STOP banner and the alarm sound. Shown
    /// modeless + topmost by the broadcast poller so it surprises the user.
    /// </summary>
    public class AdminAlertDialog : Form
    {
        private static System.Windows.Media.MediaPlayer _player;

        private readonly Timer _blinkTimer;
        private readonly Timer _shakeTimer;
        private readonly Label _stopLabel;
        private readonly Panel _leftSiren;
        private readonly Panel _rightSiren;
        private readonly Panel _bannerBorder;
        private int _shakeTick;
        private bool _blinkOn = true;

        private AdminAlertDialog(AdminAlertDto alert)
        {
            var navy = ColorTranslator.FromHtml("#1F2B45");
            var bannerBg = ColorTranslator.FromHtml("#141D33");
            var alarm = ColorTranslator.FromHtml("#E53935");
            var dimText = ColorTranslator.FromHtml("#8A94A8");

            Text = "STOP";
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(410, 560);
            BackColor = alarm;
            Padding = new Padding(2);
            TopMost = true;
            ShowInTaskbar = false;
            KeyPreview = true;
            Font = new Font("Segoe UI", 10F);
            KeyDown += (_, e) => { if (e.KeyCode == Keys.Escape) Close(); };

            var body = new Panel { Dock = DockStyle.Fill, BackColor = navy, Padding = new Padding(22, 12, 22, 20) };
            Controls.Add(body);

            var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, AutoSize = false };
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            body.Controls.Add(layout);

            // Top bar: siren pills left/right, close X at the far right.
            var topBar = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 4 };
            topBar.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            topBar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            topBar.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            topBar.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            _leftSiren = new Panel { Size = new Size(44, 10), BackColor = alarm, Margin = new Padding(0, 8, 0, 0) };
            _rightSiren = new Panel { Size = new Size(44, 10), BackColor = alarm, Margin = new Padding(0, 8, 10, 0) };
            var closeX = new Button { Text = "✕", FlatStyle = FlatStyle.Flat, ForeColor = dimText, Font = new Font("Segoe UI", 12F), Size = new Size(36, 30), Cursor = Cursors.Hand };
            closeX.FlatAppearance.BorderSize = 0;
            closeX.Click += (_, __) => Close();
            topBar.Controls.Add(_leftSiren, 0, 0);
            topBar.Controls.Add(new Label { AutoSize = false }, 1, 0);
            topBar.Controls.Add(_rightSiren, 2, 0);
            topBar.Controls.Add(closeX, 3, 0);
            layout.Controls.Add(topBar, 0, 0);

            // STOP banner with red border.
            _bannerBorder = new Panel { BackColor = alarm, Padding = new Padding(2), Dock = DockStyle.Top, Margin = new Padding(0, 8, 0, 0) };
            var banner = new Panel { BackColor = bannerBg, Dock = DockStyle.Fill, Padding = new Padding(8) };
            _stopLabel = new Label { Text = "STOP", Font = new Font("Segoe UI", 48F, FontStyle.Bold), ForeColor = alarm, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleCenter, AutoSize = false, Height = 100 };
            banner.Controls.Add(_stopLabel);
            _bannerBorder.Controls.Add(banner);
            _bannerBorder.Height = 120;
            layout.Controls.Add(_bannerBorder, 0, 1);

            var heading = new Label
            {
                Text = "SYSTEM ALERT: Unauthorized Access Attempt Detected.",
                Font = new Font("Segoe UI", 11F, FontStyle.Bold),
                ForeColor = Color.White,
                AutoSize = false,
                Height = 48,
                Dock = DockStyle.Top,
                TextAlign = ContentAlignment.MiddleCenter
            };
            layout.Controls.Add(heading, 0, 2);

            var details = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 2 };
            details.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 90));
            details.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            AddDetailRow(details, dimText, 0, "From:", string.IsNullOrWhiteSpace(alert.CreatedByName) ? "Admin" : alert.CreatedByName);
            AddDetailRow(details, dimText, 1, "Sent:", alert.CreatedAt == DateTime.MinValue ? "just now" : alert.CreatedAt.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss"));
            AddDetailRow(details, dimText, 2, "Origin IP:", GetLocalIpAddress());
            if (!string.IsNullOrWhiteSpace(alert.Message))
                AddDetailRow(details, dimText, 3, "Note:", alert.Message);
            layout.Controls.Add(details, 0, 3);

            var buttons = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1 };
            buttons.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            buttons.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            var terminate = new Button
            {
                Text = "TERMINATE SESSION",
                Font = new Font("Segoe UI", 11F, FontStyle.Bold),
                ForeColor = Color.White,
                BackColor = ColorTranslator.FromHtml("#C62828"),
                FlatStyle = FlatStyle.Flat,
                Height = 44,
                Dock = DockStyle.Top,
                Margin = new Padding(0, 14, 0, 0),
                Cursor = Cursors.Hand
            };
            terminate.FlatAppearance.BorderSize = 0;
            terminate.Click += (_, __) => Close();
            var review = new Button
            {
                Text = "REVIEW LOGS",
                Font = new Font("Segoe UI", 11F, FontStyle.Bold),
                ForeColor = ColorTranslator.FromHtml("#DDE3ED"),
                BackColor = navy,
                FlatStyle = FlatStyle.Flat,
                Height = 44,
                Dock = DockStyle.Top,
                Margin = new Padding(0, 8, 0, 0),
                Cursor = Cursors.Hand
            };
            review.FlatAppearance.BorderColor = ColorTranslator.FromHtml("#5A6B8C");
            review.FlatAppearance.BorderSize = 1;
            review.Click += (_, __) => Close();
            buttons.Controls.Add(terminate, 0, 0);
            buttons.Controls.Add(review, 0, 1);
            layout.Controls.Add(buttons, 0, 4);

            _blinkTimer = new Timer { Interval = 500 };
            _blinkTimer.Tick += (_, __) =>
            {
                _blinkOn = !_blinkOn;
                _stopLabel.Visible = _blinkOn;
                var c = _leftSiren.BackColor;
                _leftSiren.BackColor = _rightSiren.BackColor;
                _rightSiren.BackColor = c;
                _leftSiren.Visible = _rightSiren.Visible = true;
                _leftSiren.BackColor = _blinkOn ? alarm : ColorTranslator.FromHtml("#7A1512");
                _rightSiren.BackColor = _blinkOn ? ColorTranslator.FromHtml("#7A1512") : alarm;
            };
            _blinkTimer.Start();

            _shakeTimer = new Timer { Interval = 50 };
            _shakeTimer.Tick += (_, __) =>
            {
                int[] offsets = { -10, 9, -6, 4, 0 };
                if (_shakeTick < offsets.Length)
                    Left += offsets[_shakeTick++];
                else
                    _shakeTimer.Stop();
            };
            _shakeTimer.Start();

            FormClosed += (_, __) =>
            {
                try { _blinkTimer.Stop(); } catch { }
                try { _shakeTimer.Stop(); } catch { }
            };
        }

        public static void ShowAlert(AdminAlertDto alert)
        {
            ShowAlert(alert, playSound: true);
        }

        public static void ShowAlert(AdminAlertDto alert, bool playSound)
        {
            if (alert == null) return;
            try
            {
                var dlg = new AdminAlertDialog(alert);
                dlg.Show();
                if (playSound) PlayAlarmSound();
            }
            catch { }
        }

        private static void AddDetailRow(TableLayoutPanel grid, Color labelColor, int row, string label, string value)
        {
            grid.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            grid.Controls.Add(new Label { Text = label, ForeColor = labelColor, Font = new Font("Segoe UI", 9.5F), AutoSize = true, Margin = new Padding(0, 3, 0, 3) }, 0, row);
            grid.Controls.Add(new Label { Text = value, ForeColor = Color.White, Font = new Font("Segoe UI", 9.5F), AutoSize = true, MaximumSize = new Size(250, 0), Margin = new Padding(0, 3, 0, 3) }, 1, row);
        }

        private static string GetLocalIpAddress()
        {
            try
            {
                var host = System.Net.Dns.GetHostEntry(System.Net.Dns.GetHostName());
                foreach (var ip in host.AddressList)
                    if (ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork && !System.Net.IPAddress.IsLoopback(ip))
                        return ip.ToString();
            }
            catch { }
            return "Unknown";
        }

        private static void PlayAlarmSound()
        {
            try
            {
                string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                string soundPath = null;
                foreach (var candidate in new[]
                {
                    Path.Combine(baseDir, "Images", "Danger Alarm Sound Effect.mp3"),
                    Path.Combine(baseDir, "images", "Danger Alarm Sound Effect.mp3"),
                    Path.Combine(baseDir, "Danger Alarm Sound Effect.mp3")
                })
                {
                    if (File.Exists(candidate)) { soundPath = candidate; break; }
                }
                if (soundPath != null)
                {
                    try
                    {
                        try { _player?.Stop(); _player?.Close(); } catch { }
                        _player = null;
                        var player = new System.Windows.Media.MediaPlayer { Volume = 1.0 };
                        _player = player;
                        player.MediaEnded += (_, __) =>
                        {
                            try { player.Close(); } catch { }
                            if (ReferenceEquals(_player, player)) _player = null;
                        };
                        player.MediaFailed += (_, __) => PlayFallbackBeeps();
                        player.Open(new Uri(soundPath));
                        player.Play();
                        return;
                    }
                    catch { }
                }
            }
            catch { }
            PlayFallbackBeeps();
        }

        private static void PlayFallbackBeeps()
        {
            try { SystemSounds.Hand.Play(); } catch { }
            Task.Run(() =>
            {
                try
                {
                    for (int i = 0; i < 3; i++)
                    {
                        Console.Beep(988, 160);
                        Console.Beep(659, 160);
                    }
                }
                catch { }
            });
        }
    }
}
