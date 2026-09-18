using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using Yakult.Inventory.App.Models;
using Yakult.Inventory.App.Repositories;
using Yakult.Inventory.App.Session;
using Yakult.Inventory.App.Services;

namespace Yakult.Inventory.App.Forms.Notifications
{
    public partial class NotificationSettingsForm : Form
    {
        private static readonly Color CBlue      = Color.FromArgb(78, 154, 252);
        private static readonly Color CBlueDark  = Color.FromArgb(58, 134, 232);
        private static readonly Color CRed       = Color.FromArgb(213, 0, 50);
        private static readonly Color CTextDark  = Color.FromArgb(22, 22, 40);
        private static readonly Color CTextMid   = Color.FromArgb(80, 80, 100);
        private static readonly Color CBorder    = Color.FromArgb(218, 218, 230);

        private readonly UserNotificationSettingRepository _settingRepo;
        private readonly NotificationRepository            _notifRepo;

        private CheckBox _chkEnabled;
        private Label    _lblStatus;
        private Button   _btnSave;
        private Button   _btnTest;
        private Button   _btnClose;

        public NotificationSettingsForm()
        {
            _settingRepo = new UserNotificationSettingRepository();
            _notifRepo   = new NotificationRepository();
            InitializeComponent();
            BuildUI();
            LoadSettings();
        }

        private void BuildUI()
        {
            this.Text            = "Notification Settings";
            this.Size            = new Size(580, 340);
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox     = false;
            this.MinimizeBox     = false;
            this.StartPosition   = FormStartPosition.CenterParent;
            this.BackColor       = Color.White;
            this.Font            = new Font("Segoe UI", 9.5F);

            // ── Header (DockStyle.Top) ─────────────────────────────────────────
            const int headerH = 58;
            var header = new Panel
            {
                Dock      = DockStyle.Top,
                Height    = headerH,
                BackColor = CBlue
            };
            header.Paint += (s, e) =>
            {
                using (var br = new LinearGradientBrush(
                    new Rectangle(0, 0, header.Width, header.Height),
                    CBlue, CBlueDark, LinearGradientMode.Horizontal))
                    e.Graphics.FillRectangle(br, 0, 0, header.Width, header.Height);
            };
            var lblHeader = new Label
            {
                Text      = "🔔  Notification Settings",
                Font      = new Font("Segoe UI", 13F, FontStyle.Bold),
                ForeColor = Color.White,
                AutoSize  = false,
                Dock      = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding   = new Padding(16, 0, 0, 0),
                BackColor = Color.Transparent
            };
            header.Controls.Add(lblHeader);
            this.Controls.Add(header);

            // ── Body panel (DockStyle.Fill — sits below header automatically) ─
            var body = new Panel
            {
                Dock      = DockStyle.Fill,
                BackColor = Color.White,
                Padding   = new Padding(0)
            };
            this.Controls.Add(body);

            // Place body controls using local coordinates inside body panel
            const int px = 28;
            int y = 22;

            // "Enable Notifications" checkbox
            _chkEnabled = new CheckBox
            {
                Text      = "Enable Notifications",
                Font      = new Font("Segoe UI", 11F, FontStyle.Bold),
                ForeColor = CTextDark,
                AutoSize  = true,
                Location  = new Point(px, y),
                Checked   = true,
                Cursor    = Cursors.Hand
            };
            body.Controls.Add(_chkEnabled);
            y += 34;

            // Description
            var lblDesc = new Label
            {
                Text      = "When enabled, in-app notification bubbles and Windows toast alerts are shown.\r\n" +
                            "Notification records are always saved for history regardless of this setting.",
                Font      = new Font("Segoe UI", 9F),
                ForeColor = CTextMid,
                AutoSize  = false,
                Size      = new Size(this.ClientSize.Width - px * 2, 42),
                Location  = new Point(px + 24, y)
            };
            body.Controls.Add(lblDesc);
            y += 52;

            // Separator
            var sep = new Panel
            {
                Location  = new Point(px, y),
                Size      = new Size(this.ClientSize.Width - px * 2, 1),
                BackColor = CBorder
            };
            body.Controls.Add(sep);
            y += 16;

            // Status label
            _lblStatus = new Label
            {
                Text      = string.Empty,
                Font      = new Font("Segoe UI", 9F),
                ForeColor = Color.FromArgb(30, 140, 60),
                AutoSize  = false,
                Size      = new Size(this.ClientSize.Width - px * 2, 20),
                Location  = new Point(px, y)
            };
            body.Controls.Add(_lblStatus);
            y += 28;

            // Buttons
            _btnSave = MakeButton("Save",             px,        y, 100, 36, CRed,        Color.White, bold: true);
            _btnTest = MakeButton("Test Notification", px + 108, y, 152, 36, Color.White, CTextDark,   bold: false);
            _btnClose= MakeButton("Close",             px + 268, y, 90,  36, Color.White, CTextMid,    bold: false);

            _btnTest.FlatAppearance.BorderColor  = CBorder;
            _btnClose.FlatAppearance.BorderColor = CBorder;

            _btnSave.Click  += BtnSave_Click;
            _btnTest.Click  += BtnTest_Click;
            _btnClose.Click += (s, e) => this.Close();

            body.Controls.Add(_btnSave);
            body.Controls.Add(_btnTest);
            body.Controls.Add(_btnClose);
        }

        private static Button MakeButton(string text, int x, int y, int w, int h,
                                         Color back, Color fore, bool bold)
        {
            var btn = new Button
            {
                Text      = text,
                Location  = new Point(x, y),
                Size      = new Size(w, h),
                BackColor = back,
                ForeColor = fore,
                FlatStyle = FlatStyle.Flat,
                Font      = new Font("Segoe UI", 9.5F, bold ? FontStyle.Bold : FontStyle.Regular),
                Cursor    = Cursors.Hand
            };
            btn.FlatAppearance.BorderSize = back == Color.White ? 1 : 0;
            return btn;
        }

        private void LoadSettings()
        {
            if (!AppSession.IsLoggedIn) return;
            _chkEnabled.Checked = AppSession.NotificationsEnabled;
        }

        private void BtnSave_Click(object sender, EventArgs e)
        {
            if (!AppSession.IsLoggedIn) return;

            bool enabled = _chkEnabled.Checked;
            _settingRepo.Upsert(AppSession.CurrentUserId, enabled);
            AppSession.NotificationsEnabled = enabled;

            SetStatus(enabled ? "✓  Notifications enabled." : "✓  Notifications disabled.", isError: false);
        }

        private void BtnTest_Click(object sender, EventArgs e)
        {
            if (!AppSession.IsLoggedIn) return;

            _notifRepo.Create(new NotificationCreateDto
            {
                UserId           = AppSession.CurrentUserId,
                Title            = "Test Notification",
                Message          = "This is a test notification. Your notifications are working correctly.",
                NotificationType = NotificationType.Test,
                ReferenceId      = null
            });

            if (AppSession.NotificationsEnabled)
            {
                ToastNotificationService.Instance.ShowInfo("Test Notification", "Your notifications are working correctly.");
            }

            SetStatus("✓  Test sent — check your notification bell.", isError: false);
        }

        private void SetStatus(string text, bool isError)
        {
            _lblStatus.Text      = text;
            _lblStatus.ForeColor = isError ? CRed : Color.FromArgb(30, 140, 60);
        }
    }
}
