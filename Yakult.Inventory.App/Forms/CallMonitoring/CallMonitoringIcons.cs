using System;
using System.Drawing;
using System.Windows.Forms;

namespace Yakult.Inventory.App.Forms.CallMonitoring
{
    /// <summary>
    /// Provides Unicode icon characters and helper methods for the Call Monitoring System UI.
    /// Uses Segoe UI Symbol and Fluent UI System Icons.
    /// </summary>
    public static class CallMonitoringIcons
    {
        // Status Icons
        public const string NewTicket = "\uE710";        // Add
        public const string Pending = "\uE823";          // Clock
        public const string InProgress = "\uE768";       // Repair
        public const string Escalated = "\uE7BA";        // Warning
        public const string Solved = "\uE8FB";           // Checkmark
        public const string Resolved = "\uE8FB";         // Checkmark
        public const string Closed = "\uE8FB";          // Checkmark
        public const string Reopened = "\uE8C4";         // Refresh
        public const string Overdue = "\uE8C6";          // Important

        // Action Icons
        public const string Save = "\uE74E";             // Save
        public const string Refresh = "\uE72C";          // Refresh
        public const string Search = "\uE721";           // Find
        public const string Filter = "\uE71C";           // Filter
        public const string Export = "\uE7B5";           // Export
        public const string Print = "\uE749";            // Print
        public const string Delete = "\uE74D";           // Delete
        public const string Edit = "\uE70F";             // Edit
        public const string View = "\uE890";             // View
        public const string Assign = "\uE716";           // Contact
        public const string Email = "\uE715";          // Mail
        public const string Phone = "\uE717";            // Phone
        public const string Note = "\uE70B";             // Comment
        public const string History = "\uE81C";          // History
        public const string Attachment = "\uE723";       // Attach

        // Navigation Icons
        public const string Previous = "\uE72B";         // Back
        public const string Next = "\uE72A";             // Forward
        public const string First = "\uE892";            // Previous
        public const string Last = "\uE893";            // Next
        public const string Up = "\uE71B";               // Up
        public const string Down = "\uE74B";             // Down
        public const string Expand = "\uE70D";           // ChevronDown
        public const string Collapse = "\uE70E";         // ChevronUp

        // Entity Icons
        public const string Ticket = "\uE8D4";           // Page
        public const string User = "\uE77B";             // Contact
        public const string Department = "\uE80F";       // City
        public const string Branch = "\uE804";           // MapPin
        public const string Company = "\uE80F";           // City
        public const string Priority = "\uE814";          // Flag
        public const string Status = "\uE71B";           // Info
        public const string Calendar = "\uE787";         // Calendar
        public const string Clock = "\uE823";           // Clock
        public const string Settings = "\uE713";         // Setting
        public const string Notification = "\uE7E7";     // Ringer
        public const string Alert = "\uE7BA";            // Warning
        public const string Success = "\uE8FB";          // Checkmark
        public const string Error = "\uE783";            // Error
        public const string Info = "\uE897";             // Info (i)

        // SMTP/Email Icons
        public const string Smtp = "\uE715";              // Mail
        public const string Template = "\uE8C8";          // Copy
        public const string Log = "\uE8C4";               // List
        public const string Test = "\uE768";              // Play

        // Priority Icons
        public const string Critical = "\uE783";         // Error (red)
        public const string High = "\uE7BA";             // Warning (orange)
        public const string Medium = "\uE897";           // Info (blue)
        public const string Low = "\uE81C";              // History (gray)

        /// <summary>
        /// Creates a button with an icon and optional text
        /// </summary>
        public static Button CreateIconButton(string icon, string text = null, int width = 120, int height = 32)
        {
            var button = new Button
            {
                Text = string.IsNullOrEmpty(text) ? string.Empty : text,
                Width = width,
                Height = height,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9F, FontStyle.Regular),
                Cursor = Cursors.Hand,
                UseCompatibleTextRendering = true,
                Padding = new Padding(4, 0, 6, 0)
            };
            button.FlatAppearance.BorderSize = 0;
            if (!string.IsNullOrEmpty(icon))
            {
                button.Image = RenderIconBitmap(icon, 14, button.ForeColor);
                button.ImageAlign = ContentAlignment.MiddleLeft;
                button.TextAlign = ContentAlignment.MiddleRight;
                button.TextImageRelation = string.IsNullOrEmpty(text)
                    ? TextImageRelation.Overlay
                    : TextImageRelation.ImageBeforeText;
            }
            return button;
        }

