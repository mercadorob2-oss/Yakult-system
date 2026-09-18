using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace Yakult.Inventory.App.Components
{
    public class DynamicSectionControl : UserControl
    {
        private Panel sectionsContainer;
        private Button btnAddSection;
        private Button btnSaveAll;
        private List<Panel> sections = new List<Panel>();

        public delegate Panel CreateSectionDelegate(int sectionNumber);
        public CreateSectionDelegate CreateSection { get; set; }

        public event EventHandler<SaveAllEventArgs> SaveAllRequested;

        public string AddButtonText
        {
            get => btnAddSection.Text;
            set => btnAddSection.Text = value;
        }

        public string SaveButtonText
        {
            get => btnSaveAll.Text;
            set => btnSaveAll.Text = value;
        }

        public DynamicSectionControl()
        {
            InitializeComponent();
            // Don't add section here - let it be added after CreateSection is set
        }

        // Call this after setting CreateSection delegate
        public void Initialize()
        {
            if (sections.Count == 0)
            {
                AddNewSection();
            }
        }

        private void InitializeComponent()
        {
            this.AutoScroll = true;
            this.Dock = DockStyle.Fill;
            this.Padding = new Padding(20);

            // Sections Container
            sectionsContainer = new Panel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Padding = new Padding(0, 80, 0, 15)  // Added top padding to prevent cutoff
            };

            // Add Section Button
            btnAddSection = new Button
            {
                Text = "+ Add New Section",
                Height = 35,
                Width = 180,
                BackColor = ColorTranslator.FromHtml("#4CAF50"),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                Cursor = Cursors.Hand,
                Dock = DockStyle.Top,
                Margin = new Padding(0, 10, 0, 0)  // Top margin for spacing
            };
            btnAddSection.FlatAppearance.BorderSize = 0;
            btnAddSection.Click += (s, e) => AddNewSection();

            // Save All Button
            btnSaveAll = new Button
            {
                Text = "Save All",
                Height = 40,
                BackColor = ColorTranslator.FromHtml("#2196F3"),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                Cursor = Cursors.Hand,
                Dock = DockStyle.Top,
                Margin = new Padding(0, 10, 0, 0),  // Top margin for spacing
                Visible = true
            };
            btnSaveAll.FlatAppearance.BorderSize = 0;
            btnSaveAll.Click += (s, e) => OnSaveAll();

            this.Controls.Add(btnSaveAll);
            this.Controls.Add(btnAddSection);
            this.Controls.Add(sectionsContainer);
        }

        private void AddNewSection()
        {
            if (CreateSection == null)
            {
                MessageBox.Show("Section creator not configured.", "Error", 
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            // Use current section count + 1 for the number (always sequential)
            int currentSectionNumber = sections.Count + 1;

            // Create section container
            var sectionPanel = new Panel
            {
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Dock = DockStyle.Top,
                BorderStyle = BorderStyle.FixedSingle,
                BackColor = Color.White,
                Padding = new Padding(15),
                Margin = new Padding(0, 0, 0, 20)  // Increased bottom margin for spacing
            };

            // Header panel
            var headerPanel = new Panel
            {
                Height = 35,
                Dock = DockStyle.Top,
                Padding = new Padding(0, 0, 0, 10)
            };

            var sectionLabel = new Label
            {
                Text = $"Section {currentSectionNumber}",
                Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                ForeColor = ColorTranslator.FromHtml("#333333"),
                AutoSize = true,
                Dock = DockStyle.Left
            };

            var btnRemove = new Button
            {
                Text = "✕ Remove",
                Height = 28,
                Width = 90,
                BackColor = ColorTranslator.FromHtml("#F44336"),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 8F, FontStyle.Bold),
                Cursor = Cursors.Hand,
                Dock = DockStyle.Right
            };
            btnRemove.FlatAppearance.BorderSize = 0;
            btnRemove.Click += (s, e) => RemoveSection(sectionPanel);

            headerPanel.Controls.Add(btnRemove);
            headerPanel.Controls.Add(sectionLabel);

            // Separator
            var separator = new Panel
            {
                Height = 1,
                Dock = DockStyle.Top,
                BackColor = ColorTranslator.FromHtml("#E0E0E0"),
                Margin = new Padding(0, 0, 0, 15)
            };

            // Content panel
            var contentPanel = CreateSection(currentSectionNumber);
            contentPanel.Dock = DockStyle.Top;
            contentPanel.Tag = currentSectionNumber; // Store section number

            sectionPanel.Controls.Add(contentPanel);
            sectionPanel.Controls.Add(separator);
            sectionPanel.Controls.Add(headerPanel);

            sectionsContainer.Controls.Add(sectionPanel);
            sectionsContainer.Controls.SetChildIndex(sectionPanel, 0);
            
            sections.Add(sectionPanel);

            btnSaveAll.Visible = sections.Count > 0;
        }

        private void RemoveSection(Panel sectionPanel)
        {
            if (sections.Count <= 1)
            {
                MessageBox.Show("You must have at least one section.", "Notice", 
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            sectionsContainer.Controls.Remove(sectionPanel);
            sections.Remove(sectionPanel);
            sectionPanel.Dispose();

            UpdateSectionNumbers();
            btnSaveAll.Visible = sections.Count > 0;
        }

        private void UpdateSectionNumbers()
        {
            // Renumber all sections based on their current position
            int index = 1;
            foreach (var section in sections.AsEnumerable().Reverse())
            {
                var headerPanel = section.Controls.OfType<Panel>().FirstOrDefault();
                if (headerPanel != null)
                {
                    var label = headerPanel.Controls.OfType<Label>().FirstOrDefault();
                    if (label != null)
                    {
                        label.Text = $"Section {index}";
                    }
                }
                
                // Update the content panel's tag as well
                var contentPanel = section.Controls.OfType<Panel>()
                    .FirstOrDefault(p => p.Tag is int);
                if (contentPanel != null)
                {
                    contentPanel.Tag = index;
                }
                
                index++;
            }
        }

        private void OnSaveAll()
        {
            var sectionContents = new List<Panel>();
            
            foreach (var section in sections.AsEnumerable().Reverse())
            {
                // Get the content panel (last control added, which is the actual content)
                var contentPanel = section.Controls.OfType<Panel>()
                    .FirstOrDefault(p => p.Tag is int);
                
                if (contentPanel != null)
                {
                    sectionContents.Add(contentPanel);
                }
            }

            SaveAllRequested?.Invoke(this, new SaveAllEventArgs { Sections = sectionContents });
        }

        public void ClearAllSections()
        {
            while (sections.Count > 0)
            {
                var section = sections[0];
                sectionsContainer.Controls.Remove(section);
                sections.RemoveAt(0);
                section.Dispose();
            }

            AddNewSection();
        }

        public List<Panel> GetAllSections()
        {
            var sectionContents = new List<Panel>();
            
            foreach (var section in sections.AsEnumerable().Reverse())
            {
                var contentPanel = section.Controls.OfType<Panel>()
                    .FirstOrDefault(p => p.Tag is int);
                
                if (contentPanel != null)
                {
                    sectionContents.Add(contentPanel);
                }
            }

            return sectionContents;
        }
    }

    public class SaveAllEventArgs : EventArgs
    {
        public List<Panel> Sections { get; set; }
    }
}
