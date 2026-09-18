using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using Yakult.Inventory.App.Helpers;

namespace Yakult.Inventory.App.Pages
{
    public partial class BulkAddMasterDataPage : Form
    {
        private readonly string _connectionString;

        // Tab control for different sections
        private TabControl tabControl;

        // Department section
        private DataGridView dgvDepartments;
        private Button btnAddDepartment;

        // Branch section
        private DataGridView dgvBranches;
        private Button btnAddBranch;

        // Employee section
        private DataGridView dgvEmployees;
        private Button btnAddEmployee;

        // Item section
        private DataGridView dgvItems;
        private Button btnAddItem;

        // Request section (if applicable)
        private DataGridView dgvRequests;
        private Button btnAddRequest;

        // Action buttons
        private Button btnSubmitAll;
        private Button btnCancel;

        public BulkAddMasterDataPage(string connectionString)
        {
            _connectionString = connectionString;
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            this.Text = "Bulk Add Master Data";
            this.Size = new Size(1000, 700);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;

            // Create tab control
            tabControl = new TabControl
            {
                Dock = DockStyle.Fill,
                Location = new Point(10, 10),
                Size = new Size(960, 600)
            };

            // Create tabs
            var tabDepartment = new TabPage("Departments");
            var tabBranch = new TabPage("Branches");
            var tabEmployee = new TabPage("Employees");
            var tabItem = new TabPage("Items");
            var tabRequest = new TabPage("Requests");

            // Initialize Department tab
            InitializeDepartmentTab(tabDepartment);

            // Initialize Branch tab
            InitializeBranchTab(tabBranch);

            // Initialize Employee tab
            InitializeEmployeeTab(tabEmployee);

            // Initialize Item tab
            InitializeItemTab(tabItem);

            // Initialize Request tab
            InitializeRequestTab(tabRequest);

            // Add tabs to control
            tabControl.TabPages.Add(tabDepartment);
            tabControl.TabPages.Add(tabBranch);
            tabControl.TabPages.Add(tabEmployee);
            tabControl.TabPages.Add(tabItem);
            tabControl.TabPages.Add(tabRequest);

            // Create bottom button panel
            var panelButtons = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 60,
                Padding = new Padding(10)
            };

            btnSubmitAll = new Button
            {
                Text = "Submit All",
                Size = new Size(120, 35),
                Location = new Point(700, 12),
                BackColor = Color.FromArgb(0, 120, 215),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };
            btnSubmitAll.Click += BtnSubmitAll_Click;

            btnCancel = new Button
            {
                Text = "Cancel",
                Size = new Size(120, 35),
                Location = new Point(830, 12),
                BackColor = Color.Gray,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };
            btnCancel.Click += (s, e) => this.Close();

            panelButtons.Controls.Add(btnSubmitAll);
            panelButtons.Controls.Add(btnCancel);

            // Add controls to form
            this.Controls.Add(tabControl);
            this.Controls.Add(panelButtons);
        }

        private void InitializeDepartmentTab(TabPage tab)
        {
            dgvDepartments = new DataGridView
            {
                Location = new Point(10, 50),
                Size = new Size(920, 450),
                AllowUserToAddRows = false,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill
            };

            DataGridViewSafety.Attach(dgvDepartments);

            // Add columns
            dgvDepartments.Columns.Add(new DataGridViewTextBoxColumn { Name = "Name", HeaderText = "Department Name" });
            dgvDepartments.Columns.Add(new DataGridViewTextBoxColumn { Name = "Description", HeaderText = "Description" });
            dgvDepartments.Columns.Add(new DataGridViewComboBoxColumn { Name = "Company", HeaderText = "Company (Optional)" });
            
            // Load companies for dropdown
            LoadCompaniesForDepartments();

            btnAddDepartment = new Button
            {
                Text = "+ Add Row",
                Location = new Point(10, 10),
                Size = new Size(100, 30),
                BackColor = Color.FromArgb(0, 120, 215),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };
            btnAddDepartment.Click += (s, e) => dgvDepartments.Rows.Add();

            tab.Controls.Add(btnAddDepartment);
            tab.Controls.Add(dgvDepartments);
        }

