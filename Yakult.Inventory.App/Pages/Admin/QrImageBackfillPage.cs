using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using Yakult.Inventory.App.Repositories;

namespace Yakult.Inventory.App.Pages.Admin
{
    /// <summary>
    /// One-time backfill utility: reads QR code PNGs that are still only on this machine's local disk
    /// (dbo.Set.QRImagePath) and copies their bytes into dbo.Set.QRImageData.
    ///
    /// QR PNGs were historically saved to {AppDomain.CurrentDomain.BaseDirectory}\QRCodes on whichever
    /// PC generated them, so a Set's QR file may only exist on ONE staff member's machine. This page
    /// only migrates rows whose file happens to be present on the machine it's run from — safe to run
    /// again on a different PC to pick up files that live there instead.
    /// </summary>
    public partial class QrImageBackfillPage : UserControl
    {
        private readonly SetRepository _repository = new SetRepository();

        private Button _btnRun;
        private Button _btnSaveLog;
        private TextBox _txtLog;
        private Label _lblSummary;

        public QrImageBackfillPage()
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
                Text = "Migrate Set QR Codes to Database",
                Font = new Font("Segoe UI", 14F, FontStyle.Bold),
                ForeColor = Color.FromArgb(33, 37, 41),
                AutoSize = true,
                Location = new Point(24, 20)
            };

            var descLabel = new Label
            {
                Text = "QR codes generated before this feature was added are still stored only as a local file " +
                       "on the PC that generated them (in a \\QRCodes\\ folder next to the app). This tool reads " +
                       "any such files that exist on THIS machine and copies their bytes into the database. " +
                       "It's safe to run more than once, and safe to run on multiple PCs — it only ever touches " +
                       "rows that haven't been migrated yet, and skips (without changing anything) any row whose " +
                       "file isn't present on this machine.",
                Font = new Font("Segoe UI", 9.5F),
                ForeColor = Color.FromArgb(108, 117, 125),
                AutoSize = false,
                Size = new Size(760, 60),
                Location = new Point(24, 55)
            };

            _btnRun = new Button
            {
                Text = "Run Backfill",
                Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                Size = new Size(140, 36),
                Location = new Point(24, 125),
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
                Location = new Point(174, 125),
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
                Location = new Point(24, 172)
            };

            _txtLog = new TextBox
            {
                Location = new Point(24, 200),
                Size = new Size(760, 400),
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
                AppendLog("Scanning for Sets with a QR code stored only as a local file path...");
                var candidates = await _repository.GetSetsPendingQrBackfillAsync();
                AppendLog($"Found {candidates.Count} candidate row(s).");
                AppendLog(string.Empty);

                foreach (var candidate in candidates)
                {
                    if (!File.Exists(candidate.QRImagePath))
                    {
                        skippedNotFound++;
                        AppendLog($"SKIP  SetId={candidate.SetId}: file not found on this machine (\"{candidate.QRImagePath}\")");
                        continue;
                    }

                    try
                    {
                        byte[] bytes = File.ReadAllBytes(candidate.QRImagePath);
                        bool ok = await _repository.BackfillSetQrImageDataAsync(candidate.SetId, bytes);
                        if (ok)
                        {
                            migrated++;
                            AppendLog($"OK    SetId={candidate.SetId}: migrated {bytes.Length:N0} bytes from \"{candidate.QRImagePath}\"");
                        }
                        else
                        {
                            skippedAlreadyDone++;
                            AppendLog($"SKIP  SetId={candidate.SetId}: already migrated by another run");
                        }
                    }
                    catch (Exception ex)
                    {
                        failed++;
                        AppendLog($"FAIL  SetId={candidate.SetId}: {ex.Message}");
                    }
                }

                AppendLog(string.Empty);
                AppendLog("Done.");
                _lblSummary.Text = $"Migrated: {migrated}    Skipped (not found here): {skippedNotFound}    " +
                                    $"Skipped (already done): {skippedAlreadyDone}    Failed: {failed}";

                if (skippedNotFound > 0)
                {
                    AppendLog(string.Empty);
                    AppendLog($"{skippedNotFound} row(s) were skipped because their file isn't on this machine. " +
                              "Run this tool again from whichever PC(s) generated those QR codes to pick them up.");
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
                FileName = $"QrImageBackfill_{DateTime.Now:yyyyMMdd_HHmm}.txt",
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
