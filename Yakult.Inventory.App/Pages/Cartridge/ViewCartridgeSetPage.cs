﻿﻿using Yakult.Inventory.App.Session;
using Yakult.Inventory.App.Repositories;
using Yakult.Inventory.App.Services;
using Yakult.Inventory.App.Helpers;
using System;
using System.Data;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Threading.Tasks;
using Microsoft.Reporting.WinForms;
using System.Collections.Generic;
using Yakult.Inventory.App.Core;
using System.Reflection;
using Yakult.Inventory.App.Pages.Set;
using Yakult.Inventory.App.Pages.Receipt;


namespace Yakult.Inventory.App.Pages.Cartridge
{
    public partial class ViewCartridgeSetPage : UserControl
    {
        private System.Windows.Forms.Panel _headerPanel;
        private System.Windows.Forms.Panel _buttonBarPanel;
        private System.Windows.Forms.Panel _summaryPanel;
        private System.Windows.Forms.Panel _paginationPanel;
        private ReaLTaiizor.Controls.Panel _bodyPanel;
        private ReaLTaiizor.Controls.MaterialCard _gridCard;

        private ReaLTaiizor.Controls.MaterialCard _cardTotal;
        // _cardExpired and _cardWithQr removed
        private ReaLTaiizor.Controls.MaterialCard _cardHardware;
        private ReaLTaiizor.Controls.MaterialCard _cardSoftwareLicense;
        private ReaLTaiizor.Controls.MaterialCard _cardServices;

        private Label _lblTotalCount;
        // _lblExpiredCount and _lblWithQrCount removed
        private Label _lblHardwareCount;
        private Label _lblSoftwareLicenseCount;
        private Label _lblServicesCount;

        private ReaLTaiizor.Controls.PoisonDataGridView dgvSets;
        private readonly BindingSource _setsBindingSource = new BindingSource();
        private ReaLTaiizor.Controls.HopeButton btnRefresh;
        private ReaLTaiizor.Controls.HopeButton btnOpen;
        private ReaLTaiizor.Controls.HopeButton btnArchive;
        private ReaLTaiizor.Controls.HopeButton btnDelete;
            //btnRegenerateQRData, btnViewReport;
        private Label lblTitle;
        private ReaLTaiizor.Controls.HopeTextBox txtSearch;
        private ComboBox _cmbFilterBy;
        private DefaultListPageLayout _layout;
        private SetRepository _repository;
        private QRDataService _qrDataService;
        private System.Collections.Generic.List<SetDto> _allSets;
        private System.Collections.Generic.List<SetDto> _filteredSets;
        private string _connectionString;

        // Sorting state for Sort By dropdown
        private DataGridViewColumn _sortColumn;
        private System.Windows.Forms.SortOrder _sortOrder = System.Windows.Forms.SortOrder.None;
        private bool _sortByDropdownInitialized = false;

        // Pagination
        private System.Windows.Forms.Button btnFirstPage, btnPrevPage, btnNextPage, btnLastPage;
        private Label lblPageInfo;
        private int _currentPage = 1;
        private int _pageSize = 10;

        private readonly ToolTip _actionToolTip;
        private int _hoverActionRowIndex = -1;
        private int _hoverActionColumnIndex = -1;

        public ViewCartridgeSetPage()
        {
            _repository = new SetRepository();
            _qrDataService = new QRDataService();
            _actionToolTip = new ToolTip
            {
                AutoPopDelay = 5000,
                InitialDelay = 350,
                ReshowDelay = 100,
                ShowAlways = true
            };
            _connectionString = Yakult.Inventory.App.Core.DatabaseConfig.ConnectionString;

            InitializeComponent();
            BuildUi();
            _ = LoadSetsAsync();
        }

        private void BuildUi()
        {
            SuspendLayout();
            Controls.Clear();

            Dock = DockStyle.Fill;
            BackColor = Color.White;

            _layout = DefaultListPageTemplate.Create(
                "View Cartridge Sets",
                "Search by Set Code, Document #, Reference #, Status...",
                TxtSearch_TextChanged,
                () => { _ = LoadSetsAsync(); });

            _headerPanel = _layout.HeaderPanel;
            _buttonBarPanel = _layout.ButtonBarPanel;
            _summaryPanel = _layout.SummaryPanel;
            _paginationPanel = _layout.PaginationPanel;
            _bodyPanel = _layout.BodyPanel;
            _gridCard = _layout.GridCard;
            lblTitle = _layout.TitleLabel;
            txtSearch = _layout.SearchBox;
            btnRefresh = _layout.RefreshButton;
            _cmbFilterBy = _layout.FilterByComboBox;

            // Wire up Filter By ComboBox from template
            if (_cmbFilterBy != null)
            {
                _cmbFilterBy.SelectedIndexChanged += CmbFilterBy_SelectedIndexChanged;
            }

            btnOpen = new ReaLTaiizor.Controls.HopeButton
            {
                Text = "\U0001F50D  Open",
                Font = UiTheme.Fonts.Button,
                Size = new Size(130, UiTheme.Sizes.PillButtonHeight),
                Margin = new Padding(0, 6, 12, 0)
            };
            UiFactory.ConfigureOutlineHopeButton(btnOpen, UiTheme.Colors.PrimaryHover, UiTheme.Colors.OutlineHoverBack);
            btnOpen.Click += (s, e) =>
            {
                var selected = GetSelectedSet();
                if (selected == null)
                {
                    MessageBox.Show("Please select a set first.", "No Selection", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                OpenSetDetail(selected.SetId);
            };

            btnArchive = new ReaLTaiizor.Controls.HopeButton
            {
                Text = "\U0001F4E6  Archive",
                Font = UiTheme.Fonts.Button,
                Size = new Size(150, UiTheme.Sizes.PillButtonHeight),
                Margin = new Padding(0, 6, 12, 0)
            };
            UiFactory.ConfigureOutlineHopeButton(btnArchive, UiTheme.Colors.PrimaryHover, UiTheme.Colors.OutlineHoverBack);
            btnArchive.Click += (s, e) =>
            {
                var selected = GetSelectedSet();
                if (selected == null)
                {
                    MessageBox.Show("Please select a set first.", "No Selection", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                ArchiveSetAsync(selected);
            };

            btnDelete = new ReaLTaiizor.Controls.HopeButton
            {
                Text = "\U0001F5D1  Delete",
                Font = UiTheme.Fonts.Button,
                Size = new Size(140, UiTheme.Sizes.PillButtonHeight),
                Margin = new Padding(0, 6, 12, 0)
            };
            UiFactory.ConfigureOutlineHopeButton(btnDelete, UiTheme.Colors.Danger, UiTheme.Colors.DangerHoverBack);
            btnDelete.Click += async (s, e) =>
            {
                var selected = GetSelectedSet();
                if (selected == null)
                {
                    MessageBox.Show("Please select a set first.", "No Selection", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                await DeleteSetAsync(selected);
            };

            DefaultListPageTemplate.AddButtons(_layout.ButtonLeftFlow, new[] { btnOpen, btnArchive, btnDelete });

            _cardTotal = UiFactory.CreateSummaryCard("Total", out _lblTotalCount, UiTheme.Colors.Primary);
            _cardHardware = UiFactory.CreateSummaryCard("Hardware", out _lblHardwareCount, Color.FromArgb(52, 152, 219));
            _cardSoftwareLicense = UiFactory.CreateSummaryCard("Software/License", out _lblSoftwareLicenseCount, Color.FromArgb(155, 89, 182));
            _cardServices = UiFactory.CreateSummaryCard("Services", out _lblServicesCount, Color.FromArgb(26, 188, 156));

            // Hide non-cartridge cards for cartridge-specific view
            _cardHardware.Visible = false;
            _cardSoftwareLicense.Visible = false;
            _cardServices.Visible = false;

            DefaultListPageTemplate.AddSummaryCards(_layout.SummaryFlow, new[] { _cardTotal });

            // DataGridView
            dgvSets = new ReaLTaiizor.Controls.PoisonDataGridView
            {
                Dock = DockStyle.Fill,
                AutoGenerateColumns = false,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                AllowUserToResizeRows = false,
                AllowUserToResizeColumns = false,
                ReadOnly = true,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect = false,
                BackgroundColor = Color.White,
                BorderStyle = BorderStyle.None,
                RowHeadersVisible = false,
                EnableHeadersVisualStyles = false,
                GridColor = Color.White,
                CellBorderStyle = DataGridViewCellBorderStyle.None,
                ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.None,
                RowHeadersBorderStyle = DataGridViewHeaderBorderStyle.None,
                ColumnHeadersVisible = true,
                ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle
                {
                    BackColor = Color.FromArgb(52, 152, 219),
                    ForeColor = Color.White,
                    Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                    Alignment = DataGridViewContentAlignment.MiddleCenter
                },
                DefaultCellStyle = new DataGridViewCellStyle
                {
                    SelectionBackColor = Color.FromArgb(41, 128, 185),
                    SelectionForeColor = Color.White,
                    Font = new Font("Segoe UI", 9F),
                    Padding = new Padding(8, 6, 8, 6)
                },
                AlternatingRowsDefaultCellStyle = new DataGridViewCellStyle { BackColor = Color.FromArgb(232, 240, 248) },
                RowTemplate = { Height = 56, Resizable = DataGridViewTriState.False }
            };

            dgvSets.ColumnHeadersHeight = 40;
            dgvSets.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;

            EnableDoubleBuffering(dgvSets);

            DefaultListPageTemplate.AttachVendorsPillCellPainting(dgvSets, c =>
            {
                if (c == null) return false;
                return c.DataPropertyName == "ItemCount"
                    || c.DataPropertyName == "CreatedAt"
                    || IsSetActionColumn(c.Name);
            });

            dgvSets.CellMouseMove += DgvSets_CellMouseMove;
            dgvSets.MouseLeave += DgvSets_MouseLeave;

            // Define columns
            dgvSets.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "SetCode",
                DataPropertyName = "SetCode",
                HeaderText = "Set Code",
                AutoSizeMode = DataGridViewAutoSizeColumnMode.AllCells,
                MinimumWidth = 80
            });

            dgvSets.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "SetType",
                DataPropertyName = "SetType",
                HeaderText = "Type",
                AutoSizeMode = DataGridViewAutoSizeColumnMode.AllCells,
                MinimumWidth = 70
            });

            dgvSets.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "SetStatus",
                DataPropertyName = "SetStatus",
                HeaderText = "Status",
                AutoSizeMode = DataGridViewAutoSizeColumnMode.AllCells,
                MinimumWidth = 90,
                ReadOnly = true,
                DefaultCellStyle = new DataGridViewCellStyle
                {
                    Alignment = DataGridViewContentAlignment.MiddleCenter,
                    Font = new Font("Segoe UI", 9F, FontStyle.Bold)
                }
            });

            dgvSets.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "CreatedByName",
                DataPropertyName = "CreatedByName",
                HeaderText = "Created By",
                FillWeight = 12,
                MinimumWidth = 120
            });

