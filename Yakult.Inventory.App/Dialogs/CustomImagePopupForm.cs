using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using Yakult.Inventory.App.Repositories;

namespace Yakult.Inventory.App.Dialogs
{
    /// <summary>Dismissible fullscreen image alert. Esc, click, or the timeout closes it.</summary>
    public sealed class CustomImagePopupForm : Form
    {
        private readonly Image _image;
        private readonly Timer _autoCloseTimer;

        private CustomImagePopupForm(byte[] imageData)
        {
            if (imageData == null || imageData.Length == 0 || imageData.Length > 8 * 1024 * 1024)
                throw new InvalidDataException("The image alert is empty or larger than 8 MB.");

            using (var stream = new MemoryStream(imageData))
            using (var decoded = Image.FromStream(stream, useEmbeddedColorManagement: false, validateImageData: true))
            {
                if (decoded.Width < 1 || decoded.Height < 1 || decoded.Width > 20000 || decoded.Height > 20000 ||
                    (long)decoded.Width * decoded.Height > 40000000L)
                    throw new InvalidDataException("The image dimensions are too large to display safely.");
                _image = new Bitmap(decoded);
            }

            var screen = Screen.PrimaryScreen ?? Screen.AllScreens[0];
            Text = "";
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.Manual;
            Bounds = screen.Bounds;
            WindowState = FormWindowState.Maximized;
            BackColor = Color.Black;
            TopMost = true;
            ShowInTaskbar = false;
            KeyPreview = true;

            var picture = new PictureBox
            {
                Dock = DockStyle.Fill,
                BackColor = Color.Black,
                SizeMode = PictureBoxSizeMode.Zoom,
                Image = _image,
                Cursor = Cursors.Hand
            };
            Controls.Add(picture);
            picture.Click += (_, __) => Close();
            Click += (_, __) => Close();
            KeyDown += (_, e) => { if (e.KeyCode == Keys.Escape) Close(); };

            var close = new Button
            {
                Text = "×",
                Font = new Font("Segoe UI", 18F, FontStyle.Bold),
                ForeColor = Color.White,
                BackColor = Color.FromArgb(180, 32, 37, 45),
                FlatStyle = FlatStyle.Flat,
                Size = new Size(48, 44),
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                Location = new Point(Math.Max(0, ClientSize.Width - 60), 12),
                Cursor = Cursors.Hand,
                TabStop = false
            };
            close.FlatAppearance.BorderSize = 0;
            close.Click += (_, __) => Close();
            Controls.Add(close);
            close.BringToFront();
            Resize += (_, __) => close.Location = new Point(Math.Max(0, ClientSize.Width - close.Width - 12), 12);

            _autoCloseTimer = new Timer { Interval = 120000 };
            _autoCloseTimer.Tick += (_, __) => Close();
            _autoCloseTimer.Start();
            FormClosed += (_, __) =>
            {
                try { _autoCloseTimer.Stop(); _autoCloseTimer.Dispose(); } catch { }
                try { _image.Dispose(); } catch { }
            };
        }

        public static void ShowAlert(AdminAlertDto alert)
        {
            if (alert == null || alert.ImageData == null) return;
            try { new CustomImagePopupForm(alert.ImageData).Show(); }
            catch { }
        }
    }
}