        private void InitializeBranchTab(TabPage tab)
        {
            dgvBranches = new DataGridView
            {
                Location = new Point(10, 50),
                Size = new Size(920, 450),
                AllowUserToAddRows = false,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill
            };

            DataGridViewSafety.Attach(dgvBranches);

            // Add columns with enhanced categories
            dgvBranches.Columns.Add(new DataGridViewTextBoxColumn { Name = "Name", HeaderText = "Branch Name" });
            dgvBranches.Columns.Add(new DataGridViewTextBoxColumn { Name = "Description", HeaderText = "Description" });
            dgvBranches.Columns.Add(new DataGridViewComboBoxColumn { Name = "Company", HeaderText = "Company (Optional)" });
            dgvBranches.Columns.Add(new DataGridViewComboBoxColumn { Name = "Department", HeaderText = "Department (Optional)" });
            
            // Add category columns
            dgvBranches.Columns.Add(new DataGridViewCheckBoxColumn { Name = "IsFactory", HeaderText = "Factory" });
            dgvBranches.Columns.Add(new DataGridViewCheckBoxColumn { Name = "IsDepot", HeaderText = "Depot" });
            dgvBranches.Columns.Add(new DataGridViewCheckBoxColumn { Name = "IsCenter", HeaderText = "Center" });
            dgvBranches.Columns.Add(new DataGridViewComboBoxColumn { Name = "CenterRegion", HeaderText = "Center Region" });
            dgvBranches.Columns.Add(new DataGridViewCheckBoxColumn { Name = "IsDistributor", HeaderText = "Distributor" });

            // Load dropdown data
            LoadCompaniesForBranches();
            LoadDepartmentsForBranches();
            LoadCenterRegions();

            btnAddBranch = new Button
            {
                Text = "+ Add Row",
                Location = new Point(10, 10),
                Size = new Size(100, 30),
                BackColor = Color.FromArgb(0, 120, 215),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };
            btnAddBranch.Click += (s, e) => dgvBranches.Rows.Add();

            tab.Controls.Add(btnAddBranch);
            tab.Controls.Add(dgvBranches);
        }

        private void InitializeEmployeeTab(TabPage tab)
        {
            dgvEmployees = new DataGridView
            {
                Location = new Point(10, 50),
                Size = new Size(920, 450),
                AllowUserToAddRows = false,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill
            };

            DataGridViewSafety.Attach(dgvEmployees);

            // Add columns
            dgvEmployees.Columns.Add(new DataGridViewTextBoxColumn { Name = "FirstName", HeaderText = "First Name" });
            dgvEmployees.Columns.Add(new DataGridViewTextBoxColumn { Name = "LastName", HeaderText = "Last Name" });
            dgvEmployees.Columns.Add(new DataGridViewTextBoxColumn { Name = "Email", HeaderText = "Email" });
            dgvEmployees.Columns.Add(new DataGridViewTextBoxColumn { Name = "Phone", HeaderText = "Phone" });
            dgvEmployees.Columns.Add(new DataGridViewComboBoxColumn { Name = "Department", HeaderText = "Department (Optional)" });
            dgvEmployees.Columns.Add(new DataGridViewComboBoxColumn { Name = "Branch", HeaderText = "Branch (Optional)" });

            LoadDepartmentsForEmployees();
            LoadBranchesForEmployees();

            btnAddEmployee = new Button
            {
                Text = "+ Add Row",
                Location = new Point(10, 10),
                Size = new Size(100, 30),
                BackColor = Color.FromArgb(0, 120, 215),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };
            btnAddEmployee.Click += (s, e) => dgvEmployees.Rows.Add();

            tab.Controls.Add(btnAddEmployee);
            tab.Controls.Add(dgvEmployees);
        }

        private void InitializeItemTab(TabPage tab)
        {
            dgvItems = new DataGridView
            {
                Location = new Point(10, 50),
                Size = new Size(920, 450),
                AllowUserToAddRows = false,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill
            };

            DataGridViewSafety.Attach(dgvItems);

            // Add columns
            dgvItems.Columns.Add(new DataGridViewTextBoxColumn { Name = "Name", HeaderText = "Item Name" });
            dgvItems.Columns.Add(new DataGridViewTextBoxColumn { Name = "Code", HeaderText = "Item Code" });
            dgvItems.Columns.Add(new DataGridViewTextBoxColumn { Name = "Description", HeaderText = "Description" });
            dgvItems.Columns.Add(new DataGridViewTextBoxColumn { Name = "UnitPrice", HeaderText = "Unit Price" });
            dgvItems.Columns.Add(new DataGridViewComboBoxColumn { Name = "Category", HeaderText = "Category (Optional)" });

            LoadCategoriesForItems();

            btnAddItem = new Button
            {
                Text = "+ Add Row",
                Location = new Point(10, 10),
                Size = new Size(100, 30),
                BackColor = Color.FromArgb(0, 120, 215),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };
            btnAddItem.Click += (s, e) => dgvItems.Rows.Add();

            tab.Controls.Add(btnAddItem);
            tab.Controls.Add(dgvItems);
        }

