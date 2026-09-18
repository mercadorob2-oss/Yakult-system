using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using Yakult.Inventory.App.Helpers;
using Yakult.Inventory.App.Repositories;

namespace Yakult.Inventory.App.Pages.Admin
{
    /// <summary>
    /// One-time/repeatable backfill utility: reads receipt images (SI/DR/PO) that are still only on
    /// this machine's local disk (dbo.ReceiptSet.SiImagePath/DrImagePath/PoImagePath, or
    /// dbo.ReceiptSetDocumentImage.ImagePath) and copies their bytes into the database, so the
    /// receipt remains viewable even when the local file/folder isn't reachable.
    ///
    /// Receipt files were historically saved to whichever folder the uploading staff member chose on
    /// their own PC (e.g. Desktop\Receipt\...), so a given file may only exist on ONE machine. This
    /// page only migrates rows whose file happens to be present on the machine it's run from — safe
    /// to run again on a different PC to pick up files that live there instead. It never overwrites
    /// an existing DB copy, and never touches the local file.
    /// </summary>
    public partial class ReceiptBackfillPage : UserControl
    {
        private readonly ReceiptSetRepository _repository = new ReceiptSetRepository();

        private Button _btnRun;
        private Button _btnSaveLog;
        private TextBox _txtLog;
        private Label _lblSummary;

        public ReceiptBackfillPage()
        {
            InitializeComponent();
            BuildShell();
        }

        private void InitializeComponent() { }

        private void BuildShell()
        {
            Dock = DockStyle.Fill;
            BackColor = Color.White;
            Padding = new Padding(24);

            var titleLabel = new Label
            {
                Text = "Migrate Local Receipts to Database",
                Font = new Font("Segoe UI", 14F, FontStyle.Bold),
                ForeColor = Color.FromArgb(33, 37, 41),
                AutoSize = true,
                Location = new Point(24, 20)
            };

            var descLabel = new Label
            {
                Text = "Some receipt images (SI/DR/PO) are still stored only as a local file on the PC that " +
                       "uploaded them. This tool reads any such files that exist on THIS machine and copies their " +
                       "bytes into the database so the receipt stays viewable to everyone, even offline from that " +
                       "PC. It's safe to run more than once, and safe to run on multiple PCs — it only ever touches " +
                       "rows that haven't been migrated yet, skips (without changing anything) any row whose file " +
                       "isn't present on this machine, and never deletes or modifies the local file.",
                Font = new Font("Segoe UI", 9.5F),
                ForeColor = Color.FromArgb(108, 117, 125),
                AutoSize = false,
                Size = new Size(760, 75),
                Location = new Point(24, 55)
            };

            _btnRun = new Button
            {
                Text = "Run Backfill",
                Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                Size = new Size(140, 36),
                Location = new Point(24, 140),
                BackColor = Color.FromArgb(40, 167, 69),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand
            };
            _btnRun.FlatAppearance.BorderSize = 0;
            _btnRun.Click += async (s, e) => await RunBackfillAsync();

            _btnSaveLog = new Button
            {
                Text = "Save Log...",
                Font = new Font("Segoe UI", 9F),
                Size = new Size(110, 36),
                Location = new Point(174, 140),
                BackColor = Color.FromArgb(108, 117, 125),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand,
                Enabled = false
            };
            _btnSaveLog.FlatAppearance.BorderSize = 0;
            _btnSaveLog.Click += (s, e) => SaveLog();

            _lblSummary = new Label
            {
                Text = string.Empty,
                Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
                ForeColor = Color.FromArgb(33, 37, 41),
                AutoSize = true,
                Location = new Point(24, 187)
            };

            _txtLog = new TextBox
            {
                Location = new Point(24, 215),
                Size = new Size(760, 385),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom,
                Multiline = true,
                ReadOnly = true,
                ScrollBars = ScrollBars.Vertical,
                Font = new Font("Consolas", 9F),
                BackColor = Color.FromArgb(248, 249, 250)
            };

            Controls.Add(_txtLog);
            Controls.Add(_lblSummary);
            Controls.Add(_btnSaveLog);
            Controls.Add(_btnRun);
            Controls.Add(descLabel);
            Controls.Add(titleLabel);
        }

        private void AppendLog(string line)
        {
            _txtLog.AppendText(line + Environment.NewLine);
        }

        private async System.Threading.Tasks.Task RunBackfillAsync()
        {
            _btnRun.Enabled = false;
            _btnSaveLog.Enabled = false;
            _txtLog.Clear();
            _lblSummary.Text = string.Empty;

            int migrated = 0, skippedNotFound = 0, skippedAlreadyDone = 0, failed = 0;

            try
            {
                AppendLog("Scanning for receipt documents stored only as a local file path...");
                var candidates = await _repository.GetReceiptsPendingBackfillAsync();
                AppendLog($"Found {candidates.Count} candidate document(s).");
                AppendLog(string.Empty);

                foreach (var candidate in candidates)
                {
                    string resolvedPath = LocalImageFileHelper.ResolveImagePath(candidate.ImagePath);
                    string label = candidate.ImageId.HasValue
                        ? $"ReceiptSetId={candidate.ReceiptSetId} ImageId={candidate.ImageId} ({candidate.DocType})"
                        : $"ReceiptSetId={candidate.ReceiptSetId} ({candidate.DocType})";

                    if (string.IsNullOrWhiteSpace(resolvedPath) || !File.Exists(resolvedPath))
                    {
                        skippedNotFound++;
                        AppendLog($"SKIP  {label}: file not found on this machine (\"{candidate.ImagePath}\")");
                        continue;
                    }

                    try
                    {
                        byte[] bytes = File.ReadAllBytes(resolvedPath);
                        bool ok = await _repository.BackfillReceiptBytesAsync(candidate, bytes);
                        if (ok)
                        {
                            migrated++;
                            AppendLog($"OK    {label}: migrated {bytes.Length:N0} bytes from \"{resolvedPath}\"");
                        }
                        else
                        {
                            skippedAlreadyDone++;
                            AppendLog($"SKIP  {label}: already migrated by another run");
                        }
                    }
                    catch (Exception ex)
                    {
                        failed++;
                        AppendLog($"FAIL  {label}: {ex.Message}");
                    }
                }

                AppendLog(string.Empty);
                AppendLog("Done.");
                _lblSummary.Text = $"Migrated: {migrated}    Skipped (not found here): {skippedNotFound}    " +
                                    $"Skipped (already done): {skippedAlreadyDone}    Failed: {failed}";

                if (skippedNotFound > 0)
                {
                    AppendLog(string.Empty);
                    AppendLog($"{skippedNotFound} document(s) were skipped because their file isn't on this machine. " +
                              "Run this tool again from whichever PC(s) uploaded those receipts to pick them up.");
                }
            }
            catch (Exception ex)
            {
                AppendLog($"Backfill failed: {ex.Message}");
                MessageBox.Show(this, $"Backfill failed:\n\n{ex.Message}", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                _btnRun.Enabled = true;
                _btnSaveLog.Enabled = _txtLog.TextLength > 0;
            }
        }

        private void SaveLog()
        {
            using (var dlg = new SaveFileDialog
            {
                Filter = "Text files (*.txt)|*.txt",
                FileName = $"ReceiptBackfill_{DateTime.Now:yyyyMMdd_HHmm}.txt",
                Title = "Save Backfill Log"
            })
            {
                if (dlg.ShowDialog(this.FindForm()) != DialogResult.OK)
                    return;

                File.WriteAllText(dlg.FileName, _txtLog.Text);
            }
        }
    }
}
