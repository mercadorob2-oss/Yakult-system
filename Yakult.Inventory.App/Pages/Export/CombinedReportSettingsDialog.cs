using System;
using System.Drawing;
using System.Windows.Forms;

namespace Yakult.Inventory.App.Pages.Export
{
    public class CombinedReportSettingsDialog : Form
    {
        public string RenewalFilter { get; private set; }
        public string GroupedFilter { get; private set; }
        public string CartridgeFilter { get; private set; }

        private RadioButton _radRenewalBoth;
        private RadioButton _radRenewalActive;
        private RadioButton _radRenewalExpired;

        private RadioButton _radGroupedBoth;
        private RadioButton _radGroupedActive;
        private RadioButton _radGroupedExpired;

        private RadioButton _radCartridgeAll;
        private RadioButton _radCartridgeDisposed;
        private RadioButton _radCartridgeSold;

        public CombinedReportSettingsDialog(bool showInvoice, bool showSets, bool showRenewal, bool showGrouped, bool showCartridge)
        {
            Text = "Preview Selected Reports";
            Font = new Font("Segoe UI", 9F);
            BackColor = Color.White;
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;

            int yPos = 20;
            int margin = 20;

            var titleLbl = new Label
            {
                Text = "Report Preview Settings",
                Font = new Font("Segoe UI", 12F, FontStyle.Bold),
                ForeColor = Color.FromArgb(30, 41, 59),
                AutoSize = true,
                Location = new Point(margin, yPos)
            };
            Controls.Add(titleLbl);
            yPos += 35;

            // Draw a subtle separator
            var separator = new Panel
            {
                BackColor = Color.FromArgb(226, 232, 240),
                Location = new Point(margin, yPos),
                Size = new Size(300, 1)
            };
            Controls.Add(separator);
            yPos += 15;

            var headerFont = new Font("Segoe UI", 9.5F, FontStyle.Bold);
            var radioFont = new Font("Segoe UI", 9F);
            var textColor = Color.FromArgb(60, 60, 60);

            if (showInvoice)
            {
                var lbl = new Label
                {
                    Text = "Invoice Report",
                    Font = headerFont,
                    ForeColor = Color.FromArgb(58, 134, 232),
                    AutoSize = true,
                    Location = new Point(margin, yPos)
                };
                Controls.Add(lbl);
                yPos += 30;
            }

            if (showSets)
            {
                var lbl = new Label
                {
                    Text = "Sets Report",
                    Font = headerFont,
                    ForeColor = Color.FromArgb(58, 134, 232),
                    AutoSize = true,
                    Location = new Point(margin, yPos)
                };
                Controls.Add(lbl);
                yPos += 30;
            }

            if (showRenewal)
            {
                var lbl = new Label
                {
                    Text = "Renewal Report",
                    Font = headerFont,
                    ForeColor = Color.FromArgb(58, 134, 232),
                    AutoSize = true,
                    Location = new Point(margin, yPos)
                };
                Controls.Add(lbl);
                yPos += 25;

                _radRenewalBoth = new RadioButton { Text = "Active && Expired", Font = radioFont, ForeColor = textColor, AutoSize = true, Location = new Point(0, 0) };
                _radRenewalActive = new RadioButton { Text = "Active Only", Font = radioFont, ForeColor = textColor, AutoSize = true, Location = new Point(0, 25) };
                _radRenewalExpired = new RadioButton { Text = "Expired Only", Font = radioFont, ForeColor = textColor, AutoSize = true, Location = new Point(0, 50) };

                var panel = new Panel { Location = new Point(margin + 20, yPos), Size = new Size(250, 75) };
                panel.Controls.Add(_radRenewalBoth);
                panel.Controls.Add(_radRenewalActive);
                panel.Controls.Add(_radRenewalExpired);
                Controls.Add(panel);

                _radRenewalBoth.Checked = true;
                yPos = panel.Bottom + 10;
            }

            if (showGrouped)
            {
                var lbl = new Label
                {
                    Text = "Renewals (Grouped)",
                    Font = headerFont,
                    ForeColor = Color.FromArgb(58, 134, 232),
                    AutoSize = true,
                    Location = new Point(margin, yPos)
                };
                Controls.Add(lbl);
                yPos += 25;

                _radGroupedBoth = new RadioButton { Text = "Active && Expired", Font = radioFont, ForeColor = textColor, AutoSize = true, Location = new Point(0, 0) };
                _radGroupedActive = new RadioButton { Text = "Active Only", Font = radioFont, ForeColor = textColor, AutoSize = true, Location = new Point(0, 25) };
                _radGroupedExpired = new RadioButton { Text = "Expired Only", Font = radioFont, ForeColor = textColor, AutoSize = true, Location = new Point(0, 50) };

                var panel = new Panel { Location = new Point(margin + 20, yPos), Size = new Size(250, 75) };
                panel.Controls.Add(_radGroupedBoth);
                panel.Controls.Add(_radGroupedActive);
                panel.Controls.Add(_radGroupedExpired);
                Controls.Add(panel);

                _radGroupedBoth.Checked = true;
                yPos = panel.Bottom + 10;
            }

            if (showCartridge)
            {
                var lbl = new Label
                {
                    Text = "Cartridge Disposed/Sold",
                    Font = headerFont,
                    ForeColor = Color.FromArgb(58, 134, 232),
                    AutoSize = true,
                    Location = new Point(margin, yPos)
                };
                Controls.Add(lbl);
                yPos += 25;

                _radCartridgeAll = new RadioButton { Text = "All", Font = radioFont, ForeColor = textColor, AutoSize = true, Location = new Point(0, 0) };
                _radCartridgeDisposed = new RadioButton { Text = "Disposed Only", Font = radioFont, ForeColor = textColor, AutoSize = true, Location = new Point(0, 25) };
                _radCartridgeSold = new RadioButton { Text = "Sold Only", Font = radioFont, ForeColor = textColor, AutoSize = true, Location = new Point(0, 50) };

                var panel = new Panel { Location = new Point(margin + 20, yPos), Size = new Size(250, 75) };
                panel.Controls.Add(_radCartridgeAll);
                panel.Controls.Add(_radCartridgeDisposed);
                panel.Controls.Add(_radCartridgeSold);
                Controls.Add(panel);

                _radCartridgeAll.Checked = true;
                yPos = panel.Bottom + 10;
            }

            yPos += 10; // Extra padding before buttons

            var btnOk = new Button
            {
                Text = "OK",
                DialogResult = DialogResult.OK,
                Location = new Point(160, yPos),
                Size = new Size(80, 32),
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                BackColor = Color.FromArgb(58, 134, 232),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand
            };
            btnOk.FlatAppearance.BorderSize = 0;

            var btnCancel = new Button
            {
                Text = "Cancel",
                DialogResult = DialogResult.Cancel,
                Location = new Point(250, yPos),
                Size = new Size(80, 32),
                Font = new Font("Segoe UI", 9F, FontStyle.Regular),
                BackColor = Color.WhiteSmoke,
                ForeColor = Color.FromArgb(30, 41, 59),
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand
            };
            btnCancel.FlatAppearance.BorderColor = Color.LightGray;

            btnOk.Click += (s, e) => SaveFilters();

            Controls.Add(btnOk);
            Controls.Add(btnCancel);

            AcceptButton = btnOk;
            CancelButton = btnCancel;

            ClientSize = new Size(350, yPos + 50);
        }

        private void SaveFilters()
        {
            if (_radRenewalBoth != null)
            {
                if (_radRenewalBoth.Checked) RenewalFilter = null;
                else if (_radRenewalActive.Checked) RenewalFilter = "Active";
                else if (_radRenewalExpired.Checked) RenewalFilter = "Expired";
            }

            if (_radGroupedBoth != null)
            {
                if (_radGroupedBoth.Checked) GroupedFilter = null;
                else if (_radGroupedActive.Checked) GroupedFilter = "Active";
                else if (_radGroupedExpired.Checked) GroupedFilter = "Expired";
            }

            if (_radCartridgeAll != null)
            {
                if (_radCartridgeAll.Checked) CartridgeFilter = null;
                else if (_radCartridgeDisposed.Checked) CartridgeFilter = "DISPOSE";
                else if (_radCartridgeSold.Checked) CartridgeFilter = "SELL";
            }
        }
    }
}
