using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using ReaLTaiizor.Controls;
using ReaLTaiizor.Util;
using Yakult.Inventory.App.Repositories;

namespace Yakult.Inventory.App.Pages
{
    public class SelectSetDialog : Form
    {
        private readonly List<ReceiptSetLinkedSetDto> _allSets;
        private List<ReceiptSetLinkedSetDto> _filteredSets;

        public ReceiptSetLinkedSetDto SelectedSet { get; private set; }

        private ReaLTaiizor.Controls.Panel _rootPanel;
        private Label _lblTitle;
        private TextBox _txtSearch; // Changed to standard TextBox
        private ListBox _listBox;
        private HopeButton _btnOk;
        private HopeButton _btnCancel;

        // P/Invoke for rounded corners and dragging
        [DllImport("gdi32.dll")]
        private static extern IntPtr CreateRoundRectRgn(int nLeftRect, int nTopRect, int nRightRect, int nBottomRect, int nWidthEllipse, int nHeightEllipse);
        
        [DllImport("user32.dll")]
        private static extern bool ReleaseCapture();
        
        [DllImport("user32.dll")]
        private static extern int SendMessage(IntPtr hWnd, int msg, int wParam, int lParam);

        private const int WM_NCLBUTTONDOWN = 0xA1;
        private const int HTCAPTION = 0x2;

        public SelectSetDialog(List<ReceiptSetLinkedSetDto> sets)
        {
            _allSets = sets ?? new List<ReceiptSetLinkedSetDto>();
            _filteredSets = new List<ReceiptSetLinkedSetDto>(_allSets);

            BuildUi();
            ApplyEvents();
            
            // Initial render
            UpdateList();
        }

        private void BuildUi()
        {
            Text = "Select Set";
            Width = 450;
            Height = 500;
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.CenterParent;
            BackColor = Color.FromArgb(245, 247, 250); // App background color
            DoubleBuffered = true;

            // Region for rounded corners
            Region = Region.FromHrgn(CreateRoundRectRgn(0, 0, Width, Height, 20, 20));

            // Root Panel (Card effect)
            _rootPanel = new ReaLTaiizor.Controls.Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.White,
                EdgeColor = Color.FromArgb(220, 220, 220),
                Padding = new Padding(20)
            };
            Controls.Add(_rootPanel);

            // Header layout
            var headerPanel = new System.Windows.Forms.Panel
            {
                Dock = DockStyle.Top,
                Height = 60,
                BackColor = Color.White
            };

            var accentBar = new System.Windows.Forms.Panel
            {
                Width = 5,
                Height = 32,
                BackColor = Color.FromArgb(52, 152, 219),
                Location = new Point(0, 14)
            };

            _lblTitle = new Label
            {
                Text = "Select Linked Set",
                Font = new Font("Segoe UI", 16F, FontStyle.Bold),
                ForeColor = Color.FromArgb(40, 40, 40),
                AutoSize = true,
                Location = new Point(15, 12)
            };

            headerPanel.Controls.Add(accentBar);
            headerPanel.Controls.Add(_lblTitle);
            _rootPanel.Controls.Add(headerPanel);

            // Footer (Buttons)
            var footerPanel = new System.Windows.Forms.Panel
            {
                Dock = DockStyle.Bottom,
                Height = 60,
                BackColor = Color.White,
                Padding = new Padding(0, 10, 0, 0)
            };

            _btnCancel = new HopeButton
            {
                Text = "Cancel",
                Size = new Size(100, 40),
                ButtonType = HopeButtonType.Primary,
                PrimaryColor = Color.FromArgb(240, 240, 240),
                TextColor = Color.FromArgb(80, 80, 80),
                HoverTextColor = Color.FromArgb(40, 40, 40),
                Dock = DockStyle.Right,
                Cursor = Cursors.Hand
            };

            _btnOk = new HopeButton
            {
                Text = "Select",
                Size = new Size(120, 40),
                ButtonType = HopeButtonType.Primary,
                PrimaryColor = Color.FromArgb(52, 152, 219),
                TextColor = Color.White,
                Dock = DockStyle.Right,
                Cursor = Cursors.Hand
            };
            
            // Spacer between buttons
            var spacer = new System.Windows.Forms.Panel { Width = 10, Dock = DockStyle.Right };

            footerPanel.Controls.Add(_btnOk);
            footerPanel.Controls.Add(spacer);
            footerPanel.Controls.Add(_btnCancel);
            _rootPanel.Controls.Add(footerPanel);

