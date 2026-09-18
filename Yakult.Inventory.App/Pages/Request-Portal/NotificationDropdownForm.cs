using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using Yakult.Inventory.App.Models;
using Yakult.Inventory.App.Repositories;

namespace Yakult.Inventory.App.Pages.RequestPortal
{
    /// <summary>
    /// Borderless notification dropdown shown when the bell button is clicked.
    /// Closes automatically when the user clicks outside it (Deactivate).
    /// Pass onNavigate to handle clicking a notification (receives ReferenceId).
    /// </summary>
    internal class NotificationDropdownForm : Form
    {
        // ── Layout constants ──────────────────────────────────────────────────
        private const int FormW    = 500;
        private const int RowH     = 108;   // extra height so descenders (g,p,q,y) aren't clipped
        private const int HeadH    = 52;
        private const int SepH     = 1;
        private const int IconSize = 38;
        private const int IconLeft = 14;
        private const int TextLeft = IconLeft + IconSize + 12;
        private const int RightPad = 22;
        private const int TextW    = FormW - TextLeft - RightPad - 10;
        private const int MaxRows  = 5;
        private const int MaxH     = HeadH + SepH + RowH * MaxRows;

        // ── Colors ────────────────────────────────────────────────────────────
        private static readonly Color CUnreadBg  = Color.FromArgb(255, 249, 240);
        private static readonly Color CReadBg    = Color.White;
        private static readonly Color CHoverBg   = Color.FromArgb(238, 244, 255);
        private static readonly Color CAccentRed = Color.FromArgb(213, 0, 50);
        private static readonly Color CTextDark  = Color.FromArgb(22, 22, 40);
        private static readonly Color CTextMid   = Color.FromArgb(80, 80, 100);
        private static readonly Color CTextLight = Color.FromArgb(150, 150, 168);
        private static readonly Color CBorder    = Color.FromArgb(218, 218, 230);

        // ── State ─────────────────────────────────────────────────────────────
        private readonly List<NotificationDto> _notifications;
        private readonly int _userId;
        private readonly NotificationRepository _repo;
        private readonly Action<NotificationDto> _onNavigate; // called with the full dto when a row is clicked
        // When non-null, "Mark all read" / "Clear all" only touch rows owned by this portal
        // (dbo.Notification.PortalId), so this bell never clears another portal's notifications.
        private readonly string _portalScope;

        private Panel  _listPanel;
        private Label  _lblTitle;
        private Button _btnMarkAll;
        private Button _btnClearAll;
        private bool   _suppressDeactivateClose;

        public NotificationDropdownForm(
            List<NotificationDto> notifications,
            int userId,
            Action<NotificationDto> onNavigate = null,
            string portalScope = null)
        {
            _notifications = notifications ?? new List<NotificationDto>();
            _userId        = userId;
            _repo          = new NotificationRepository();
            _onNavigate    = onNavigate;
            _portalScope   = portalScope;
            BuildForm();
        }

        // ── Form shell ────────────────────────────────────────────────────────

