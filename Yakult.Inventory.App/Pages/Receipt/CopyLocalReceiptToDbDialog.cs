using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using Yakult.Inventory.App.Helpers;
using Yakult.Inventory.App.Repositories;

namespace Yakult.Inventory.App.Pages.Receipt
{
    /// <summary>
    /// "Copy Local Receipt to DB" dialog, opened from the receipt viewer's action bar. Backfills the
    /// SI/DR/PO images for the ONE receipt set currently open (not a global scan) from whatever local
    /// file path is on this machine into the database, so the receipt stays viewable even when that
    /// local file/folder isn't reachable from other PCs.
    ///
    /// Lets the user pick whether they're copying for a "Set" or an "Invoice" — currently only the
    /// Invoice path is wired up, since that's the only case in active use; "Set" is reserved for
    /// when that need comes up.
    /// </summary>
    public class CopyLocalReceiptToDbDialog : Form
    {
        private readonly ReceiptSetRepository _repository;
        private readonly int _receiptSetId;

        private RadioButton _rbSet;
        private RadioButton _rbInvoice;
        private Label _lblLinkedTarget;
        private Button _btnRun;
        private Button _btnClose;
        private TextBox _txtLog;
        private Label _lblSummary;

        public CopyLocalReceiptToDbDialog(ReceiptSetRepository repository, int receiptSetId, ReceiptSetLinkedSetDto linkedTarget, bool linkedTargetIsInvoice)
        {
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));
            _receiptSetId = receiptSetId;
            BuildShell(linkedTarget, linkedTargetIsInvoice);
        }

        private void BuildShell(ReceiptSetLinkedSetDto linkedTarget, bool linkedTargetIsInvoice)
        {
            Text = "Copy Local Receipt to Database";
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ClientSize = new Size(520, 440);
            Font = new Font("Segoe UI", 9F);

            var descLabel = new Label
            {
                Text = "Reads the SI/DR/PO receipt image(s) for this receipt that are still stored only as a " +
                       "local file on THIS machine, and copies their bytes into the database. Files already " +
                       "backed by the database, and files not present on this PC, are left untouched.",
                Location = new Point(20, 16),
                Size = new Size(480, 55),
                ForeColor = Color.FromArgb(108, 117, 125)
            };

            var lblCopyFor = new Label
            {
                Text = "Copy for:",
                AutoSize = true,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                Location = new Point(20, 80)
            };

            _rbSet = new RadioButton
            {
                Text = "Set (not yet supported)",
                AutoSize = true,
                Location = new Point(100, 80),
                Enabled = false
            };

            _rbInvoice = new RadioButton
            {
                Text = "Invoice",
                AutoSize = true,
                Location = new Point(260, 80),
                Checked = linkedTargetIsInvoice,
                Enabled = linkedTargetIsInvoice
            };

            string targetText = linkedTarget != null
                ? $"Linked to: {linkedTarget.SetCode} ({(linkedTargetIsInvoice ? "Invoice" : "Set")})"
                : "This receipt is not linked to a Set or Invoice yet.";

            _lblLinkedTarget = new Label
            {
                Text = targetText,
                AutoSize = true,
                ForeColor = Color.FromArgb(108, 117, 125),
                Location = new Point(20, 110)
            };

            _btnRun = new Button
            {
                Text = "Copy to Database",
                Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
                Size = new Size(150, 32),
                Location = new Point(20, 140),
                BackColor = Color.FromArgb(40, 167, 69),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand,
                Enabled = linkedTargetIsInvoice
            };
            _btnRun.FlatAppearance.BorderSize = 0;
            _btnRun.Click += async (s, e) => await RunBackfillAsync();

            if (!linkedTargetIsInvoice)
            {
                var toolTip = new ToolTip();
                toolTip.SetToolTip(_btnRun, "Copying is currently only supported for receipts linked to an Invoice.");
            }

            _lblSummary = new Label
            {
                Text = string.Empty,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                AutoSize = true,
                Location = new Point(20, 182)
            };

            _txtLog = new TextBox
            {
                Location = new Point(20, 210),
                Size = new Size(480, 175),
                Multiline = true,
                ReadOnly = true,
                ScrollBars = ScrollBars.Vertical,
                Font = new Font("Consolas", 8.5F),
                BackColor = Color.FromArgb(248, 249, 250)
            };

            _btnClose = new Button
            {
                Text = "Close",
                Size = new Size(90, 32),
                Location = new Point(410, 395),
                DialogResult = DialogResult.OK
            };

            Controls.Add(descLabel);
            Controls.Add(lblCopyFor);
            Controls.Add(_rbSet);
            Controls.Add(_rbInvoice);
            Controls.Add(_lblLinkedTarget);
            Controls.Add(_btnRun);
            Controls.Add(_lblSummary);
            Controls.Add(_txtLog);
            Controls.Add(_btnClose);

            AcceptButton = null;
            CancelButton = _btnClose;
        }

        private void AppendLog(string line)
        {
            _txtLog.AppendText(line + Environment.NewLine);
        }

        private async System.Threading.Tasks.Task RunBackfillAsync()
        {
            _btnRun.Enabled = false;
            _txtLog.Clear();
            _lblSummary.Text = string.Empty;

            int migrated = 0, skippedNotFound = 0, skippedAlreadyDone = 0, failed = 0;

            try
            {
                var candidates = await _repository.GetReceiptsPendingBackfillForReceiptSetAsync(_receiptSetId);

                if (candidates.Count == 0)
                {
                    AppendLog("Nothing to copy — every document already has database bytes, or has no local path on file.");
                }

                foreach (var candidate in candidates)
                {
                    string resolvedPath = LocalImageFileHelper.ResolveImagePath(candidate.ImagePath);

                    if (string.IsNullOrWhiteSpace(resolvedPath) || !File.Exists(resolvedPath))
                    {
                        skippedNotFound++;
                        AppendLog($"SKIP  {candidate.DocType}: file not found on this machine (\"{candidate.ImagePath}\")");
                        continue;
                    }

                    try
                    {
                        byte[] bytes = File.ReadAllBytes(resolvedPath);
                        bool ok = await _repository.BackfillReceiptBytesAsync(candidate, bytes);
                        if (ok)
                        {
                            migrated++;
                            AppendLog($"OK    {candidate.DocType}: copied {bytes.Length:N0} bytes from \"{resolvedPath}\"");
                        }
                        else
                        {
                            skippedAlreadyDone++;
                            AppendLog($"SKIP  {candidate.DocType}: already copied");
                        }
                    }
                    catch (Exception ex)
                    {
                        failed++;
                        AppendLog($"FAIL  {candidate.DocType}: {ex.Message}");
                    }
                }

                _lblSummary.Text = $"Copied: {migrated}    Not found here: {skippedNotFound}    " +
                                    $"Already done: {skippedAlreadyDone}    Failed: {failed}";
            }
            catch (Exception ex)
            {
                AppendLog($"Copy failed: {ex.Message}");
                MessageBox.Show(this, $"Copy failed:\n\n{ex.Message}", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                _btnRun.Enabled = true;
            }
        }
    }
}
