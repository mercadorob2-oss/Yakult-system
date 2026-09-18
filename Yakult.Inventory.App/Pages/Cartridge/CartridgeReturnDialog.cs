using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using Yakult.Inventory.App.Models;
using Yakult.Inventory.App.Repositories;
using Yakult.Inventory.App.Session;

namespace Yakult.Inventory.App.Pages.Cartridge
{
    // IMPORTANT:
    // Cartridge ModelNumber must NEVER be used for lookup, grouping, or auto-matching.
    // Model is NOT unique; multiple cartridges can share the same model.
    // All inbound and refill operations must explicitly target a cartridge
    // by SerialNumber or ItemId only.

    /// <summary>
    /// Dialog for recording cartridge returns.
    /// - Quantity-based (no serial numbers required)
    /// - Tied to Employee + Branch + Department
    /// - Captures condition (Empty, Broken, Good)
    /// 
    /// IMPORTANT: Cartridge selection uses ItemId (linked to SerialNumber).
    /// Model-based selection is NOT allowed.
    /// </summary>
    public partial class CartridgeReturnDialog : Form
    {
        private readonly CartridgeRepository _repository;

        // Controls
        private ComboBox cmbCartridgeItem;
        private NumericUpDown numQuantity;
        private ComboBox cmbEmployee;
        private ComboBox cmbBranch;
        private ComboBox cmbDepartment;
        private ComboBox cmbCondition;
        private TextBox txtRemarks;
        private Button btnSave;
        private Button btnCancel;

        public CartridgeReturnDialog()
        {
            _repository = new CartridgeRepository();
            InitializeComponent();
            BuildUi();
            LoadLookups();
        }

        private void InitializeComponent()
        {
            this.SuspendLayout();
            this.AutoScaleDimensions = new SizeF(7F, 15F);
            this.AutoScaleMode = AutoScaleMode.Font;
            this.ClientSize = new Size(500, 420);
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.StartPosition = FormStartPosition.CenterParent;
            this.Text = "Record Cartridge Return";
            this.Font = new Font("Segoe UI", 9F);
            this.BackColor = Color.White;
            this.ResumeLayout(false);
        }

        private void BuildUi()
        {
            int labelWidth = 110;
            int controlWidth = 340;
            int leftMargin = 25;
            int topMargin = 25;
            int rowHeight = 45;
            int y = topMargin;

            // Header
            var lblHeader = new Label
            {
                Text = "Cartridge Return",
                Font = new Font("Segoe UI", 14F, FontStyle.Bold),
                ForeColor = Color.FromArgb(52, 73, 94),
                Location = new Point(leftMargin, y),
                AutoSize = true
            };
            Controls.Add(lblHeader);
            y += 40;

            // Cartridge Item
            Controls.Add(CreateLabel("Cartridge:", leftMargin, y));
            cmbCartridgeItem = new ComboBox
            {
                Location = new Point(leftMargin + labelWidth, y - 3),
                Width = controlWidth,
                DropDownStyle = ComboBoxStyle.DropDownList,
                Font = new Font("Segoe UI", 9F)
            };
            Controls.Add(cmbCartridgeItem);
            y += rowHeight;

            // Quantity
            Controls.Add(CreateLabel("Quantity:", leftMargin, y));
            numQuantity = new NumericUpDown
            {
                Location = new Point(leftMargin + labelWidth, y - 3),
                Width = 100,
                Minimum = 1,
                Maximum = 1000,
                Value = 1,
                Font = new Font("Segoe UI", 9F)
            };
            Controls.Add(numQuantity);
            y += rowHeight;

            // Employee
            Controls.Add(CreateLabel("Employee:", leftMargin, y));
            cmbEmployee = new ComboBox
            {
                Location = new Point(leftMargin + labelWidth, y - 3),
                Width = controlWidth,
                DropDownStyle = ComboBoxStyle.DropDownList,
                Font = new Font("Segoe UI", 9F)
            };
            Controls.Add(cmbEmployee);
            y += rowHeight;

            // Branch
            Controls.Add(CreateLabel("Branch:", leftMargin, y));
            cmbBranch = new ComboBox
            {
                Location = new Point(leftMargin + labelWidth, y - 3),
                Width = controlWidth,
                DropDownStyle = ComboBoxStyle.DropDownList,
                Font = new Font("Segoe UI", 9F)
            };
            Controls.Add(cmbBranch);
            y += rowHeight;

            // Department
            Controls.Add(CreateLabel("Department:", leftMargin, y));
            cmbDepartment = new ComboBox
            {
                Location = new Point(leftMargin + labelWidth, y - 3),
                Width = controlWidth,
                DropDownStyle = ComboBoxStyle.DropDownList,
                Font = new Font("Segoe UI", 9F)
            };
            Controls.Add(cmbDepartment);
            y += rowHeight;

            // Condition
            Controls.Add(CreateLabel("Condition:", leftMargin, y));
            cmbCondition = new ComboBox
            {
                Location = new Point(leftMargin + labelWidth, y - 3),
                Width = 150,
                DropDownStyle = ComboBoxStyle.DropDownList,
                Font = new Font("Segoe UI", 9F)
            };
            cmbCondition.Items.AddRange(CartridgeConditionTypes.All);
            cmbCondition.SelectedIndex = 0; // Default to "Empty"
            Controls.Add(cmbCondition);
            y += rowHeight;

            // Remarks
            Controls.Add(CreateLabel("Remarks:", leftMargin, y));
            txtRemarks = new TextBox
            {
                Location = new Point(leftMargin + labelWidth, y - 3),
                Width = controlWidth,
                Height = 60,
                Multiline = true,
                Font = new Font("Segoe UI", 9F)
            };
            Controls.Add(txtRemarks);
            y += 75;

            // Buttons
            btnSave = new Button
            {
                Text = "Save Return",
                Location = new Point(leftMargin + labelWidth, y),
                Width = 120,
                Height = 35,
                BackColor = Color.FromArgb(46, 204, 113),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            btnSave.FlatAppearance.BorderSize = 0;
            btnSave.Click += BtnSave_Click;
            Controls.Add(btnSave);

            btnCancel = new Button
            {
                Text = "Cancel",
                Location = new Point(leftMargin + labelWidth + 130, y),
                Width = 100,
                Height = 35,
                BackColor = Color.FromArgb(149, 165, 166),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9F),
                Cursor = Cursors.Hand
            };
            btnCancel.FlatAppearance.BorderSize = 0;
            btnCancel.Click += (s, e) => { DialogResult = DialogResult.Cancel; Close(); };
            Controls.Add(btnCancel);
        }