        private void BuildForm()
        {
            FormBorderStyle = FormBorderStyle.None;
            StartPosition   = FormStartPosition.Manual;
            ShowInTaskbar   = false;
            TopMost         = true;
            BackColor       = Color.White;
            Width           = FormW;

            int contentH = _notifications.Count > 0 ? _notifications.Count * RowH : RowH;
            Height = Math.Min(HeadH + SepH + contentH, MaxH);

            // ── Header ───────────────────────────────────────────────────────
            var pnlHead = new Panel
            {
                Dock      = DockStyle.Top,
                Height    = HeadH,
                BackColor = Color.White
            };

            _lblTitle = new Label
            {
                Text      = "Notifications",
                Font      = new Font("Segoe UI", 11F, FontStyle.Bold),
                ForeColor = CTextDark,
                AutoSize  = true,
                Location  = new Point(16, (HeadH - 20) / 2)
            };

            _btnMarkAll = new Button
            {
                Text      = "Mark all read",
                Font      = new Font("Segoe UI", 8.5F),
                ForeColor = CAccentRed,
                BackColor = Color.Transparent,
                FlatStyle = FlatStyle.Flat,
                AutoSize  = true,
                Cursor    = Cursors.Hand,
                TabStop   = false,
                Visible   = _notifications.Exists(n => !n.IsRead)
            };
            _btnMarkAll.FlatAppearance.BorderSize         = 0;
            _btnMarkAll.FlatAppearance.MouseOverBackColor = Color.FromArgb(255, 238, 238);
            _btnMarkAll.Click += BtnMarkAll_Click;

            _btnClearAll = new Button
            {
                Text      = "Clear all",
                Font      = new Font("Segoe UI", 8.5F),
                ForeColor = Color.FromArgb(100, 116, 139),
                BackColor = Color.Transparent,
                FlatStyle = FlatStyle.Flat,
                AutoSize  = true,
                Cursor    = Cursors.Hand,
                TabStop   = false,
                Visible   = _notifications.Count > 0
            };
            _btnClearAll.FlatAppearance.BorderSize         = 0;
            _btnClearAll.FlatAppearance.MouseOverBackColor = Color.FromArgb(241, 245, 249);
            _btnClearAll.Click += BtnClearAll_Click;

            pnlHead.Controls.Add(_lblTitle);
            pnlHead.Controls.Add(_btnClearAll);
            pnlHead.Controls.Add(_btnMarkAll);
            pnlHead.Resize += (s, e) =>
            {
                _btnClearAll.Location = new Point(
                    pnlHead.Width - _btnClearAll.Width - 12,
                    (pnlHead.Height - _btnClearAll.Height) / 2);
                _btnMarkAll.Location = new Point(
                    _btnClearAll.Left - _btnMarkAll.Width - 8,
                    (pnlHead.Height - _btnMarkAll.Height) / 2);
            };

            UpdateTitle(_notifications.FindAll(n => !n.IsRead).Count);

            // ── Separator ─────────────────────────────────────────────────────
            var sep = new Panel { Dock = DockStyle.Top, Height = SepH, BackColor = CBorder };

            // ── List ──────────────────────────────────────────────────────────
            _listPanel = new Panel { Dock = DockStyle.Fill, AutoScroll = true, BackColor = Color.White };

            if (_notifications.Count == 0)
            {
                _listPanel.Controls.Add(new Label
                {
                    Text      = "No notifications yet",
                    Font      = new Font("Segoe UI", 10F),
                    ForeColor = CTextLight,
                    Dock      = DockStyle.Top,
                    Height    = RowH,
                    TextAlign = ContentAlignment.MiddleCenter
                });
            }
            else
            {
                for (int i = _notifications.Count - 1; i >= 0; i--)
                    _listPanel.Controls.Add(BuildRow(_notifications[i]));
            }

            Controls.Add(_listPanel);
            Controls.Add(sep);
            Controls.Add(pnlHead);

            Region = new System.Drawing.Region(RoundedPath(ClientRectangle, 10));
            Deactivate += (s, e) => { if (!_suppressDeactivateClose) Close(); };
        }

        // ── Row builder ───────────────────────────────────────────────────────

        private Panel BuildRow(NotificationDto n)
        {
            bool hasLink = n.ReferenceId.HasValue && _onNavigate != null;
            var origBg   = n.IsRead ? CReadBg : CUnreadBg;

            var row = new Panel
            {
                Dock      = DockStyle.Top,
                Height    = RowH,
                BackColor = origBg,
                Cursor    = hasLink ? Cursors.Hand : Cursors.Default,
                Tag       = n
            };

            // ── Icon (custom circle paint) ────────────────────────────────────
            var iconLbl = new Label
            {
                Size      = new Size(IconSize, IconSize),
                Location  = new Point(IconLeft, (RowH - IconSize) / 2),
                BackColor = GetIconBg(n.NotificationType),
                ForeColor = GetIconFg(n.NotificationType)
            };
            iconLbl.Paint += (s, e) =>
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                using var bg = new SolidBrush(GetIconBg(n.NotificationType));
                e.Graphics.FillEllipse(bg, 0, 0, IconSize - 1, IconSize - 1);
                var sf = new StringFormat
                {
                    Alignment     = StringAlignment.Center,
                    LineAlignment = StringAlignment.Center
                };
                using var fg = new SolidBrush(GetIconFg(n.NotificationType));
                using var f  = new Font("Segoe UI", 14F);
                e.Graphics.DrawString(GetIconChar(n.NotificationType), f, fg,
                    new RectangleF(0, 0, IconSize, IconSize), sf);
            };

            // ── Title — extra height prevents descender clipping (p, q, g…) ──
            var lblTitle = new Label
            {
                Text         = n.Title,
                Font         = new Font("Segoe UI", 9.5F, FontStyle.Bold),
                ForeColor    = CTextDark,
                BackColor    = Color.Transparent,
                Location     = new Point(TextLeft, 10),
                Size         = new Size(TextW, 24),   // +4px for descenders
                AutoEllipsis = true
            };

            // ── Message — 2 wrapping lines, bottom padded for descenders ──────
            var lblMsg = new Label
            {
                Text         = n.Message,
                Font         = new Font("Segoe UI", 9F),
                ForeColor    = CTextMid,
                BackColor    = Color.Transparent,
                Location     = new Point(TextLeft, 36),
                Size         = new Size(TextW, 44),   // 2-line + descender room
                AutoEllipsis = true
            };

