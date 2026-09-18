﻿﻿using Yakult.Inventory.App.Session;
using Yakult.Inventory.App.Repositories;
using Yakult.Inventory.App.Helpers;
using System;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Windows.Forms;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Collections.Generic;
using Yakult.Inventory.App.Core;
using Yakult.Inventory.App.Pages.Request;


namespace Yakult.Inventory.App.Pages.Cartridge
{
    // IMPORTANT:
    // Cartridge ModelNumber must NEVER be used for lookup, grouping, or auto-matching.
    // Model is NOT unique; multiple cartridges can share the same model.
    // All inbound and refill operations must explicitly target a cartridge
    // by SerialNumber or ItemId only.

    public partial class ViewCartridgeRequestsPage : UserControl
    {
        private System.Windows.Forms.Panel _headerPanel;
        private System.Windows.Forms.Panel _buttonBarPanel;
        private System.Windows.Forms.Panel _summaryPanel;
        private System.Windows.Forms.Panel _paginationPanel;
        private ReaLTaiizor.Controls.Panel _bodyPanel;
        private bool _allSelected = false;
        private System.Windows.Forms.CheckBox _selectAllCheckBox;

        private ReaLTaiizor.Controls.MaterialCard _gridCard;
        private ReaLTaiizor.Controls.MaterialCard _cardTotal;
        private ReaLTaiizor.Controls.MaterialCard _cardUnderReview;
        private ReaLTaiizor.Controls.MaterialCard _cardOnHold;
        private ReaLTaiizor.Controls.MaterialCard _cardSubmitted;
        private ReaLTaiizor.Controls.MaterialCard _cardCompleted;

        private Label _lblTotalCount;
        private Label _lblUnderReviewCount;
        private Label _lblOnHoldCount;
        private Label _lblSubmittedCount;
        private Label _lblCompletedCount;

        private ReaLTaiizor.Controls.PoisonDataGridView dgvRequests;
        private ComboBox cmbStatusFilter;
        private ReaLTaiizor.Controls.HopeButton btnRefresh;
        private ReaLTaiizor.Controls.HopeButton btnAdd;
        private ReaLTaiizor.Controls.HopeButton btnEdit;
        private ReaLTaiizor.Controls.HopeButton btnArchive;
        private ReaLTaiizor.Controls.HopeButton btnDelete;
        private ReaLTaiizor.Controls.HopeButton btnMarkSubmitted;
        private Label lblTitle;
        private ReaLTaiizor.Controls.HopeTextBox txtSearch;
        private ComboBox cmbFilterBy;
        private ComboBox cmbSortBy;
        private RequestRepository _repository;
        private CartridgeManagementRepository _cartridgeRepository;
        private System.Collections.Generic.List<RequestDto> _allRequests;
        private System.Collections.Generic.List<RequestDto> _filteredRequests;
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

        public ViewCartridgeRequestsPage()
        {
            _repository = new RequestRepository();
            _cartridgeRepository = new CartridgeManagementRepository();
            _connectionString = Yakult.Inventory.App.Core.DatabaseConfig.ConnectionString;

            InitializeComponent();
            BuildUi();
            LoadRequests();
        }

        private void BuildUi()
        {
            SuspendLayout();
            Controls.Clear();

            Dock = DockStyle.Fill;
            BackColor = Color.White;

            _headerPanel = new System.Windows.Forms.Panel
            {
                Dock = DockStyle.Top,
                Height = 100,
                BackColor = Color.FromArgb(245, 247, 250),
                Padding = new Padding(12, 10, 12, 10)
            };

            lblTitle = new Label
            {
                Text = "Cartridge Requests",
                AutoSize = true,
                Font = new Font("Segoe UI", 16F, FontStyle.Bold),
                ForeColor = Color.FromArgb(40, 40, 40),
                Location = new Point(15, 10)
            };

            txtSearch = new ReaLTaiizor.Controls.HopeTextBox
            {
                BackColor = Color.White,
                BaseColor = Color.White,
                BorderColorA = Color.FromArgb(220, 220, 220),
                BorderColorB = Color.FromArgb(220, 220, 220),
                ForeColor = Color.FromArgb(60, 60, 60),
                Font = new Font("Segoe UI", 9F),
                Hint = "Search by Employee, Item, Model, Serial, Category, Entry Type...",
                Location = new Point(18, 54),
                Size = new Size(360, 32),
                MaxLength = 32767,
                Multiline = false,
                UseSystemPasswordChar = false
            };
            txtSearch.TextChanged += TxtSearch_TextChanged;

            var lblFilterBy = new Label
            {
                Text = "Filter By:",
                AutoSize = true,
                Font = new Font("Segoe UI", 9F, FontStyle.Regular),
                ForeColor = Color.FromArgb(60, 60, 60),
                Location = new Point(387, 62)
            };

            cmbFilterBy = new ComboBox
            {
                Location = new Point(450, 58),
                Width = 160,
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            cmbFilterBy.Items.AddRange(new object[] { "Default", "Most Recently Added", "Oldest Added" });
            cmbFilterBy.SelectedIndex = 0;
            cmbFilterBy.SelectedIndexChanged += CmbFilterBy_SelectedIndexChanged;

            var lblSortBy = new Label
            {
                Text = "Sort By:",
                AutoSize = true,
                Font = new Font("Segoe UI", 9F, FontStyle.Regular),
                ForeColor = Color.FromArgb(60, 60, 60),
                Location = new Point(620, 62)
            };

            cmbSortBy = new ComboBox
            {
                Location = new Point(680, 58),
                Width = 200,
                DropDownStyle = ComboBoxStyle.DropDownList
            };

            _headerPanel.Controls.Add(lblTitle);
            _headerPanel.Controls.Add(txtSearch);
            _headerPanel.Controls.Add(lblFilterBy);
            _headerPanel.Controls.Add(cmbFilterBy);
            _headerPanel.Controls.Add(lblSortBy);
            _headerPanel.Controls.Add(cmbSortBy);

            _buttonBarPanel = new System.Windows.Forms.Panel
            {
                Dock = DockStyle.Top,
                Height = 94,
                BackColor = Color.FromArgb(245, 247, 250),
                Padding = new Padding(12, 0, 12, 10)
            };

            var buttonCard = new ReaLTaiizor.Controls.MaterialCard
            {
                Dock = DockStyle.Fill,
                BackColor = Color.White,
                Padding = new Padding(14, 12, 14, 12)
            };

            var buttonTable = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 1,
                Margin = new Padding(0),
                Padding = new Padding(0),
                BackColor = Color.Transparent
            };
            buttonTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            buttonTable.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

            var leftFlow = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                AutoScroll = true,
                BackColor = Color.Transparent,
                Padding = new Padding(0),
                Margin = new Padding(0)
            };

            var lblStatus = new Label
            {
                AutoSize = true,
                Text = "Status:",
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                ForeColor = Color.FromArgb(60, 60, 60),
                Margin = new Padding(0, 16, 6, 0)
            };

