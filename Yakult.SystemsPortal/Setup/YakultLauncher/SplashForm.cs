using System.Drawing;
using System.Windows.Forms;

namespace YakultLauncher;

internal sealed class SplashForm : Form
{
    private readonly Label _messageLabel;
    private readonly System.Windows.Forms.Timer _closeTimer;

    public SplashForm(string message)
    {
        // Form setup
        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.CenterScreen;
        Size = new Size(400, 120);
        BackColor = Color.White;
        TopMost = true;
        ShowInTaskbar = false;
        Text = "Yakult Launcher";

        // Top accent bar (Yakult red)
        var accentPanel = new Panel
        {
            Height = 4,
            Dock = DockStyle.Top,
            BackColor = Color.FromArgb(220, 38, 38)
        };
        Controls.Add(accentPanel);

        // Message label
        _messageLabel = new Label
        {
            Text = message,
            Font = new Font("Segoe UI", 11f),
            ForeColor = Color.FromArgb(45, 42, 38),
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleCenter,
            Padding = new Padding(20)
        };
        Controls.Add(_messageLabel);

        // Auto-close timer (safety net, 30 seconds)
        _closeTimer = new System.Windows.Forms.Timer { Interval = 30000 };
        _closeTimer.Tick += (_, _) => Close();
        _closeTimer.Start();
    }

    public void UpdateMessage(string message)
    {
        if (InvokeRequired)
        {
            BeginInvoke(new Action(() => _messageLabel.Text = message));
        }
        else
        {
            _messageLabel.Text = message;
        }
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        _closeTimer?.Stop();
        _closeTimer?.Dispose();
        base.OnFormClosing(e);
    }
}
