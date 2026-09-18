using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using Microsoft.Reporting.WinForms;
using Yakult.Inventory.App.Dialogs;
using Yakult.Inventory.App.Pages;
using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;

namespace Yakult.Inventory.App.Pages.Export
{
    public class CombinedRdlcPreviewWindow : Form
    {
        private TabControl _tabControl;
        private Button _btnExportAll;
        // Keeps the ReportViewerForm alive after page.Tag is nulled by LoadTab
        private readonly Dictionary<TabPage, ReportViewerForm> _viewerForms =
            new Dictionary<TabPage, ReportViewerForm>();

        public CombinedRdlcPreviewWindow()
        {
            Text = "Preview Selected Reports";
            Width = 1320;
            Height = 900;
            WindowState = FormWindowState.Maximized;
            MinimumSize = new Size(960, 620);
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = Color.FromArgb(248, 249, 250);

            // Create a top panel for global actions
            var headerPanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 60,
                BackColor = Color.White,
                Padding = new Padding(20, 10, 20, 10)
            };
            
            headerPanel.Paint += (s, e) =>
            {
                using (var pen = new Pen(Color.FromArgb(226, 232, 240)))
                    e.Graphics.DrawLine(pen, 0, headerPanel.Height - 1, headerPanel.Width, headerPanel.Height - 1);
            };

            var lblTitle = new Label
            {
                Text = "Reports Preview",
                Font = new Font("Segoe UI", 12F, FontStyle.Bold),
                ForeColor = Color.FromArgb(30, 41, 59),
                AutoSize = true,
                Location = new Point(20, 18)
            };

            _btnExportAll = new Button
            {
                Text = "Export All",
                Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
                Size = new Size(160, 36),
                BackColor = Color.FromArgb(58, 134, 232),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand
            };
            _btnExportAll.FlatAppearance.BorderSize = 0;
            _btnExportAll.Click += BtnExportAll_Click;

            headerPanel.Layout += (s, e) =>
            {
                _btnExportAll.Location = new Point(headerPanel.Width - _btnExportAll.Width - 20, (headerPanel.Height - _btnExportAll.Height) / 2);
            };

            headerPanel.Controls.Add(lblTitle);
            headerPanel.Controls.Add(_btnExportAll);

            _tabControl = new TabControl
            {
                Dock = DockStyle.Fill,
                Font = new Font("Segoe UI", 9.5F),
                ItemSize = new Size(180, 32),
                SizeMode = TabSizeMode.Fixed,
                Padding = new Point(12, 6),
                DrawMode = TabDrawMode.OwnerDrawFixed // Required for drawing 'x'
            };

            _tabControl.SelectedIndexChanged += TabControl_SelectedIndexChanged;
            _tabControl.DrawItem += TabControl_DrawItem;
            _tabControl.MouseClick += TabControl_MouseClick;

            Controls.Add(_tabControl);
            Controls.Add(headerPanel);
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            if (_tabControl.TabCount > 0)
            {
                LoadTab(_tabControl.TabPages[0]);
            }
        }

        private void TabControl_DrawItem(object sender, DrawItemEventArgs e)
        {
            var tabRect = _tabControl.GetTabRect(e.Index);
            var page = _tabControl.TabPages[e.Index];

            bool isSelected = _tabControl.SelectedIndex == e.Index;
            Color bgColor = isSelected ? Color.White : Color.FromArgb(240, 240, 240);
            e.Graphics.FillRectangle(new SolidBrush(bgColor), tabRect);

            var textBrush = isSelected ? Brushes.Black : Brushes.DimGray;
            StringFormat sf = new StringFormat
            {
                Alignment = StringAlignment.Near,
                LineAlignment = StringAlignment.Center
            };
            
            var textRect = new Rectangle(tabRect.X + 8, tabRect.Y, tabRect.Width - 30, tabRect.Height);
            e.Graphics.DrawString(page.Text, e.Font, textBrush, textRect, sf);

            using (var p = new Pen(isSelected ? Color.Gray : Color.LightGray, 2))
            {
                int size = 10;
                int top = tabRect.Y + (tabRect.Height - size) / 2;
                int left = tabRect.Right - size - 12;
                
                e.Graphics.DrawLine(p, left, top, left + size, top + size);
                e.Graphics.DrawLine(p, left + size, top, left, top + size);
            }
        }