            dgvSets.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "CreatedAt",
                DataPropertyName = "CreatedAt",
                HeaderText = "Created At",
                FillWeight = 12,
                MinimumWidth = 140,
                DefaultCellStyle = new DataGridViewCellStyle { Format = "MM/dd/yyyy HH:mm" }
            });

            dgvSets.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "ItemCount",
                DataPropertyName = "ItemCount",
                HeaderText = "Item Count",
                AutoSizeMode = DataGridViewAutoSizeColumnMode.AllCells,
                MinimumWidth = 80,
                DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleCenter }
            });

            // Employee information
            dgvSets.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "CurrentEmployeeName",
                DataPropertyName = "CurrentEmployeeName",
                HeaderText = "Employee",
                FillWeight = 14,
                MinimumWidth = 130
            });

            dgvSets.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "CurrentCompanyName",
                DataPropertyName = "CurrentCompanyName",
                HeaderText = "Company",
                FillWeight = 12,
                MinimumWidth = 110
            });

            dgvSets.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "CurrentBranchName",
                DataPropertyName = "CurrentBranchName",
                HeaderText = "Branch",
                FillWeight = 12,
                MinimumWidth = 105
            });

            dgvSets.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "CurrentDepartmentName",
                DataPropertyName = "CurrentDepartmentName",
                HeaderText = "Department",
                FillWeight = 12,
                MinimumWidth = 115
            });

            // Expiry, Days Left, and QR Image columns removed

            dgvSets.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "Remarks",
                DataPropertyName = "Remarks",
                HeaderText = "Remarks",
                FillWeight = 12,
                MinimumWidth = 140
            });

            // Add action buttons column
            var btnOpenColumn = new DataGridViewButtonColumn
            {
                Name = "btnOpen",
                HeaderText = "Open",
                Text = "\uE721",
                UseColumnTextForButtonValue = true,
                AutoSizeMode = DataGridViewAutoSizeColumnMode.AllCells,
                MinimumWidth = 60
            };
            btnOpenColumn.ToolTipText = "Open set";
            btnOpenColumn.DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleCenter, Font = new Font("Segoe MDL2 Assets", 11F, FontStyle.Regular) };
            dgvSets.Columns.Add(btnOpenColumn);

            var btnArchiveColumn = new DataGridViewButtonColumn
            {
                Name = "btnArchive",
                HeaderText = "Archive",
                Text = "\uE7B8",
                UseColumnTextForButtonValue = true,
                AutoSizeMode = DataGridViewAutoSizeColumnMode.AllCells,
                MinimumWidth = 70
            };
            btnArchiveColumn.ToolTipText = "Archive set";
            btnArchiveColumn.DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleCenter, Font = new Font("Segoe MDL2 Assets", 11F, FontStyle.Regular) };
            dgvSets.Columns.Add(btnArchiveColumn);

            var btnDeleteColumn = new DataGridViewButtonColumn
            {
                Name = "btnDelete",
                HeaderText = "Delete",
                Text = "\uE74D",
                UseColumnTextForButtonValue = true,
                AutoSizeMode = DataGridViewAutoSizeColumnMode.AllCells,
                MinimumWidth = 70
            };
            btnDeleteColumn.ToolTipText = "Delete set";
            btnDeleteColumn.DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleCenter, Font = new Font("Segoe MDL2 Assets", 11F, FontStyle.Regular) };
            dgvSets.Columns.Add(btnDeleteColumn);

            // Handle button clicks
            dgvSets.CellContentClick += DgvSets_CellContentClick;

            // Bind once; update via BindingSource to avoid full grid redraw flicker
            dgvSets.DataSource = _setsBindingSource;

            _gridCard.Controls.Clear();
            _gridCard.Controls.Add(dgvSets);

            // Pagination controls (ViewBranchPage style)
            btnFirstPage = new System.Windows.Forms.Button { Text = "<<", Width = 45, Height = 25, Left = 0, Top = 5 };
            btnPrevPage = new System.Windows.Forms.Button { Text = "<", Width = 45, Height = 25, Left = 50, Top = 5 };
            lblPageInfo = new Label { AutoSize = false, Width = 180, Left = 100, Top = 10, Font = new Font("Segoe UI", 9F, FontStyle.Regular), TextAlign = ContentAlignment.MiddleLeft };
            btnNextPage = new System.Windows.Forms.Button { Text = ">", Width = 45, Height = 25, Left = 290, Top = 5 };
            btnLastPage = new System.Windows.Forms.Button { Text = ">>", Width = 45, Height = 25, Left = 340, Top = 5 };

            btnFirstPage.Click += BtnFirstPage_Click;
            btnPrevPage.Click += BtnPrevPage_Click;
            btnNextPage.Click += BtnNextPage_Click;
            btnLastPage.Click += BtnLastPage_Click;

            _paginationPanel.Controls.Clear();
            _paginationPanel.Controls.AddRange(new Control[] { btnFirstPage, btnPrevPage, lblPageInfo, btnNextPage, btnLastPage });

            Controls.Add(_layout.BodyPanel);
            Controls.Add(_layout.PaginationPanel);
            Controls.Add(_layout.SummaryPanel);
            Controls.Add(_layout.ButtonBarPanel);
            Controls.Add(_layout.HeaderPanel);

            ResumeLayout(true);
        }

        private void MakePill(Control control)
        {
            if (control == null || control.Width <= 0 || control.Height <= 0)
                return;

            int radius = control.Height;
            using (var path = new GraphicsPath())
            {
                path.AddArc(0, 0, radius, radius, 90, 180);
                path.AddArc(control.Width - radius, 0, radius, radius, 270, 180);
                path.CloseAllFigures();
                control.Region = new Region(path);
            }
        }

        private void ConfigurePillHopeButton(ReaLTaiizor.Controls.HopeButton button, Color baseColor, Color hoverColor)
        {
            if (button == null)
                return;

            button.PrimaryColor = baseColor;
            button.DefaultColor = baseColor;
            button.BorderColor = baseColor;
            button.TextColor = Color.White;
            button.HoverTextColor = Color.White;
            button.Cursor = Cursors.Hand;

            button.MouseEnter += (s, e) =>
            {
                button.PrimaryColor = hoverColor;
                button.DefaultColor = hoverColor;
                button.BorderColor = hoverColor;
                button.Invalidate();
            };

            button.MouseLeave += (s, e) =>
            {
                button.PrimaryColor = baseColor;
                button.DefaultColor = baseColor;
                button.BorderColor = baseColor;
                button.Invalidate();
            };

            button.Resize += (s, e) => MakePill(button);
            MakePill(button);
        }

        private void ConfigureOutlineHopeButton(ReaLTaiizor.Controls.HopeButton button, Color borderColor, Color hoverBackColor)
        {
            if (button == null)
                return;

            button.PrimaryColor = Color.White;
            button.DefaultColor = Color.White;
            button.BorderColor = borderColor;
            button.TextColor = borderColor;
            button.HoverTextColor = borderColor;
            button.Cursor = Cursors.Hand;

            button.Paint += (s, e) =>
            {
                var b = s as Control;
                if (b == null || b.Width <= 1 || b.Height <= 1)
                    return;

                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                using (var pen = new Pen(borderColor, 2.2f))
                using (var path = new GraphicsPath())
                {
                    pen.Alignment = PenAlignment.Inset;
                    var rect = new Rectangle(1, 1, b.Width - 3, b.Height - 3);
                    int radius = Math.Max(8, rect.Height);
                    int d = radius;
                    path.AddArc(rect.X, rect.Y, d, d, 90, 180);
                    path.AddLine(rect.X + (d / 2), rect.Bottom, rect.Right - (d / 2), rect.Bottom);
                    path.AddArc(rect.Right - d, rect.Y, d, d, 270, 180);
                    path.AddLine(rect.Right - (d / 2), rect.Y, rect.X + (d / 2), rect.Y);
                    path.CloseFigure();
                    e.Graphics.DrawPath(pen, path);
                }
            };

            button.MouseEnter += (s, e) =>
            {
                button.PrimaryColor = hoverBackColor;
                button.DefaultColor = hoverBackColor;
                button.BorderColor = borderColor;
                button.TextColor = borderColor;
                button.Invalidate();
            };

            button.MouseLeave += (s, e) =>
            {
                button.PrimaryColor = Color.White;
                button.DefaultColor = Color.White;
                button.BorderColor = borderColor;
                button.TextColor = borderColor;
                button.Invalidate();
            };

            button.Resize += (s, e) => MakePill(button);
            MakePill(button);
        }

        private ReaLTaiizor.Controls.MaterialCard CreateSummaryCard(string title, out Label valueLabel, Color accent)
        {
            var card = new ReaLTaiizor.Controls.MaterialCard
            {
                Size = new Size(210, 70),
                BackColor = Color.White,
                Padding = new Padding(12, 10, 12, 10),
                Margin = new Padding(0, 0, 10, 0)
            };

            var lblCardTitle = new Label
            {
                Text = title,
                AutoSize = true,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                ForeColor = Color.FromArgb(90, 90, 90),
                Location = new Point(12, 10)
            };

            valueLabel = new Label
            {
                Text = "0",
                AutoSize = true,
                Font = new Font("Segoe UI", 18F, FontStyle.Bold),
                ForeColor = accent,
                Location = new Point(12, 32)
            };

            card.Controls.Add(lblCardTitle);
            card.Controls.Add(valueLabel);
            return card;
        }

        private void UpdateSummaryCards()
        {
            if (_lblTotalCount == null)
                return;

            var data = _filteredSets ?? new List<SetDto>();
            int total = data.Count;

            int hardware = data.Count(s => s != null && string.Equals(s.SetType, "Hardware", StringComparison.OrdinalIgnoreCase));
            int softwareLicense = data.Count(s => s != null && (
                string.Equals(s.SetType, "Software/License", StringComparison.OrdinalIgnoreCase)
                || string.Equals(s.SetType, "Software", StringComparison.OrdinalIgnoreCase)
                || string.Equals(s.SetType, "License", StringComparison.OrdinalIgnoreCase)
                || string.Equals(s.SetType, "SoftwareLicense", StringComparison.OrdinalIgnoreCase)));
            int services = data.Count(s => s != null && (
                string.Equals(s.SetType, "Services", StringComparison.OrdinalIgnoreCase)
                || string.Equals(s.SetType, "Service", StringComparison.OrdinalIgnoreCase)));

            _lblTotalCount.Text = total.ToString();
            _lblHardwareCount.Text = hardware.ToString();
            _lblSoftwareLicenseCount.Text = softwareLicense.ToString();
            _lblServicesCount.Text = services.ToString();
        }

        private SetDto GetSelectedSet()
        {
            if (dgvSets == null || dgvSets.CurrentRow == null)
                return null;

            return dgvSets.CurrentRow.DataBoundItem as SetDto;
        }

        private void DgvSets_CellContentClick(object sender, DataGridViewCellEventArgs e)
        {
            // Ignore header clicks
            if (e.RowIndex < 0) return;

            var setDto = dgvSets.Rows[e.RowIndex].DataBoundItem as SetDto;
            if (setDto == null) return;

            // Check which button was clicked
            var columnName = dgvSets.Columns[e.ColumnIndex].Name;

            if (columnName == "btnOpen")
            {
                OpenSetDetail(setDto.SetId);
            }
            else if (columnName == "btnQR")
            {
                GenerateQRCode(setDto.SetId);
            }
            else if (columnName == "btnArchive")
            {
                ArchiveSetAsync(setDto);
            }
            else if (columnName == "btnDelete")
            {
                _ = DeleteSetAsync(setDto);
            }
        }

        private async Task UpgradeSetAsync(SetDto setDto)
        {
            try
            {
                using (var upgradeDialog = new Form())
                {
                    upgradeDialog.Text = "Upgrade Set";
                    upgradeDialog.Size = new Size(920, 520);
                    upgradeDialog.StartPosition = FormStartPosition.CenterParent;
                    upgradeDialog.FormBorderStyle = FormBorderStyle.FixedDialog;
                    upgradeDialog.MaximizeBox = false;
                    upgradeDialog.MinimizeBox = false;
                    upgradeDialog.BackColor = Color.FromArgb(14, 18, 24);

                    var baseStart = setDto.EndDate ?? DateTime.Today;

                    var rootCard = new ReaLTaiizor.Controls.Panel
                    {
                        Dock = DockStyle.Fill,
                        Padding = new Padding(24),
                        BackColor = Color.FromArgb(26, 31, 38),
                        EdgeColor = Color.FromArgb(40, 50, 62),
                        SmoothingType = SmoothingMode.HighQuality
                    };
                    upgradeDialog.Controls.Add(rootCard);

                    var rootLayout = new TableLayoutPanel
                    {
                        Dock = DockStyle.Fill,
                        ColumnCount = 1,
                        RowCount = 3,
                        BackColor = Color.Transparent,
                        Padding = new Padding(0),
                        Margin = new Padding(0)
                    };
                    rootLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 92F));
                    rootLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
                    rootLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 78F));
                    rootCard.Controls.Add(rootLayout);

                    var headerPanel = new Panel
                    {
                        Dock = DockStyle.Fill,
                        BackColor = Color.Transparent,
                        Margin = new Padding(0, 0, 0, 10)
                    };
                    rootLayout.Controls.Add(headerPanel, 0, 0);

                    var iconBox = new Panel
                    {
                        Size = new Size(46, 46),
                        Location = new Point(0, 18),
                        BackColor = Color.FromArgb(19, 24, 33)
                    };
                    iconBox.Paint += (s, e) =>
                    {
                        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                        using (var pen = new Pen(Color.FromArgb(70, 130, 180), 2f))
                        {
                            e.Graphics.DrawRectangle(pen, 1, 1, iconBox.Width - 3, iconBox.Height - 3);
                        }
                    };
                    var iconLabel = new Label
                    {
                        Dock = DockStyle.Fill,
                        Text = "\u2934",
                        TextAlign = ContentAlignment.MiddleCenter,
                        ForeColor = Color.FromArgb(70, 130, 180),
                        Font = new Font("Segoe UI", 18F, FontStyle.Bold)
                    };
                    iconBox.Controls.Add(iconLabel);
                    headerPanel.Controls.Add(iconBox);

                    var titleLabel = new Label
                    {
                        Text = "Upgrade Set",
                        AutoSize = true,
                        Location = new Point(60, 18),
                        ForeColor = Color.White,
                        Font = new Font("Segoe UI", 16F, FontStyle.Bold)
                    };
                    var subtitleLabel = new Label
                    {
                        Text = "You are about to start a new lifecycle for this set.",
                        AutoSize = true,
                        Location = new Point(62, 50),
                        ForeColor = Color.FromArgb(170, 180, 190),
                        Font = new Font("Segoe UI", 9.5F, FontStyle.Regular)
                    };
                    headerPanel.Controls.Add(titleLabel);
                    headerPanel.Controls.Add(subtitleLabel);

                    var bodyPanel = new Panel
                    {
                        Dock = DockStyle.Fill,
                        BackColor = Color.Transparent,
                        Margin = new Padding(0)
                    };
                    rootLayout.Controls.Add(bodyPanel, 0, 1);

                    var bodyLayout = new TableLayoutPanel
                    {
                        Dock = DockStyle.Fill,
                        ColumnCount = 2,
                        RowCount = 2,
                        BackColor = Color.Transparent,
                        Padding = new Padding(0),
                        Margin = new Padding(0)
                    };
                    bodyLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 48F));
                    bodyLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 52F));
                    bodyLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 220F));
                    bodyLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
                    bodyPanel.Controls.Add(bodyLayout);

                    Label MakeFieldLabel(string text)
                    {
                        return new Label
                        {
                            Text = text,
                            AutoSize = true,
                            ForeColor = Color.FromArgb(190, 200, 210),
                            Font = new Font("Segoe UI", 10F, FontStyle.Regular),
                            Margin = new Padding(0, 0, 0, 6)
                        };
                    }

                    Label MakeValueLabel(string text)
                    {
                        return new Label
                        {
                            Text = text,
                            AutoSize = true,
                            ForeColor = Color.White,
                            Font = new Font("Segoe UI", 12F, FontStyle.Bold),
                            Margin = new Padding(0, 0, 0, 18)
                        };
                    }

                    Panel MakeInputHost(Control inner)
                    {
                        var host = new Panel
                        {
                            Height = 46,
                            Dock = DockStyle.Fill,
                            BackColor = Color.FromArgb(19, 24, 33),
                            Padding = new Padding(12, 6, 12, 6),
                            Margin = new Padding(0, 0, 0, 10)
                        };
                        host.Paint += (s, e) =>
                        {
                            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                            using (var pen = new Pen(Color.FromArgb(52, 92, 130), 1.6f))
                            {
                                var rect = new Rectangle(1, 1, host.Width - 3, host.Height - 3);
                                e.Graphics.DrawRectangle(pen, rect);
                            }
                        };

                        inner.Dock = DockStyle.Fill;
                        host.Controls.Add(inner);
                        return host;
                    }

                    var leftInfo = new Panel
                    {
                        Dock = DockStyle.Fill,
                        BackColor = Color.Transparent,
                        Padding = new Padding(0, 10, 25, 0)
                    };
                    bodyLayout.Controls.Add(leftInfo, 0, 0);

                    var leftInfoLayout = new TableLayoutPanel
                    {
                        Dock = DockStyle.Top,
                        ColumnCount = 1,
                        RowCount = 4,
                        AutoSize = true,
                        AutoSizeMode = AutoSizeMode.GrowAndShrink,
                        BackColor = Color.Transparent,
                        Margin = new Padding(0),
                        Padding = new Padding(0)
                    };
                    leftInfoLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
                    leftInfo.Controls.Add(leftInfoLayout);

                    leftInfoLayout.Controls.Add(MakeFieldLabel("Set Code:"), 0, 0);
                    leftInfoLayout.Controls.Add(MakeValueLabel(setDto.SetCode ?? string.Empty), 0, 1);
                    leftInfoLayout.Controls.Add(MakeFieldLabel("Current Expiry:"), 0, 2);
                    leftInfoLayout.Controls.Add(MakeValueLabel(setDto.EndDate.HasValue
                        ? setDto.EndDate.Value.ToString("yyyy-MM-dd")
                        : "Not set"), 0, 3);

                    var rightDates = new Panel
                    {
                        Dock = DockStyle.Fill,
                        BackColor = Color.Transparent,
                        Padding = new Padding(25, 10, 0, 0)
                    };
                    bodyLayout.Controls.Add(rightDates, 1, 0);

                    var rightDatesLayout = new TableLayoutPanel
                    {
                        Dock = DockStyle.Top,
                        ColumnCount = 1,
                        RowCount = 4,
                        AutoSize = true,
                        AutoSizeMode = AutoSizeMode.GrowAndShrink,
                        BackColor = Color.Transparent,
                        Margin = new Padding(0),
                        Padding = new Padding(0)
                    };
                    rightDatesLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
                    rightDates.Controls.Add(rightDatesLayout);

                    var dtpStart = new DateTimePicker
                    {
                        Format = DateTimePickerFormat.Short,
                        Value = baseStart.Date,
                        Font = new Font("Segoe UI", 10.5F, FontStyle.Regular),
                        BackColor = Color.FromArgb(19, 24, 33),
                        ForeColor = Color.White,
                        CalendarForeColor = Color.White,
                        CalendarMonthBackground = Color.FromArgb(19, 24, 33),
                        CalendarTitleBackColor = Color.FromArgb(19, 24, 33),
                        CalendarTitleForeColor = Color.White,
                        CalendarTrailingForeColor = Color.FromArgb(140, 150, 160)
                    };
                    var dtpEnd = new DateTimePicker
                    {
                        Format = DateTimePickerFormat.Short,
                        Value = baseStart.AddYears(5).Date,
                        Font = new Font("Segoe UI", 10.5F, FontStyle.Regular),
                        BackColor = Color.FromArgb(19, 24, 33),
                        ForeColor = Color.White,
                        CalendarForeColor = Color.White,
                        CalendarMonthBackground = Color.FromArgb(19, 24, 33),
                        CalendarTitleBackColor = Color.FromArgb(19, 24, 33),
                        CalendarTitleForeColor = Color.White,
                        CalendarTrailingForeColor = Color.FromArgb(140, 150, 160)
                    };

                    rightDatesLayout.Controls.Add(MakeFieldLabel("New Start Date:"), 0, 0);
                    rightDatesLayout.Controls.Add(MakeInputHost(dtpStart), 0, 1);
                    rightDatesLayout.Controls.Add(MakeFieldLabel("New End Date (5 yrs):"), 0, 2);
                    rightDatesLayout.Controls.Add(MakeInputHost(dtpEnd), 0, 3);

                    var reasonPanel = new Panel
                    {
                        Dock = DockStyle.Fill,
                        BackColor = Color.Transparent,
                        Padding = new Padding(0, 0, 0, 0)
                    };
                    bodyLayout.Controls.Add(reasonPanel, 0, 1);
                    bodyLayout.SetColumnSpan(reasonPanel, 2);

                    var lblReason = MakeFieldLabel("Reason for pullout / upgrade:");
                    lblReason.Margin = new Padding(0, 8, 0, 8);

                    var txtReason = new ReaLTaiizor.Controls.HopeTextBox
                    {
                        BackColor = Color.FromArgb(19, 24, 33),
                        BaseColor = Color.FromArgb(19, 24, 33),
                        BorderColorA = Color.FromArgb(52, 92, 130),
                        BorderColorB = Color.FromArgb(52, 92, 130),
                        ForeColor = Color.White,
                        Font = new Font("Segoe UI", 10F, FontStyle.Regular),
                        Hint = "Reason for pullout / upgrade",
                        Size = new Size(0, 110),
                        Multiline = true,
                        UseSystemPasswordChar = false
                    };
                    txtReason.Dock = DockStyle.Fill;

                    var reasonHost = new Panel
                    {
                        Dock = DockStyle.Fill,
                        BackColor = Color.FromArgb(19, 24, 33),
                        Padding = new Padding(12, 12, 12, 12),
                        Margin = new Padding(0)
                    };
                    reasonHost.Paint += (s, e) =>
                    {
                        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                        using (var pen = new Pen(Color.FromArgb(52, 92, 130), 1.6f))
                        {
                            var rect = new Rectangle(1, 1, reasonHost.Width - 3, reasonHost.Height - 3);
                            e.Graphics.DrawRectangle(pen, rect);
                        }
                    };
                    reasonHost.Controls.Add(txtReason);

                    var reasonLayout = new TableLayoutPanel
                    {
                        Dock = DockStyle.Fill,
                        ColumnCount = 1,
                        RowCount = 2,
                        BackColor = Color.Transparent,
                        Margin = new Padding(0),
                        Padding = new Padding(0)
                    };
                    reasonLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
                    reasonLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
                    reasonLayout.Controls.Add(lblReason, 0, 0);
                    reasonLayout.Controls.Add(reasonHost, 0, 1);
                    reasonPanel.Controls.Add(reasonLayout);

                    var footerPanel = new Panel
                    {
                        Dock = DockStyle.Fill,
                        BackColor = Color.Transparent,
                        Margin = new Padding(0, 10, 0, 0)
                    };
                    rootLayout.Controls.Add(footerPanel, 0, 2);

                    var footerFlow = new FlowLayoutPanel
                    {
                        Dock = DockStyle.Right,
                        FlowDirection = FlowDirection.LeftToRight,
                        WrapContents = false,
                        AutoSize = true,
                        BackColor = Color.Transparent,
                        Padding = new Padding(0),
                        Margin = new Padding(0)
                    };
                    footerPanel.Controls.Add(footerFlow);

                    var btnUpgrade = new ReaLTaiizor.Controls.HopeButton
                    {
                        Text = "Upgrade",
                        Font = new Font("Segoe UI", 11F, FontStyle.Bold),
                        Size = new Size(140, 44),
                        Margin = new Padding(0, 12, 12, 0)
                    };
                    Yakult.Inventory.App.Helpers.UiFactory.ConfigurePillHopeButton(
                        btnUpgrade,
                        Color.FromArgb(70, 130, 180),
                        Color.FromArgb(90, 150, 200));

                    var btnCancel = new ReaLTaiizor.Controls.HopeButton
                    {
                        Text = "Cancel",
                        Font = new Font("Segoe UI", 11F, FontStyle.Bold),
                        Size = new Size(140, 44),
                        Margin = new Padding(0, 12, 0, 0)
                    };
                    Yakult.Inventory.App.Helpers.UiFactory.ConfigurePillHopeButton(
                        btnCancel,
                        Color.FromArgb(70, 75, 85),
                        Color.FromArgb(90, 95, 105));

                    btnUpgrade.Click += (s, e) =>
                    {
                        upgradeDialog.DialogResult = DialogResult.OK;
                        upgradeDialog.Close();
                    };
                    btnCancel.Click += (s, e) =>
                    {
                        upgradeDialog.DialogResult = DialogResult.Cancel;
                        upgradeDialog.Close();
                    };

                    footerFlow.Controls.Add(btnUpgrade);
                    footerFlow.Controls.Add(btnCancel);

                    if (upgradeDialog.ShowDialog() == DialogResult.OK)
                    {
                        // Do not strip time — database requires full timestamp
                        var newStart = dtpStart.Value;
                        var newEnd = dtpEnd.Value;
                        var reason = string.IsNullOrWhiteSpace(txtReason.Text)
                            ? "Lifecycle upgrade (5-year rule)"
                            : txtReason.Text.Trim();

                        var renewalRepo = new RenewalRepository();
                        var userId = AppSession.CurrentUserId > 0 ? AppSession.CurrentUserId : 0;

                        renewalRepo.UpdateSetDates(setDto.SetId, newStart, newEnd, userId);
                        await _repository.UpdateUpgradeReasonAsync(setDto.SetId, reason);

                        MessageBox.Show("Set lifecycle dates updated and upgrade reason saved.",
                            "Upgrade Complete",
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Information);

                        await LoadSetsAsync();
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error upgrading set:\n\n{ex.Message}", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private async Task LoadSetsAsync()
        {
            try
            {
                var sets = await _repository.GetCartridgeSetsAsync();
                _allSets = sets;
                ApplyFilters();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading cartridge sets: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void TxtSearch_TextChanged(object sender, EventArgs e)
        {
            ApplyFilters();
        }

        private void ApplyFilters()
        {
            if (_allSets == null) return;

            var filtered = _allSets.AsEnumerable();

            // Filter by search text
            string searchText = txtSearch?.Text?.Trim().ToLower();
            if (!string.IsNullOrWhiteSpace(searchText) &&
                searchText != "search by set code, document #, reference #, status...")
            {
                filtered = filtered.Where(s =>
                    (s.SetCode != null && s.SetCode.ToLower().Contains(searchText)) ||
                    (s.SetType != null && s.SetType.ToLower().Contains(searchText)) ||
                    (s.DocumentNumber != null && s.DocumentNumber.ToLower().Contains(searchText)) ||
                    (s.ReferenceNumber != null && s.ReferenceNumber.ToLower().Contains(searchText)) ||
                    (s.Status != null && s.Status.ToLower().Contains(searchText)) ||
                    (s.CreatedByName != null && s.CreatedByName.ToLower().Contains(searchText)) ||
                    (s.CurrentEmployeeName != null && s.CurrentEmployeeName.ToLower().Contains(searchText)) ||
                    (s.CurrentCompanyName != null && s.CurrentCompanyName.ToLower().Contains(searchText)) ||
                    (s.CurrentBranchName != null && s.CurrentBranchName.ToLower().Contains(searchText)) ||
                    (s.CurrentDepartmentName != null && s.CurrentDepartmentName.ToLower().Contains(searchText)) ||
                    (s.Remarks != null && s.Remarks.ToLower().Contains(searchText)) ||
                    (s.UpgradeReason != null && s.UpgradeReason.ToLower().Contains(searchText))
                );
            }

            _filteredSets = filtered.ToList();
            _currentPage = 1;
            UpdateSummaryCards();
            UpdatePagination();

            dgvSets.ClearSelection();
            dgvSets.CurrentCell = null;
        }

        private void UpdatePagination()
        {
            if (_filteredSets == null || _filteredSets.Count == 0)
            {
                dgvSets.SuspendLayout();
                _setsBindingSource.DataSource = new System.Collections.Generic.List<SetDto>();
                _setsBindingSource.ResetBindings(false);
                dgvSets.ResumeLayout();
                if (lblPageInfo != null)
                    lblPageInfo.Text = "Page 0 of 0 (0 sets)";
                if (btnFirstPage != null) btnFirstPage.Enabled = false;
                if (btnPrevPage != null) btnPrevPage.Enabled = false;
                if (btnNextPage != null) btnNextPage.Enabled = false;
                if (btnLastPage != null) btnLastPage.Enabled = false;
                return;
            }

            int totalPages = (int)Math.Ceiling((double)_filteredSets.Count / _pageSize);
            if (_currentPage > totalPages) _currentPage = totalPages;
            if (_currentPage < 1) _currentPage = 1;

            var pagedData = _filteredSets
                .Skip((_currentPage - 1) * _pageSize)
                .Take(_pageSize)
                .ToList();

            dgvSets.SuspendLayout();
            _setsBindingSource.DataSource = pagedData;
            _setsBindingSource.ResetBindings(false);
            dgvSets.ResumeLayout();

            EnableSortingGlyphs();

            // Initialize Sort By dropdown after first load (when columns are available)
            if (!_sortByDropdownInitialized && _layout?.SortByComboBox != null && dgvSets != null && dgvSets.Columns.Count > 0)
            {
                SetupSortByDropdown();
                _sortByDropdownInitialized = true;
            }

            if (lblPageInfo != null)
                lblPageInfo.Text = $"Page {_currentPage} of {totalPages} ({_filteredSets.Count} sets)";

            if (btnFirstPage != null) btnFirstPage.Enabled = _currentPage > 1;
            if (btnPrevPage != null) btnPrevPage.Enabled = _currentPage > 1;
            if (btnNextPage != null) btnNextPage.Enabled = _currentPage < totalPages;
            if (btnLastPage != null) btnLastPage.Enabled = _currentPage < totalPages;
        }

        private void SetupSortByDropdown()
        {
            if (_layout?.SortByComboBox == null || dgvSets == null || dgvSets.Columns.Count == 0)
                return;

            var defaultSortKey = DefaultListPageTemplate.ResolveDefaultSortColumnKey(dgvSets);
            DefaultListPageTemplate.SetupSortByDropdown(
                _layout.SortByComboBox,
                dgvSets,
                (columnKey, direction) =>
                {
                    var col = dgvSets.Columns.Cast<DataGridViewColumn>()
                        .FirstOrDefault(c => c.DataPropertyName == columnKey || c.Name == columnKey);

                    if (col == null)
                        return;

                    _sortColumn = col;
                    _sortOrder = direction;

                    var dir = direction == System.Windows.Forms.SortOrder.Ascending
                        ? System.ComponentModel.ListSortDirection.Ascending
                        : System.ComponentModel.ListSortDirection.Descending;

                    SortDataSource(col.DataPropertyName, dir);
                    UpdatePagination();

                    foreach (DataGridViewColumn c in dgvSets.Columns)
                        c.HeaderCell.SortGlyphDirection = System.Windows.Forms.SortOrder.None;
                    if (_sortColumn != null)
                        _sortColumn.HeaderCell.SortGlyphDirection = _sortOrder;
                    dgvSets.Invalidate();
                },
                defaultColumnKey: defaultSortKey,
                defaultDirection: System.Windows.Forms.SortOrder.Ascending
            );
        }

        private static void EnableDoubleBuffering(DataGridView grid)
        {
            if (grid == null)
                return;

            try
            {
                var prop = grid.GetType().GetProperty("DoubleBuffered", BindingFlags.Instance | BindingFlags.NonPublic);
                prop?.SetValue(grid, true, null);
            }
            catch
            {
            }
        }

        private void BtnFirstPage_Click(object sender, EventArgs e)
        {
            _currentPage = 1;
            UpdatePagination();
        }

        private void BtnPrevPage_Click(object sender, EventArgs e)
        {
            if (_currentPage > 1)
            {
                _currentPage--;
                UpdatePagination();
            }
        }

        private void BtnNextPage_Click(object sender, EventArgs e)
        {
            if (_filteredSets == null) return;

            int totalPages = (int)Math.Ceiling((double)_filteredSets.Count / _pageSize);
            if (_currentPage < totalPages)
            {
                _currentPage++;
                UpdatePagination();
            }
        }

        private void BtnLastPage_Click(object sender, EventArgs e)
        {
            if (_filteredSets == null) return;

            int totalPages = (int)Math.Ceiling((double)_filteredSets.Count / _pageSize);
            _currentPage = totalPages;
            UpdatePagination();
        }

        private static bool IsSetActionColumn(string columnName)
        {
            return columnName == "btnOpen" || columnName == "btnArchive" || columnName == "btnDelete";
        }

        private void DgvSets_CellMouseMove(object sender, DataGridViewCellMouseEventArgs e)
        {
            var grid = sender as DataGridView;
            if (grid == null)
                return;

            if (e.RowIndex < 0 || e.ColumnIndex < 0)
            {
                ResetSetActionHover(grid);
                return;
            }

            var colName = grid.Columns[e.ColumnIndex].Name;
            if (!IsSetActionColumn(colName))
            {
                ResetSetActionHover(grid);
                return;
            }

            grid.Cursor = Cursors.Hand;

            if (_hoverActionRowIndex == e.RowIndex && _hoverActionColumnIndex == e.ColumnIndex)
                return;

            int prevRow = _hoverActionRowIndex;
            int prevCol = _hoverActionColumnIndex;

            _hoverActionRowIndex = e.RowIndex;
            _hoverActionColumnIndex = e.ColumnIndex;

            if (prevRow >= 0 && prevCol >= 0)
                grid.InvalidateCell(prevCol, prevRow);
            grid.InvalidateCell(_hoverActionColumnIndex, _hoverActionRowIndex);

            string tipText = colName == "btnOpen"
                ? "Open set"
                : (colName == "btnArchive" ? "Archive set" : "Delete set");
            var tipPoint = grid.PointToClient(Cursor.Position);
            tipPoint.Offset(16, 16);
            _actionToolTip.Show(tipText, grid, tipPoint, 1200);
        }

        private void DgvSets_MouseLeave(object sender, EventArgs e)
        {
            var grid = sender as DataGridView;
            if (grid == null)
                return;

            ResetSetActionHover(grid);
        }

        private void ResetSetActionHover(DataGridView grid)
        {
            if (_hoverActionRowIndex >= 0 && _hoverActionColumnIndex >= 0)
            {
                int prevRow = _hoverActionRowIndex;
                int prevCol = _hoverActionColumnIndex;
                _hoverActionRowIndex = -1;
                _hoverActionColumnIndex = -1;
                grid.InvalidateCell(prevCol, prevRow);
            }

            grid.Cursor = Cursors.Default;
            _actionToolTip.Hide(grid);
        }

        private void DgvSets_CellPainting(object sender, DataGridViewCellPaintingEventArgs e)
        {
            var grid = sender as DataGridView;
            if (grid == null)
                return;

            var column = (e.ColumnIndex >= 0 && e.ColumnIndex < grid.Columns.Count)
                ? grid.Columns[e.ColumnIndex]
                : null;
            if (column == null)
                return;

            if (e.RowIndex < 0)
            {
                e.Handled = true;

                var oldSmoothing = e.Graphics.SmoothingMode;
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;

                e.Graphics.FillRectangle(Brushes.White, e.CellBounds);

                bool isFirstHeaderCell = e.ColumnIndex == 0;
                bool isLastHeaderCell = e.ColumnIndex == grid.Columns.Count - 1;

                int radius = 14;
                Rectangle r = e.CellBounds;
                r.Width -= 1;
                r.Height -= 1;

                if (e.ColumnIndex > 0)
                {
                    r.X -= 1;
                    r.Width += 1;
                }

                using (var brush = new SolidBrush(Color.FromArgb(52, 152, 219)))
                using (var pen = new Pen(Color.FromArgb(52, 152, 219)))
                {
                    if (isFirstHeaderCell || isLastHeaderCell)
                    {
                        using (var path = new GraphicsPath())
                        {
                            int d = radius * 2;

                            if (isFirstHeaderCell)
                            {
                                path.AddArc(r.X, r.Y, d, d, 180, 90);
                                path.AddLine(r.X + radius, r.Y, r.Right, r.Y);
                                path.AddLine(r.Right, r.Y, r.Right, r.Bottom);
                                path.AddLine(r.Right, r.Bottom, r.X, r.Bottom);
                                path.AddLine(r.X, r.Bottom, r.X, r.Y + radius);
                            }
                            else
                            {
                                path.AddLine(r.X, r.Y, r.Right - radius, r.Y);
                                path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
                                path.AddLine(r.Right, r.Y + radius, r.Right, r.Bottom);
                                path.AddLine(r.Right, r.Bottom, r.X, r.Bottom);
                                path.AddLine(r.X, r.Bottom, r.X, r.Y);
                            }

                            path.CloseFigure();
                            e.Graphics.FillPath(brush, path);
                            e.Graphics.DrawPath(pen, path);
                        }
                    }
                    else
                    {
                        e.Graphics.FillRectangle(brush, r);
                    }
                }

                using (var dividerPen = new Pen(Color.FromArgb(200, 255, 255, 255)))
                {
                    e.Graphics.DrawLine(dividerPen, e.CellBounds.Left, e.CellBounds.Bottom - 1, e.CellBounds.Right, e.CellBounds.Bottom - 1);
                }

                using (var textBrush = new SolidBrush(Color.White))
                using (var format = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                {
                    e.Graphics.DrawString(column.HeaderText, e.CellStyle.Font ?? grid.Font, textBrush, e.CellBounds, format);
                }

                e.Graphics.SmoothingMode = oldSmoothing;
                return;
            }

            bool isSelected = grid.Rows[e.RowIndex].Selected;
            bool isFirstColumn = e.ColumnIndex == 0;
            bool isLastColumn = e.ColumnIndex == grid.Columns.Count - 1;

            bool isCenteredDataColumn =
                column.DataPropertyName == "ItemCount" ||
                column.DataPropertyName == "CreatedAt" ||
                IsSetActionColumn(column.Name);

            Color pillColor = (e.RowIndex % 2 == 0)
                ? Color.FromArgb(227, 242, 253)
                : Color.FromArgb(232, 245, 233);

            if (isSelected)
                pillColor = Color.FromArgb(41, 128, 185);

            e.Graphics.FillRectangle(Brushes.White, e.CellBounds);

            int topPadding = 5;
            int bottomPadding = 5;
            int leftPadding = isFirstColumn ? 10 : 0;
            int rightPadding = isLastColumn ? 10 : 0;
            int cornerRadius = 14;

            Rectangle pillBounds = new Rectangle(
                e.CellBounds.X + leftPadding,
                e.CellBounds.Y + topPadding,
                e.CellBounds.Width - leftPadding - rightPadding,
                e.CellBounds.Height - topPadding - bottomPadding
            );

            using (var brush = new SolidBrush(pillColor))
            {
                if (isFirstColumn)
                {
                    e.Graphics.FillEllipse(brush, pillBounds.X, pillBounds.Y, cornerRadius * 2, pillBounds.Height);
                    e.Graphics.FillRectangle(brush, pillBounds.X + cornerRadius, pillBounds.Y, pillBounds.Width - cornerRadius, pillBounds.Height);
                }
                else if (isLastColumn)
                {
                    e.Graphics.FillEllipse(brush, pillBounds.Right - cornerRadius * 2, pillBounds.Y, cornerRadius * 2, pillBounds.Height);
                    e.Graphics.FillRectangle(brush, pillBounds.X, pillBounds.Y, pillBounds.Width - cornerRadius, pillBounds.Height);
                }
                else
                {
                    e.Graphics.FillRectangle(brush, pillBounds);
                }
            }

            Color textColor = isSelected ? Color.White : Color.FromArgb(40, 40, 40);

            if (e.Value != null)
            {
                using (var textBrush = new SolidBrush(textColor))
                using (var format = new StringFormat
                {
                    Alignment = isCenteredDataColumn ? StringAlignment.Center : StringAlignment.Near,
                    LineAlignment = StringAlignment.Center,
                    Trimming = StringTrimming.EllipsisCharacter
                })
                {
                    var textRect = new Rectangle(
                        e.CellBounds.X + leftPadding + (isCenteredDataColumn ? 0 : 10),
                        e.CellBounds.Y + topPadding,
                        e.CellBounds.Width - leftPadding - rightPadding - (isCenteredDataColumn ? 0 : 20),
                        e.CellBounds.Height - topPadding - bottomPadding
                    );

                    e.Graphics.DrawString(e.FormattedValue?.ToString() ?? string.Empty, e.CellStyle.Font ?? grid.Font, textBrush, textRect, format);
                }
            }

            e.Handled = true;
        }


        private void OpenSetDetail(int setId)
        {
            // Open ViewSetDetailPage - you can implement this as a dialog or navigate to it
            using (var detailPage = new ViewSetDetailPage(setId))
            {
                detailPage.ShowDialog();

                // Refresh after closing detail page
                _ = LoadSetsAsync();
            }
        }

        private void GenerateQRCode(int setId)
        {
            MessageBox.Show(
                $"QR Code generation for Set ID {setId} will be implemented here.\n\n" +
                "You can use a library like QRCoder or ZXing.Net to generate QR codes.",
                "Generate QR Code",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }

        private void OpenReceiptViewerForSet(SetDto setDto)
        {
            var receiptRepo = new ReceiptSetRepository();

            // If a receipt set is already attached to this set, let the user open or unlink it
            var existing = receiptRepo.GetBySetId(setDto.SetId);
            if (existing != null)
            {
                var choice = MessageBox.Show(
                    "This set already has a receipt set.\n\n" +
                    "Click Yes to open the receipt set.\n" +
                    "Click No to unlink it from this set.",
                    "Receipts",
                    MessageBoxButtons.YesNoCancel,
                    MessageBoxIcon.Question);

                if (choice == DialogResult.Yes)
                {
                    Yakult.Inventory.App.Wpf.Receipt.ReceiptSetViewerLauncher.ShowForReceipt(FindForm(), existing);
                }
                else if (choice == DialogResult.No)
                {
                    var confirm = MessageBox.Show(
                        "Are you sure you want to unlink this receipt set from the set?\n\n" +
                        "The receipt set will remain saved as a manual/unlinked receipt.",
                        "Unlink Receipt Set",
                        MessageBoxButtons.YesNo,
                        MessageBoxIcon.Warning);

                    if (confirm == DialogResult.Yes)
                    {
                        int? userId = AppSession.CurrentUserId > 0 ? (int?)AppSession.CurrentUserId : null;
                        receiptRepo.UnlinkReceiptSet(existing.ReceiptSetId, setDto.SetId, userId);

                        MessageBox.Show(
                            "Receipt set has been unlinked from this set.",
                            "Receipts",
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Information);
                    }
                }

                return;
            }

            var result = MessageBox.Show(
                "This set has no receipt set yet.\n\n" +
                "Click Yes to create a new receipt set for this set.\n" +
                "Click No to attach an existing manual receipt set.",
                "Receipts",
                MessageBoxButtons.YesNoCancel,
                MessageBoxIcon.Question);

            if (result == DialogResult.Yes)
            {
                // Create a new receipt set linked to this Set
                Yakult.Inventory.App.Wpf.Receipt.ReceiptSetViewerLauncher.ShowForSet(FindForm(), setDto.SetId);
            }
            else if (result == DialogResult.No)
            {
                // Attach an existing unlinked receipt set
                using (var attachDialog = new AttachReceiptSetDialog(setDto.SetId))
                {
                    if (attachDialog.ShowDialog() == DialogResult.OK && attachDialog.SelectedReceiptSetId.HasValue)
                    {
                        int? userId = AppSession.CurrentUserId > 0 ? (int?)AppSession.CurrentUserId : null;
                        receiptRepo.AttachReceiptSetToSet(attachDialog.SelectedReceiptSetId.Value, setDto.SetId, userId);

                        var attached = receiptRepo.GetBySetId(setDto.SetId);
                        if (attached != null)
                        {
                            Yakult.Inventory.App.Wpf.Receipt.ReceiptSetViewerLauncher.ShowForReceipt(FindForm(), attached);
                        }
                    }
                }
            }
        }

        private void ArchiveSetAsync(SetDto setDto)
        {
            try
            {
                // Archive confirmation dialog
                using (var archiveDialog = new Form())
                {
                    archiveDialog.Text = "Archive Set";
                    archiveDialog.Size = new Size(550, 340);
                    archiveDialog.StartPosition = FormStartPosition.CenterParent;
                    archiveDialog.FormBorderStyle = FormBorderStyle.FixedDialog;
                    archiveDialog.MaximizeBox = false;
                    archiveDialog.MinimizeBox = false;

                    var lblMessage = new Label
                    {
                        Text = $"Are you sure you want to archive this set?\n\n" +
                               $"Set Code: {setDto.SetCode}\n" +
                               $"Document #: {setDto.DocumentNumber}\n" +
                               $"Status: {setDto.Status}\n\n" +
                               $"The set and its related items will be moved to the archive.",
                        AutoSize = false,
                        Size = new Size(510, 100),
                        Location = new Point(10, 10)
                    };

                    var lblReason = new Label
                    {
                        Text = "Reason for archiving:",
                        AutoSize = true,
                        Location = new Point(10, 115)
                    };

                    var txtReason = new TextBox
                    {
                        Size = new Size(510, 20),
                        Location = new Point(10, 135)
                    };

                    var chkArchiveItems = new CheckBox
                    {
                        Text = "Also archive all items in this set",
                        AutoSize = true,
                        Location = new Point(10, 165),
                        Checked = true
                    };

                    var chkDeactivateItems = new CheckBox
                    {
                        Text = "Mark archived items as inactive",
                        AutoSize = true,
                        Location = new Point(10, 190),
                        Checked = true
                    };

                    var btnArchive = new Button
                    {
                        Text = "Archive",
                        DialogResult = DialogResult.OK,
                        Location = new Point(340, 220),
                        Size = new Size(90, 25)
                    };

                    var btnCancel = new Button
                    {
                        Text = "Cancel",
                        DialogResult = DialogResult.Cancel,
                        Location = new Point(440, 220),
                        Size = new Size(90, 25)
                    };

                    archiveDialog.Controls.AddRange(new Control[] {
                        lblMessage, lblReason, txtReason, chkArchiveItems, chkDeactivateItems,
                        btnArchive, btnCancel
                    });
                    archiveDialog.AcceptButton = btnArchive;
                    archiveDialog.CancelButton = btnCancel;

                    if (archiveDialog.ShowDialog() == DialogResult.OK)
                    {
                        string reason = string.IsNullOrWhiteSpace(txtReason.Text)
                            ? "No reason provided"
                            : txtReason.Text;
                        bool archiveItems = chkArchiveItems.Checked;
                        bool deactivateItems = chkDeactivateItems.Checked;

                        ArchiveSet(setDto.SetId, reason, archiveItems, deactivateItems);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error archiving set: {ex.Message}", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void ArchiveSet(int setId, string reason, bool archiveItems, bool deactivateItems)
        {
            try
            {
                using (var con = new SqlConnection(_connectionString))
                {
                    con.Open();
                    using (var transaction = con.BeginTransaction())
                    {
                        try
                        {
                            // Insert Set into universal ArchiveStatus table
                            string insertArchiveSql = @"
                                INSERT INTO ArchiveStatus (EntityType, EntityId, IsArchived, ArchivedAt, ArchivedBy, ArchiveReason)
                                VALUES ('Set', @SetId, 1, GETDATE(), @ArchivedBy, @ArchiveReason)";

                            using (var cmd = new SqlCommand(insertArchiveSql, con, transaction))
                            {
                                cmd.Parameters.AddWithValue("@SetId", setId);
                                cmd.Parameters.AddWithValue("@ArchivedBy", AppSession.CurrentUserName ?? "System");
                                cmd.Parameters.AddWithValue("@ArchiveReason", reason);
                                cmd.ExecuteNonQuery();
                            }

                            // Archive related items if requested
                            if (archiveItems)
                            {
                                string archiveItemsSql = @"
                                    INSERT INTO ArchiveStatus (EntityType, EntityId, IsArchived, ArchivedAt, ArchivedBy, ArchiveReason)
                                    SELECT 'Item', r.ItemId, 1, GETDATE(), @ArchivedBy,
                                           'Archived with Set ' + CAST(@SetId AS NVARCHAR(20))
                                    FROM dbo.Request r
                                    WHERE r.SetId = @SetId
                                      AND NOT EXISTS (
                                          SELECT 1 FROM ArchiveStatus
                                          WHERE EntityType = 'Item' AND EntityId = r.ItemId
                                      )";

                                using (var cmd = new SqlCommand(archiveItemsSql, con, transaction))
                                {
                                    cmd.Parameters.AddWithValue("@SetId", setId);
                                    cmd.Parameters.AddWithValue("@ArchivedBy", AppSession.CurrentUserName ?? "System");
                                    cmd.ExecuteNonQuery();
                                }

                                // Optionally deactivate items
                                if (deactivateItems)
                                {
                                    string deactivateItemsSql = @"
                                        UPDATE i
                                        SET i.Active = 0
                                        FROM dbo.Item i
                                        INNER JOIN dbo.Request r ON i.ItemId = r.ItemId
                                        WHERE r.SetId = @SetId";

                                    using (var cmd = new SqlCommand(deactivateItemsSql, con, transaction))
                                    {
                                        cmd.Parameters.AddWithValue("@SetId", setId);
                                        cmd.ExecuteNonQuery();
                                    }
                                }
                            }

                            transaction.Commit();

                            MessageBox.Show("Set archived successfully!\n\nYou can view archived sets in the Archive page.",
                                "Success",
                                MessageBoxButtons.OK,
                                MessageBoxIcon.Information);

                            _ = LoadSetsAsync();
                        }
                        catch
                        {
                            transaction.Rollback();
                            throw;
                        }
                    }
                }
            }
            catch (SqlException ex)
            {
                MessageBox.Show($"Database error while archiving set:\n\n{ex.Message}",
                    "Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error archiving set:\n\n{ex.Message}",
                    "Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }

        private async void BtnRegenerateQRData_Click(object sender, EventArgs e)
        {
            var result = MessageBox.Show(
                "This will regenerate QRData for all Sets that don't have it yet.\n\n" +
                "This is safe and won't affect existing data.\n\n" +
                "Continue?",
                "Regenerate QRData",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);

            if (result == DialogResult.Yes)
            {
                try
                {
                    Cursor = Cursors.WaitCursor;
                    //btnRegenerateQRData.Enabled = false;
                    //btnRegenerateQRData.Text = "Processing...";

                    int count = await _qrDataService.RegenerateQRDataForExistingSetsAsync();

                    Cursor = Cursors.Default;
                    //btnRegenerateQRData.Enabled = true;
                    //btnRegenerateQRData.Text = "Regenerate QRData";

                    MessageBox.Show(
                        $"\u2705 Successfully regenerated QRData for {count} set(s)!",
                        "Success",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information);

                    // Refresh the grid
                    await LoadSetsAsync();
                }
                catch (Exception ex)
                {
                    Cursor = Cursors.Default;
                    //btnRegenerateQRData.Enabled = true;
                    //btnRegenerateQRData.Text = "Regenerate QRData";

                    MessageBox.Show(
                        $"\u274C Error regenerating QRData:\n\n{ex.Message}",
                        "Error",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Error);
                }
            }
        }

        private async Task DeleteSetAsync(SetDto setDto)
        {
            try
            {
                // Show warning dialog
                var result = MessageBox.Show(
                    $"\u26A0\uFE0F PERMANENT DELETE WARNING \u26A0\uFE0F\n\n" +
                    $"This will PERMANENTLY delete this set and restore stock:\n\n" +
                    $"Set Code: {setDto.SetCode}\n" +
                    $"Document #: {setDto.DocumentNumber ?? "N/A"}\n" +
                    $"Status: {setDto.Status}\n" +
                    $"Item Count: {setDto.ItemCount}\n\n" +
                    $"All items in this set will have their stock restored.\n" +
                    $"Related inventory entries will also be deleted.\n\n" +
                    $"This action CANNOT be undone!\n\n" +
                    $"\u26A0\uFE0F Only proceed if this was a DATA ENTRY ERROR.\n" +
                    $"\u26A0\uFE0F Use 'Archive' button instead for normal records.\n\n" +
                    $"Are you absolutely sure you want to permanently delete?",
                    "Confirm Permanent Deletion",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Warning,
                    MessageBoxDefaultButton.Button2
                );

                if (result == DialogResult.Yes)
                {
                    // Double confirmation for safety
                    var doubleCheck = MessageBox.Show(
                        "FINAL CONFIRMATION\n\n" +
                        "This set will be permanently deleted and cannot be recovered.\n" +
                        "Stock will be restored for all items in this set.\n\n" +
                        "Are you absolutely certain?",
                        "Final Confirmation",
                        MessageBoxButtons.YesNo,
                        MessageBoxIcon.Exclamation,
                        MessageBoxDefaultButton.Button2);

                    if (doubleCheck == DialogResult.Yes)
                    {
                        try
                        {
                            bool success = await _repository.DeleteSetAndRestoreStock(setDto.SetId);

                            if (success)
                            {
                                MessageBox.Show(
                                    $"Set permanently deleted!\n\n" +
                                    $"\u2714 Set removed from database\n" +
                                    $"\u2714 Stock restored for all items\n" +
                                    $"\u2714 Related inventory entries deleted",
                                    "Deleted Successfully",
                                    MessageBoxButtons.OK,
                                    MessageBoxIcon.Information);

                                await LoadSetsAsync();  // Refresh
                            }
                        }
                        catch (Exception ex)
                        {
                            MessageBox.Show($"Error deleting set:\n\n{ex.Message}", "Error",
                                MessageBoxButtons.OK, MessageBoxIcon.Error);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error in delete operation:\n\n{ex.Message}", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        // NEW: View Report Button Click Handler
        private void BtnViewReport_Click(object sender, EventArgs e)
        {
            try
            {
                // Open the SetReport.rdl that you created in Report Builder
                ReportHelper.ShowReport("SetReport.rdl", "Dispatch Report");
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Error opening report:\n\n{ex.Message}\n\n" +
                    "Make sure SetReport.rdl exists in the Reports folder.",
                    "Report Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }

        #region Sorting Implementation

        // Common date column name candidates for created date
        private static readonly string[] CreatedDateCandidates = new[]
        {
            "CreatedAt",
            "DateCreated",
            "CreatedDate",
            "Date",
            "StartDate",
            "EndDate"
        };

        /// <summary>
        /// Resolves the created date column for this grid by checking which candidate column exists
        /// </summary>
        private string ResolveCreatedDateColumn()
        {
            if (dgvSets == null || dgvSets.Columns == null)
                return null;

            foreach (var candidate in CreatedDateCandidates)
            {
                var col = dgvSets.Columns
                    .Cast<DataGridViewColumn>()
                    .FirstOrDefault(c => string.Equals(c.DataPropertyName, candidate, StringComparison.OrdinalIgnoreCase));

                if (col != null)
                    return col.DataPropertyName;
            }

            return null;
        }

        /// <summary>
        /// Enables column header sorting by configuring sort modes and wiring the column header click event
        /// </summary>
        private void EnableSortingGlyphs()
        {
            if (dgvSets == null)
                return;

            // Configure sort mode for each column
            foreach (DataGridViewColumn column in dgvSets.Columns)
            {
                if (column is DataGridViewCheckBoxColumn ||
                    column is DataGridViewButtonColumn ||
                    column is DataGridViewImageColumn)
                {
                    column.SortMode = DataGridViewColumnSortMode.NotSortable;
                }
                else
                {
                    column.SortMode = DataGridViewColumnSortMode.Programmatic;
                }
            }

            // Enable visual styles for sort glyphs
            dgvSets.EnableHeadersVisualStyles = false;

            // Wire up the column header click event (remove existing handler first to avoid duplicates)
            dgvSets.ColumnHeaderMouseClick -= DgvSets_ColumnHeaderMouseClick;
            dgvSets.ColumnHeaderMouseClick += DgvSets_ColumnHeaderMouseClick;
        }

        /// <summary>
        /// Handles column header clicks for sorting
        /// </summary>
        private void DgvSets_ColumnHeaderMouseClick(object sender, DataGridViewCellMouseEventArgs e)
        {
            if (dgvSets == null || e.ColumnIndex < 0 || e.ColumnIndex >= dgvSets.Columns.Count)
                return;

            var clickedColumn = dgvSets.Columns[e.ColumnIndex];

            // Don't sort non-sortable columns
            if (clickedColumn.SortMode == DataGridViewColumnSortMode.NotSortable)
                return;

            // Store column index for later reference
            int columnIndex = e.ColumnIndex;

            // Determine new sort direction
            System.Windows.Forms.SortOrder newSortOrder;
            if (clickedColumn.HeaderCell.SortGlyphDirection == System.Windows.Forms.SortOrder.None ||
                clickedColumn.HeaderCell.SortGlyphDirection == System.Windows.Forms.SortOrder.Descending)
            {
                newSortOrder = System.Windows.Forms.SortOrder.Ascending;
            }
            else
            {
                newSortOrder = System.Windows.Forms.SortOrder.Descending;
            }

            // Apply sort
            string propertyName = clickedColumn.DataPropertyName;
            if (!string.IsNullOrEmpty(propertyName))
            {
                var direction = newSortOrder == System.Windows.Forms.SortOrder.Ascending
                    ? System.ComponentModel.ListSortDirection.Ascending
                    : System.ComponentModel.ListSortDirection.Descending;

                _sortColumn = clickedColumn;
                _sortOrder = newSortOrder;

                SortDataSource(propertyName, direction);

                // Sync the Sort By dropdown with the column header sort
                if (_layout?.SortByComboBox != null && _sortColumn != null)
                {
                    DefaultListPageTemplate.SyncSortByDropdown(_layout.SortByComboBox, _sortColumn, _sortOrder);
                }

                // Refresh display - this rebinds the DataSource
                UpdatePagination();

                // Get fresh column reference AFTER pagination rebind and set glyphs
                if (columnIndex >= 0 && columnIndex < dgvSets.Columns.Count)
                {
                    // Clear all glyphs
                    foreach (DataGridViewColumn col in dgvSets.Columns)
                        col.HeaderCell.SortGlyphDirection = System.Windows.Forms.SortOrder.None;

                    // Set glyph on sorted column using fresh reference
                    var sortedColumn = dgvSets.Columns[columnIndex];
                    sortedColumn.HeaderCell.SortGlyphDirection = newSortOrder;

                    dgvSets.Invalidate();
                }
            }
        }

        private T FindControlRecursive<T>(Control parent, Func<T, bool> predicate) where T : Control
        {
            if (parent is T t && predicate(t))
                return t;

            foreach (Control child in parent.Controls)
            {
                var result = FindControlRecursive(child, predicate);
                if (result != null)
                    return result;
            }

            return null;
        }

        /// <summary>
        /// Sorts the underlying data source and refreshes the grid
        /// </summary>
        private void SortDataSource(string propertyName, System.ComponentModel.ListSortDirection direction)
        {
            if (_filteredSets == null || _filteredSets.Count == 0)
                return;

            try
            {
                var propertyInfo = typeof(SetDto).GetProperty(propertyName);
                if (propertyInfo == null)
                    return;

                // Sort the filtered list
                if (direction == System.ComponentModel.ListSortDirection.Ascending)
                {
                    _filteredSets = _filteredSets
                        .OrderBy(x => propertyInfo.GetValue(x, null))
                        .ToList();
                }
                else
                {
                    _filteredSets = _filteredSets
                        .OrderByDescending(x => propertyInfo.GetValue(x, null))
                        .ToList();
                }
            }
            catch
            {
                // If sorting fails, leave data as-is
            }
        }

        /// <summary>
        /// Handles Filter By dropdown selection changes
        /// </summary>
        private void CmbFilterBy_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (_cmbFilterBy == null || _filteredSets == null)
                return;

            string selectedFilter = _cmbFilterBy.SelectedItem?.ToString();
            if (string.IsNullOrEmpty(selectedFilter))
                return;

            // Resolve the created date column
            string dateColumn = ResolveCreatedDateColumn();

            if (string.IsNullOrEmpty(dateColumn))
            {
                // No date column found - cannot apply date-based filter
                if (selectedFilter != "Default")
                {
                    MessageBox.Show(
                        "Cannot apply date-based filter: No date column found in the grid.",
                        "Filter Not Available",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information);
                    _cmbFilterBy.SelectedIndex = 0; // Reset to Default
                }
                return;
            }

            // Apply filter-based sorting
            System.ComponentModel.ListSortDirection? dir = null;
            if (selectedFilter == "Most Recently Added")
                dir = System.ComponentModel.ListSortDirection.Descending;
            else if (selectedFilter == "Oldest Added")
                dir = System.ComponentModel.ListSortDirection.Ascending;

            if (dir.HasValue)
            {
                // Sort by the resolved date column
                SortDataSource(dateColumn, dir.Value);
            }

            // Refresh display - this rebinds the DataSource
            UpdatePagination();

            // Set glyphs AFTER UpdatePagination to ensure they persist
            // Clear all existing sort glyphs
            foreach (DataGridViewColumn col in dgvSets.Columns)
            {
                col.HeaderCell.SortGlyphDirection = System.Windows.Forms.SortOrder.None;
            }

            if (dir.HasValue)
            {
                // Set sort glyph on the date column using fresh reference
                var dateCol = dgvSets.Columns
                    .Cast<DataGridViewColumn>()
                    .FirstOrDefault(c => string.Equals(c.DataPropertyName, dateColumn, StringComparison.OrdinalIgnoreCase));

                if (dateCol != null)
                {
                    dateCol.HeaderCell.SortGlyphDirection = dir.Value == System.ComponentModel.ListSortDirection.Ascending
                        ? System.Windows.Forms.SortOrder.Ascending
                        : System.Windows.Forms.SortOrder.Descending;

                    dgvSets.Invalidate();
                }
            }
        }

        #endregion
    }
}

