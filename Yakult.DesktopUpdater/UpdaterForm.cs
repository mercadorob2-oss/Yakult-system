using System;
using System.Drawing;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Yakult.DesktopUpdates
{
    public sealed class UpdaterForm : Form
    {
        private readonly string _jobPath;
        private readonly Label _status;
        private readonly ProgressBar _progress;
        private readonly Button _close;
        private bool _finished;

        public UpdaterForm(string jobPath)
        {
            _jobPath = jobPath;
            Text = "Updating Yakult"; ClientSize = new Size(510, 255); StartPosition = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.FixedDialog; MaximizeBox = false; MinimizeBox = false;
            BackColor = Color.White; Font = new Font("Segoe UI", 10);
            Controls.Add(new Label { Text = "Updating your system", Font = new Font("Segoe UI", 18, FontStyle.Bold), Location = new Point(26, 24), AutoSize = true, ForeColor = Color.FromArgb(185, 35, 53) });
            _status = new Label { Text = "Preparing the update…", Location = new Point(28, 78), Size = new Size(452, 92) };
            _progress = new ProgressBar { Location = new Point(28, 179), Size = new Size(452, 12), Style = ProgressBarStyle.Marquee };
            _close = new Button { Text = "Close", Location = new Point(370, 205), Size = new Size(110, 32), Visible = false };
            _close.Click += (_, __) => Close(); Controls.AddRange(new Control[] { _status, _progress, _close });
            FormClosing += (_, e) => { if (!_finished && e.CloseReason == CloseReason.UserClosing) e.Cancel = true; };
            Shown += async (_, __) => await RunAsync();
        }

        private async Task RunAsync()
        {
            try
            {
                await new UpdateInstaller(new WindowsUpdateHost()).RunAsync(_jobPath, message => _status.Text = message);
                _progress.Style = ProgressBarStyle.Continuous; _progress.Value = 100;
                await Task.Delay(1200); _finished = true; Close();
            }
            catch (Exception ex)
            {
                try { File.AppendAllText(Path.Combine(Path.GetDirectoryName(_jobPath), "update.log"), DateTime.UtcNow.ToString("O") + " " + ex + Environment.NewLine); } catch { }
                _status.Text = ex.Message; _progress.Style = ProgressBarStyle.Continuous; _progress.Value = 0;
                _finished = true; _close.Visible = true; Text = "Yakult update could not complete";
            }
        }
    }
}
