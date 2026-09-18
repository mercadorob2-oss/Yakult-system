using System;
using System.Drawing;
using System.Windows.Forms;

namespace Yakult.Inventory.App.Wpf.RepairPortal.Shared
{
    /// <summary>
    /// Small code-only WinForms dialog prompting for a completion date when a Ticket or Part is
    /// moved to a terminal status (Completed/Unrepairable for tickets, Repaired/CannotRepair for
    /// parts). Returns the chosen DateTime via <see cref="SelectedDate"/> when ShowDialog() returns
    /// DialogResult.OK; the caller must abort the status change (not silently use "now") if the
    /// user cancels.
    /// </summary>
    internal sealed class CompletionDatePromptForm : Form
    {
        private readonly DateTimePicker _picker;

        public DateTime SelectedDate => _picker.Value;

        public CompletionDatePromptForm(string statusLabel)
        {
            Text = "Completion Date";
            StartPosition = FormStartPosition.CenterParent;
            AutoScaleMode = AutoScaleMode.None;
            // ClientSize (not Size) so the usable content area is guaranteed regardless of the
            // title bar/border chrome height — using Size here left the OK/Cancel row clipped
            // below the visible client area on some DPI settings.
            ClientSize = new Size(360, 190);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            BackColor = Color.White;

            var lblTitle = new Label
            {
                Text = "When was this " + (string.IsNullOrWhiteSpace(statusLabel) ? "item" : statusLabel.Trim()) + "?",
                AutoSize = true,
                Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                Location = new Point(16, 16)
            };

            var lblHint = new Label
            {
                Text = "Choose the actual completion date/time — defaults to now.",
                AutoSize = true,
                Font = new Font("Segoe UI", 8.5F),
                ForeColor = Color.FromArgb(100, 116, 139),
                Location = new Point(16, 42)
            };

            _picker = new DateTimePicker
            {
                Format = DateTimePickerFormat.Custom,
                CustomFormat = "MMM d, yyyy  h:mm tt",
                ShowUpDown = false,
                Value = DateTime.Now,
                MaxDate = DateTime.Now,
                Location = new Point(16, 70),
                Width = 310,
                Font = new Font("Segoe UI", 9.5F)
            };

            var btnOk = new Button
            {
                Text = "OK",
                DialogResult = DialogResult.OK,
                Width = 90,
                Height = 32,
                Location = new Point(150, 140),
                BackColor = Color.FromArgb(37, 99, 235),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };
            btnOk.FlatAppearance.BorderSize = 0;

            var btnCancel = new Button
            {
                Text = "Cancel",
                DialogResult = DialogResult.Cancel,
                Width = 90,
                Height = 32,
                Location = new Point(246, 140),
                BackColor = Color.FromArgb(238, 242, 247),
                ForeColor = Color.FromArgb(51, 65, 85),
                FlatStyle = FlatStyle.Flat
            };
            btnCancel.FlatAppearance.BorderSize = 0;

            Controls.Add(lblTitle);
            Controls.Add(lblHint);
            Controls.Add(_picker);
            Controls.Add(btnOk);
            Controls.Add(btnCancel);

            AcceptButton = btnOk;
            CancelButton = btnCancel;
        }

        /// <summary>Shows the dialog and returns the chosen date, or null if the user cancelled.</summary>
        public static DateTime? PromptFor(IWin32Window owner, string statusLabel)
        {
            using (var dlg = new CompletionDatePromptForm(statusLabel))
            {
                return dlg.ShowDialog(owner) == DialogResult.OK ? dlg.SelectedDate : (DateTime?)null;
            }
        }
    }
}