            cmbStatusFilter = new ComboBox
            {
                Width = 160,
                DropDownStyle = ComboBoxStyle.DropDownList,
                Margin = new Padding(0, 13, 14, 0)
            };
            cmbStatusFilter.Items.AddRange(new object[] { "All", "Under Review", "On Hold", "Submitted", "Completed" });
            cmbStatusFilter.SelectedIndex = 0;
            cmbStatusFilter.SelectedIndexChanged += (s, e) => ApplyFilter();

            btnAdd = new ReaLTaiizor.Controls.HopeButton
            {
                Text = "\u2795  Add New",
                Font = new Font("Segoe UI Emoji", 10.5F, FontStyle.Bold),
                Size = new Size(150, 46),
                Margin = new Padding(0, 6, 12, 0)
            };
            ConfigurePillHopeButton(btnAdd, Color.FromArgb(52, 152, 219), Color.FromArgb(41, 128, 185));
            btnAdd.Click += (s, e) => AddNewRequest();

            btnEdit = new ReaLTaiizor.Controls.HopeButton
            {
                Text = "\u270E  Edit",
                Font = new Font("Segoe UI Emoji", 10.5F, FontStyle.Bold),
                Size = new Size(120, 46),
                Margin = new Padding(0, 6, 12, 0)
            };
            ConfigureOutlineHopeButton(btnEdit, Color.FromArgb(41, 128, 185), Color.FromArgb(235, 245, 255));
            btnEdit.Click += (s, e) => EditSelectedRequest();

            btnMarkSubmitted = new ReaLTaiizor.Controls.HopeButton
            {
                Text = "\U0001F4E4  Mark Submitted",
                Font = new Font("Segoe UI Emoji", 10.5F, FontStyle.Bold),
                Size = new Size(200, 46),
                Margin = new Padding(0, 6, 12, 0)
            };
            ConfigureOutlineHopeButton(btnMarkSubmitted, Color.FromArgb(41, 128, 185), Color.FromArgb(235, 245, 255));
            btnMarkSubmitted.Click += (s, e) => MarkAsSubmitted();

            btnArchive = new ReaLTaiizor.Controls.HopeButton
            {
                Text = "\U0001F4E6  Archive",
                Font = new Font("Segoe UI Emoji", 10.5F, FontStyle.Bold),
                Size = new Size(140, 46),
                Margin = new Padding(0, 6, 12, 0)
            };
            ConfigureOutlineHopeButton(btnArchive, Color.FromArgb(41, 128, 185), Color.FromArgb(235, 245, 255));
            btnArchive.Click += (s, e) => ArchiveSelectedRequest();

            btnDelete = new ReaLTaiizor.Controls.HopeButton
            {
                Text = "\U0001F5D1  Delete",
                Font = new Font("Segoe UI Emoji", 10.5F, FontStyle.Bold),
                Size = new Size(130, 46),
                Margin = new Padding(0, 6, 12, 0)
            };
            ConfigureOutlineHopeButton(btnDelete, Color.FromArgb(231, 76, 60), Color.FromArgb(255, 240, 240));
            btnDelete.Click += (s, e) => DeleteSelectedRequest();

            leftFlow.Controls.Add(lblStatus);
            leftFlow.Controls.Add(cmbStatusFilter);
            leftFlow.Controls.Add(btnAdd);
            leftFlow.Controls.Add(btnEdit);
            leftFlow.Controls.Add(btnMarkSubmitted);
            leftFlow.Controls.Add(btnArchive);
            leftFlow.Controls.Add(btnDelete);

            var rightFlow = new FlowLayoutPanel
            {
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                BackColor = Color.Transparent,
                Margin = new Padding(0),
                Padding = new Padding(0)
            };

            btnRefresh = new ReaLTaiizor.Controls.HopeButton
            {
                Text = "\u27F3",
                Font = new Font("Segoe UI", 18F, FontStyle.Bold),
                Size = new Size(58, 58),
                MinimumSize = new Size(58, 58),
                MaximumSize = new Size(58, 58),
                Margin = new Padding(0)
            };
            ConfigurePillHopeButton(btnRefresh, Color.FromArgb(52, 152, 219), Color.FromArgb(41, 128, 185));
            btnRefresh.Click += (s, e) => LoadRequests();
            btnRefresh.Resize += (s, e) =>
            {
                using (var path = new GraphicsPath())
                {
                    int w = Math.Max(1, btnRefresh.Width - 1);
                    int h = Math.Max(1, btnRefresh.Height - 1);
                    path.AddEllipse(0, 0, w, h);
                    btnRefresh.Region = new Region(path);
                }
            };
            rightFlow.Controls.Add(btnRefresh);

            buttonTable.Controls.Add(leftFlow, 0, 0);
            buttonTable.Controls.Add(rightFlow, 1, 0);
            buttonCard.Controls.Add(buttonTable);
            _buttonBarPanel.Controls.Add(buttonCard);

            _summaryPanel = new System.Windows.Forms.Panel
            {
                Dock = DockStyle.Top,
                Height = 90,
                Padding = new Padding(12, 8, 12, 8),
                BackColor = Color.White
            };

            var summaryFlow = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                AutoScroll = true,
                BackColor = Color.Transparent,
                Padding = new Padding(0),
                Margin = new Padding(0)
            };

            _cardTotal = CreateSummaryCard("Total", out _lblTotalCount, Color.FromArgb(41, 128, 185));
            _cardUnderReview = CreateSummaryCard("Under Review", out _lblUnderReviewCount, Color.FromArgb(41, 128, 185));
            _cardOnHold = CreateSummaryCard("On Hold", out _lblOnHoldCount, Color.FromArgb(41, 128, 185));
            _cardSubmitted = CreateSummaryCard("Submitted", out _lblSubmittedCount, Color.FromArgb(41, 128, 185));
            _cardCompleted = CreateSummaryCard("Completed", out _lblCompletedCount, Color.FromArgb(41, 128, 185));

            summaryFlow.Controls.Add(_cardTotal);
            summaryFlow.Controls.Add(_cardUnderReview);
            summaryFlow.Controls.Add(_cardOnHold);
            summaryFlow.Controls.Add(_cardSubmitted);
            summaryFlow.Controls.Add(_cardCompleted);
            _summaryPanel.Controls.Add(summaryFlow);

            _paginationPanel = new System.Windows.Forms.Panel
            {
                Dock = DockStyle.Bottom,
                Height = 40,
                BackColor = Color.White,
                Padding = new Padding(12, 5, 12, 5)
            };

            // DataGridView
            dgvRequests = new ReaLTaiizor.Controls.PoisonDataGridView
            {
                Dock = DockStyle.Fill,
                AutoGenerateColumns = false,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                AllowUserToResizeRows = false,
                AllowUserToResizeColumns = true,
                ReadOnly = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect = false,
                BackgroundColor = Color.White,
                BorderStyle = BorderStyle.None,
                RowHeadersVisible = false,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                EnableHeadersVisualStyles = false,
                GridColor = Color.White,
                CellBorderStyle = DataGridViewCellBorderStyle.Single,
                ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.Single,
                RowHeadersBorderStyle = DataGridViewHeaderBorderStyle.Single
            };

