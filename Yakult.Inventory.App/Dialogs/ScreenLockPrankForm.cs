using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace Yakult.Inventory.App.Dialogs
{
    /// <summary>
    /// Fullscreen fake ransomware lock screen prank. Covers the taskbar,
    /// ignores Esc and Alt+F4, and only closes when the password 1234 is
    /// entered. Windows itself remains unlocked underneath.
    /// </summary>
    public class ScreenLockPrankForm : Form
    {
        private const string UnlockPassword = "1234";

        private readonly TextBox _passwordBox;
        private readonly Label _passwordPlaceholder;
        private readonly Label _errorLabel;
        private readonly Label _countdownLabel;
        private readonly Label _stampLabel;
        private readonly Panel _alertBar;
        private readonly Label _alertLabel;
        private readonly Timer _countdownTimer;
        private readonly Timer _alertTimer;
        private readonly string _referenceId;
        private TimeSpan _remaining = TimeSpan.FromSeconds(86399);
        private bool _unlocked;
        private bool _blinkOn = true;

        private ScreenLockPrankForm()
        {
            var black = ColorTranslator.FromHtml("#050506");
            var cardBg = ColorTranslator.FromHtml("#0C0A0B");
            var deepRed = ColorTranslator.FromHtml("#4A0D0D");
            var alertRed = ColorTranslator.FromHtml("#C40000");
            var brightRed = ColorTranslator.FromHtml("#FF3B30");
            var white = Color.FromArgb(245, 237, 237);
            var dimText = ColorTranslator.FromHtml("#C9B2B2");
            var mutedText = ColorTranslator.FromHtml("#8A6B6B");

            _referenceId = "REF-" + Guid.NewGuid().ToString("N").Substring(0, 12).ToUpperInvariant();

            Text = "SYSTEM LOCKED";
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.Manual;
            Bounds = Screen.PrimaryScreen.Bounds;
            WindowState = FormWindowState.Maximized;
            BackColor = black;
            TopMost = true;
            ShowInTaskbar = false;
            KeyPreview = true;
            Cursor = Cursors.Default;
            Font = new Font("Segoe UI", 10F);
            DoubleBuffered = true;

            var root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                BackColor = Color.Transparent
            };
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 54F));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            Controls.Add(root);

            // Blinking red banner across the top.
            _alertBar = new Panel { Dock = DockStyle.Fill, BackColor = alertRed, Margin = new Padding(0) };
            _alertLabel = new Label
            {
                Text = "SYSTEM LOCKED — YOUR FILES HAVE BEEN ENCRYPTED",
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleCenter,
                Font = new Font("Segoe UI", 13F, FontStyle.Bold),
                ForeColor = Color.White,
                BackColor = Color.Transparent,
                Margin = new Padding(0)
            };
            _alertBar.Controls.Add(_alertLabel);
            root.Controls.Add(_alertBar, 0, 0);

            var contentHost = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 1,
                BackColor = Color.Transparent,
                Padding = new Padding(10)
            };
            contentHost.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            contentHost.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.Controls.Add(contentHost, 0, 1);

            var card = new TableLayoutPanel
            {
                Width = 720,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                ColumnCount = 1,
                BackColor = cardBg,
                Padding = new Padding(24, 22, 24, 22),
                Anchor = AnchorStyles.None,
                Margin = new Padding(0)
            };
            card.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            card.Paint += (_, e) =>
            {
                using (var pen = new Pen(ColorTranslator.FromHtml("#7A0C0C"), 1))
                    e.Graphics.DrawRectangle(pen, 0, 0, card.Width - 1, card.Height - 1);
            };
            contentHost.Controls.Add(card, 0, 0);

            var warning = new RansomWarningIcon
            {
                Size = new Size(66, 60),
                Anchor = AnchorStyles.None,
                Margin = new Padding(0)
            };
            card.Controls.Add(warning, 0, AddRow(card));

            var title = new Label
            {
                Text = "YOUR FILES HAVE BEEN ENCRYPTED",
                Font = new Font("Segoe UI", 22F, FontStyle.Bold),
                ForeColor = white,
                AutoSize = true,
                Anchor = AnchorStyles.None,
                Margin = new Padding(0, 10, 0, 0)
            };
            card.Controls.Add(title, 0, AddRow(card));

            var subtitle = new Label
            {
                Text = "All documents, photos, and databases on this computer are locked.",
                Font = new Font("Segoe UI", 10.5F),
                ForeColor = dimText,
                AutoSize = true,
                Anchor = AnchorStyles.None,
                Margin = new Padding(0, 4, 0, 0)
            };
            card.Controls.Add(subtitle, 0, AddRow(card));

            var divider = new Panel
            {
                Width = 600,
                Height = 1,
                BackColor = deepRed,
                Anchor = AnchorStyles.None,
                Margin = new Padding(0, 14, 0, 14)
            };
            card.Controls.Add(divider, 0, AddRow(card));

            var note = new Label
            {
                Text = "To restore your files, obtain the decryption key from the administrator.\r\n" +
                       "Do not restart or shut down this computer. Failure to enter the key\r\n" +
                       "will result in permanent data loss.",
                Font = new Font("Consolas", 10F),
                ForeColor = dimText,
                AutoSize = false,
                Width = 620,
                Height = 62,
                TextAlign = ContentAlignment.MiddleCenter,
                Anchor = AnchorStyles.None,
                Margin = new Padding(0)
            };
            card.Controls.Add(note, 0, AddRow(card));

            // Dummy payment QR: purely decorative, scans to nothing.
            var qrBlock = new TableLayoutPanel
            {
                ColumnCount = 1,
                AutoSize = true,
                Anchor = AnchorStyles.None,
                Margin = new Padding(0, 12, 0, 0)
            };
            qrBlock.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            qrBlock.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            qrBlock.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            var qr = new DummyQrBox
            {
                Size = new Size(126, 126),
                Anchor = AnchorStyles.None,
                Margin = new Padding(0)
            };
            qrBlock.Controls.Add(qr, 0, 0);

            var qrCaption = new Label
            {
                Text = "PAY TO UNLOCK",
                Font = new Font("Segoe UI", 10.5F, FontStyle.Bold),
                ForeColor = brightRed,
                AutoSize = false,
                Width = 126,
                Height = 22,
                TextAlign = ContentAlignment.MiddleCenter,
                Anchor = AnchorStyles.None,
                Margin = new Padding(0, 5, 0, 0)
            };
            qrBlock.Controls.Add(qrCaption, 0, 1);
            card.Controls.Add(qrBlock, 0, AddRow(card));

            _countdownLabel = new Label
            {
                Text = "TIME REMAINING  23:59:59",
                Font = new Font("Consolas", 12F, FontStyle.Bold),
                ForeColor = brightRed,
                AutoSize = true,
                Anchor = AnchorStyles.None,
                Margin = new Padding(0, 14, 0, 0)
            };
            card.Controls.Add(_countdownLabel, 0, AddRow(card));

            // Decryption-key row: dark input box plus a red DECRYPT button.
            var inputRow = new FlowLayoutPanel
            {
                AutoSize = true,
                WrapContents = false,
                Anchor = AnchorStyles.None,
                Margin = new Padding(0, 10, 0, 0)
            };

            var passwordHost = new Panel
            {
                Width = 380,
                Height = 44,
                BackColor = deepRed,
                Padding = new Padding(1),
                Margin = new Padding(0)
            };
            var passwordInner = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = ColorTranslator.FromHtml("#120A0B"),
                Padding = new Padding(12, 0, 12, 0)
            };
            _passwordBox = new TextBox
            {
                Dock = DockStyle.Fill,
                BorderStyle = BorderStyle.None,
                BackColor = ColorTranslator.FromHtml("#120A0B"),
                ForeColor = white,
                Font = new Font("Segoe UI", 13F),
                UseSystemPasswordChar = true,
                Margin = new Padding(0)
            };
            _passwordBox.KeyDown += PasswordBox_KeyDown;
            passwordInner.Controls.Add(_passwordBox);

            _passwordPlaceholder = new Label
            {
                Text = "Enter decryption key",
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                ForeColor = mutedText,
                BackColor = ColorTranslator.FromHtml("#120A0B"),
                Font = new Font("Segoe UI", 11.5F),
                Cursor = Cursors.IBeam,
                Margin = new Padding(0)
            };
            _passwordPlaceholder.Click += (_, __) => _passwordBox.Focus();
            _passwordBox.TextChanged += (_, __) =>
                _passwordPlaceholder.Visible = _passwordBox.Text.Length == 0;
            passwordInner.Controls.Add(_passwordPlaceholder);
            _passwordPlaceholder.BringToFront();
            passwordHost.Controls.Add(passwordInner);
            inputRow.Controls.Add(passwordHost);

            var decryptButton = new Button
            {
                Text = "DECRYPT",
                Width = 120,
                Height = 44,
                FlatStyle = FlatStyle.Flat,
                BackColor = ColorTranslator.FromHtml("#C62828"),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                Cursor = Cursors.Hand,
                Margin = new Padding(8, 0, 0, 0)
            };
            decryptButton.FlatAppearance.BorderSize = 0;
            decryptButton.Click += (_, __) => TryUnlock();
            inputRow.Controls.Add(decryptButton);

            card.Controls.Add(inputRow, 0, AddRow(card));

            _errorLabel = new Label
            {
                Text = "",
                Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                ForeColor = brightRed,
                AutoSize = true,
                Anchor = AnchorStyles.None,
                Margin = new Padding(0, 8, 0, 0)
            };
            card.Controls.Add(_errorLabel, 0, AddRow(card));

            _stampLabel = new Label
            {
                Text = _referenceId + "   •   " + DateTime.Now.ToString("dd MMM yyyy HH:mm"),
                Font = new Font("Consolas", 8.5F),
                ForeColor = ColorTranslator.FromHtml("#6E5A5A"),
                AutoSize = true,
                Anchor = AnchorStyles.None,
                Margin = new Padding(0, 12, 0, 0)
            };
            card.Controls.Add(_stampLabel, 0, AddRow(card));

            _countdownTimer = new Timer { Interval = 1000 };
            _countdownTimer.Tick += (_, __) => UpdateCountdown();
            _countdownTimer.Start();

            _alertTimer = new Timer { Interval = 700 };
            _alertTimer.Tick += (_, __) =>
            {
                _blinkOn = !_blinkOn;
                _alertBar.BackColor = _blinkOn ? alertRed : ColorTranslator.FromHtml("#7A0000");
                _alertLabel.ForeColor = _blinkOn ? Color.White : ColorTranslator.FromHtml("#FFD6D6");
            };
            _alertTimer.Start();

            Shown += (_, __) =>
            {
                Activate();
                _passwordBox.Focus();
            };
            Activated += (_, __) =>
            {
                if (!_passwordBox.Focused) _passwordBox.Focus();
            };
            FormClosed += (_, __) =>
            {
                try { _countdownTimer.Stop(); } catch { }
                try { _alertTimer.Stop(); } catch { }
            };
        }

        private static int AddRow(TableLayoutPanel layout)
        {
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            return layout.RowCount++;
        }

        private void UpdateCountdown()
        {
            if (_remaining > TimeSpan.Zero)
                _remaining = _remaining.Subtract(TimeSpan.FromSeconds(1));

            _countdownLabel.Text = _remaining > TimeSpan.Zero
                ? "TIME REMAINING  " + _remaining.ToString(@"hh\:mm\:ss")
                : "TIME EXPIRED — FILES LOCKED";
        }

        private void PasswordBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode != Keys.Enter) return;
            e.SuppressKeyPress = true;
            TryUnlock();
        }

        private void TryUnlock()
        {
            if (string.Equals(_passwordBox.Text, UnlockPassword, StringComparison.Ordinal))
            {
                _unlocked = true;
                Close();
                return;
            }

            _passwordBox.Clear();
            _errorLabel.Text = "INVALID DECRYPTION KEY. CONTACT THE ADMINISTRATOR.";
            _passwordBox.Focus();
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == Keys.Escape) return true;
            if (keyData == (Keys.Alt | Keys.F4)) return true;
            return base.ProcessCmdKey(ref msg, keyData);
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (!_unlocked && e.CloseReason != CloseReason.WindowsShutDown
                           && e.CloseReason != CloseReason.TaskManagerClosing)
            {
                e.Cancel = true;
                _passwordBox.Focus();
                return;
            }
            base.OnFormClosing(e);
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            var rect = ClientRectangle;
            if (rect.Width <= 0 || rect.Height <= 0) return;

            using (var gradient = new LinearGradientBrush(rect,
                Color.FromArgb(4, 4, 5), Color.FromArgb(32, 5, 7), 90f))
            {
                e.Graphics.FillRectangle(gradient, rect);
            }
        }

        public static void ShowAlert()
        {
            try { new ScreenLockPrankForm().Show(); }
            catch { }
        }

        /// <summary>Drawn red warning triangle (emoji fonts are unreliable).</summary>
        private sealed class RansomWarningIcon : Control
        {
            public RansomWarningIcon()
            {
                DoubleBuffered = true;
                BackColor = ColorTranslator.FromHtml("#0C0A0B");
                TabStop = false;
                SetStyle(ControlStyles.Selectable, false);
                SetStyle(ControlStyles.ResizeRedraw, true);
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                var points = new[]
                {
                    new PointF(33, 3),
                    new PointF(62, 56),
                    new PointF(4, 56)
                };

                using (var pen = new Pen(ColorTranslator.FromHtml("#7A0C0C"), 2f))
                using (var brush = new SolidBrush(ColorTranslator.FromHtml("#E53935")))
                {
                    e.Graphics.FillPolygon(brush, points);
                    e.Graphics.DrawPolygon(pen, points);
                }

                using (var brush = new SolidBrush(Color.White))
                {
                    e.Graphics.FillRectangle(brush, 30, 20, 6, 19);
                    e.Graphics.FillRectangle(brush, 30, 43, 6, 6);
                }
            }
        }

        /// <summary>
        /// Fake QR code drawn with GDI+, with proper finder squares and timing
        /// rows so it looks real. Deterministic pattern, scans to nothing.
        /// </summary>
        private sealed class DummyQrBox : Control
        {
            private const int ModuleCount = 25;
            private readonly bool[,] _cells = new bool[ModuleCount, ModuleCount];

            public DummyQrBox()
            {
                DoubleBuffered = true;
                BackColor = Color.White;
                TabStop = false;
                SetStyle(ControlStyles.Selectable, false);
                SetStyle(ControlStyles.ResizeRedraw, true);

                var rng = new Random(1234);
                for (int y = 0; y < ModuleCount; y++)
                    for (int x = 0; x < ModuleCount; x++)
                        _cells[x, y] = rng.Next(2) == 0;

                ClearFinder(0, 0);
                ClearFinder(ModuleCount - 7, 0);
                ClearFinder(0, ModuleCount - 7);

                for (int i = 8; i < ModuleCount - 8; i++)
                {
                    _cells[i, 6] = i % 2 == 0;
                    _cells[6, i] = i % 2 == 0;
                }
            }

            private void ClearFinder(int left, int top)
            {
                for (int y = 0; y < 7; y++)
                    for (int x = 0; x < 7; x++)
                        _cells[left + x, top + y] = false;
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                e.Graphics.Clear(Color.White);

                const int margin = 8;
                float module = (Math.Min(Width, Height) - margin * 2f) / ModuleCount;

                using (var ink = new SolidBrush(Color.FromArgb(18, 18, 18)))
                {
                    for (int y = 0; y < ModuleCount; y++)
                        for (int x = 0; x < ModuleCount; x++)
                            if (_cells[x, y])
                                e.Graphics.FillRectangle(ink,
                                    margin + x * module, margin + y * module, module, module);

                    DrawFinder(e.Graphics, ink, margin, module, 0, 0);
                    DrawFinder(e.Graphics, ink, margin, module, ModuleCount - 7, 0);
                    DrawFinder(e.Graphics, ink, margin, module, 0, ModuleCount - 7);
                }
            }

            private static void DrawFinder(Graphics g, Brush ink, int margin, float module, int left, int top)
            {
                float x = margin + left * module;
                float y = margin + top * module;
                g.FillRectangle(ink, x, y, module * 7, module * 7);
                g.FillRectangle(Brushes.White, x + module, y + module, module * 5, module * 5);
                g.FillRectangle(ink, x + module * 2, y + module * 2, module * 3, module * 3);
            }
        }
    }
}
