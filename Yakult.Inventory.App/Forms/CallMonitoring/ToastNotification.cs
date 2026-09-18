using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace Yakult.Inventory.App.Forms.CallMonitoring
{
    /// <summary>
    /// Non-intrusive toast notification for the Call Monitoring System.
    /// Slides in from bottom-right, auto-dismisses after timeout, supports stacking.
    /// </summary>
    public sealed class ToastNotification : Form
    {
        private Timer _animationTimer;
        private Timer _dismissTimer;
        private Panel _iconPanel;
        private Label _titleLabel;
        private Label _messageLabel;
        private Button _closeButton;
        
        private const int AnimationDuration = 200; // ms
        private const int DismissDelay = 5000;     // ms
        private const int SlideStep = 20;          // pixels per frame
        private const int MaxToasts = 3;
        
        private int _targetX;
        private int _targetY;
        private int _currentX;
        private int _currentY;
        private bool _isClosing;
        
        private static int _activeToastCount = 0;
        private static readonly object _lock = new object();

        public enum ToastType
        {
            Success,
            Warning,
            Error,
            Info
        }

        /// <summary>
        /// Creates a new toast notification
        /// </summary>
        /// <param name="title">Short title text</param>
        /// <param name="message">Longer description</param>
        /// <param name="type">Type determines icon and color</param>
        /// <param name="action">Optional click action</param>
        public ToastNotification(string title, string message, ToastType type = ToastType.Info, Action action = null)
        {
            lock (_lock)
            {
                if (_activeToastCount >= MaxToasts)
                {
                    // Don't show if too many toasts are active
                    this.Dispose();
                    return;
                }
                _activeToastCount++;
            }

            InitializeComponent(title, message, type, action);
            PositionToast();
            SetupAnimation();
            StartDismissTimer();
        }

        private void InitializeComponent(string title, string message, ToastType type, Action action)
        {
            // Form settings
            this.FormBorderStyle = FormBorderStyle.None;
            this.StartPosition = FormStartPosition.Manual;
            this.ShowInTaskbar = false;
            this.TopMost = true;
            this.Width = 360;
            this.Height = 90;
            this.BackColor = Color.White;
            
            // Rounded corners
            this.Region = CreateRoundedRegion(this.Width, this.Height, 8);

            // Shadow effect (simulated with border)
            var shadowPanel = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.White,
                Padding = new Padding(1)
            };
            shadowPanel.Paint += (s, e) =>
            {
                using (var pen = new Pen(Color.FromArgb(40, 0, 0, 0), 1))
                {
                    var rect = new Rectangle(0, 0, shadowPanel.Width - 1, shadowPanel.Height - 1);
                    using (var path = CreateRoundedPath(rect, 8))
                    {
                        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                        e.Graphics.DrawPath(pen, path);
                    }
                }
            };
            this.Controls.Add(shadowPanel);

            // Main layout
            var mainLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 3,
                RowCount = 2,
                BackColor = Color.White,
                Padding = new Padding(12, 10, 12, 10),
                Margin = Padding.Empty
            };
            mainLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 40F));  // Icon
            mainLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F)); // Text
            mainLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 24F));  // Close button
            mainLayout.RowStyles.Add(new ColumnStyle(SizeType.Absolute, 22F));    // Title
            mainLayout.RowStyles.Add(new ColumnStyle(SizeType.Percent, 100F));    // Message
            shadowPanel.Controls.Add(mainLayout);

            // Icon panel
            _iconPanel = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = GetIconBackgroundColor(type),
                Margin = new Padding(0, 0, 10, 0)
            };
            _iconPanel.Region = CreateRoundedRegion(_iconPanel.Width, _iconPanel.Height, 6);
            
            var iconLabel = new Label
            {
                Text = GetIcon(type),
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleCenter,
                Font = new Font("Segoe MDL2 Assets", 16F, FontStyle.Regular),
                ForeColor = GetIconColor(type),
                UseCompatibleTextRendering = true
            };
            _iconPanel.Controls.Add(iconLabel);
            mainLayout.Controls.Add(_iconPanel, 0, 0);
            mainLayout.SetRowSpan(_iconPanel, 2);

            // Title label
            _titleLabel = new Label
            {
                Text = title,
                Dock = DockStyle.Fill,
                Font = new Font("Segoe UI", 11F, FontStyle.Bold),
                ForeColor = Color.FromArgb(17, 24, 39),
                AutoEllipsis = true
            };
            mainLayout.Controls.Add(_titleLabel, 1, 0);

            // Close button
            _closeButton = new Button
            {
                Text = "\uE711", // Close icon
                Dock = DockStyle.Fill,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe MDL2 Assets", 10F, FontStyle.Regular),
                ForeColor = Color.FromArgb(107, 114, 128),
                UseCompatibleTextRendering = true,
                Cursor = Cursors.Hand
            };
            _closeButton.FlatAppearance.BorderSize = 0;
            _closeButton.FlatAppearance.MouseOverBackColor = Color.FromArgb(243, 244, 246);
            _closeButton.Click += (s, e) => CloseToast();
            mainLayout.Controls.Add(_closeButton, 2, 0);

            // Message label
            _messageLabel = new Label
            {
                Text = message,
                Dock = DockStyle.Fill,
                Font = new Font("Segoe UI", 9.5F, FontStyle.Regular),
                ForeColor = Color.FromArgb(75, 85, 99),
                AutoEllipsis = true
            };
            mainLayout.Controls.Add(_messageLabel, 1, 1);
            mainLayout.SetColumnSpan(_messageLabel, 2);

            // Click to perform action
            if (action != null)
            {
                this.Click += (s, e) =>
                {
                    action();
                    CloseToast();
                };
                mainLayout.Click += (s, e) =>
                {
                    action();
                    CloseToast();
                };
                _titleLabel.Click += (s, e) =>
                {
                    action();
                    CloseToast();
                };
                _messageLabel.Click += (s, e) =>
                {
                    action();
                    CloseToast();
                };
                this.Cursor = Cursors.Hand;
            }

            // Hover to pause dismiss
            this.MouseEnter += (s, e) => _dismissTimer?.Stop();
            this.MouseLeave += (s, e) => _dismissTimer?.Start();
        }

        private void PositionToast()
        {
            var screen = Screen.PrimaryScreen.WorkingArea;
            _targetX = screen.Right - this.Width - 20;
            _targetY = screen.Bottom - this.Height - 20 - (_activeToastCount - 1) * (this.Height + 10);
            
            // Start off-screen
            _currentX = screen.Right;
            _currentY = _targetY;
            
            this.Location = new Point(_currentX, _currentY);
        }

        private void SetupAnimation()
        {
            _animationTimer = new Timer { Interval = 16 }; // ~60fps
            _animationTimer.Tick += (s, e) =>
            {
                if (_currentX > _targetX)
                {
                    _currentX = Math.Max(_targetX, _currentX - SlideStep);
                    this.Location = new Point(_currentX, _currentY);
                }
                else
                {
                    _animationTimer.Stop();
                }
            };
        }

        private void StartDismissTimer()
        {
            _dismissTimer = new Timer { Interval = DismissDelay };
            _dismissTimer.Tick += (s, e) => CloseToast();
            _dismissTimer.Start();
            _animationTimer.Start();
            this.Show();
        }

        private void CloseToast()
        {
            if (_isClosing) return;
            _isClosing = true;

            _dismissTimer?.Stop();
            _animationTimer?.Stop();

            // Animate out
            var closeTimer = new Timer { Interval = 16 };
            closeTimer.Tick += (s, e) =>
            {
                if (this.Left < Screen.PrimaryScreen.WorkingArea.Right)
                {
                    this.Left += SlideStep;
                }
                else
                {
                    closeTimer.Stop();
                    this.Close();
                    this.Dispose();
                    lock (_lock)
                    {
                        _activeToastCount = Math.Max(0, _activeToastCount - 1);
                    }
                }
            };
            closeTimer.Start();
        }

        private static string GetIcon(ToastType type)
        {
            switch (type)
            {
                case ToastType.Success: return CallMonitoringIcons.Success;
                case ToastType.Warning: return CallMonitoringIcons.Alert;
                case ToastType.Error: return CallMonitoringIcons.Error;
                default: return CallMonitoringIcons.Info;
            }
        }

        private static Color GetIconColor(ToastType type)
        {
            switch (type)
            {
                case ToastType.Success: return Color.FromArgb(16, 185, 129);
                case ToastType.Warning: return Color.FromArgb(245, 158, 11);
                case ToastType.Error: return Color.FromArgb(239, 68, 68);
                default: return Color.FromArgb(59, 130, 246);
            }
        }

        private static Color GetIconBackgroundColor(ToastType type)
        {
            switch (type)
            {
                case ToastType.Success: return Color.FromArgb(236, 253, 245);
                case ToastType.Warning: return Color.FromArgb(255, 251, 235);
                case ToastType.Error: return Color.FromArgb(254, 242, 242);
                default: return Color.FromArgb(239, 246, 255);
            }
        }

        private static GraphicsPath CreateRoundedPath(Rectangle rect, int radius)
        {
            var path = new GraphicsPath();
            int diameter = radius * 2;
            
            path.AddArc(rect.X, rect.Y, diameter, diameter, 180, 90);
            path.AddArc(rect.Right - diameter, rect.Y, diameter, diameter, 270, 90);
            path.AddArc(rect.Right - diameter, rect.Bottom - diameter, diameter, diameter, 0, 90);
            path.AddArc(rect.X, rect.Bottom - diameter, diameter, diameter, 90, 90);
            path.CloseFigure();
            
            return path;
        }

        private static Region CreateRoundedRegion(int width, int height, int radius)
        {
            using (var path = CreateRoundedPath(new Rectangle(0, 0, width - 1, height - 1), radius))
            {
                return new Region(path);
            }
        }

        #region Static Helper Methods

        /// <summary>
        /// Show a success toast notification
        /// </summary>
        public static void ShowSuccess(string title, string message, Action action = null)
        {
            Show(title, message, ToastType.Success, action);
        }

        /// <summary>
        /// Show a warning toast notification
        /// </summary>
        public static void ShowWarning(string title, string message, Action action = null)
        {
            Show(title, message, ToastType.Warning, action);
        }

        /// <summary>
        /// Show an error toast notification
        /// </summary>
        public static void ShowError(string title, string message, Action action = null)
        {
            Show(title, message, ToastType.Error, action);
        }

        /// <summary>
        /// Show an info toast notification
        /// </summary>
        public static void ShowInfo(string title, string message, Action action = null)
        {
            Show(title, message, ToastType.Info, action);
        }

        /// <summary>
        /// Show a toast notification
        /// </summary>
        private static void Show(string title, string message, ToastType type, Action action)
        {
            // Invoke on UI thread if needed
            if (Application.OpenForms.Count > 0)
            {
                var mainForm = Application.OpenForms[0];
                if (mainForm.InvokeRequired)
                {
                    mainForm.Invoke(new Action(() =>
                    {
                        new ToastNotification(title, message, type, action);
                    }));
                    return;
                }
            }
            new ToastNotification(title, message, type, action);
        }

        #endregion
    }
}
