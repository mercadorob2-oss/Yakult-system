// using System;
// using System.Drawing;
// using System.Windows.Forms;
// using Yakult.Inventory.App.Models;
// 
// namespace Yakult.Inventory.App.Forms.Reports
// {
//     public class ReportDetailsForm : Form
//     {
//         private ReportListItem report;
//         private Label lblTitle;
//         private TextBox txtTitle;
//         private Label lblDescription;
//         private TextBox txtDescription;
//         private Label lblSubmittedBy;
//         private TextBox txtSubmittedBy;
//         private Label lblSubmittedDate;
//         private TextBox txtSubmittedDate;
//         private Label lblPriority;
//         private TextBox txtPriority;
//         private Label lblCategory;
//         private TextBox txtCategory;
//         private Label lblStatus;
//         private ComboBox cboStatus;
//         private Button btnSave;
//         private Button btnCancel;
// 
//         public ReportDetailsForm(ReportListItem reportItem)
//         {
//             this.report = reportItem ?? throw new ArgumentNullException(nameof(reportItem));
//             InitializeComponent();
//             LoadReportData();
//         }
// 
//         private void InitializeComponent()
//         {
//             this.Text = "Report Details";
//             this.Size = new Size(600, 500);
//             this.StartPosition = FormStartPosition.CenterParent;
//             this.MaximizeBox = false;
//             this.MinimizeBox = false;
//             this.FormBorderStyle = FormBorderStyle.FixedDialog;
// 
//             var mainPanel = new Panel
//             {
//                 Dock = DockStyle.Fill,
//                 Padding = new Padding(20),
//                 BackColor = Color.White
//             };
//             this.Controls.Add(mainPanel);
// 
//             var scrollPanel = new Panel
//             {
//                 Dock = DockStyle.Fill,
//                 AutoScroll = true,
//                 BackColor = Color.White
//             };
//             mainPanel.Controls.Add(scrollPanel);
// 
//             int yPos = 10;
//             const int labelWidth = 120;
//             const int controlHeight = 30;
//             const int spacing = 15;
// 
//             // Title
//             lblTitle = new Label { Text = "Title:", Location = new Point(10, yPos), Size = new Size(labelWidth, controlHeight) };
//             scrollPanel.Controls.Add(lblTitle);
//             txtTitle = new TextBox 
//             { 
//                 Location = new Point(140, yPos), 
//                 Size = new Size(400, controlHeight),
//                 ReadOnly = true,
//                 BackColor = Color.WhiteSmoke
//             };
//             scrollPanel.Controls.Add(txtTitle);
//             yPos += controlHeight + spacing;
// 
//             // Description
//             lblDescription = new Label { Text = "Description:", Location = new Point(10, yPos), Size = new Size(labelWidth, controlHeight) };
//             scrollPanel.Controls.Add(lblDescription);
//             txtDescription = new TextBox 
//             { 
//                 Location = new Point(140, yPos), 
//                 Size = new Size(400, 80),
//                 Multiline = true,
//                 ReadOnly = true,
//                 BackColor = Color.WhiteSmoke
//             };
//             scrollPanel.Controls.Add(txtDescription);
//             yPos += 95 + spacing;
// 
//             // Submitted By
//             lblSubmittedBy = new Label { Text = "Submitted By:", Location = new Point(10, yPos), Size = new Size(labelWidth, controlHeight) };
//             scrollPanel.Controls.Add(lblSubmittedBy);
//             txtSubmittedBy = new TextBox 
//             { 
//                 Location = new Point(140, yPos), 
//                 Size = new Size(400, controlHeight),
//                 ReadOnly = true,
//                 BackColor = Color.WhiteSmoke
//             };
//             scrollPanel.Controls.Add(txtSubmittedBy);
//             yPos += controlHeight + spacing;
// 
//             // Submitted Date
//             lblSubmittedDate = new Label { Text = "Submitted Date:", Location = new Point(10, yPos), Size = new Size(labelWidth, controlHeight) };
//             scrollPanel.Controls.Add(lblSubmittedDate);
//             txtSubmittedDate = new TextBox 
//             { 
//                 Location = new Point(140, yPos), 
//                 Size = new Size(400, controlHeight),
//                 ReadOnly = true,
//                 BackColor = Color.WhiteSmoke
//             };
//             scrollPanel.Controls.Add(txtSubmittedDate);
//             yPos += controlHeight + spacing;
// 
//             // Priority
//             lblPriority = new Label { Text = "Priority:", Location = new Point(10, yPos), Size = new Size(labelWidth, controlHeight) };
//             scrollPanel.Controls.Add(lblPriority);
//             txtPriority = new TextBox 
//             { 
//                 Location = new Point(140, yPos), 
//                 Size = new Size(400, controlHeight),
//                 ReadOnly = true,
//                 BackColor = Color.WhiteSmoke
//             };
//             scrollPanel.Controls.Add(txtPriority);
//             yPos += controlHeight + spacing;
// 
//             // Category
//             lblCategory = new Label { Text = "Category:", Location = new Point(10, yPos), Size = new Size(labelWidth, controlHeight) };
//             scrollPanel.Controls.Add(lblCategory);
//             txtCategory = new TextBox 
//             { 
//                 Location = new Point(140, yPos), 
//                 Size = new Size(400, controlHeight),
//                 ReadOnly = true,
//                 BackColor = Color.WhiteSmoke
//             };
//             scrollPanel.Controls.Add(txtCategory);
//             yPos += controlHeight + spacing;
// 
//             // Status (Editable)
//             lblStatus = new Label { Text = "Status:", Location = new Point(10, yPos), Size = new Size(labelWidth, controlHeight), ForeColor = Color.FromArgb(0, 150, 136), Font = new Font("Segoe UI", 10F, FontStyle.Bold) };
//             scrollPanel.Controls.Add(lblStatus);
//             cboStatus = new ComboBox 
//             { 
//                 Location = new Point(140, yPos), 
//                 Size = new Size(400, controlHeight),
//                 DropDownStyle = ComboBoxStyle.DropDownList,
//                 Items = { "Open", "In Progress", "Resolved", "Closed" }
//             };
//             scrollPanel.Controls.Add(cboStatus);
//             yPos += controlHeight + spacing + 20;
// 
//             // Buttons Panel
//             var buttonPanel = new Panel
//             {
//                 Dock = DockStyle.Bottom,
//                 Height = 50,
//                 BackColor = Color.FromArgb(245, 247, 250)
//             };
//             mainPanel.Controls.Add(buttonPanel);
// 
//             btnSave = new Button
//             {
//                 Text = "? Save Changes",
//                 Location = new Point(10, 10),
//                 Size = new Size(120, 35),
//                 BackColor = Color.FromArgb(0, 150, 136),
//                 ForeColor = Color.White,
//                 FlatStyle = FlatStyle.Flat,
//                 Cursor = Cursors.Hand
//             };
//             btnSave.FlatAppearance.BorderSize = 0;
//             btnSave.Click += (s, e) => SaveChanges();
//             buttonPanel.Controls.Add(btnSave);
// 
//             btnCancel = new Button
//             {
//                 Text = "? Cancel",
//                 Location = new Point(140, 10),
//                 Size = new Size(100, 35),
//                 BackColor = Color.FromArgb(220, 220, 220),
//                 ForeColor = Color.Black,
//                 FlatStyle = FlatStyle.Flat,
//                 Cursor = Cursors.Hand
//             };
//             btnCancel.FlatAppearance.BorderSize = 0;
//             btnCancel.Click += (s, e) => this.Close();
//             buttonPanel.Controls.Add(btnCancel);
//         }
// 
//         private void LoadReportData()
//         {
//             if (report == null) return;
// 
//             txtTitle.Text = report.Title ?? "";
//             txtDescription.Text = report.Description ?? "";
//             txtSubmittedBy.Text = report.SubmittedBy ?? "";
//             txtSubmittedDate.Text = report.SubmittedDate.ToString("yyyy-MM-dd HH:mm");
//             txtPriority.Text = report.Priority ?? "";
//             txtCategory.Text = report.Category ?? "";
//             cboStatus.SelectedItem = report.Status ?? "Open";
//             this.Text = $"Report Details - {report.Title}";
//         }
// 
//         private void SaveChanges()
//         {
//             if (cboStatus.SelectedItem != null)
//             {
//                 report.Status = cboStatus.SelectedItem.ToString();
//                 MessageBox.Show("Report status updated successfully!", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
//                 this.DialogResult = DialogResult.OK;
//                 this.Close();
//             }
//         }
// 
//         public ReportListItem GetUpdatedReport()
//         {
//             return report;
//         }
//     }
// }
