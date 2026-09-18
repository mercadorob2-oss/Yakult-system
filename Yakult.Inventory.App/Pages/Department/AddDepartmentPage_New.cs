using Yakult.Inventory.App.Session;
using Yakult.Inventory.App.Components;
using Yakult.Inventory.App.Repositories;
using System;
using System.Collections.Generic;
using System.Windows.Forms;
using System.Drawing;
using System.Threading.Tasks;

namespace Yakult.Inventory.App.Pages.Department
{
    public partial class AddDepartmentPage_New : UserControl
    {
        private Panel _headerPanel;
        private DynamicSectionControl _dynamicSections;
        private Label _titleLabel;
        private DepartmentRepository _departmentRepository;

        public event Action<List<DepartmentDto>> Submitted;

        public AddDepartmentPage_New()
        {
            InitializeComponent();
            _departmentRepository = new DepartmentRepository();
            BuildUi();
        }

        private void BuildUi()
        {
            Dock = DockStyle.Fill;
            AutoScroll = false;

            // Header
            _headerPanel = new Panel { Dock = DockStyle.Top, Height = 100, Padding = new Padding(20) };
            _titleLabel = new Label
            {
                Text = "Add New Departments",
                Font = new Font("Segoe UI", 14F, FontStyle.Bold),
                ForeColor = ColorTranslator.FromHtml("#333333"),
                AutoSize = true,
                Dock = DockStyle.Top
            };
            
            var subtitleLabel = new Label
            {
                Text = "Add one or multiple departments at once",
                Font = new Font("Segoe UI", 9F),
                ForeColor = ColorTranslator.FromHtml("#666666"),
                AutoSize = true,
                Dock = DockStyle.Top,
                Padding = new Padding(0, 5, 0, 0)
            };

            _headerPanel.Controls.Add(subtitleLabel);
            _headerPanel.Controls.Add(_titleLabel);
            Controls.Add(_headerPanel);

            // Dynamic Sections
            _dynamicSections = new DynamicSectionControl
            {
                AddButtonText = "+ Add Another Department",
                SaveButtonText = "Save All Departments"
            };
            _dynamicSections.CreateSection = CreateDepartmentFields;
            _dynamicSections.SaveAllRequested += OnSaveAllDepartments;
            _dynamicSections.Initialize(); // Add the first section
            
            Controls.Add(_dynamicSections);
        }

        private Panel CreateDepartmentFields(int sectionNumber)
        {
            var panel = new Panel
            {
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Padding = new Padding(0, 10, 0, 0)
            };

            int yPos = 0;

            // Department Name
            var lblName = new Label
            {
                Text = "Department Name *",
                Left = 0,
                Top = yPos,
                AutoSize = true,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold)
            };
            
            var txtName = new TextBox
            {
                Name = $"txtName_{sectionNumber}",
                Left = 0,
                Top = yPos + 20,
                Width = 400
            };

            yPos += 60;

            // Description
            var lblDesc = new Label
            {
                Text = "Description",
                Left = 0,
                Top = yPos,
                AutoSize = true,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold)
            };
            
            var txtDesc = new TextBox
            {
                Name = $"txtDesc_{sectionNumber}",
                Left = 0,
                Top = yPos + 20,
                Width = 400,
                Multiline = true,
                Height = 60
            };

            yPos += 90;

            // Date Created
            var lblCreated = new Label
            {
                Text = "Date Created",
                Left = 450,
                Top = 0,
                AutoSize = true,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold)
            };
            
            var dtCreated = new DateTimePicker
            {
                Name = $"dtCreated_{sectionNumber}",
                Left = 450,
                Top = 20,
                Width = 180,
                Value = DateTime.Now
            };

            // Created By
            var lblBy = new Label
            {
                Text = "Created By",
                Left = 450,
                Top = 60,
                AutoSize = true,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold)
            };
            
            var txtCreatedBy = new TextBox
            {
                Name = $"txtCreatedBy_{sectionNumber}",
                Left = 450,
                Top = 80,
                Width = 180,
                ReadOnly = true,
                BackColor = SystemColors.Window,
                Text = AppSession.CurrentUserName ?? string.Empty
            };

            panel.Controls.AddRange(new Control[] {
                lblName, txtName, lblDesc, txtDesc,
                lblCreated, dtCreated, lblBy, txtCreatedBy
            });

            panel.Height = yPos;

            return panel;
        }

        private async void OnSaveAllDepartments(object sender, SaveAllEventArgs e)
        {
            var departments = new List<DepartmentDto>();
            var errorMessages = new List<string>();
            int sectionNum = 1;

            foreach (var sectionPanel in e.Sections)
            {
                var sectionNumber = (int)sectionPanel.Tag;
                
                var txtName = FindControl<TextBox>(sectionPanel, $"txtName_{sectionNumber}");
                var txtDesc = FindControl<TextBox>(sectionPanel, $"txtDesc_{sectionNumber}");
                var dtCreated = FindControl<DateTimePicker>(sectionPanel, $"dtCreated_{sectionNumber}");

                bool isCurrentSectionValid = true;

                // Validate THIS section only
                if (string.IsNullOrWhiteSpace(txtName?.Text))
                {
                    errorMessages.Add($"Section {sectionNum}: Department Name is required");
                    isCurrentSectionValid = false;
                }

                // Only add THIS section if it's valid
                if (isCurrentSectionValid)
                {
                    var dept = new DepartmentDto
                    {
                        Name = txtName.Text.Trim(),
                        Description = txtDesc?.Text?.Trim(),
                        DateCreated = dtCreated?.Value ?? DateTime.Now,
                        CreatedByUserId = AppSession.CurrentUserId,
                        CreatedByName = AppSession.CurrentUserName
                    };

                    departments.Add(dept);
                }

                sectionNum++;
            }

            if (errorMessages.Count > 0)
            {
                MessageBox.Show(string.Join("\n", errorMessages), "Validation Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            try
            {
                foreach (var dept in departments)
                {
                    await _departmentRepository.CreateAsync(dept);
                }

                MessageBox.Show($"Successfully added {departments.Count} department(s)!",
                    "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);

                _dynamicSections.ClearAllSections();
                Submitted?.Invoke(departments);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to save departments: {ex.Message}",
                    "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private T FindControl<T>(Control parent, string name) where T : Control
        {
            foreach (Control control in parent.Controls)
            {
                if (control is T && control.Name == name)
                    return (T)control;
                
                var found = FindControl<T>(control, name);
                if (found != null)
                    return found;
            }
            return null;
        }
    }
}