            // Search Box Container
            var searchPanel = new System.Windows.Forms.Panel 
            { 
                Dock = DockStyle.Top, 
                Height = 60, 
                Padding = new Padding(5, 5, 5, 15) // Extra bottom padding
            };

            // Custom Border Panel for TextBox
            var borderPanel = new System.Windows.Forms.Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.White,
                Padding = new Padding(1) // 1px border
            };
            borderPanel.Paint += (s, e) => 
            {
                using (var p = new Pen(Color.FromArgb(200, 200, 200)))
                {
                    e.Graphics.DrawRectangle(p, 0, 0, borderPanel.Width - 1, borderPanel.Height - 1);
                }
            };
            
            var textContainer = new System.Windows.Forms.Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.White,
                Padding = new Padding(10, 8, 10, 8) // Inner padding for text
            };

            _txtSearch = new TextBox
            {
                BorderStyle = BorderStyle.None,
                Dock = DockStyle.Fill,
                Font = new Font("Segoe UI", 11F, FontStyle.Regular),
                ForeColor = Color.FromArgb(60, 60, 60),
                BackColor = Color.White
            };
            
            // Assembly Search Box
            textContainer.Controls.Add(_txtSearch);
            borderPanel.Controls.Add(textContainer);
            searchPanel.Controls.Add(borderPanel);
            
            // Add Search Panel to Root
            _rootPanel.Controls.Add(searchPanel);

            // List Box
            _listBox = new ListBox
            {
                Dock = DockStyle.Fill,
                BorderStyle = BorderStyle.None,
                DrawMode = DrawMode.OwnerDrawFixed,
                ItemHeight = 44,
                Font = new Font("Segoe UI", 11F, FontStyle.Regular),
                BackColor = Color.White,
                IntegralHeight = false
            };
            _rootPanel.Controls.Add(_listBox);
            
            // Ensure proper docking order (Z-order)
            // Add controls in reverse order of docking priority if using correct z-order management,
            // or use BringToFront/SendToBack.
            // In WinForms:
            // Top of Z-Order (Index 0) = Inner-most Docking
            // Bottom of Z-Order (Index N) = Outer-most Docking (Priority)
            
            // So we want:
            // 1. Header (Top)
            // 2. Footer (Bottom)
            // 3. Search (Top)
            // 4. List (Fill)
            
            // We need to ensure their Z-indices reflect this.
            // SendToBack() moves to end of collection (Priority Docking).
            // BringToFront() moves to start of collection (Inner Docking).
            
            headerPanel.SendToBack(); // Outer-most Top
            footerPanel.SendToBack(); // Outer-most Bottom
            searchPanel.BringToFront(); // Inner Top (below Header effectively if header is already established?)
            // Actually, if Header is Top, and Footer is Bottom.
            // And Search is Top.
            // If Header is Outer (Index N), Search is Inner (Index 0).
            // Then Search will sit BELOW Header.
            
            _listBox.BringToFront(); // Fill remaining.
            
            // Just to be safe, let's explicit:
            // Order added: Header, Footer, Search, List.
            // Header is [0], Footer [1], Search [2], List [3] ?? No Add puts at 0.
            // Header added first -> [0].
            // Footer added second -> Footer [0], Header [1].
            // Search added third -> Search [0], Footer [1], Header [2].
            // List added fourth -> List [0], Search [1], Footer [2], Header [3].
            
            // Docking priority goes from Bottom (Index N) to Top (Index 0).
            // [3] Header (Top) -> Takes Top slice.
            // [2] Footer (Bottom) -> Takes Bottom slice.
            // [1] Search (Top) -> Takes Top slice (below Header).
            // [0] List (Fill) -> Fills rest.
            // This is EXACTLY what we want.
            // So strictly adding them in order: Header, Footer, Search, List is correct WITHOUT BringToFront/SendToBack manipulation.
            // But verify:
            // _rootPanel.Controls.Add(headerPanel);
            // _rootPanel.Controls.Add(footerPanel);
            // _rootPanel.Controls.Add(searchPanel);
            // _rootPanel.Controls.Add(_listBox);
            // Result Z-Order: List[0], Search[1], Footer[2], Header[3].
            // Result Render: Header Top, Footer Bottom, Search Top(BelowHeader), List Fill.
            // Correct.
        }

        private void ApplyEvents()
        {
            // Dragging
            _lblTitle.MouseDown += DragForm;
            _rootPanel.MouseDown += DragForm;

            // Search
            _txtSearch.TextChanged += (s, e) => FilterList(_txtSearch.Text);
            
            // Buttons
            _btnCancel.Click += (s, e) =>
            {
                DialogResult = DialogResult.Cancel;
                Close();
            };

            _btnOk.Click += (s, e) => ConfirmSelection();

            // List interactions
            _listBox.DrawItem += ListBox_DrawItem;
            _listBox.DoubleClick += (s, e) => ConfirmSelection();
            
            // Key handling
            KeyPreview = true;
            KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.Escape)
                {
                    DialogResult = DialogResult.Cancel;
                    Close();
                }
                else if (e.KeyCode == Keys.Enter)
                {
                    ConfirmSelection();
                }
                else if (e.KeyCode == Keys.Down)
                {
                    if (_listBox.Items.Count > 0 && _listBox.SelectedIndex < _listBox.Items.Count - 1)
                    {
                        _listBox.SelectedIndex++;
                    }
                     e.Handled = true;
                }
                else if (e.KeyCode == Keys.Up)
                {
                    if (_listBox.Items.Count > 0 && _listBox.SelectedIndex > 0)
                    {
                        _listBox.SelectedIndex--;
                    }
                    e.Handled = true;
                }
            };
        }

        private void DragForm(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                ReleaseCapture();
                SendMessage(Handle, WM_NCLBUTTONDOWN, HTCAPTION, 0);
            }
        }

        private void FilterList(string query)
        {
            if (string.IsNullOrWhiteSpace(query))
            {
                _filteredSets = new List<ReceiptSetLinkedSetDto>(_allSets);
            }
            else
            {
                string q = query.ToLowerInvariant();
                _filteredSets = _allSets
                    .Where(s => s.SetCode != null && s.SetCode.ToLowerInvariant().Contains(q))
                    .ToList();
            }
            UpdateList();
        }

        private void UpdateList()
        {
            _listBox.BeginUpdate();
            _listBox.Items.Clear();
            foreach (var item in _filteredSets)
            {
                _listBox.Items.Add(item);
            }

            if (_listBox.Items.Count > 0)
                _listBox.SelectedIndex = 0;
            _listBox.EndUpdate();
            _listBox.Refresh(); // Force redraw
        }

        private void ConfirmSelection()
        {
            if (_listBox.SelectedItem is ReceiptSetLinkedSetDto dto)
            {
                SelectedSet = dto;
                DialogResult = DialogResult.OK;
                Close();
            }
        }

        private void ListBox_DrawItem(object sender, DrawItemEventArgs e)
        {
            e.DrawBackground();

            if (e.Index < 0 || e.Index >= _listBox.Items.Count) return;

            bool isSelected = (e.State & DrawItemState.Selected) == DrawItemState.Selected;
            var g = e.Graphics;
            var item = _listBox.Items[e.Index] as ReceiptSetLinkedSetDto;
            string text = item?.SetCode ?? "Unknown";

            // Custom Background for selection
            if (isSelected)
            {
                using (var brush = new SolidBrush(Color.FromArgb(235, 245, 251))) // Very light blue
                {
                    g.FillRectangle(brush, e.Bounds);
                }
                
                // Left indicator strip
                using (var brush = new SolidBrush(Color.FromArgb(52, 152, 219)))
                {
                    g.FillRectangle(brush, e.Bounds.X, e.Bounds.Y + 4, 4, e.Bounds.Height - 8);
                }
            }
            else
            {
                using (var brush = new SolidBrush(Color.White))
                {
                    g.FillRectangle(brush, e.Bounds);
                }
            }

            // Text
            using (var brush = new SolidBrush(isSelected ? Color.FromArgb(41, 128, 185) : Color.FromArgb(60, 60, 60)))
            {
                var font = isSelected ? new Font(e.Font, FontStyle.Bold) : e.Font;
                
                // Centered vertically
                var stringFormat = new StringFormat 
                { 
                    LineAlignment = StringAlignment.Center,
                    Alignment = StringAlignment.Near
                };
                
                var textRect = new Rectangle(e.Bounds.X + 15, e.Bounds.Y, e.Bounds.Width - 15, e.Bounds.Height);
                g.DrawString(text, font, brush, textRect, stringFormat);
            }

            // Separator line
            using (var pen = new Pen(Color.FromArgb(245, 245, 245)))
            {
                g.DrawLine(pen, e.Bounds.Left + 10, e.Bounds.Bottom - 1, e.Bounds.Right - 10, e.Bounds.Bottom - 1);
            }

            e.DrawFocusRectangle();
        }
    }
}
