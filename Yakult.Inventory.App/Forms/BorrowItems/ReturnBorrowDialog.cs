using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using Yakult.Inventory.App.Core;
using Yakult.Inventory.App.Helpers;
using Yakult.Inventory.App.Models.BorrowItems;
using Yakult.Inventory.App.Repositories;
using Yakult.Inventory.App.Session;

namespace Yakult.Inventory.App.Forms.BorrowItems
{
    public sealed class ReturnBorrowDialog : Form
    {
        private readonly BorrowItemsRepository _repo;
        private readonly List<BorrowEmployeeLookup> _employees;
        private readonly BorrowLogRow _openBorrow;

        private ComboBox _cboReturner;
        private Label _lblDept;
        private Button _btnReturn;
        private Button _btnCancel;

        public BorrowLogRow ReturnedRow { get; private set; }

        public ReturnBorrowDialog(BorrowItemsRepository repo, List<BorrowEmployeeLookup> employees, BorrowLogRow openBorrow)
        {
            _repo = repo ?? throw new ArgumentNullException(nameof(repo));
            _employees = employees ?? throw new ArgumentNullException(nameof(employees));
            _openBorrow = openBorrow ?? throw new ArgumentNullException(nameof(openBorrow));

            InitializeComponent();
        }

        private void InitializeComponent()
        {
            this.Text = "Return Item";
            this.StartPosition = FormStartPosition.CenterParent;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.ClientSize = new Size(640, 330);
            this.BackColor = ModernUiHelper.ColorBackground;

            var root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(16),
                ColumnCount = 1,
                RowCount = 3
            };
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            var header = ModernUiHelper.CreateHeaderLabel("Return Borrowed Item");
            header.Height = 32;
            root.Controls.Add(header, 0, 0);

            var card = ModernUiHelper.CreateCard();
            card.Dock = DockStyle.Fill;