            dgvRequests.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(52, 152, 219);
            dgvRequests.ColumnHeadersDefaultCellStyle.ForeColor = Color.White;
            dgvRequests.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            dgvRequests.ColumnHeadersHeight = 40;
            dgvRequests.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
            dgvRequests.RowTemplate.Height = 56;
            dgvRequests.DefaultCellStyle.Font = new Font("Segoe UI", 9F);
            dgvRequests.DefaultCellStyle.SelectionBackColor = Color.FromArgb(41, 128, 185);
            dgvRequests.DefaultCellStyle.SelectionForeColor = Color.White;
            dgvRequests.DefaultCellStyle.Padding = new Padding(8, 6, 8, 6);
            dgvRequests.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(232, 240, 248);

            dgvRequests.CellPainting += DgvRequests_CellPainting;
            dgvRequests.CellFormatting += DgvRequests_CellFormatting;

            // Define columns
            // Add checkbox column as first column
            var selectColumn = new DataGridViewCheckBoxColumn
            {
                Name = "colSelect",
                DataPropertyName = "Selected",
                HeaderText = "",
                Width = 40,
                ReadOnly = false,
                ThreeState = false,
                TrueValue = true,
                FalseValue = false,
                ValueType = typeof(bool)
            };
            dgvRequests.Columns.Add(selectColumn);

            dgvRequests.Columns.Add(new DataGridViewTextBoxColumn {
                DataPropertyName = "ReqId",
                HeaderText = "ID",
                AutoSizeMode = DataGridViewAutoSizeColumnMode.AllCells,
                MinimumWidth = 60
            });
            dgvRequests.Columns.Add(new DataGridViewTextBoxColumn {
                DataPropertyName = "EmployeeName",
                HeaderText = "Employee",
                FillWeight = 15,
                MinimumWidth = 130
            });
            dgvRequests.Columns.Add(new DataGridViewTextBoxColumn {
                DataPropertyName = "ItemName",
                HeaderText = "Item",
                FillWeight = 18,
                MinimumWidth = 150
            });
            dgvRequests.Columns.Add(new DataGridViewTextBoxColumn {
                DataPropertyName = "ModelNumber",
                HeaderText = "Model #",
                FillWeight = 12,
                MinimumWidth = 110
            });
            dgvRequests.Columns.Add(new DataGridViewTextBoxColumn {
                DataPropertyName = "Category",
                HeaderText = "Category",
                FillWeight = 10,
                MinimumWidth = 100
            });
            dgvRequests.Columns.Add(new DataGridViewTextBoxColumn {
                Name = "colFixedAsset",
                HeaderText = "Fixed Asset",
                AutoSizeMode = DataGridViewAutoSizeColumnMode.AllCells,
                MinimumWidth = 90,
                ReadOnly = true
            });
            dgvRequests.Columns.Add(new DataGridViewTextBoxColumn {
                DataPropertyName = "SerialNumber",
                HeaderText = "Serial #",
                FillWeight = 12,
                MinimumWidth = 110
            });
            dgvRequests.Columns.Add(new DataGridViewTextBoxColumn {
                DataPropertyName = "Quantity",
                HeaderText = "Qty",
                AutoSizeMode = DataGridViewAutoSizeColumnMode.AllCells,
                MinimumWidth = 70
            });
            dgvRequests.Columns.Add(new DataGridViewTextBoxColumn {
                DataPropertyName = "Status",
                HeaderText = "Status",
                FillWeight = 10,
                MinimumWidth = 110
            });
            dgvRequests.Columns.Add(new DataGridViewTextBoxColumn {
                DataPropertyName = "DateRequested",
                HeaderText = "Date Requested",
                FillWeight = 12,
                MinimumWidth = 120,
                DefaultCellStyle = new DataGridViewCellStyle { Format = "MM/dd/yyyy" }
            });
            dgvRequests.Columns.Add(new DataGridViewTextBoxColumn {
                DataPropertyName = "EntryType",
                HeaderText = "Entry Type",
                FillWeight = 10,
                MinimumWidth = 100
            });
            dgvRequests.Columns.Add(new DataGridViewTextBoxColumn {
                DataPropertyName = "Description",
                HeaderText = "Description",
                FillWeight = 16,
                MinimumWidth = 180
            });
            dgvRequests.Columns.Add(new DataGridViewTextBoxColumn {
                DataPropertyName = "Remarks",
                HeaderText = "Remarks",
                FillWeight = 16,
                MinimumWidth = 180
            });
            dgvRequests.Columns.Add(new DataGridViewTextBoxColumn {
                DataPropertyName = "CreatedByName",
                HeaderText = "Created By",
                FillWeight = 12,
                MinimumWidth = 120
            });

            // Make all columns read-only except the checkbox
            foreach (DataGridViewColumn col in dgvRequests.Columns)
            {
                if (col.Name != "colSelect")
                {
                    col.ReadOnly = true;
                }
            }

            // Add checkbox event handlers for single-click toggling
            dgvRequests.CellValueChanged += (s, e) =>
            {
                if (e.ColumnIndex == 0 && e.RowIndex >= 0)
                {
                    // Sync the Selected property in the DTO
                    var item = dgvRequests.Rows[e.RowIndex].DataBoundItem as RequestDto;
                    if (item != null)
                    {
                        var cellValue = dgvRequests.Rows[e.RowIndex].Cells[0].Value;
                        item.Selected = cellValue != null && (bool)cellValue;
                    }
                }
            };

            dgvRequests.CurrentCellDirtyStateChanged += (s, e) =>
            {
                if (dgvRequests.CurrentCell is DataGridViewCheckBoxCell)
                    dgvRequests.CommitEdit(DataGridViewDataErrorContexts.Commit);
            };

            dgvRequests.DoubleClick += (s, e) =>
            {
                // We need to check if the click was on the checkbox column, but DoubleClick event doesn't give us CellEventArgs.
                // However, since we are using FullRowSelect, we can check the CurrentCell.
                if (dgvRequests.CurrentCell != null && dgvRequests.CurrentCell.ColumnIndex != dgvRequests.Columns["colSelect"].Index)
                    EditSelectedRequest();
            };

            // Add Select All checkbox using the helper method
            _selectAllCheckBox = Helpers.DefaultListPageTemplate.AddSelectAllCheckBox(dgvRequests, "colSelect");

            // Style the checkbox column
            Helpers.DefaultListPageTemplate.StyleSelectionCheckBoxColumn(dgvRequests, "colSelect");