            // ── Time ago — extra height so "g" in "ago" isn't clipped ─────────
            var lblTime = new Label
            {
                Text      = n.TimeAgo,
                Font      = new Font("Segoe UI", 8F),
                ForeColor = CTextLight,
                BackColor = Color.Transparent,
                Location  = new Point(TextLeft, 84),
                Size      = new Size(TextW, 20)       // +4px for descenders
            };

            // ── "View details" hint (only if clickable) ───────────────────────
            Label lblHint = null;
            if (hasLink)
            {
                lblHint = new Label
                {
                    Text      = "Click to view details →",
                    Font      = new Font("Segoe UI", 7.5F, FontStyle.Italic),
                    ForeColor = Color.FromArgb(78, 154, 252),
                    BackColor = Color.Transparent,
                    Location  = new Point(TextLeft, 84),
                    Size      = new Size(TextW, 20),
                    Visible   = false   // shown only on hover
                };
            }

            // ── Unread dot ────────────────────────────────────────────────────
            Panel dot = null;
            if (!n.IsRead)
            {
                dot = new Panel
                {
                    Size      = new Size(9, 9),
                    Location  = new Point(FormW - RightPad, (RowH - 9) / 2),
                    BackColor = CAccentRed,
                    Anchor    = AnchorStyles.Top | AnchorStyles.Right,
                    Tag       = "unread-dot"
                };
                dot.Paint += (s, e) =>
                {
                    e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                    using var br = new SolidBrush(CAccentRed);
                    e.Graphics.FillEllipse(br, 0, 0, 8, 8);
                };
            }

            // ── Bottom divider ────────────────────────────────────────────────
            var divider = new Panel { Dock = DockStyle.Bottom, Height = 1, BackColor = CBorder };

            row.Controls.Add(iconLbl);
            row.Controls.Add(lblTitle);
            row.Controls.Add(lblMsg);
            row.Controls.Add(lblTime);
            if (lblHint != null) row.Controls.Add(lblHint);
            if (dot != null) row.Controls.Add(dot);
            row.Controls.Add(divider);

            // ── Hover ─────────────────────────────────────────────────────────
            ApplyToAll(row, ctrl =>
            {
                ctrl.MouseEnter += (s, e) =>
                {
                    row.BackColor = CHoverBg;
                    if (lblHint != null) { lblHint.Visible = true; lblTime.Visible = false; }
                };
                ctrl.MouseLeave += (s, e) =>
                {
                    row.BackColor = n.IsRead ? CReadBg : CUnreadBg;
                    if (lblHint != null) { lblHint.Visible = false; lblTime.Visible = true; }
                };
                ctrl.MouseClick += (s, e) => RowClicked(n, row, dot);
            });

            return row;
        }

        // ── Event handlers ────────────────────────────────────────────────────

        private void RowClicked(NotificationDto n, Panel row, Panel dot)
        {
            // Mark read in DB and visually
            if (!n.IsRead)
            {
                _repo.MarkAsRead(n.NotificationId, _userId);
                n.IsRead      = true;
                row.BackColor = CReadBg;
                if (dot != null) dot.Visible = false;

                int remaining = _notifications.FindAll(x => !x.IsRead).Count;
                UpdateTitle(remaining);
                if (remaining == 0) _btnMarkAll.Visible = false;
            }

            // Always close the dropdown first, then navigate
            Close();

            if (n.ReferenceId.HasValue && _onNavigate != null)
                _onNavigate(n);
        }

        private void BtnMarkAll_Click(object sender, EventArgs e)
        {
            if (_portalScope != null) _repo.MarkAllAsReadByPortal(_userId, _portalScope);
            else                      _repo.MarkAllAsRead(_userId);
            foreach (var n in _notifications) n.IsRead = true;
            UpdateTitle(0);
            _btnMarkAll.Visible = false;

            foreach (Control c in _listPanel.Controls)
            {
                if (!(c is Panel row)) continue;
                row.BackColor = CReadBg;
                foreach (Control child in row.Controls)
                    if (child is Panel p && "unread-dot".Equals(p.Tag))
                        p.Visible = false;
            }
        }