        internal static Bitmap RenderIconBitmap(string icon, int size, Color color)
        {
            var bmp = new Bitmap(size, size);
            using (var g = Graphics.FromImage(bmp))
            {
                g.Clear(Color.Transparent);
                g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAlias;
                using (var font = new Font("Segoe MDL2 Assets", size - 2f, FontStyle.Regular, GraphicsUnit.Pixel))
                using (var brush = new SolidBrush(color))
                {
                    var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                    g.DrawString(icon, font, brush, new RectangleF(0, 0, size, size), sf);
                }
            }
            return bmp;
        }

        /// <summary>
        /// Creates a label with just an icon
        /// </summary>
        public static Label CreateIconLabel(string icon, Color? color = null, float fontSize = 12f)
        {
            return new Label
            {
                Text = icon,
                AutoSize = true,
                Font = new Font("Segoe MDL2 Assets", fontSize, FontStyle.Regular),
                ForeColor = color ?? Color.FromArgb(44, 62, 80),
                UseCompatibleTextRendering = true
            };
        }

        /// <summary>
        /// Applies an icon to an existing button
        /// </summary>
        public static void ApplyIcon(Button button, string icon, string text = null)
        {
            button.Font = new Font("Segoe UI", 9F, FontStyle.Regular);
            button.UseCompatibleTextRendering = true;
            button.Text = text ?? string.Empty;
            if (!string.IsNullOrEmpty(icon))
            {
                button.Image = RenderIconBitmap(icon, 14, button.ForeColor);
                button.ImageAlign = ContentAlignment.MiddleLeft;
                button.TextAlign = ContentAlignment.MiddleRight;
                button.TextImageRelation = string.IsNullOrEmpty(text)
                    ? TextImageRelation.Overlay
                    : TextImageRelation.ImageBeforeText;
            }
        }

        /// <summary>
        /// Gets the icon for a ticket status
        /// </summary>
        public static string GetStatusIcon(string status)
        {
            var s = (status ?? string.Empty).Trim().ToLower();
            if (s.Contains("pending")) return Pending;
            if (s.Contains("progress")) return InProgress;
            if (s.Contains("escalat")) return Escalated;
            if (s.Contains("solved")) return Solved;
            if (s.Contains("resolved")) return Resolved;
            if (s.Contains("closed")) return Closed;
            if (s.Contains("reopen")) return Reopened;
            if (s.Contains("overdue")) return Overdue;
            return Ticket;
        }

        /// <summary>
        /// Gets the color associated with a status
        /// </summary>
        public static Color GetStatusColor(string status)
        {
            var s = (status ?? string.Empty).Trim().ToLower();
            if (s.Contains("pending")) return Color.FromArgb(52, 152, 219);      // Blue
            if (s.Contains("progress")) return Color.FromArgb(155, 89, 182);    // Purple
            if (s.Contains("escalat")) return Color.FromArgb(230, 126, 34);    // Orange
            if (s.Contains("solved") || s.Contains("resolved") || s.Contains("closed")) return Color.FromArgb(46, 204, 113); // Green
            if (s.Contains("reopen")) return Color.FromArgb(231, 76, 60);       // Red
            if (s.Contains("overdue")) return Color.FromArgb(231, 76, 60);     // Red
            return Color.FromArgb(44, 62, 80);                                  // Dark
        }

        /// <summary>
        /// Gets the icon for a priority level
        /// </summary>
        public static string GetPriorityIcon(string priority)
        {
            var p = (priority ?? string.Empty).Trim().ToLower();
            if (p.Contains("critical")) return Critical;
            if (p.Contains("high")) return High;
            if (p.Contains("medium")) return Medium;
            if (p.Contains("low")) return Low;
            return Priority;
        }

        /// <summary>
        /// Gets the color associated with a priority
        /// </summary>
        public static Color GetPriorityColor(string priority)
        {
            var p = (priority ?? string.Empty).Trim().ToLower();
            if (p.Contains("critical")) return Color.FromArgb(231, 76, 60);      // Red
            if (p.Contains("high")) return Color.FromArgb(230, 126, 34);       // Orange
            if (p.Contains("medium")) return Color.FromArgb(52, 152, 219);      // Blue
            if (p.Contains("low")) return Color.FromArgb(127, 140, 141);       // Gray
            return Color.FromArgb(44, 62, 80);
        }
    }
}