        private void TabControl_MouseClick(object sender, MouseEventArgs e)
        {
            for (int i = 0; i < _tabControl.TabCount; i++)
            {
                var tabRect = _tabControl.GetTabRect(i);
                int size = 10;
                int top = tabRect.Y + (tabRect.Height - size) / 2;
                int left = tabRect.Right - size - 12;
                
                Rectangle closeRect = new Rectangle(left - 4, top - 4, size + 8, size + 8);
                if (closeRect.Contains(e.Location))
                {
                    _tabControl.TabPages.RemoveAt(i);
                    break;
                }
            }
        }

        private void TabControl_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (_tabControl.SelectedTab != null)
            {
                LoadTab(_tabControl.SelectedTab);
            }
        }

        private void LoadTab(TabPage page)
        {
            if (page.Tag is ReportViewerForm viewerForm)
            {
                page.Tag = null; 

                viewerForm.InitializeReport();
                var viewer = viewerForm.GetViewer();
                if (viewer != null)
                {
                    viewer.Parent = page;
                    viewer.Dock = DockStyle.Fill;
                    viewer.ZoomMode = Microsoft.Reporting.WinForms.ZoomMode.Percent;
                    viewer.ZoomPercent = 100;
                }
            }
        }

        public void AddReportTab(string tabName, ReportViewerForm viewerForm)
        {
            if (viewerForm == null) return;
            var page = new TabPage(tabName);
            page.BackColor = Color.White;
            page.Tag = viewerForm;
            _viewerForms[page] = viewerForm;
            _tabControl.TabPages.Add(page);
        }

        private void BtnExportAll_Click(object sender, EventArgs e)
        {
            if (_tabControl.TabPages.Count == 0) return;

            var tabNames = _tabControl.TabPages.Cast<TabPage>().Select(p => p.Text).ToList();

            // Step 1 — choose which reports to export
            List<string> selectedNames;
            using (var dlg = new ExportSelectionDialog(tabNames))
            {
                if (dlg.ShowDialog(this) != DialogResult.OK) return;
                selectedNames = dlg.CheckedList.CheckedItems.Cast<string>().ToList();
            }

            if (selectedNames.Count == 0)
            {
                MessageBox.Show("No reports selected for export.", "Selection Empty",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            // Step 2 — save path + export
            // Signatories are pre-baked into each form (picked before preview opened).
            using (var sfd = new SaveFileDialog())
            {
                sfd.Filter   = "PDF Files (*.pdf)|*.pdf";
                sfd.Title    = selectedNames.Count > 1 ? "Save Combined Reports" : "Save Report";
                sfd.FileName = selectedNames.Count > 1 ? "Combined Reports.pdf" : $"{selectedNames[0]}.pdf";

                if (sfd.ShowDialog() == DialogResult.OK)
                    ExportSelectedTabsToPdf(sfd.FileName, selectedNames);
            }
        }

        private void ExportSelectedTabsToPdf(string outputPath, List<string> selectedTabNames)
        {
            Cursor = Cursors.WaitCursor;
            _btnExportAll.Enabled = false;

            try
            {
                using (var masterDoc = new PdfDocument())
                {
                    var summary = new System.Text.StringBuilder();
                    int currentPage = 1;

                    foreach (TabPage page in _tabControl.TabPages)
                    {
                        if (!selectedTabNames.Contains(page.Text)) continue;
                        if (!_viewerForms.TryGetValue(page, out var viewerForm)) continue;

                        // Signatories are pre-baked into each form's data sources.
                        byte[] pdfBytes = viewerForm.RenderToPdf();
                        using (var stream = new MemoryStream(pdfBytes))
                        using (var reportPdf = PdfReader.Open(stream, PdfDocumentOpenMode.Import))
                        {
                            int pageCount = reportPdf.PageCount;
                            int startPage = currentPage;
                            for (int i = 0; i < pageCount; i++)
                                masterDoc.AddPage(reportPdf.Pages[i]);

                            summary.AppendLine(pageCount == 1
                                ? $"  • {page.Text}: page {startPage}"
                                : $"  • {page.Text}: pages {startPage}–{startPage + pageCount - 1}  (signatory on page {startPage + pageCount - 1})");
                            currentPage += pageCount;
                        }
                    }

                    masterDoc.Save(outputPath);

                    MessageBox.Show(
                        $"Reports exported successfully!\n\nPage layout:\n{summary}",
                        "Export Complete", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
            catch (Exception ex)
            {
                var msg = ex.Message;
                var inner = ex.InnerException;
                while (inner != null)
                {
                    msg += "\n\n→ " + inner.Message;
                    inner = inner.InnerException;
                }
                MessageBox.Show($"An error occurred during export:\n{msg}",
                    "Export Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                Cursor = Cursors.Default;
                _btnExportAll.Enabled = true;
            }
        }
    }

    // ════════════════════════════════════════════════════════════════════════
    //  Signatory mode chooser — shown after report selection when > 1 report
    // ════════════════════════════════════════════════════════════════════════
    public class SignatoryModeDialog : Form
    {
        private static readonly Color Teal    = Color.FromArgb(58, 134, 232);
        private static readonly Color Dark    = Color.FromArgb(30, 41, 59);
        private static readonly Color Muted   = Color.FromArgb(100, 116, 139);
        private static readonly Color Border  = Color.FromArgb(226, 232, 240);

        /// <summary>True = user chose "Per Report"; false = "Same for All".</summary>
        public bool PerReport { get; private set; }

        public SignatoryModeDialog()
        {
            Text            = "Signatory Options";
            Size            = new Size(500, 310);
            MinimumSize     = new Size(460, 290);
            StartPosition   = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox     = false;
            MinimizeBox     = false;
            BackColor       = Color.White;

            // ── Header ────────────────────────────────────────────────────
            var header = new Panel { Dock = DockStyle.Top, Height = 56, BackColor = Dark };
            header.Controls.Add(new Label
            {
                Text      = "How would you like to assign signatories?",
                Font      = new Font("Segoe UI", 11F, FontStyle.Bold),
                ForeColor = Color.White,
                AutoSize  = true,
                Location  = new Point(16, 10)
            });
            header.Controls.Add(new Label
            {
                Text      = "Choose one option below, then fill in the signatory details.",
                Font      = new Font("Segoe UI", 8.5F),
                ForeColor = Color.FromArgb(148, 163, 184),
                AutoSize  = true,
                Location  = new Point(18, 34)
            });

            // ── Footer ────────────────────────────────────────────────────
            var footer = new Panel { Dock = DockStyle.Bottom, Height = 52, BackColor = Color.FromArgb(248, 250, 252) };
            footer.Paint += (s, e) =>
            {
                using (var pen = new Pen(Border))
                    e.Graphics.DrawLine(pen, 0, 0, footer.Width, 0);
            };
            var btnCancel = new Button
            {
                Text        = "Cancel",
                DialogResult = DialogResult.Cancel,
                Size        = new Size(88, 34),
                FlatStyle   = FlatStyle.Flat,
                BackColor   = Color.White,
                ForeColor   = Dark,
                Cursor      = Cursors.Hand
            };
            btnCancel.FlatAppearance.BorderColor = Border;
            footer.Controls.Add(btnCancel);
            footer.Layout += (s, e) =>
                btnCancel.Location = new Point(12, (footer.Height - btnCancel.Height) / 2);

            // ── Option cards ──────────────────────────────────────────────
            var body = new Panel { Dock = DockStyle.Fill, Padding = new Padding(20, 14, 20, 10) };

            var cardAll = MakeOptionCard(
                "Same for All Reports",
                "One set of signatories is applied to every selected report.",
                false);

            var cardPer = MakeOptionCard(
                "Per Report",
                "You will be prompted to pick signatories for each report individually.",
                true);

            body.Layout += (s, e) =>
            {
                int w = body.ClientSize.Width - body.Padding.Horizontal;
                int x = body.Padding.Left;
                int h = (body.ClientSize.Height - body.Padding.Vertical - 10) / 2;
                cardAll.SetBounds(x, body.Padding.Top,      w, h);
                cardPer.SetBounds(x, body.Padding.Top + h + 10, w, h);
            };

            body.Controls.Add(cardAll);
            body.Controls.Add(cardPer);

            Controls.Add(body);
            Controls.Add(footer);
            Controls.Add(header);
            CancelButton = btnCancel;
        }

        private Panel MakeOptionCard(string title, string subtitle, bool perReport)
        {
            var card = new Panel
            {
                BackColor = Color.White,
                Cursor    = Cursors.Hand
            };

            card.Paint += (s, e) =>
            {
                using (var pen = new Pen(Border, 1.5f))
                    e.Graphics.DrawRectangle(pen, 0, 0, card.Width - 1, card.Height - 1);
                using (var brush = new SolidBrush(Teal))
                    e.Graphics.FillRectangle(brush, 0, 8, 4, card.Height - 16);
            };

            var lblTitle = new Label
            {
                Text      = title,
                Font      = new Font("Segoe UI", 10F, FontStyle.Bold),
                ForeColor = Dark,
                AutoSize  = true,
                BackColor = Color.Transparent,
                Cursor    = Cursors.Hand
            };
            var lblSub = new Label
            {
                Text      = subtitle,
                Font      = new Font("Segoe UI", 8.5F),
                ForeColor = Muted,
                AutoSize  = false,
                BackColor = Color.Transparent,
                Cursor    = Cursors.Hand
            };

            card.Layout += (s, e) =>
            {
                int midY = card.Height / 2;
                lblTitle.Location = new Point(18, midY - lblTitle.Height - 1);
                lblSub.SetBounds(18, midY + 3, card.Width - 36, card.Height / 2 - 6);
            };

            // Hover highlight
            void OnEnter(object s, EventArgs ev) { card.BackColor = Color.FromArgb(240, 253, 250); card.Invalidate(); }
            void OnLeave(object s, EventArgs ev) { card.BackColor = Color.White; card.Invalidate(); }
            foreach (Control c in new Control[] { card, lblTitle, lblSub })
            {
                c.MouseEnter += OnEnter;
                c.MouseLeave += OnLeave;
            }

            // Click → set result and close
            EventHandler choose = (s, ev) =>
            {
                PerReport    = perReport;
                DialogResult = DialogResult.OK;
                Close();
            };
            card.Click     += choose;
            lblTitle.Click += choose;
            lblSub.Click   += choose;

            card.Controls.Add(lblSub);
            card.Controls.Add(lblTitle);
            return card;
        }
    }

    public class ExportSelectionDialog : Form
    {
        public CheckedListBox CheckedList { get; private set; }

        public ExportSelectionDialog(IEnumerable<string> tabNames)
        {
            Text = "Select Reports";
            Width = 400;
            Height = 350;
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            BackColor = Color.White;

            var lbl = new Label
            {
                Text = "Please select the reports to include in the PDF export:",
                Dock = DockStyle.Top,
                Padding = new Padding(10, 15, 10, 10),
                Height = 60,
                Font = new Font("Segoe UI", 9.5F)
            };

            CheckedList = new CheckedListBox
            {
                Dock = DockStyle.Fill,
                Font = new Font("Segoe UI", 10F),
                CheckOnClick = true,
                BorderStyle = BorderStyle.FixedSingle,
                IntegralHeight = false
            };
            foreach (var name in tabNames)
            {
                CheckedList.Items.Add(name, true);
            }

            var pnlBottom = new Panel { Dock = DockStyle.Bottom, Height = 60, BackColor = Color.FromArgb(248, 249, 250) };
            
            var btnOk = new Button
            {
                Text = "Continue",
                DialogResult = DialogResult.OK,
                BackColor = Color.FromArgb(58, 134, 232),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                Size = new Size(100, 34),
                Cursor = Cursors.Hand
            };
            btnOk.FlatAppearance.BorderSize = 0;
            
            var btnCancel = new Button
            {
                Text = "Cancel",
                DialogResult = DialogResult.Cancel,
                BackColor = Color.White,
                ForeColor = Color.FromArgb(30, 41, 59),
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9F),
                Size = new Size(100, 34),
                Cursor = Cursors.Hand
            };
            btnCancel.FlatAppearance.BorderColor = Color.FromArgb(226, 232, 240);

            pnlBottom.Layout += (s, e) =>
            {
                btnOk.Location = new Point(pnlBottom.Width - btnOk.Width - 15, (pnlBottom.Height - btnOk.Height) / 2);
                btnCancel.Location = new Point(btnOk.Left - btnCancel.Width - 10, btnOk.Top);
            };

            pnlBottom.Paint += (s, e) =>
            {
                using (var pen = new Pen(Color.FromArgb(226, 232, 240)))
                    e.Graphics.DrawLine(pen, 0, 0, pnlBottom.Width, 0);
            };

            pnlBottom.Controls.Add(btnOk);
            pnlBottom.Controls.Add(btnCancel);

            var pnlContainer = new Panel { Dock = DockStyle.Fill, Padding = new Padding(15, 0, 15, 10) };
            pnlContainer.Controls.Add(CheckedList);

            Controls.Add(pnlContainer);
            Controls.Add(lbl);
            Controls.Add(pnlBottom);
            AcceptButton = btnOk;
            CancelButton = btnCancel;
        }
    }
}
