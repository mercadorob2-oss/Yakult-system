using Yakult.Inventory.App.Session;
using Yakult.Inventory.App.Components;
using Yakult.Inventory.App.Repositories;
using System;
using System.Collections.Generic;
using System.Windows.Forms;
using System.Drawing;
using System.Threading.Tasks;

namespace Yakult.Inventory.App.Pages.Branch
{
    public partial class AddBranchPage_New : UserControl
    {
        private Panel _headerPanel;
        private DynamicSectionControl _dynamicSections;
        private Label _titleLabel;
        private BranchRepository _branchRepository;

        public event Action<List<BranchDto>> Submitted;

        public AddBranchPage_New()
        {
            InitializeComponent();
            _branchRepository = new BranchRepository();
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
                Text = "Add New Branches",
                Font = new Font("Segoe UI", 14F, FontStyle.Bold),
                ForeColor = ColorTranslator.FromHtml("#333333"),
                AutoSize = true,
                Dock = DockStyle.Top
            };
            
            var subtitleLabel = new Label
            {
                Text = "Add one or multiple branches at once",
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
                AddButtonText = "+ Add Another Branch",
                SaveButtonText = "Save All Branches"
            };
            _dynamicSections.CreateSection = CreateBranchFields;
            _dynamicSections.SaveAllRequested += OnSaveAllBranches;
            _dynamicSections.Initialize(); // Add the first section
            
            Controls.Add(_dynamicSections);
        }

        private Panel CreateBranchFields(int sectionNumber)
        {
            var panel = new Panel
            {
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Padding = new Padding(0, 10, 0, 0)
            };

            int yPos = 0;

            // Branch Name
            var lblName = new Label
            {
                Text = "Branch Name *",
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

            // Date Created (RIGHT COLUMN)
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

            // Created By (RIGHT COLUMN)
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

            // Branch Categories Section
            var lblCategories = new Label
            {
                Text = "Branch Categories",
                Left = 0,
                Top = yPos,
                AutoSize = true,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold)
            };

            yPos += 25;

            // Factory Checkbox
            var chkFactory = new CheckBox
            {
                Name = $"chkFactory_{sectionNumber}",
                Text = "Factory",
                Left = 0,
                Top = yPos,
                AutoSize = true,
                Font = new Font("Segoe UI", 9F)
            };

            yPos += 30;

            // Depot Checkbox
            var chkDepot = new CheckBox
            {
                Name = $"chkDepot_{sectionNumber}",
                Text = "Depot",
                Left = 0,
                Top = yPos,
                AutoSize = true,
                Font = new Font("Segoe UI", 9F)
            };

            yPos += 30;

            // Distributor Checkbox
            var chkDistributor = new CheckBox
            {
                Name = $"chkDistributor_{sectionNumber}",
                Text = "Distributor",
                Left = 0,
                Top = yPos,
                AutoSize = true,
                Font = new Font("Segoe UI", 9F)
            };

            yPos += 30;

            // Center Checkbox
            var chkCenter = new CheckBox
            {
                Name = $"chkCenter_{sectionNumber}",
                Text = "Center",
                Left = 0,
                Top = yPos,
                AutoSize = true,
                Font = new Font("Segoe UI", 9F)
            };

            yPos += 30;

            // Center Region Label
            var lblRegion = new Label
            {
                Name = $"lblRegion_{sectionNumber}",
                Text = "Center Region:",
                Left = 20,
                Top = yPos,
                AutoSize = true,
                Font = new Font("Segoe UI", 9F),
                Enabled = false
            };

            // Center Region ComboBox
            var cmbRegion = new ComboBox
            {
                Name = $"cmbRegion_{sectionNumber}",
                Left = 120,
                Top = yPos - 3,
                Width = 200,
                DropDownStyle = ComboBoxStyle.DropDownList,
                Enabled = false
            };
            cmbRegion.Items.AddRange(new object[] {
                "-- Select Region --",
                "NCR",
                "CAR",
                "Region I",
                "Region II",
                "Region III",
                "Region IV-A",
                "Region IV-B",
                "Region V",
                "Region VI",
                "Region VII",
                "Region VIII",
                "Region IX",
                "Region X",
                "Region XI",
                "Region XII",
                "Region XIII",
                "BARMM"
            });
            cmbRegion.SelectedIndex = 0;

            // Enable/disable region dropdown based on Center checkbox
            chkCenter.CheckedChanged += (s, e) =>
            {
                lblRegion.Enabled = chkCenter.Checked;
                cmbRegion.Enabled = chkCenter.Checked;
                if (!chkCenter.Checked)
                    cmbRegion.SelectedIndex = 0;
            };

            yPos += 40;

            panel.Controls.AddRange(new Control[] {
                lblName, txtName, lblDesc, txtDesc,
                lblCreated, dtCreated, lblBy, txtCreatedBy,
                lblCategories, chkFactory, chkDepot, chkDistributor, chkCenter,
                lblRegion, cmbRegion
            });

            panel.Height = yPos;

            return panel;
        }