            var grid = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                ColumnCount = 2
            };
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 150));
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

            var lblSerial = ModernUiHelper.CreateLabel("Serial:");
            var lblItem = ModernUiHelper.CreateLabel("Item:");
            var lblBorrowedBy = ModernUiHelper.CreateLabel("Borrowed By:");
            var lblBorrowedAt = ModernUiHelper.CreateLabel("Borrowed At:");

            var valSerial = new Label { AutoSize = true, Font = ModernUiHelper.FontNormal, ForeColor = ModernUiHelper.ColorTextPrimary, Text = (_openBorrow.SerialNumber ?? string.Empty).Trim() };
            var valItem = new Label { AutoSize = true, Font = ModernUiHelper.FontNormal, ForeColor = ModernUiHelper.ColorTextPrimary, Text = _openBorrow.ItemDisplay };
            var valBorrowedBy = new Label { AutoSize = true, Font = ModernUiHelper.FontNormal, ForeColor = ModernUiHelper.ColorTextPrimary, Text = $"{_openBorrow.BorrowedByEmpName} - {_openBorrow.BorrowedByDeptName}" };
            var valBorrowedAt = new Label { AutoSize = true, Font = ModernUiHelper.FontNormal, ForeColor = ModernUiHelper.ColorTextPrimary, Text = _openBorrow.BorrowedAtLocal };

            _cboReturner = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDown,
                DisplayMember = "DisplayText",
                ValueMember = "EmpId",
                Font = ModernUiHelper.FontNormal,
                Width = 420
            };
            _cboReturner.AutoCompleteMode = AutoCompleteMode.SuggestAppend;
            _cboReturner.AutoCompleteSource = AutoCompleteSource.ListItems;
            _cboReturner.DataSource = _employees;
            _cboReturner.SelectedIndexChanged += (_, __) => UpdateDept();

            _lblDept = new Label
            {
                AutoSize = true,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                ForeColor = ModernUiHelper.ColorTextSecondary
            };

            grid.Controls.Add(lblSerial, 0, 0);
            grid.Controls.Add(valSerial, 1, 0);
            grid.Controls.Add(lblItem, 0, 1);
            grid.Controls.Add(valItem, 1, 1);
            grid.Controls.Add(lblBorrowedBy, 0, 2);
            grid.Controls.Add(valBorrowedBy, 1, 2);
            grid.Controls.Add(lblBorrowedAt, 0, 3);
            grid.Controls.Add(valBorrowedAt, 1, 3);

            grid.Controls.Add(ModernUiHelper.CreateLabel("Returned By:"), 0, 4);

            var returnerStack = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.TopDown, WrapContents = false, Margin = new Padding(0) };
            returnerStack.Controls.Add(BorrowUiTheme.CreateComboHost(_cboReturner));
            returnerStack.Controls.Add(_lblDept);
            grid.Controls.Add(returnerStack, 1, 4);

            card.Controls.Add(grid);
            root.Controls.Add(card, 0, 1);

            var footer = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.RightToLeft,
                AutoSize = true,
                WrapContents = false
            };

            _btnReturn = ModernUiHelper.CreatePrimaryButton("Return", width: 110);
            _btnReturn.Click += async (_, __) => await ReturnAsync();

            _btnCancel = ModernUiHelper.CreateSecondaryButton("Cancel", width: 110);
            _btnCancel.Click += (_, __) => { this.DialogResult = DialogResult.Cancel; this.Close(); };

            footer.Controls.Add(_btnReturn);
            footer.Controls.Add(_btnCancel);
            root.Controls.Add(footer, 0, 2);

            this.Controls.Add(root);

            UpdateDept();
            ValidateState();
            ApplyTheme();
        }

        private void ApplyTheme()
        {
            BorrowUiTheme.ApplyForm(this);
            ApplyThemeTree(this, false);
            BorrowUiTheme.ApplyButton(_btnReturn, BorrowButtonKind.Success);
            BorrowUiTheme.ApplyButton(_btnCancel, BorrowButtonKind.Secondary);
        }

        private void ApplyThemeTree(Control root, bool surface)
        {
            if (root == null)
                return;

            var palette = BorrowUiTheme.Current;

            foreach (Control child in root.Controls)
            {
                if (child is Button)
                    continue;

                if (child is TextBox)
                {
                    BorrowUiTheme.ApplyTextBox((TextBox)child, false);
                    continue;
                }

                if (child is ComboBox)
                {
                    BorrowUiTheme.ApplyComboBox((ComboBox)child);
                    continue;
                }

                if (BorrowUiTheme.IsComboHost(child))
                {
                    BorrowUiTheme.ApplyComboHost((Panel)child);
                    continue;
                }

                if (child is Label)
                {
                    var label = (Label)child;
                    var secondary = !label.Font.Bold && label.Font.Size <= 10F;
                    BorrowUiTheme.ApplyLabel(label, secondary);
                    continue;
                }

                if (child is Panel || child is TableLayoutPanel || child is FlowLayoutPanel)
                {
                    var nextSurface = surface
                        || child.BackColor == Color.White;
                    child.BackColor = nextSurface ? palette.SurfaceBack : palette.PageBack;
                    child.ForeColor = palette.TextPrimary;
                    ApplyThemeTree(child, nextSurface);
                    continue;
                }

                child.BackColor = surface ? palette.SurfaceBack : palette.PageBack;
                child.ForeColor = palette.TextPrimary;
                ApplyThemeTree(child, surface);
            }
        }

        private void UpdateDept()
        {
            var selected = _cboReturner?.SelectedItem as BorrowEmployeeLookup;
            var dept = selected?.DepartmentName;
            _lblDept.Text = string.IsNullOrWhiteSpace(dept) ? "Department: (missing)" : $"Department: {dept.Trim()}";
            ValidateState();
        }

        private void ValidateState()
        {
            var selected = _cboReturner?.SelectedItem as BorrowEmployeeLookup;
            var ok = selected != null && selected.EmpId > 0;
            if (_btnReturn != null)
                _btnReturn.Enabled = ok;
        }

        private async Task ReturnAsync()
        {
            var selected = _cboReturner?.SelectedItem as BorrowEmployeeLookup;
            if (selected == null || selected.EmpId <= 0)
            {
                MessageBox.Show("Please select an employee.", "Validation",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var encodedByUserId = AppSession.CurrentUserId;
            if (encodedByUserId <= 0)
                encodedByUserId = 1;

            try
            {
                _btnReturn.Enabled = false;
                _btnCancel.Enabled = false;
                this.Cursor = Cursors.WaitCursor;

                ReturnedRow = await _repo.ReturnAsync(_openBorrow.BorrowId, selected.EmpId, encodedByUserId);
                this.DialogResult = DialogResult.OK;
                this.Close();
            }
            catch (Exception ex)
            {
                Logger.LogError($"[ReturnBorrowDialog] ReturnAsync failed for BorrowId={_openBorrow?.BorrowId}.", ex);
                MessageBox.Show("We couldn't return the item right now. Please try again.", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                this.Cursor = Cursors.Default;
                _btnCancel.Enabled = true;
                ValidateState();
            }
        }
    }
}