        private void InitializeRequestTab(TabPage tab)
        {
            dgvRequests = new DataGridView
            {
                Location = new Point(10, 50),
                Size = new Size(920, 450),
                AllowUserToAddRows = false,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill
            };

            DataGridViewSafety.Attach(dgvRequests);

            // Add columns
            dgvRequests.Columns.Add(new DataGridViewTextBoxColumn { Name = "RequestNumber", HeaderText = "Request #" });
            dgvRequests.Columns.Add(new DataGridViewTextBoxColumn { Name = "Description", HeaderText = "Description" });
            dgvRequests.Columns.Add(new DataGridViewComboBoxColumn { Name = "RequestedBy", HeaderText = "Requested By" });
            dgvRequests.Columns.Add(new DataGridViewComboBoxColumn { Name = "Branch", HeaderText = "Branch" });
            dgvRequests.Columns.Add(new DataGridViewTextBoxColumn { Name = "RequestDate", HeaderText = "Request Date" });

            LoadEmployeesForRequests();
            LoadBranchesForRequests();

            btnAddRequest = new Button
            {
                Text = "+ Add Row",
                Location = new Point(10, 10),
                Size = new Size(100, 30),
                BackColor = Color.FromArgb(0, 120, 215),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };
            btnAddRequest.Click += (s, e) => dgvRequests.Rows.Add();

            tab.Controls.Add(btnAddRequest);
            tab.Controls.Add(dgvRequests);
        }

        #region Load Dropdown Data