        private async void OnSaveAllBranches(object sender, SaveAllEventArgs e)
        {
            var branches = new List<BranchDto>();
            var errorMessages = new List<string>();
            int sectionNum = 1;

            foreach (var sectionPanel in e.Sections)
            {
                var sectionNumber = (int)sectionPanel.Tag;
                
                var txtName = FindControl<TextBox>(sectionPanel, $"txtName_{sectionNumber}");
                var txtDesc = FindControl<TextBox>(sectionPanel, $"txtDesc_{sectionNumber}");
                var dtCreated = FindControl<DateTimePicker>(sectionPanel, $"dtCreated_{sectionNumber}");
                var chkFactory = FindControl<CheckBox>(sectionPanel, $"chkFactory_{sectionNumber}");
                var chkDepot = FindControl<CheckBox>(sectionPanel, $"chkDepot_{sectionNumber}");
                var chkDistributor = FindControl<CheckBox>(sectionPanel, $"chkDistributor_{sectionNumber}");
                var chkCenter = FindControl<CheckBox>(sectionPanel, $"chkCenter_{sectionNumber}");
                var cmbRegion = FindControl<ComboBox>(sectionPanel, $"cmbRegion_{sectionNumber}");

                bool isCurrentSectionValid = true;

                // Validate THIS section only
                if (string.IsNullOrWhiteSpace(txtName?.Text))
                {
                    errorMessages.Add($"Section {sectionNum}: Branch Name is required");
                    isCurrentSectionValid = false;
                }

                // Validate Center region if Center is checked
                if (chkCenter?.Checked == true && (cmbRegion?.SelectedIndex ?? 0) == 0)
                {
                    errorMessages.Add($"Section {sectionNum}: Please select a Center Region when Center is checked");
                    isCurrentSectionValid = false;
                }

                // Only add THIS section if it's valid
                if (isCurrentSectionValid)
                {
                    var branch = new BranchDto
                    {
                        Name = txtName.Text.Trim(),
                        Description = txtDesc?.Text?.Trim(),
                        DateCreated = dtCreated?.Value ?? DateTime.Now,
                        CreatedByUserId = AppSession.CurrentUserId,
                        CreatedByName = AppSession.CurrentUserName,
                        IsFactory = chkFactory?.Checked ?? false,
                        IsDepot = chkDepot?.Checked ?? false,
                        IsDistributor = chkDistributor?.Checked ?? false,
                        IsCenter = chkCenter?.Checked ?? false,
                        CenterRegion = (chkCenter?.Checked == true && cmbRegion?.SelectedIndex > 0) 
                            ? cmbRegion.SelectedItem.ToString() 
                            : null
                    };

                    branches.Add(branch);
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
                foreach (var branch in branches)
                {
                    await _branchRepository.CreateAsync(branch);
                }

                MessageBox.Show($"Successfully added {branches.Count} branch(es)!",
                    "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);

                _dynamicSections.ClearAllSections();
                Submitted?.Invoke(branches);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to save branches: {ex.Message}",
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