        private void BtnClearAll_Click(object sender, EventArgs e)
        {
            _suppressDeactivateClose = true;
            var confirm = MessageBox.Show(
                "Clear all notifications? This cannot be undone.",
                "Clear Notifications",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);
            _suppressDeactivateClose = false;

            if (confirm != DialogResult.Yes) { Close(); return; }

            if (_portalScope != null) _repo.ClearAllByPortal(_userId, _portalScope);
            else                      _repo.ClearAll(_userId);
            _notifications.Clear();
            UpdateTitle(0);
            _btnMarkAll.Visible  = false;
            _btnClearAll.Visible = false;

            _listPanel.Controls.Clear();
            _listPanel.Controls.Add(new Label
            {
                Text      = "No notifications yet",
                Font      = new Font("Segoe UI", 10F),
                ForeColor = CTextLight,
                Dock      = DockStyle.Top,
                Height    = RowH,
                TextAlign = ContentAlignment.MiddleCenter
            });
        }

        private void UpdateTitle(int unread)
        {
            _lblTitle.Text = unread > 0 ? $"Notifications  ({unread} unread)" : "Notifications";
        }

        // ── Painting & chrome ─────────────────────────────────────────────────

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using var pen = new Pen(CBorder, 1.5f);
            e.Graphics.DrawPath(pen, RoundedPath(new Rectangle(0, 0, Width - 1, Height - 1), 10));
        }

        protected override CreateParams CreateParams
        {
            get
            {
                const int CS_DROPSHADOW = 0x20000;
                var cp = base.CreateParams;
                cp.ClassStyle |= CS_DROPSHADOW;
                return cp;
            }
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        private static void ApplyToAll(Control root, Action<Control> action)
        {
            action(root);
            foreach (Control c in root.Controls)
                ApplyToAll(c, action);
        }

        private static GraphicsPath RoundedPath(Rectangle b, int r)
        {
            int d = r * 2;
            var p = new GraphicsPath();
            p.AddArc(b.X, b.Y, d, d, 180, 90);
            p.AddArc(b.Right - d, b.Y, d, d, 270, 90);
            p.AddArc(b.Right - d, b.Bottom - d, d, d, 0, 90);
            p.AddArc(b.X, b.Bottom - d, d, d, 90, 90);
            p.CloseFigure();
            return p;
        }

        private static Color GetIconBg(string type)
        {
            switch (type)
            {
                case NotificationType.AuthorizationApproved:     return Color.FromArgb(228, 244, 230); // green
                case NotificationType.AuthorizationRejected:     return Color.FromArgb(253, 233, 233); // red
                case NotificationType.RequestApproved:           return Color.FromArgb(228, 244, 230); // green
                case NotificationType.RequestRejected:           return Color.FromArgb(253, 233, 233); // red
                case NotificationType.RequestCompleted:          return Color.FromArgb(225, 240, 253); // blue
                case NotificationType.RequestCancelled:          return Color.FromArgb(242, 242, 242); // gray
                case NotificationType.RequestFulfilled:          return Color.FromArgb(213, 244, 240); // teal
                case NotificationType.RequestPartiallyFulfilled: return Color.FromArgb(255, 243, 224); // amber
                case NotificationType.RequestUnfulfilled:        return Color.FromArgb(253, 233, 233); // red
                default:                                         return Color.FromArgb(225, 240, 253);
            }
        }

        private static Color GetIconFg(string type)
        {
            switch (type)
            {
                case NotificationType.AuthorizationApproved:     return Color.FromArgb(40,  120,  44); // green
                case NotificationType.AuthorizationRejected:     return Color.FromArgb(195,  38,  38); // red
                case NotificationType.RequestApproved:           return Color.FromArgb(40,  120,  44); // green
                case NotificationType.RequestRejected:           return Color.FromArgb(195,  38,  38); // red
                case NotificationType.RequestCompleted:          return Color.FromArgb(18,   98, 190); // blue
                case NotificationType.RequestCancelled:          return Color.FromArgb(115, 115, 115); // gray
                case NotificationType.RequestFulfilled:          return Color.FromArgb(0,   128, 112); // teal
                case NotificationType.RequestPartiallyFulfilled: return Color.FromArgb(180, 100,   0); // amber
                case NotificationType.RequestUnfulfilled:        return Color.FromArgb(195,  38,  38); // red
                default:                                         return Color.FromArgb(18,   98, 190);
            }
        }

        private static string GetIconChar(string type)
        {
            switch (type)
            {
                case NotificationType.AuthorizationApproved:     return "✓";  // authorization signed
                case NotificationType.AuthorizationRejected:     return "✕";  // authorization rejected
                case NotificationType.RequestApproved:           return "✓";
                case NotificationType.RequestRejected:           return "✕";
                case NotificationType.RequestCompleted:          return "★";
                case NotificationType.RequestCancelled:          return "⊘";
                case NotificationType.RequestFulfilled:          return "▶";  // dispatched / issued
                case NotificationType.RequestPartiallyFulfilled: return "◑";  // partial
                case NotificationType.RequestUnfulfilled:        return "⊖";  // blocked / none issued
                default:                                         return "●";
            }
        }
    }
}