        private void LoadCompaniesForDepartments()
        {
            var comboColumn = (DataGridViewComboBoxColumn)dgvDepartments.Columns["Company"];
            comboColumn.Items.Add(""); // Empty option for no company
            
            using (var conn = new SqlConnection(_connectionString))
            {
                conn.Open();
                using (var cmd = new SqlCommand("SELECT Id, Name FROM Company", conn))
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        comboColumn.Items.Add(new ComboItem 
                        { 
                            Id = (int)reader["Id"], 
                            Name = reader["Name"].ToString() 
                        });
                    }
                }
            }
            comboColumn.DisplayMember = "Name";
            comboColumn.ValueMember = "Id";
        }

        private void LoadCompaniesForBranches()
        {
            var comboColumn = (DataGridViewComboBoxColumn)dgvBranches.Columns["Company"];
            comboColumn.Items.Add("");
            
            using (var conn = new SqlConnection(_connectionString))
            {
                conn.Open();
                using (var cmd = new SqlCommand("SELECT Id, Name FROM Company", conn))
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        comboColumn.Items.Add(new ComboItem 
                        { 
                            Id = (int)reader["Id"], 
                            Name = reader["Name"].ToString() 
                        });
                    }
                }
            }
            comboColumn.DisplayMember = "Name";
            comboColumn.ValueMember = "Id";
        }

        private void LoadDepartmentsForBranches()
        {
            var comboColumn = (DataGridViewComboBoxColumn)dgvBranches.Columns["Department"];
            comboColumn.Items.Add("");
            
            using (var conn = new SqlConnection(_connectionString))
            {
                conn.Open();
                using (var cmd = new SqlCommand("SELECT Id, Name FROM Department", conn))
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        comboColumn.Items.Add(new ComboItem 
                        { 
                            Id = (int)reader["Id"], 
                            Name = reader["Name"].ToString() 
                        });
                    }
                }
            }
            comboColumn.DisplayMember = "Name";
            comboColumn.ValueMember = "Id";
        }

        private void LoadCenterRegions()
        {
            var comboColumn = (DataGridViewComboBoxColumn)dgvBranches.Columns["CenterRegion"];
            comboColumn.Items.Add(""); // Empty for non-centers
            comboColumn.Items.Add("NCR");
            comboColumn.Items.Add("Region I - Ilocos");
            comboColumn.Items.Add("Region II - Cagayan Valley");
            comboColumn.Items.Add("Region III - Central Luzon");
            comboColumn.Items.Add("Region IV-A - CALABARZON");
            comboColumn.Items.Add("Region IV-B - MIMAROPA");
            comboColumn.Items.Add("Region V - Bicol");
            comboColumn.Items.Add("Region VI - Western Visayas");
            comboColumn.Items.Add("Region VII - Central Visayas");
            comboColumn.Items.Add("Region VIII - Eastern Visayas");
            comboColumn.Items.Add("Region IX - Zamboanga Peninsula");
            comboColumn.Items.Add("Region X - Northern Mindanao");
            comboColumn.Items.Add("Region XI - Davao");
            comboColumn.Items.Add("Region XII - SOCCSKSARGEN");
            comboColumn.Items.Add("Region XIII - Caraga");
            comboColumn.Items.Add("CAR - Cordillera");
            comboColumn.Items.Add("BARMM - Bangsamoro");
        }

        private void LoadDepartmentsForEmployees()
        {
            var comboColumn = (DataGridViewComboBoxColumn)dgvEmployees.Columns["Department"];
            comboColumn.Items.Add("");
            
            using (var conn = new SqlConnection(_connectionString))
            {
                conn.Open();
                using (var cmd = new SqlCommand("SELECT Id, Name FROM Department", conn))
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        comboColumn.Items.Add(new ComboItem 
                        { 
                            Id = (int)reader["Id"], 
                            Name = reader["Name"].ToString() 
                        });
                    }
                }
            }
            comboColumn.DisplayMember = "Name";
            comboColumn.ValueMember = "Id";
        }

        private void LoadBranchesForEmployees()
        {
            var comboColumn = (DataGridViewComboBoxColumn)dgvEmployees.Columns["Branch"];
            comboColumn.Items.Add("");
            
            using (var conn = new SqlConnection(_connectionString))
            {
                conn.Open();
                using (var cmd = new SqlCommand("SELECT Id, Name FROM Branch", conn))
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        comboColumn.Items.Add(new ComboItem 
                        { 
                            Id = (int)reader["Id"], 
                            Name = reader["Name"].ToString() 
                        });
                    }
                }
            }
            comboColumn.DisplayMember = "Name";
            comboColumn.ValueMember = "Id";
        }

        private void LoadCategoriesForItems()
        {
            var comboColumn = (DataGridViewComboBoxColumn)dgvItems.Columns["Category"];
            comboColumn.Items.Add("");
            
            using (var conn = new SqlConnection(_connectionString))
            {
                conn.Open();
                using (var cmd = new SqlCommand("SELECT Id, Name FROM Categories", conn))
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        comboColumn.Items.Add(new ComboItem 
                        { 
                            Id = (int)reader["Id"], 
                            Name = reader["Name"].ToString() 
                        });
                    }
                }
            }
            comboColumn.DisplayMember = "Name";
            comboColumn.ValueMember = "Id";
        }

        private void LoadEmployeesForRequests()
        {
            var comboColumn = (DataGridViewComboBoxColumn)dgvRequests.Columns["RequestedBy"];
            comboColumn.Items.Add("");
            
            using (var conn = new SqlConnection(_connectionString))
            {
                conn.Open();
                using (var cmd = new SqlCommand("SELECT Id, FirstName + ' ' + LastName AS Name FROM Employee", conn))
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        comboColumn.Items.Add(new ComboItem 
                        { 
                            Id = (int)reader["Id"], 
                            Name = reader["Name"].ToString() 
                        });
                    }
                }
            }
            comboColumn.DisplayMember = "Name";
            comboColumn.ValueMember = "Id";
        }

        private void LoadBranchesForRequests()
        {
            var comboColumn = (DataGridViewComboBoxColumn)dgvRequests.Columns["Branch"];
            comboColumn.Items.Add("");
            
            using (var conn = new SqlConnection(_connectionString))
            {
                conn.Open();
                using (var cmd = new SqlCommand("SELECT Id, Name FROM Branch", conn))
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        comboColumn.Items.Add(new ComboItem 
                        { 
                            Id = (int)reader["Id"], 
                            Name = reader["Name"].ToString() 
                        });
                    }
                }
            }
            comboColumn.DisplayMember = "Name";
            comboColumn.ValueMember = "Id";
        }

        #endregion

        private void BtnSubmitAll_Click(object sender, EventArgs e)
        {
            try
            {
                using (var conn = new SqlConnection(_connectionString))
                {
                    conn.Open();
                    using (var transaction = conn.BeginTransaction())
                    {
                        try
                        {
                            // Save all departments
                            SaveDepartments(conn, transaction);

                            // Save all branches
                            SaveBranches(conn, transaction);

                            // Save all employees
                            SaveEmployees(conn, transaction);

                            // Save all items
                            SaveItems(conn, transaction);

                            // Save all requests
                            SaveRequests(conn, transaction);

                            transaction.Commit();
                            MessageBox.Show("All master data saved successfully!", "Success", 
                                MessageBoxButtons.OK, MessageBoxIcon.Information);
                            this.DialogResult = DialogResult.OK;
                            this.Close();
                        }
                        catch (Exception ex)
                        {
                            transaction.Rollback();
                            throw new Exception("Transaction failed: " + ex.Message, ex);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error saving data: {ex.Message}", "Error", 
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void SaveDepartments(SqlConnection conn, SqlTransaction transaction)
        {
            foreach (DataGridViewRow row in dgvDepartments.Rows)
            {
                if (row.IsNewRow) continue;

                var name = row.Cells["Name"].Value?.ToString();
                if (string.IsNullOrWhiteSpace(name)) continue;

                var description = row.Cells["Description"].Value?.ToString();
                var companyObj = row.Cells["Company"].Value;
                int? companyId = null;

                if (companyObj is ComboItem comboItem && comboItem.Id > 0)
                {
                    companyId = comboItem.Id;
                }

                var sql = @"INSERT INTO Department (Name, Description, ComId) 
                           VALUES (@Name, @Description, @ComId)";

                using (var cmd = new SqlCommand(sql, conn, transaction))
                {
                    cmd.Parameters.AddWithValue("@Name", name);
                    cmd.Parameters.AddWithValue("@Description", (object)description ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@ComId", (object)companyId ?? DBNull.Value);
                    cmd.ExecuteNonQuery();
                }
            }
        }

        private void SaveBranches(SqlConnection conn, SqlTransaction transaction)
        {
            foreach (DataGridViewRow row in dgvBranches.Rows)
            {
                if (row.IsNewRow) continue;

                var name = row.Cells["Name"].Value?.ToString();
                if (string.IsNullOrWhiteSpace(name)) continue;

                var description = row.Cells["Description"].Value?.ToString();
                
                // Get company
                var companyObj = row.Cells["Company"].Value;
                int? companyId = null;
                if (companyObj is ComboItem companyItem && companyItem.Id > 0)
                    companyId = companyItem.Id;

                // Get department
                var deptObj = row.Cells["Department"].Value;
                int? deptId = null;
                if (deptObj is ComboItem deptItem && deptItem.Id > 0)
                    deptId = deptItem.Id;

                // Get category flags
                bool isFactory = row.Cells["IsFactory"].Value != null && (bool)row.Cells["IsFactory"].Value;
                bool isDepot = row.Cells["IsDepot"].Value != null && (bool)row.Cells["IsDepot"].Value;
                bool isCenter = row.Cells["IsCenter"].Value != null && (bool)row.Cells["IsCenter"].Value;
                bool isDistributor = row.Cells["IsDistributor"].Value != null && (bool)row.Cells["IsDistributor"].Value;
                string centerRegion = row.Cells["CenterRegion"].Value?.ToString();

                var sql = @"INSERT INTO Branch 
                           (Name, Description, ComId, DeptId, IsFactory, IsDepot, IsCenter, CenterRegion, IsDistributor) 
                           VALUES 
                           (@Name, @Description, @ComId, @DeptId, @IsFactory, @IsDepot, @IsCenter, @CenterRegion, @IsDistributor)";

                using (var cmd = new SqlCommand(sql, conn, transaction))
                {
                    cmd.Parameters.AddWithValue("@Name", name);
                    cmd.Parameters.AddWithValue("@Description", (object)description ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@ComId", (object)companyId ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@DeptId", (object)deptId ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@IsFactory", isFactory);
                    cmd.Parameters.AddWithValue("@IsDepot", isDepot);
                    cmd.Parameters.AddWithValue("@IsCenter", isCenter);
                    cmd.Parameters.AddWithValue("@CenterRegion", (object)centerRegion ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@IsDistributor", isDistributor);
                    cmd.ExecuteNonQuery();
                }
            }
        }

        private void SaveEmployees(SqlConnection conn, SqlTransaction transaction)
        {
            foreach (DataGridViewRow row in dgvEmployees.Rows)
            {
                if (row.IsNewRow) continue;

                var firstName = row.Cells["FirstName"].Value?.ToString();
                var lastName = row.Cells["LastName"].Value?.ToString();
                if (string.IsNullOrWhiteSpace(firstName) || string.IsNullOrWhiteSpace(lastName)) continue;

                var email = row.Cells["Email"].Value?.ToString();
                var phone = row.Cells["Phone"].Value?.ToString();

                // Get department
                var deptObj = row.Cells["Department"].Value;
                int? deptId = null;
                if (deptObj is ComboItem deptItem && deptItem.Id > 0)
                    deptId = deptItem.Id;

                // Get branch
                var branchObj = row.Cells["Branch"].Value;
                int? branchId = null;
                if (branchObj is ComboItem branchItem && branchItem.Id > 0)
                    branchId = branchItem.Id;

                var sql = @"INSERT INTO Employee 
                           (FirstName, LastName, Email, Phone, DeptId, BranchId) 
                           VALUES 
                           (@FirstName, @LastName, @Email, @Phone, @DeptId, @BranchId)";

                using (var cmd = new SqlCommand(sql, conn, transaction))
                {
                    cmd.Parameters.AddWithValue("@FirstName", firstName);
                    cmd.Parameters.AddWithValue("@LastName", lastName);
                    cmd.Parameters.AddWithValue("@Email", (object)email ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@Phone", (object)phone ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@DeptId", (object)deptId ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@BranchId", (object)branchId ?? DBNull.Value);
                    cmd.ExecuteNonQuery();
                }
            }
        }

        private void SaveItems(SqlConnection conn, SqlTransaction transaction)
        {
            foreach (DataGridViewRow row in dgvItems.Rows)
            {
                if (row.IsNewRow) continue;

                var name = row.Cells["Name"].Value?.ToString();
                if (string.IsNullOrWhiteSpace(name)) continue;

                var code = row.Cells["Code"].Value?.ToString();
                var description = row.Cells["Description"].Value?.ToString();
                var unitPriceStr = row.Cells["UnitPrice"].Value?.ToString();
                
                decimal? unitPrice = null;
                if (decimal.TryParse(unitPriceStr, out var price))
                    unitPrice = price;

                // Get category
                var categoryObj = row.Cells["Category"].Value;
                int? categoryId = null;
                if (categoryObj is ComboItem categoryItem && categoryItem.Id > 0)
                    categoryId = categoryItem.Id;

                var sql = @"INSERT INTO Items 
                           (Name, Code, Description, UnitPrice, CategoryId) 
                           VALUES 
                           (@Name, @Code, @Description, @UnitPrice, @CategoryId)";

                using (var cmd = new SqlCommand(sql, conn, transaction))
                {
                    cmd.Parameters.AddWithValue("@Name", name);
                    cmd.Parameters.AddWithValue("@Code", (object)code ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@Description", (object)description ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@UnitPrice", (object)unitPrice ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@CategoryId", (object)categoryId ?? DBNull.Value);
                    cmd.ExecuteNonQuery();
                }
            }
        }

        private void SaveRequests(SqlConnection conn, SqlTransaction transaction)
        {
            foreach (DataGridViewRow row in dgvRequests.Rows)
            {
                if (row.IsNewRow) continue;

                var requestNumber = row.Cells["RequestNumber"].Value?.ToString();
                if (string.IsNullOrWhiteSpace(requestNumber)) continue;

                var description = row.Cells["Description"].Value?.ToString();
                var requestDateStr = row.Cells["RequestDate"].Value?.ToString();

                DateTime requestDate = DateTime.Now;
                if (DateTime.TryParse(requestDateStr, out var parsedDate))
                    requestDate = parsedDate;

                // Get employee
                var empObj = row.Cells["RequestedBy"].Value;
                int? empId = null;
                if (empObj is ComboItem empItem && empItem.Id > 0)
                    empId = empItem.Id;

                // Get branch
                var branchObj = row.Cells["Branch"].Value;
                int? branchId = null;
                if (branchObj is ComboItem branchItem && branchItem.Id > 0)
                    branchId = branchItem.Id;

                var sql = @"INSERT INTO Requests 
                           (RequestNumber, Description, RequestedBy, BranchId, RequestDate) 
                           VALUES 
                           (@RequestNumber, @Description, @RequestedBy, @BranchId, @RequestDate)";

                using (var cmd = new SqlCommand(sql, conn, transaction))
                {
                    cmd.Parameters.AddWithValue("@RequestNumber", requestNumber);
                    cmd.Parameters.AddWithValue("@Description", (object)description ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@RequestedBy", (object)empId ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@BranchId", (object)branchId ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@RequestDate", requestDate);
                    cmd.ExecuteNonQuery();
                }
            }
        }

        // Helper class for ComboBox items
        private class ComboItem
        {
            public int Id { get; set; }
            public string Name { get; set; }

            public override string ToString() => Name;
        }
    }
}