        private Label CreateLabel(string text, int x, int y)
        {
            return new Label
            {
                Text = text,
                Location = new Point(x, y),
                AutoSize = true,
                Font = new Font("Segoe UI", 9F),
                ForeColor = Color.FromArgb(52, 73, 94)
            };
        }

        private void LoadLookups()
        {
            try
            {
                // Load cartridge items
                var items = _repository.GetCartridgeItemsForReturn();
                cmbCartridgeItem.DataSource = null;
                cmbCartridgeItem.DisplayMember = "Name";
                cmbCartridgeItem.ValueMember = "Id";
                cmbCartridgeItem.DataSource = items;

                // Load employees
                var employees = _repository.GetEmployeesLookup();
                cmbEmployee.DataSource = null;
                cmbEmployee.DisplayMember = "Name";
                cmbEmployee.ValueMember = "Id";
                cmbEmployee.DataSource = employees;

                // Load branches
                var branches = _repository.GetBranchesLookup();
                cmbBranch.DataSource = null;
                cmbBranch.DisplayMember = "Name";
                cmbBranch.ValueMember = "Id";
                cmbBranch.DataSource = branches;

                // Load departments
                var departments = _repository.GetDepartmentsLookup();
                cmbDepartment.DataSource = null;
                cmbDepartment.DisplayMember = "Name";
                cmbDepartment.ValueMember = "Id";
                cmbDepartment.DataSource = departments;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to load lookups: {ex.Message}", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void BtnSave_Click(object sender, EventArgs e)
        {
            // Validation
            if (cmbCartridgeItem.SelectedValue == null || (int)cmbCartridgeItem.SelectedValue <= 0)
            {
                MessageBox.Show("Please select a cartridge item.", "Validation", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                cmbCartridgeItem.Focus();
                return;
            }

            if (cmbEmployee.SelectedValue == null || (int)cmbEmployee.SelectedValue <= 0)
            {
                MessageBox.Show("Please select an employee.", "Validation", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                cmbEmployee.Focus();
                return;
            }

            if (cmbBranch.SelectedValue == null || (int)cmbBranch.SelectedValue <= 0)
            {
                MessageBox.Show("Please select a branch.", "Validation", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                cmbBranch.Focus();
                return;
            }

            if (cmbDepartment.SelectedValue == null || (int)cmbDepartment.SelectedValue <= 0)
            {
                MessageBox.Show("Please select a department.", "Validation", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                cmbDepartment.Focus();
                return;
            }

            if (cmbCondition.SelectedItem == null)
            {
                MessageBox.Show("Please select a condition.", "Validation", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                cmbCondition.Focus();
                return;
            }

            try
            {
                var returnDto = new CartridgeReturnDto
                {
                    ItemId = (int)cmbCartridgeItem.SelectedValue,
                    Quantity = (int)numQuantity.Value,
                    EmployeeId = (int)cmbEmployee.SelectedValue,
                    BranchId = (int)cmbBranch.SelectedValue,
                    DeptId = (int)cmbDepartment.SelectedValue,
                    ConditionType = cmbCondition.SelectedItem.ToString(),
                    Remarks = txtRemarks.Text.Trim(),
                    CreatedBy = AppSession.CurrentUserId
                };

                int movementId = _repository.RecordCartridgeReturn(returnDto);

                MessageBox.Show(
                    $"Cartridge return recorded successfully!\n\n" +
                    $"Movement ID: {movementId}\n" +
                    $"Quantity: {returnDto.Quantity}\n" +
                    $"Condition: {returnDto.ConditionType}",
                    "Success",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);

                DialogResult = DialogResult.OK;
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to record cartridge return: {ex.Message}", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}