            _bodyPanel = new ReaLTaiizor.Controls.Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.White,
                Padding = new Padding(15, 12, 15, 15)
            };

            _gridCard = new ReaLTaiizor.Controls.MaterialCard
            {
                Dock = DockStyle.Fill,
                BackColor = Color.White,
                Padding = new Padding(10)
            };

            _gridCard.Controls.Add(dgvRequests);
            _bodyPanel.Controls.Add(_gridCard);

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

            _paginationPanel.Controls.AddRange(new Control[] { btnFirstPage, btnPrevPage, lblPageInfo, btnNextPage, btnLastPage });

            Controls.Add(_bodyPanel);
            Controls.Add(_paginationPanel);
            Controls.Add(_summaryPanel);
            Controls.Add(_buttonBarPanel);
            Controls.Add(_headerPanel);

            ResumeLayout(true);

            DefaultListPageTemplate.SetupInitialPageFocus(txtSearch, dgvRequests,
                btnAdd, btnEdit, btnMarkSubmitted, btnArchive, btnDelete, btnRefresh);
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

        private void UpdateSummaryCards(IEnumerable<RequestDto> data)
        {
            if (_lblTotalCount == null)
                return;

            var list = data?.ToList() ?? new List<RequestDto>();

            int total = list.Count;
            int underReview = list.Count(r => string.Equals((r.Status ?? string.Empty).Trim(), "Under Review", StringComparison.OrdinalIgnoreCase));
            int onHold = list.Count(r => string.Equals((r.Status ?? string.Empty).Trim(), "On Hold", StringComparison.OrdinalIgnoreCase));
            int submitted = list.Count(r => string.Equals((r.Status ?? string.Empty).Trim(), "Submitted", StringComparison.OrdinalIgnoreCase));
            int completed = list.Count(r => string.Equals((r.Status ?? string.Empty).Trim(), "Completed", StringComparison.OrdinalIgnoreCase));

            _lblTotalCount.Text = total.ToString();
            _lblUnderReviewCount.Text = underReview.ToString();
            _lblOnHoldCount.Text = onHold.ToString();
            _lblSubmittedCount.Text = submitted.ToString();
            _lblCompletedCount.Text = completed.ToString();
        }

        private void TxtSearch_TextChanged(object sender, EventArgs e)
        {
            ApplyFilter();
        }

        private void LoadRequests()
        {
            try
            {
                _allRequests = _repository.GetCartridgeRequests();
                ApplyFilter();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to load cartridge requests: {ex.Message}", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void ApplyFilter()
        {
            if (_allRequests == null) return;

            var filtered = _allRequests.AsEnumerable();

            // Filter by search text
            string searchText = txtSearch?.Text?.Trim().ToLower();
            if (!string.IsNullOrWhiteSpace(searchText) &&
                searchText != "search by employee, item, model, serial, category, entry type...")
            {
                filtered = filtered.Where(r =>
                    (r.EmployeeName != null && r.EmployeeName.ToLower().Contains(searchText)) ||
                    (r.ItemName != null && r.ItemName.ToLower().Contains(searchText)) ||
                    (r.ModelNumber != null && r.ModelNumber.ToLower().Contains(searchText)) ||
                    (r.SerialNumber != null && r.SerialNumber.ToLower().Contains(searchText)) ||
                    (r.Category != null && r.Category.ToLower().Contains(searchText)) ||
                    (r.Description != null && r.Description.ToLower().Contains(searchText)) ||
                    (r.Remarks != null && r.Remarks.ToLower().Contains(searchText)) ||
                    (r.EntryType != null && r.EntryType.ToLower().Contains(searchText))
                );
            }

            // Filter by status
            if (cmbStatusFilter.SelectedItem != null)
            {
                string selectedStatus = cmbStatusFilter.SelectedItem.ToString();
                if (selectedStatus != "All")
                {
                    filtered = filtered.Where(r => r.Status == selectedStatus);
                }
            }

            _filteredRequests = filtered.ToList();
            _currentPage = 1;
            UpdateSummaryCards(_filteredRequests);
            UpdatePagination();
        }

        private void UpdatePagination()
        {
            if (_filteredRequests == null || _filteredRequests.Count == 0)
            {
                dgvRequests.DataSource = new System.Collections.Generic.List<RequestDto>();
                DefaultListPageTemplate.DisableDefaultRowHighlight(dgvRequests,
                    btnAdd, btnEdit, btnMarkSubmitted, btnArchive, btnDelete, btnRefresh);
                if (lblPageInfo != null)
                    lblPageInfo.Text = "Page 0 of 0 (0 requests)";
                if (btnFirstPage != null) btnFirstPage.Enabled = false;
                if (btnPrevPage != null) btnPrevPage.Enabled = false;
                if (btnNextPage != null) btnNextPage.Enabled = false;
                if (btnLastPage != null) btnLastPage.Enabled = false;
                return;
            }

            int totalPages = (int)Math.Ceiling((double)_filteredRequests.Count / _pageSize);
            if (_currentPage > totalPages) _currentPage = totalPages;
            if (_currentPage < 1) _currentPage = 1;

            var pagedData = _filteredRequests
                .Skip((_currentPage - 1) * _pageSize)
                .Take(_pageSize)
                .ToList();

            dgvRequests.DataSource = pagedData;
            EnableSortingGlyphs();

            DefaultListPageTemplate.DisableDefaultRowHighlight(dgvRequests,
                btnAdd, btnEdit, btnMarkSubmitted, btnArchive, btnDelete, btnRefresh);

            // Initialize Sort By dropdown after first load (when columns are available)
            if (!_sortByDropdownInitialized && cmbSortBy != null && dgvRequests != null && dgvRequests.Columns.Count > 0)
            {
                var defaultSortKey = DefaultListPageTemplate.ResolveDefaultSortColumnKey(dgvRequests);
                DefaultListPageTemplate.SetupSortByDropdown(
                    cmbSortBy,
                    dgvRequests,
                    (columnKey, direction) =>
                    {
                        var col = dgvRequests.Columns.Cast<DataGridViewColumn>()
                            .FirstOrDefault(c => c.DataPropertyName == columnKey || c.Name == columnKey);

                        if (col != null)
                        {
                            _sortColumn = col;
                            _sortOrder = direction;

                            var dir = direction == System.Windows.Forms.SortOrder.Ascending
                                ? System.ComponentModel.ListSortDirection.Ascending
                                : System.ComponentModel.ListSortDirection.Descending;

                            SortDataSource(col.DataPropertyName, dir);
                            UpdatePagination();

                            // Set glyph after pagination
                            foreach (DataGridViewColumn c in dgvRequests.Columns)
                                c.HeaderCell.SortGlyphDirection = System.Windows.Forms.SortOrder.None;
                            _sortColumn.HeaderCell.SortGlyphDirection = _sortOrder;
                            dgvRequests.Invalidate();
                        }
                    },
                    defaultColumnKey: defaultSortKey,
                    defaultDirection: System.Windows.Forms.SortOrder.Ascending
                );

                _sortByDropdownInitialized = true;
            }

            if (lblPageInfo != null)
                lblPageInfo.Text = $"Page {_currentPage} of {totalPages} ({_filteredRequests.Count} requests)";

            if (btnFirstPage != null) btnFirstPage.Enabled = _currentPage > 1;
            if (btnPrevPage != null) btnPrevPage.Enabled = _currentPage > 1;
            if (btnNextPage != null) btnNextPage.Enabled = _currentPage < totalPages;
            if (btnLastPage != null) btnLastPage.Enabled = _currentPage < totalPages;
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
            if (_filteredRequests == null) return;

            int totalPages = (int)Math.Ceiling((double)_filteredRequests.Count / _pageSize);
            if (_currentPage < totalPages)
            {
                _currentPage++;
                UpdatePagination();
            }
        }

        private void BtnLastPage_Click(object sender, EventArgs e)
        {
            if (_filteredRequests == null) return;

            int totalPages = (int)Math.Ceiling((double)_filteredRequests.Count / _pageSize);
            _currentPage = totalPages;
            UpdatePagination();
        }

        private void DgvRequests_CellPainting(object sender, DataGridViewCellPaintingEventArgs e)
        {
            var grid = sender as DataGridView;
            if (grid == null)
                return;

            if (e.ColumnIndex < 0)
                return;

            var column = grid.Columns[e.ColumnIndex];
            if (column == null)
                return;

            // Header painting (rounded corners like ViewVendorsPage)
            if (e.RowIndex < 0)
            {
                e.Handled = true;

                // Fill background with white for border separation
                e.Graphics.FillRectangle(Brushes.White, e.CellBounds);

                // Fill header cell with header color (leaving 1px border)
                Rectangle r = new Rectangle(
                    e.CellBounds.X,
                    e.CellBounds.Y,
                    e.CellBounds.Width - 1,  // Leave 1px for right border
                    e.CellBounds.Height - 1  // Leave 1px for bottom border
                );

                using (var brush = new SolidBrush(Color.FromArgb(52, 152, 219)))
                {
                    e.Graphics.FillRectangle(brush, r);
                }

                // Draw white vertical border on the right edge
                using (var borderPen = new Pen(Color.White, 1))
                {
                    e.Graphics.DrawLine(borderPen,
                        e.CellBounds.Right - 1, e.CellBounds.Top,
                        e.CellBounds.Right - 1, e.CellBounds.Bottom - 1);
                }

                // Draw white horizontal border on the bottom edge
                using (var borderPen = new Pen(Color.White, 1))
                {
                    e.Graphics.DrawLine(borderPen,
                        e.CellBounds.Left, e.CellBounds.Bottom - 1,
                        e.CellBounds.Right, e.CellBounds.Bottom - 1);
                }

                // Draw header text
                using (var textBrush = new SolidBrush(Color.White))
                using (var format = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                {
                    // Adjust text rectangle to make room for sort glyph
                    var textRect = e.CellBounds;
                    
                    // Draw Sort Glyph on ALL sortable columns
                    bool isSortable = column.SortMode == DataGridViewColumnSortMode.Programmatic;
                    
                    if (isSortable)
                    {
                        // Reduce text width to avoid overlap
                        textRect.Width -= 16;
                        
                        var glyphX = e.CellBounds.Right - 18;
                        var glyphY = e.CellBounds.Y + (e.CellBounds.Height - 8) / 2;
                        
                        // Determine arrow color - bright white for active sort, dimmed for inactive
                        Color arrowColor = column.HeaderCell.SortGlyphDirection != System.Windows.Forms.SortOrder.None 
                            ? Color.White 
                            : Color.FromArgb(180, 255, 255, 255); // Semi-transparent white
                        
                        // Draw arrow
                        using (var pen = new Pen(arrowColor, 2))
                        {
                            if (column.HeaderCell.SortGlyphDirection == System.Windows.Forms.SortOrder.Ascending)
                            {
                                // Up arrow (active sort)
                                e.Graphics.DrawLine(pen, glyphX, glyphY + 6, glyphX + 5, glyphY);
                                e.Graphics.DrawLine(pen, glyphX + 5, glyphY, glyphX + 10, glyphY + 6);
                            }
                            else if (column.HeaderCell.SortGlyphDirection == System.Windows.Forms.SortOrder.Descending)
                            {
                                // Down arrow (active sort)
                                e.Graphics.DrawLine(pen, glyphX, glyphY, glyphX + 5, glyphY + 6);
                                e.Graphics.DrawLine(pen, glyphX + 5, glyphY + 6, glyphX + 10, glyphY);
                            }
                            else
                            {
                                // Default: show both arrows (inactive state)
                                // Up arrow
                                e.Graphics.DrawLine(pen, glyphX + 2, glyphY + 2, glyphX + 5, glyphY - 1);
                                e.Graphics.DrawLine(pen, glyphX + 5, glyphY - 1, glyphX + 8, glyphY + 2);
                                // Down arrow
                                e.Graphics.DrawLine(pen, glyphX + 2, glyphY + 4, glyphX + 5, glyphY + 7);
                                e.Graphics.DrawLine(pen, glyphX + 5, glyphY + 7, glyphX + 8, glyphY + 4);
                            }
                        }
                    }

                    e.Graphics.DrawString(column.HeaderText, e.CellStyle.Font ?? grid.Font, textBrush, textRect, format);
                }
                return;
            }

            // Data row painting - pill-shaped like ViewVendorsPage
            bool isSelected = grid.Rows[e.RowIndex].Selected;
            bool isFirstColumn = e.ColumnIndex == 0;
            bool isLastColumn = e.ColumnIndex == grid.Columns.Count - 1;

            bool isCenteredDataColumn =
                column.DataPropertyName == "ReqId" ||
                column.DataPropertyName == "Quantity" ||
                column.DataPropertyName == "Status" ||
                column.DataPropertyName == "DateRequested" ||
                column.DataPropertyName == "EntryType";

            // Alternating row colors like ViewVendorsPage (blue/green)
            Color pillColor = (e.RowIndex % 2 == 0)
                ? Color.FromArgb(227, 242, 253)  // Light blue
                : Color.FromArgb(232, 245, 233);  // Light green

            if (isSelected)
                pillColor = Color.FromArgb(41, 128, 185);

            // Fill background with white for border separation
            e.Graphics.FillRectangle(Brushes.White, e.CellBounds);

            int topPadding = 6;
            int bottomPadding = 6;

            // Fill cell with color (leaving space for borders)
            Rectangle pillBounds = new Rectangle(
                e.CellBounds.X,
                e.CellBounds.Y + topPadding,
                e.CellBounds.Width - 1,  // Leave 1px for right border
                e.CellBounds.Height - topPadding - bottomPadding - 1  // Leave 1px for bottom border
            );

            using (var brush = new SolidBrush(pillColor))
            {
                e.Graphics.FillRectangle(brush, pillBounds);
            }

            Color textColor = isSelected ? Color.White : Color.FromArgb(40, 40, 40);

            // Handle checkbox column
            if (column is DataGridViewCheckBoxColumn)
            {
                var checkBoxSize = 18;
                var checkBoxX = e.CellBounds.X + (e.CellBounds.Width - checkBoxSize) / 2;
                var checkBoxY = e.CellBounds.Y + (e.CellBounds.Height - checkBoxSize) / 2;
                var checkBoxRect = new Rectangle(checkBoxX, checkBoxY, checkBoxSize, checkBoxSize);

                bool isChecked = false;
                if (e.Value != null && e.Value is bool)
                {
                    isChecked = (bool)e.Value;
                }

                System.Windows.Forms.VisualStyles.CheckBoxState state = isChecked
                    ? System.Windows.Forms.VisualStyles.CheckBoxState.CheckedNormal
                    : System.Windows.Forms.VisualStyles.CheckBoxState.UncheckedNormal;

                CheckBoxRenderer.DrawCheckBox(e.Graphics, checkBoxRect.Location, state);
            }
            else if (e.Value != null)
            {
                using (var textBrush = new SolidBrush(textColor))
                {
                    var format = new StringFormat
                    {
                        Alignment = isCenteredDataColumn ? StringAlignment.Center : StringAlignment.Near,
                        LineAlignment = StringAlignment.Center,
                        Trimming = StringTrimming.EllipsisCharacter
                    };

                    var textRect = new Rectangle(
                        e.CellBounds.X + (isCenteredDataColumn ? 0 : 10),
                        e.CellBounds.Y + topPadding,
                        e.CellBounds.Width - (isCenteredDataColumn ? 0 : 20),
                        e.CellBounds.Height - topPadding - bottomPadding
                    );

                    e.Graphics.DrawString(e.FormattedValue?.ToString() ?? string.Empty,
                        e.CellStyle.Font ?? grid.Font,
                        textBrush,
                        textRect,
                        format);
                }
            }

            // Draw white vertical border on the right edge of the cell
            using (var borderPen = new Pen(Color.White, 1))
            {
                e.Graphics.DrawLine(borderPen,
                    e.CellBounds.Right - 1, e.CellBounds.Top,
                    e.CellBounds.Right - 1, e.CellBounds.Bottom - 1);
            }

            // Draw white horizontal border on the bottom edge of the cell
            using (var borderPen = new Pen(Color.White, 1))
            {
                e.Graphics.DrawLine(borderPen,
                    e.CellBounds.Left, e.CellBounds.Bottom - 1,
                    e.CellBounds.Right, e.CellBounds.Bottom - 1);
            }

            e.Handled = true;
        }

        private void DgvRequests_CellFormatting(object sender, DataGridViewCellFormattingEventArgs e)
        {
            var columnName = dgvRequests.Columns[e.ColumnIndex].Name ?? dgvRequests.Columns[e.ColumnIndex].HeaderText;

            // Format the Fixed Asset column
            if (columnName == "colFixedAsset" && e.RowIndex >= 0)
            {
                try
                {
                    var row = dgvRequests.Rows[e.RowIndex];
                    if (row.DataBoundItem is RequestDto request)
                    {
                        // Get the IsTrackedAsset value from the Item
                        // Note: RequestDto may need to include IsTrackedAsset from the Item join
                        e.Value = request.IsTrackedAsset ? "Yes" : "No";
                        e.FormattingApplied = true;
                    }
                }
                catch
                {
                    // Ignore formatting errors
                }
            }
        }

        private void AddNewRequest()
        {
            using (var dialog = new BatchAddRequestDialog())
            {
                if (dialog.ShowDialog() == DialogResult.OK)
                {
                    LoadRequests();
                }
            }
        }

        private void EditSelectedRequest()
        {
            if (dgvRequests.SelectedRows.Count == 0)
            {
                MessageBox.Show("Please select a request to edit.", "No Selection",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            var selectedRequest = dgvRequests.SelectedRows[0].DataBoundItem as RequestDto;
            if (selectedRequest == null) return;

            // Store original status to detect change
            string originalStatus = selectedRequest.Status;

            using (var editDialog = new EditRequestDialog(selectedRequest))
            {
                if (editDialog.ShowDialog() == DialogResult.OK)
                {
                    // Check if request was deleted
                    if (editDialog.Tag?.ToString() == "DELETED")
                    {
                        // Just refresh the list
                        LoadRequests();
                        return;
                    }

                    try
                    {
                        // Set modified by current user
                        selectedRequest.ModifiedByUserId = AppSession.CurrentUserId;
                        
                        // Check if status was changed to "Submitted"
                        if (!string.Equals(originalStatus, "Submitted", StringComparison.OrdinalIgnoreCase) && 
                            string.Equals(selectedRequest.Status, "Submitted", StringComparison.OrdinalIgnoreCase))
                        {
                            // Use SubmitRequest to create Inventory entry and update stock
                            bool success = _repository.SubmitRequest(selectedRequest.ReqId, AppSession.CurrentUserId);
                            
                            if (success)
                            {
                                MessageBox.Show(
                                    "Request submitted successfully!\n\n" +
                                    "\u2714 Status changed to Submitted\n" +
                                    "\u2714 Inventory entry created\n" +
                                    "\u2714 Stock updated",
                                    "Success",
                                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                            }
                        }
                        else
                        {
                            // Normal update (status not changed to Submitted)
                            _repository.UpdateRequest(selectedRequest);
                            
                            MessageBox.Show("Request updated successfully.", "Success",
                                MessageBoxButtons.OK, MessageBoxIcon.Information);
                        }
                        
                        LoadRequests();
                    }
                    catch (InvalidOperationException ex)
                    {
                        // Stock validation error
                        MessageBox.Show(
                            $"Cannot submit request:\n\n{ex.Message}\n\n" +
                            "Please check stock availability.",
                            "Insufficient Stock",
                            MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        
                        // Reload to revert any UI changes
                        LoadRequests();
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Failed to update request: {ex.Message}", "Error",
                            MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
            }
        }

        private void ArchiveSelectedRequest()
        {
            if (dgvRequests.SelectedRows.Count == 0)
            {
                MessageBox.Show("Please select a request to archive.", "No Selection",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            var selectedRequest = dgvRequests.SelectedRows[0].DataBoundItem as RequestDto;
            if (selectedRequest == null) return;

            // Archive confirmation dialog
            using (var archiveDialog = new Form())
            {
                archiveDialog.Text = "Archive Request";
                archiveDialog.Size = new Size(500, 330);
                archiveDialog.StartPosition = FormStartPosition.CenterParent;
                archiveDialog.FormBorderStyle = FormBorderStyle.FixedDialog;
                archiveDialog.MaximizeBox = false;
                archiveDialog.MinimizeBox = false;

                var lblMessage = new Label
                {
                    Text = $"Are you sure you want to archive this request?\n\n" +
                           $"Employee: {selectedRequest.EmployeeName}\n" +
                           $"Item: {selectedRequest.ItemName}\n" +
                           $"Quantity: {selectedRequest.Quantity}\n" +
                           $"Status: {selectedRequest.Status}\n\n" +
                           $"The request will be moved to the archive.",
                    AutoSize = false,
                    Size = new Size(460, 100),
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
                    Size = new Size(460, 20),
                    Location = new Point(10, 135)
                };

                var chkArchiveItem = new System.Windows.Forms.CheckBox
                {
                    Text = "Also archive the associated item",
                    AutoSize = true,
                    Location = new Point(10, 165),
                    Checked = false
                };

                var lblNote = new Label
                {
                    Text = "Note: The employee will NOT be archived",
                    AutoSize = true,
                    Location = new Point(10, 185),
                    ForeColor = Color.Gray,
                    Font = new Font(DefaultFont, FontStyle.Italic)
                };

                var btnArchive = new System.Windows.Forms.Button
                {
                    Text = "Archive",
                    DialogResult = DialogResult.OK,
                    Location = new Point(290, 215),
                    Size = new Size(90, 25)
                };

                var btnCancel = new System.Windows.Forms.Button
                {
                    Text = "Cancel",
                    DialogResult = DialogResult.Cancel,
                    Location = new Point(390, 215),
                    Size = new Size(90, 25)
                };

                archiveDialog.Controls.AddRange(new Control[] { lblMessage, lblReason, txtReason, chkArchiveItem, lblNote, btnArchive, btnCancel });
                archiveDialog.AcceptButton = btnArchive;
                archiveDialog.CancelButton = btnCancel;

                if (archiveDialog.ShowDialog() == DialogResult.OK)
                {
                    string reason = string.IsNullOrWhiteSpace(txtReason.Text) ? "No reason provided" : txtReason.Text;
                    bool archiveItem = chkArchiveItem.Checked;
                    ArchiveRequest(selectedRequest.ReqId, selectedRequest.ItemId, reason, archiveItem);
                }
            }
        }

        private void ArchiveRequest(int reqId, int itemId, string reason, bool archiveItem)
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
                            // Archive the Request
                            string insertArchiveSql = @"
                                INSERT INTO ArchiveStatus (EntityType, EntityId, IsArchived, ArchivedAt, ArchivedBy, ArchiveReason)
                                VALUES ('Request', @ReqId, 1, GETDATE(), @ArchivedBy, @ArchiveReason)";

                            using (var cmd = new SqlCommand(insertArchiveSql, con, transaction))
                            {
                                cmd.Parameters.AddWithValue("@ReqId", reqId);
                                cmd.Parameters.AddWithValue("@ArchivedBy", AppSession.CurrentUserName ?? "System");
                                cmd.Parameters.AddWithValue("@ArchiveReason", reason);
                                cmd.ExecuteNonQuery();
                            }

                            // Optionally archive the associated Item (NOT the employee)
                            if (archiveItem)
                            {
                                string archiveItemSql = @"
                                    INSERT INTO ArchiveStatus (EntityType, EntityId, IsArchived, ArchivedAt, ArchivedBy, ArchiveReason)
                                    VALUES ('Item', @ItemId, 1, GETDATE(), @ArchivedBy, @ArchiveReason)";

                                using (var cmd = new SqlCommand(archiveItemSql, con, transaction))
                                {
                                    cmd.Parameters.AddWithValue("@ItemId", itemId);
                                    cmd.Parameters.AddWithValue("@ArchivedBy", AppSession.CurrentUserName ?? "System");
                                    cmd.Parameters.AddWithValue("@ArchiveReason", $"Archived with Request {reqId}: {reason}");
                                    cmd.ExecuteNonQuery();
                                }
                            }

                            transaction.Commit();

                            string message = archiveItem
                                ? "Request and associated item archived successfully!\n\nYou can view archived requests in the Archive page."
                                : "Request archived successfully!\n\nYou can view archived requests in the Archive page.";

                            MessageBox.Show(message,
                                "Success",
                                MessageBoxButtons.OK,
                                MessageBoxIcon.Information);

                            LoadRequests();
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
                MessageBox.Show($"Database error while archiving request:\n\n{ex.Message}",
                    "Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error archiving request:\n\n{ex.Message}",
                    "Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }

        private async void DeleteSelectedRequest()
        {
            var checkedItems = new System.Collections.Generic.List<RequestDto>();

            for (int i = 0; i < dgvRequests.Rows.Count; i++)
            {
                var row = dgvRequests.Rows[i];
                if (row.IsNewRow) continue;

                var checkValue = row.Cells["colSelect"].Value;
                bool isChecked = checkValue != null && (bool)checkValue == true;

                if (isChecked && row.DataBoundItem is RequestDto entity)
                {
                    checkedItems.Add(entity);
                }
            }

            if (checkedItems.Count == 0)
            {
                MessageBox.Show("Please select at least one request to delete.", "No Selection",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            // Get details for confirmation
            try
            {
                var detailsList = new System.Collections.Generic.List<string>();
                foreach (var req in checkedItems)
                {
                    var (itemName, quantity, employeeName) = await _repository.GetRequestDetailsForDelete(req.ReqId);
                    detailsList.Add($"\u2022 {employeeName} - {itemName} (Qty: {quantity})");
                }

                string countText = checkedItems.Count == 1 ? "this request" : $"these {checkedItems.Count} requests";

                // Show warning dialog
                var result = MessageBox.Show(
                    $"\u26A0\uFE0F PERMANENT DELETE WARNING \u26A0\uFE0F\n\n" +
                    $"This will PERMANENTLY delete {countText} and restore stock:\n\n" +
                    $"{string.Join("\n", detailsList)}\n\n" +
                    $"Stock will be restored for all deleted requests.\n\n" +
                    $"This action CANNOT be undone!\n\n" +
                    $"\u26A0\uFE0F Only proceed if this was a DATA ENTRY ERROR.\n" +
                    $"\u26A0\uFE0F Use 'Archive' button instead for normal records.\n\n" +
                    $"Are you absolutely sure you want to permanently delete?",
                    "Confirm Permanent Deletion",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Warning,
                    MessageBoxDefaultButton.Button2  // Default to "No"
                );

                if (result == DialogResult.Yes)
                {
                    // Double confirmation for safety
                    var doubleCheck = MessageBox.Show(
                        "FINAL CONFIRMATION\n\n" +
                        $"{checkedItems.Count} request(s) will be permanently deleted and cannot be recovered.\n\n" +
                        "Are you absolutely certain?",
                        "Final Confirmation",
                        MessageBoxButtons.YesNo,
                        MessageBoxIcon.Exclamation,
                        MessageBoxDefaultButton.Button2
                    );

                    if (doubleCheck == DialogResult.Yes)
                    {
                        try
                        {
                            int successCount = 0;
                            var errorMessages = new System.Collections.Generic.List<string>();
                            foreach (var request in checkedItems)
                            {
                                var deleteResult = _cartridgeRepository.ForceDeleteCartridgeRequest(request.ReqId, AppSession.CurrentUserId);
                                if (deleteResult.Success)
                                {
                                    successCount++;
                                }
                                else
                                {
                                    errorMessages.Add($"Request #{request.ReqId}: {deleteResult.Message}");
                                }
                            }

                            if (successCount > 0)
                            {
                                MessageBox.Show(
                                    $"{successCount} request(s) permanently deleted!\n\n" +
                                    $"\u2714 Requests removed from database\n" +
                                    $"\u2714 Stock restored for deleted requests",
                                    "Deleted Successfully",
                                    MessageBoxButtons.OK,
                                    MessageBoxIcon.Information);

                                LoadRequests();  // Refresh
                            }

                            if (errorMessages.Count > 0)
                            {
                                MessageBox.Show(
                                    $"Some requests could not be deleted:\n\n{string.Join("\n", errorMessages)}",
                                    "Delete Incomplete",
                                    MessageBoxButtons.OK,
                                    MessageBoxIcon.Error);
                            }
                        }
                        catch (Exception ex)
                        {
                            MessageBox.Show($"Error deleting request:\n\n{ex.Message}", "Error",
                                MessageBoxButtons.OK, MessageBoxIcon.Error);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error retrieving request details:\n\n{ex.Message}", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void MarkAsSubmitted()
        {
            if (dgvRequests.SelectedRows.Count == 0)
            {
                MessageBox.Show("Please select a request to mark as submitted.", "No Selection",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            var selectedRequest = dgvRequests.SelectedRows[0].DataBoundItem as RequestDto;
            if (selectedRequest == null) return;

            if (selectedRequest.Status == "Submitted")
            {
                MessageBox.Show("This request is already marked as Submitted.", "Already Submitted",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var result = MessageBox.Show(
                $"Mark this request as SUBMITTED?\n\n" +
                $"Employee: {selectedRequest.EmployeeName}\n" +
                $"Item: {selectedRequest.ItemName}\n" +
                $"Quantity: {selectedRequest.Quantity}\n\n" +
                $"This will:\n" +
                $"\u2714 Change status to 'Submitted'\n" +
                $"\u2714 Create an Inventory entry (ledger)\n" +
                $"\u2714 Deduct from stock on hand\n\n" +
                $"Continue?",
                "Confirm Submit",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);

            if (result == DialogResult.Yes)
            {
                try
                {
                    // Use SubmitRequest method which handles all operations atomically
                    bool success = _repository.SubmitRequest(selectedRequest.ReqId, AppSession.CurrentUserId);
                    
                    if (success)
                    {
                        MessageBox.Show(
                            "Request submitted successfully!\n\n" +
                            "\u2714 Status changed to Submitted\n" +
                            "\u2714 Inventory entry created\n" +
                            "\u2714 Stock updated",
                            "Success",
                            MessageBoxButtons.OK, MessageBoxIcon.Information);
                        
                        LoadRequests();
                    }
                    else
                    {
                        MessageBox.Show("Failed to submit request. Request not found.", "Error",
                            MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
                catch (InvalidOperationException ex)
                {
                    // Stock validation error
                    MessageBox.Show(
                        $"Cannot submit request:\n\n{ex.Message}\n\n" +
                        "Please check stock availability.",
                        "Insufficient Stock",
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Failed to submit request: {ex.Message}", "Error",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        #region Sorting Implementation

        // Common date column name candidates for created date
        private static readonly string[] CreatedDateCandidates = new[]
        {
            "DateRequested",
            "DateCreated",
            "CreatedDate",
            "CreatedAt",
            "Date",
            "RequestedDate"
        };

        /// <summary>
        /// Resolves the created date column for this grid by checking which candidate column exists
        /// </summary>
        private string ResolveCreatedDateColumn()
        {
            if (dgvRequests == null || dgvRequests.Columns == null)
                return null;

            foreach (var candidate in CreatedDateCandidates)
            {
                var col = dgvRequests.Columns
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
            if (dgvRequests == null)
                return;

            // Configure sort mode for each column
            foreach (DataGridViewColumn column in dgvRequests.Columns)
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
            dgvRequests.EnableHeadersVisualStyles = false;

            // Wire up the column header click event (remove existing handler first to avoid duplicates)
            dgvRequests.ColumnHeaderMouseClick -= DgvRequests_ColumnHeaderMouseClick;
            dgvRequests.ColumnHeaderMouseClick += DgvRequests_ColumnHeaderMouseClick;
        }

        /// <summary>
        /// Handles column header clicks for sorting
        /// </summary>
        private void DgvRequests_ColumnHeaderMouseClick(object sender, DataGridViewCellMouseEventArgs e)
        {
            if (dgvRequests == null || e.ColumnIndex < 0 || e.ColumnIndex >= dgvRequests.Columns.Count)
                return;

            var clickedColumn = dgvRequests.Columns[e.ColumnIndex];

            // Don't sort non-sortable columns
            if (clickedColumn.SortMode == DataGridViewColumnSortMode.NotSortable)
                return;

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

            // Clear all glyphs
            foreach (DataGridViewColumn col in dgvRequests.Columns)
            {
                col.HeaderCell.SortGlyphDirection = System.Windows.Forms.SortOrder.None;
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
                if (cmbSortBy != null && _sortColumn != null)
                {
                    DefaultListPageTemplate.SyncSortByDropdown(cmbSortBy, _sortColumn, _sortOrder);
                }

                // Refresh display first (this rebinds data)
                UpdatePagination();

                // Set glyph AFTER pagination to persist it
                clickedColumn.HeaderCell.SortGlyphDirection = newSortOrder;
                dgvRequests.Invalidate();
            }
        }

        /// <summary>
        /// Sorts the underlying data source and refreshes the grid
        /// </summary>
        private void SortDataSource(string propertyName, System.ComponentModel.ListSortDirection direction)
        {
            if (_filteredRequests == null || _filteredRequests.Count == 0)
                return;

            try
            {
                var propertyInfo = typeof(RequestDto).GetProperty(propertyName);
                if (propertyInfo == null)
                    return;

                // Sort the filtered list
                if (direction == System.ComponentModel.ListSortDirection.Ascending)
                {
                    _filteredRequests = _filteredRequests
                        .OrderBy(x => propertyInfo.GetValue(x, null))
                        .ToList();
                }
                else
                {
                    _filteredRequests = _filteredRequests
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
            if (cmbFilterBy == null || _filteredRequests == null)
                return;

            string selectedFilter = cmbFilterBy.SelectedItem?.ToString();
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
                    cmbFilterBy.SelectedIndex = 0; // Reset to Default
                }
                return;
            }

            // Clear all existing sort glyphs
            foreach (DataGridViewColumn col in dgvRequests.Columns)
            {
                col.HeaderCell.SortGlyphDirection = System.Windows.Forms.SortOrder.None;
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

                // Set sort glyph on the date column
                var dateCol = dgvRequests.Columns
                    .Cast<DataGridViewColumn>()
                    .FirstOrDefault(c => string.Equals(c.DataPropertyName, dateColumn, StringComparison.OrdinalIgnoreCase));

                if (dateCol != null)
                {
                    dateCol.HeaderCell.SortGlyphDirection = dir.Value == System.ComponentModel.ListSortDirection.Ascending
                        ? System.Windows.Forms.SortOrder.Ascending
                        : System.Windows.Forms.SortOrder.Descending;
                }
            }

            // Refresh display
            UpdatePagination();
        }

        #endregion
    }
}

