﻿using System;
using System.Configuration;
using System.Data.SqlClient;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using Yakult.Inventory.App.Repositories;
using Yakult.Inventory.App.Session;
using Yakult.Inventory.App.Core;
using Yakult.Inventory.App.Services;


namespace Yakult.Inventory.App.Pages.Invoice
{
    public partial class InvoiceImportDialog : Form
    {
        private readonly string _invoiceFilePath;
        private string _croppedImagePath;
        private Label _lblTitle;
        private Label _lblFilePath;
        private Label _lblSupplier;
        private Label _lblInvoiceNumber;
        private Label _lblInvoiceDate;
        private Label _lblInvoiceTotal;
        private Label _lblCategory;
        private Label _lblItemType;
        private PictureBox _picPreview;
        private DataGridView _dgvLines;
        private Button _btnOk;
        private Button _btnCancel;
        private Button _btnScan;
        private Button _btnParseSelected;
        private Button _btnCrop;
        private ComboBox _cmbCategory;
        private ComboBox _cmbItemType;
        private TextBox _txtSupplier;
        private TextBox _txtInvoiceNumber;
        private TextBox _txtInvoiceTotal;
        private DateTimePicker _dtpInvoiceDate;

        public InvoiceImportDialog(string invoiceFilePath)
        {
            _invoiceFilePath = invoiceFilePath ?? string.Empty;
            BuildUi();
        }

        private void BuildUi()
        {
            Text = "Invoice Import";
            Width = 900;
            Height = 600;
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.Sizable;
            MinimizeBox = false;
            MaximizeBox = true;

            _lblTitle = new Label
            {
                Text = "Review invoice and enter item lines",
                AutoSize = true,
                Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                Left = 12,
                Top = 10
            };

            _lblFilePath = new Label
            {
                Text = "File: " + _invoiceFilePath,
                AutoSize = false,
                Left = 12,
                Top = 32,
                Width = ClientSize.Width - 24,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };

            _lblSupplier = new Label
            {
                Text = "Supplier:",
                AutoSize = true,
                Left = 12,
                Top = 55
            };

            _txtSupplier = new TextBox
            {
                Left = 80,
                Top = 52,
                Width = 180
            };

            _lblInvoiceNumber = new Label
            {
                Text = "Invoice #:",
                AutoSize = true,
                Left = 280,
                Top = 55
            };

            _txtInvoiceNumber = new TextBox
            {
                Left = 350,
                Top = 52,
                Width = 120
            };

            _lblInvoiceDate = new Label
            {
                Text = "Date:",
                AutoSize = true,
                Left = 480,
                Top = 55
            };

            _dtpInvoiceDate = new DateTimePicker
            {
                Left = 520,
                Top = 52,
                Width = 110,
                Format = DateTimePickerFormat.Short
            };

            _lblInvoiceTotal = new Label
            {
                Text = "Amount:",
                AutoSize = true,
                Left = 640,
                Top = 55
            };

            _txtInvoiceTotal = new TextBox
            {
                Left = 700,
                Top = 52,
                Width = 90
            };

            _lblCategory = new Label
            {
                Text = "Category:",
                AutoSize = true,
                Left = 12,
                Top = 82
            };

            _cmbCategory = new ComboBox
            {
                Left = 80,
                Top = 78,
                Width = 170,
                DropDownStyle = ComboBoxStyle.DropDownList
            };

            _lblItemType = new Label
            {
                Text = "Type:",
                AutoSize = true,
                Left = 280,
                Top = 82
            };

            _cmbItemType = new ComboBox
            {
                Left = 320,
                Top = 78,
                Width = 140,
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            _cmbItemType.Items.AddRange(new object[] { "Hardware", "Software/License", "Service" });
            _cmbItemType.SelectedIndex = 0;

            _picPreview = new PictureBox
            {
                Left = 12,
                Top = 136,
                Width = 260,
                Height = 180,
                SizeMode = PictureBoxSizeMode.Zoom,
                BorderStyle = BorderStyle.FixedSingle,
                Anchor = AnchorStyles.Top | AnchorStyles.Left
            };

            TryLoadPreviewImage();

            _dgvLines = new DataGridView
            {
                Left = 280,
                Top = 136,
                Width = ClientSize.Width - 292,
                Height = ClientSize.Height - 146,
                Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
                AllowUserToAddRows = true,
                AllowUserToDeleteRows = true,
                AutoGenerateColumns = false
            };

            _dgvLines.Columns.Add(new DataGridViewTextBoxColumn { Name = "ItemName", HeaderText = "Item Name", Width = 180 });
            _dgvLines.Columns.Add(new DataGridViewTextBoxColumn { Name = "Description", HeaderText = "Description", Width = 220 });
            _dgvLines.Columns.Add(new DataGridViewTextBoxColumn { Name = "Quantity", HeaderText = "Qty", Width = 60 });
            _dgvLines.Columns.Add(new DataGridViewTextBoxColumn { Name = "UnitOfMeasure", HeaderText = "Unit", Width = 80 });
            _dgvLines.Columns.Add(new DataGridViewTextBoxColumn { Name = "UnitCost", HeaderText = "Unit Cost", Width = 90 });
            _dgvLines.Columns.Add(new DataGridViewTextBoxColumn { Name = "Total", HeaderText = "Line Total", Width = 90 });
            _dgvLines.Columns.Add(new DataGridViewTextBoxColumn { Name = "SerialNumbers", HeaderText = "Serials (comma / newline separated)", Width = 220 });

            _btnOk = new Button
            {
                Text = "Import",
                Width = 90,
                Height = 26,
                Anchor = AnchorStyles.Top | AnchorStyles.Right
            };
            _btnOk.Top = 76; // just under the header row
            _btnOk.Left = ClientSize.Width - _btnOk.Width - 10;
            _btnOk.Click += BtnOk_Click;

            _btnCancel = new Button
            {
                Text = "Cancel",
                Width = 80,
                Height = 26,
                Anchor = AnchorStyles.Top | AnchorStyles.Right
            };
            _btnCancel.Top = 76;
            _btnCancel.Left = _btnOk.Left - _btnCancel.Width - 5;
            _btnCancel.Click += (s, e) =>
            {
                DialogResult = DialogResult.Cancel;
                Close();
            };

            _btnScan = new Button
            {
                Text = "Scan (OCR)",
                Width = 100,
                Height = 26,
                Anchor = AnchorStyles.Top | AnchorStyles.Right
            };
            _btnScan.Top = 76;
            _btnScan.Left = _btnCancel.Left - _btnScan.Width - 5;
            _btnScan.Click += BtnScan_Click;

            _btnParseSelected = new Button
            {
                Text = "Parse Selected",
                Width = 110,
                Height = 26,
                Anchor = AnchorStyles.Top | AnchorStyles.Right
            };
            _btnParseSelected.Top = 76;
            _btnParseSelected.Left = _btnScan.Left - _btnParseSelected.Width - 5;
            _btnParseSelected.Click += BtnParseSelected_Click;

            _btnCrop = new Button
            {
                Text = "Crop",
                Width = 70,
                Height = 26,
                Anchor = AnchorStyles.Top | AnchorStyles.Right
            };
            _btnCrop.Top = 76;
            _btnCrop.Left = _btnParseSelected.Left - _btnCrop.Width - 5;
            _btnCrop.Click += BtnCrop_Click;

            Controls.Add(_lblTitle);
            Controls.Add(_lblFilePath);
            Controls.Add(_lblSupplier);
            Controls.Add(_txtSupplier);
            Controls.Add(_lblInvoiceNumber);
            Controls.Add(_txtInvoiceNumber);
            Controls.Add(_lblInvoiceDate);
            Controls.Add(_dtpInvoiceDate);
            Controls.Add(_lblInvoiceTotal);
            Controls.Add(_txtInvoiceTotal);
            Controls.Add(_lblCategory);
            Controls.Add(_cmbCategory);
            Controls.Add(_lblItemType);
            Controls.Add(_cmbItemType);
            Controls.Add(_picPreview);
            Controls.Add(_dgvLines);
            Controls.Add(_btnOk);
            Controls.Add(_btnCancel);
            Controls.Add(_btnScan);
            Controls.Add(_btnParseSelected);
            Controls.Add(_btnCrop);

            Resize += (s, e) =>
            {
                _lblFilePath.Width = ClientSize.Width - 24;
                _dgvLines.Width = ClientSize.Width - 292;
                _dgvLines.Height = ClientSize.Height - 146;
                _btnOk.Left = ClientSize.Width - _btnOk.Width - 10;
                _btnOk.Top = 76;
                _btnCancel.Left = _btnOk.Left - _btnCancel.Width - 5;
                _btnCancel.Top = 76;
                _btnScan.Left = _btnCancel.Left - _btnScan.Width - 5;
                _btnScan.Top = 76;
                _btnParseSelected.Left = _btnScan.Left - _btnParseSelected.Width - 5;
                _btnParseSelected.Top = 76;
                _btnCrop.Left = _btnParseSelected.Left - _btnCrop.Width - 5;
                _btnCrop.Top = 76;
            };

            FormClosed += (s, e) =>
            {
                if (_picPreview.Image != null)
                {
                    _picPreview.Image.Dispose();
                }
            };

            LoadCategories();
        }

        private void TryLoadPreviewImage()
        {
            try
            {
                if (File.Exists(_invoiceFilePath))
                {
                    var ext = Path.GetExtension(_invoiceFilePath).ToLowerInvariant();
                    if (ext == ".jpg" || ext == ".jpeg" || ext == ".png" || ext == ".bmp" || ext == ".gif" || ext == ".tif" || ext == ".tiff")
                    {
                        _picPreview.Image = LoadImageWithCorrectOrientation(_invoiceFilePath);
                    }
                    else
                    {
                        _picPreview.Visible = false;
                    }
                }
                else
                {
                    _picPreview.Visible = false;
                }
            }
            catch
            {
                _picPreview.Visible = false;
            }
        }

        private void FocusPlaceholderRowIfPresent()
        {
            try
            {
                const string placeholderText = "ENTER MISSING ITEM DESCRIPTION";
                DataGridViewRow targetRow = null;

                foreach (DataGridViewRow row in _dgvLines.Rows)
                {
                    if (row.IsNewRow) continue;
                    var desc = Convert.ToString(row.Cells["Description"].Value) ?? string.Empty;
                    if (string.Equals(desc, placeholderText, StringComparison.OrdinalIgnoreCase))
                    {
                        targetRow = row;
                        break;
                    }
                }

                if (targetRow != null)
                {
                    var cell = targetRow.Cells["Description"];
                    _dgvLines.CurrentCell = cell;
                    _dgvLines.BeginEdit(true);
                }
            }
            catch
            {
                // Non-fatal: if focusing fails, just leave grid as-is.
            }
        }

        private Image LoadImageWithCorrectOrientation(string path)
        {
            var img = Image.FromFile(path);

            try
            {
                const int ExifOrientationId = 0x0112;
                if (img.PropertyIdList != null && img.PropertyIdList.Contains(ExifOrientationId))
                {
                    var prop = img.GetPropertyItem(ExifOrientationId);
                    int orientation = BitConverter.ToUInt16(prop.Value, 0);

                    RotateFlipType rotateFlip = RotateFlipType.RotateNoneFlipNone;
                    switch (orientation)
                    {
                        case 2:
                            rotateFlip = RotateFlipType.RotateNoneFlipX;
                            break;
                        case 3:
                            rotateFlip = RotateFlipType.Rotate180FlipNone;
                            break;
                        case 4:
                            rotateFlip = RotateFlipType.Rotate180FlipX;
                            break;
                        case 5:
                            rotateFlip = RotateFlipType.Rotate90FlipX;
                            break;
                        case 6:
                            rotateFlip = RotateFlipType.Rotate90FlipNone;
                            break;
                        case 7:
                            rotateFlip = RotateFlipType.Rotate270FlipX;
                            break;
                        case 8:
                            rotateFlip = RotateFlipType.Rotate270FlipNone;
                            break;
                    }

                    if (rotateFlip != RotateFlipType.RotateNoneFlipNone)
                    {
                        img.RotateFlip(rotateFlip);

                        prop.Value = BitConverter.GetBytes((ushort)1);
                        img.SetPropertyItem(prop);
                    }
                }
            }
            catch
            {
            }

            return img;
        }

        private string PrepareImageForOcr(string originalPath)
        {
            try
            {
                var ext = Path.GetExtension(originalPath);
                if (string.IsNullOrEmpty(ext))
                {
                    ext = ".png";
                }

                var tempPath = Path.Combine(Path.GetTempPath(),
                    "YakultInvoiceOcr_" + Guid.NewGuid().ToString("N") + ext);

                using (var src = LoadImageWithCorrectOrientation(originalPath))
                {
                    // Simple, reliable behavior: save the oriented image as-is
                    src.Save(tempPath);
                }

                return tempPath;
            }
            catch
            {
                // If anything goes wrong, just fall back to the original image path
                return originalPath;
            }
        }

        private void TryDeleteTempOcrFile(string path)
        {
            try
            {
                if (!string.Equals(path, _invoiceFilePath, StringComparison.OrdinalIgnoreCase)
                    && (string.IsNullOrWhiteSpace(_croppedImagePath) || !string.Equals(path, _croppedImagePath, StringComparison.OrdinalIgnoreCase))
                    && File.Exists(path))
                {
                    File.Delete(path);
                }
            }
            catch
            {
            }
        }

        private async void BtnScan_Click(object sender, EventArgs e)
        {
            try
            {
                string baseImagePath = !string.IsNullOrWhiteSpace(_croppedImagePath) && File.Exists(_croppedImagePath)
                    ? _croppedImagePath
                    : _invoiceFilePath;

                if (string.IsNullOrWhiteSpace(baseImagePath) || !File.Exists(baseImagePath))
                {
                    MessageBox.Show("No invoice file found to scan.", "OCR", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                var ext = Path.GetExtension(baseImagePath).ToLowerInvariant();
                if (ext != ".jpg" && ext != ".jpeg" && ext != ".png" && ext != ".bmp" && ext != ".tif" && ext != ".tiff")
                {
                    MessageBox.Show("OCR is currently supported only for image files (JPG, PNG, BMP, TIFF).", "OCR", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                string exePath = ConfigurationManager.AppSettings["TesseractExePath"];

                // If config key is missing or empty, fall back to the standard Windows install path
                if (string.IsNullOrWhiteSpace(exePath))
                {
                    exePath = @"C:\\Program Files\\Tesseract-OCR\\tesseract.exe";
                }

                if (!File.Exists(exePath))
                {
                    MessageBox.Show(
                        "Tesseract OCR is not configured correctly. The application looked for:\n\n" + exePath +
                        "\n\nbut that file does not exist. Please install Tesseract or update the 'TesseractExePath' key in App.config to the correct full path of tesseract.exe.",
                        "OCR",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);
                    return;
                }

                string ocrInputPath = PrepareImageForOcr(baseImagePath);

                Cursor = Cursors.WaitCursor;
                string ocrText = await System.Threading.Tasks.Task.Run(() => RunTesseract(exePath, ocrInputPath));
                Cursor = Cursors.Default;

                TryDeleteTempOcrFile(ocrInputPath);

                if (string.IsNullOrWhiteSpace(ocrText))
                {
                    MessageBox.Show("OCR did not return any text.", "OCR", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                DumpOcrText(ocrText);
                FillGridFromOcrText(ocrText);
                FocusPlaceholderRowIfPresent();
                MessageBox.Show("OCR completed. The grid has been pre-filled. Please review, adjust quantities, and enter serial numbers.",
                    "OCR", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                Cursor = Cursors.Default;
                MessageBox.Show("OCR failed: " + ex.Message, "OCR", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void BtnCrop_Click(object sender, EventArgs e)
        {
            try
            {
                string baseImagePath = !string.IsNullOrWhiteSpace(_croppedImagePath) && File.Exists(_croppedImagePath)
                    ? _croppedImagePath
                    : _invoiceFilePath;

                if (string.IsNullOrWhiteSpace(baseImagePath) || !File.Exists(baseImagePath))
                {
                    MessageBox.Show("No invoice image found to crop.", "Crop", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                using (var fullImage = LoadImageWithCorrectOrientation(baseImagePath))
                using (var cropForm = new SimpleCropForm(fullImage))
                {
                    var result = cropForm.ShowDialog(this);
                    if (result != DialogResult.OK)
                    {
                        return;
                    }

                    var croppedImage = cropForm.GetCroppedImage();
                    if (croppedImage == null)
                    {
                        MessageBox.Show("No crop region was selected.", "Crop", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        return;
                    }

                    // Save cropped image to a temp file and remember its path
                    var ext = Path.GetExtension(baseImagePath);
                    if (string.IsNullOrEmpty(ext))
                    {
                        ext = ".png";
                    }

                    var tempPath = Path.Combine(Path.GetTempPath(),
                        "YakultInvoiceCrop_" + Guid.NewGuid().ToString("N") + ext);

                    croppedImage.Save(tempPath);
                    croppedImage.Dispose();

                    _croppedImagePath = tempPath;

                    // Update preview to show cropped area
                    if (_picPreview.Image != null)
                    {
                        _picPreview.Image.Dispose();
                    }
                    _picPreview.Image = Image.FromFile(_croppedImagePath);

                    // Also update the label so user knows OCR will use the cropped version
                    _lblFilePath.Text = "File: " + _croppedImagePath;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Cropping failed: " + ex.Message, "Crop", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private string RunTesseract(string exePath, string imagePath)
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = exePath,
                Arguments = "\"" + imagePath + "\" stdout -l eng",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            using (var process = new Process())
            {
                process.StartInfo = startInfo;
                process.Start();

                string output = process.StandardOutput.ReadToEnd();
                string error = process.StandardError.ReadToEnd();
                process.WaitForExit();

                if (process.ExitCode != 0)
                {
                    throw new InvalidOperationException("Tesseract exited with code " + process.ExitCode + ": " + error);
                }

                return output;
            }
        }

        private void FillGridFromOcrText(string ocrText)
        {
            // For now, always use the simple, predictable behavior: each OCR text line
            // becomes one grid row with its text in Description and an optional Qty at
            // the end if a trailing integer is present.
            if (!TryFillGridFromJumpSolutionsDeliveryReceipt(ocrText))
            {
                FillGridFromOcrTextFallback(ocrText);
            }
        }

        private bool TryFillGridFromJumpSolutionsDeliveryReceipt(string ocrText)
        {
            _dgvLines.Rows.Clear();

            if (string.IsNullOrWhiteSpace(ocrText))
            {
                return false;
            }

            var allLines = ocrText
                .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(l => l.Trim())
                .Where(l => l.Length > 0)
                .ToArray();

            if (allLines.Length == 0)
            {
                return false;
            }

            int headerIndex = -1;
            for (int i = 0; i < allLines.Length; i++)
            {
                if (allLines[i].IndexOf("DESCRIPTION", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    headerIndex = i;
                    break;
                }
            }

            int startIndex = headerIndex >= 0 ? headerIndex + 1 : 0;

            var currentDescriptionLines = new System.Collections.Generic.List<string>();
            var currentSerials = new System.Collections.Generic.List<string>();
            int? currentQty = null;
            bool anyRow = false;

            System.Action finalizeCurrentItem = () =>
            {
                if (currentDescriptionLines.Count == 0)
                {
                    return;
                }

                if (AddParsedItemRow(currentDescriptionLines, currentQty, currentSerials))
                {
                    anyRow = true;
                }

                currentDescriptionLines.Clear();
                currentSerials.Clear();
                // Keep currentQty so it can apply to subsequent items if no new qty line is seen.
            };

            for (int i = startIndex; i < allLines.Length; i++)
            {
                var line = allLines[i];
                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }

                if (IsLikelyFooterLine(line))
                {
                    finalizeCurrentItem();
                    break;
                }

                bool hasLetterOrDigit = line.Any(char.IsLetterOrDigit);
                if (!hasLetterOrDigit)
                {
                    continue;
                }

                var trimmed = line.Trim();

                int parsedQty;
                if (int.TryParse(trimmed, NumberStyles.Integer, CultureInfo.InvariantCulture, out parsedQty)
                    && parsedQty > 0 && parsedQty <= 100)
                {
                    currentQty = parsedQty;
                    continue;
                }

                if (IsNewItemStartLine(trimmed))
                {
                    finalizeCurrentItem();
                    currentDescriptionLines.Add(trimmed);
                    continue;
                }

                int snIndex = IndexOfSnMarker(trimmed);
                if (snIndex >= 0)
                {
                    string serialPart = trimmed.Substring(snIndex + 3);
                    var serialTokens = serialPart
                        .Split(new[] { ' ', ',', ';', '\t' }, StringSplitOptions.RemoveEmptyEntries)
                        .Where(t => t.Any(char.IsLetterOrDigit))
                        .ToArray();

                    currentSerials.Clear();
                    currentSerials.AddRange(serialTokens);
                    continue;
                }

                if (currentDescriptionLines.Count > 0)
                {
                    currentDescriptionLines.Add(trimmed);
                }
            }

            finalizeCurrentItem();

            if (anyRow)
            {
                // For this delivery receipt layout we usually expect 4 logical items.
                // If OCR only gave us 1-3, add a placeholder row so the user can
                // easily enter the missing line manually.
                int existingItemCount = 0;
                foreach (DataGridViewRow r in _dgvLines.Rows)
                {
                    if (r.IsNewRow) continue;
                    var desc = Convert.ToString(r.Cells["Description"].Value) ?? string.Empty;
                    if (!string.IsNullOrWhiteSpace(desc)) existingItemCount++;
                }

                if (existingItemCount > 0 && existingItemCount < 4)
                {
                    int qtyForPlaceholder = (currentQty.HasValue && currentQty.Value > 0)
                        ? currentQty.Value
                        : 3;

                    int rowIndex = _dgvLines.Rows.Add();
                    var row = _dgvLines.Rows[rowIndex];
                    row.Cells["Description"].Value = "ENTER MISSING ITEM DESCRIPTION";
                    if (qtyForPlaceholder > 0)
                    {
                        row.Cells["Quantity"].Value = qtyForPlaceholder;
                    }
                }
            }

            return anyRow;
        }

        private bool AddParsedItemRow(System.Collections.Generic.List<string> descriptionLines, int? qtyFromText, System.Collections.Generic.List<string> serials)
        {
            if (descriptionLines == null || descriptionLines.Count == 0)
            {
                return false;
            }

            string description = string.Join(" ", descriptionLines).Trim();
            if (string.IsNullOrWhiteSpace(description))
            {
                return false;
            }

            int quantity = 0;
            if (qtyFromText.HasValue && qtyFromText.Value > 0)
            {
                quantity = qtyFromText.Value;
            }
            else if (serials != null && serials.Count > 0)
            {
                quantity = serials.Count;
            }

            int rowIndex = _dgvLines.Rows.Add();
            var row = _dgvLines.Rows[rowIndex];
            row.Cells["Description"].Value = description;
            if (quantity > 0)
            {
                row.Cells["Quantity"].Value = quantity;
            }

            if (serials != null && serials.Count > 0)
            {
                row.Cells["SerialNumbers"].Value = string.Join(Environment.NewLine, serials);
            }

            return true;
        }

        private bool IsLikelyFooterLine(string line)
        {
            if (string.IsNullOrEmpty(line))
            {
                return false;
            }

            if (line.IndexOf("THIS DOCUMENT IS NOT VALID", StringComparison.OrdinalIgnoreCase) >= 0) return true;
            if (line.IndexOf("Printer's Accreditation", StringComparison.OrdinalIgnoreCase) >= 0) return true;
            if (line.IndexOf("Date of Accreditation", StringComparison.OrdinalIgnoreCase) >= 0) return true;
            if (line.IndexOf("LEE & SONS PRINTING", StringComparison.OrdinalIgnoreCase) >= 0) return true;
            if (line.IndexOf("Date of ATP", StringComparison.OrdinalIgnoreCase) >= 0) return true;
            if (line.IndexOf("Scanned with CamScanner", StringComparison.OrdinalIgnoreCase) >= 0) return true;

            return false;
        }

        private int IndexOfSnMarker(string line)
        {
            if (string.IsNullOrEmpty(line))
            {
                return -1;
            }

            int idx = line.IndexOf("SN:", StringComparison.OrdinalIgnoreCase);
            if (idx >= 0)
            {
                return idx;
            }

            idx = line.IndexOf("S/N", StringComparison.OrdinalIgnoreCase);
            return idx;
        }

        private bool IsNewItemStartLine(string line)
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                return false;
            }

            var parts = line.TrimStart().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0)
            {
                return false;
            }

            string firstTokenUpper = parts[0].ToUpperInvariant();
            return firstTokenUpper == "LENOVO" || firstTokenUpper == "MS" || firstTokenUpper == "APC";
        }

        private void FillGridFromOcrTextFallback(string ocrText)
        {
            _dgvLines.Rows.Clear();

            var lines = ocrText
                .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(l => l.Trim())
                .Where(l => l.Length > 10) // ignore very short lines
                .ToArray();

            foreach (var line in lines)
            {
                // Heuristic: find a trailing integer token to use as quantity
                var tokens = line.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                int qty = 0;
                int qtyIndex = -1;

                for (int i = tokens.Length - 1; i >= 0; i--)
                {
                    int parsedQty;
                    if (int.TryParse(tokens[i], NumberStyles.Integer, CultureInfo.InvariantCulture, out parsedQty)
                        && parsedQty > 0 && parsedQty <= 20) // assume realistic small quantities
                    {
                        qty = parsedQty;
                        qtyIndex = i;
                        break;
                    }
                }

                string description;
                if (qtyIndex > 0)
                {
                    description = string.Join(" ", tokens.Take(qtyIndex));
                }
                else
                {
                    description = line;
                }

                int rowIndex = _dgvLines.Rows.Add();
                var row = _dgvLines.Rows[rowIndex];
                row.Cells["Description"].Value = description;
                if (qty > 0)
                {
                    row.Cells["Quantity"].Value = qty;
                }
            }
        }

        private void DumpOcrText(string text)
        {
            try
            {
                // Write to project root so it is easy to inspect from source tree
                var baseDir = AppDomain.CurrentDomain.BaseDirectory; // bin/Debug
                var projectRoot = Path.GetFullPath(Path.Combine(baseDir, "..", ".."));
                var logDir = Path.Combine(projectRoot, "OcrLogs");
                Directory.CreateDirectory(logDir);

                var filePath = Path.Combine(logDir, "last-ocr.txt");
                File.WriteAllText(filePath, text ?? string.Empty);
            }
            catch
            {
                // Swallow logging errors \u2014 OCR should still work even if logging fails
            }
        }

        private void BtnParseSelected_Click(object sender, EventArgs e)
        {
            if (_dgvLines.SelectedRows == null || _dgvLines.SelectedRows.Count == 0)
            {
                MessageBox.Show("Select one or more rows first, then click 'Parse Selected' to extract Qty.",
                    "Parse Selected", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            foreach (DataGridViewRow row in _dgvLines.Rows)
            {
                if (row.IsNewRow || !row.Selected)
                {
                    continue;
                }

                var description = Convert.ToString(row.Cells["Description"].Value) ?? string.Empty;
                if (string.IsNullOrWhiteSpace(description))
                {
                    continue;
                }

                var tokens = description
                    .Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries)
                    .ToArray();

                if (tokens.Length == 0)
                {
                    continue;
                }

                var numericTokens = new System.Collections.Generic.List<(int Index, string Token)>();
                for (int i = tokens.Length - 1; i >= 0 && numericTokens.Count < 4; i--)
                {
                    if (tokens[i].Any(char.IsDigit))
                    {
                        numericTokens.Add((i, tokens[i]));
                    }
                }

                if (numericTokens.Count == 0)
                {
                    continue;
                }

                numericTokens.Reverse(); // now roughly [qty?, unit?, total?] order

                // Try to parse total (last numeric)
                decimal totalVal;
                var last = numericTokens[numericTokens.Count - 1];
                var totalToken = new string(last.Token.Where(c => char.IsDigit(c) || c == '.' || c == ',').ToArray());
                totalToken = totalToken.Replace(",", string.Empty);
                if (decimal.TryParse(totalToken, NumberStyles.Number, CultureInfo.InvariantCulture, out totalVal))
                {
                }

                // Try to parse unit cost (second to last numeric)
                if (numericTokens.Count >= 2)
                {
                    var unit = numericTokens[numericTokens.Count - 2];
                    var unitToken = new string(unit.Token.Where(c => char.IsDigit(c) || c == '.' || c == ',').ToArray());
                    unitToken = unitToken.Replace(",", string.Empty);
                    decimal unitVal;
                    if (decimal.TryParse(unitToken, NumberStyles.Number, CultureInfo.InvariantCulture, out unitVal))
                    {
                    }
                }

                // Try to parse quantity (first numeric, small integer)
                var first = numericTokens[0];
                var qtyToken = new string(first.Token.Where(char.IsDigit).ToArray());
                int parsedQty;
                if (int.TryParse(qtyToken, NumberStyles.Integer, CultureInfo.InvariantCulture, out parsedQty)
                    && parsedQty > 0 && parsedQty <= 100)
                {
                    row.Cells["Quantity"].Value = parsedQty;

                    // Optionally trim quantity token out of description text
                    if (first.Index < tokens.Length)
                    {
                        var trimmedTokens = tokens.ToList();
                        trimmedTokens.RemoveAt(first.Index);
                        row.Cells["Description"].Value = string.Join(" ", trimmedTokens);
                    }
                }
            }

            MessageBox.Show("Parsing complete. Please review the Qty for the selected rows.",
                "Parse Selected", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void LoadCategories()
        {
            try
            {
                _cmbCategory.Items.Clear();

                var connectionString = Yakult.Inventory.App.Core.DatabaseConfig.ConnectionString;

                if (string.IsNullOrWhiteSpace(connectionString))
                {
                    return;
                }

                using (var con = new SqlConnection(connectionString))
                {
                    con.Open();
                    const string query = "SELECT CategoryId, Name FROM dbo.ItemCategory WHERE Active = 1 ORDER BY Name";
                    using (var cmd = new SqlCommand(query, con))
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            _cmbCategory.Items.Add(new CategoryItem
                            {
                                CategoryId = reader.GetInt32(0),
                                Name = reader.GetString(1)
                            });
                        }
                    }
                }

                if (_cmbCategory.Items.Count > 0)
                {
                    _cmbCategory.SelectedIndex = 0;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to load categories: {ex.Message}", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private async void BtnOk_Click(object sender, EventArgs e)
        {
            var selectedCategory = _cmbCategory.SelectedItem as CategoryItem;
            if (selectedCategory == null)
            {
                MessageBox.Show("Please select a category before importing.", "Validation",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            var itemType = _cmbItemType.SelectedItem?.ToString() ?? "Hardware";
            var repository = new ItemRepository();
            var currentUserId = AppSession.CurrentUserId;
            var currentUserName = AppSession.CurrentUserName;

            string supplier = _txtSupplier.Text.Trim();
            string invoiceNumber = _txtInvoiceNumber.Text.Trim();
            // Do not strip time — database requires full timestamp
            DateTime invoiceDate = _dtpInvoiceDate.Value;
            decimal invoiceAmount = 0m;
            decimal.TryParse(_txtInvoiceTotal.Text, out invoiceAmount);

            int invoiceSetId = 0;
            InvoiceRepository invoiceRepository = null;

            try
            {
                string connectionString = Yakult.Inventory.App.Core.DatabaseConfig.ConnectionString;

                bool shouldCreateInvoice = !string.IsNullOrWhiteSpace(invoiceNumber) &&
                                            !string.IsNullOrWhiteSpace(connectionString);

                if (shouldCreateInvoice)
                {
                    invoiceRepository = new InvoiceRepository(connectionString);

                    invoiceSetId = await invoiceRepository.CreateInvoiceAsync(
                        documentNumber: invoiceNumber,
                        referenceNumber: null,
                        site: null,
                        startDate: invoiceDate,
                        endDate: invoiceDate,
                        subtotal: invoiceAmount,
                        vatAmount: 0m,
                        whtAmount: 0m,
                        discountAmount: 0m,
                        totalAmountDue: invoiceAmount,
                        companyId: null,
                        status: "Yes",
                        remarks: string.IsNullOrWhiteSpace(supplier) ? null : ("Supplier: " + supplier),
                        createdBy: currentUserId
                    );
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "Failed to create invoice header. Items will still be imported without invoice linkage.\n" + ex.Message,
                    "Invoice Import",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                invoiceRepository = null;
                invoiceSetId = 0;
            }

            int successCount = 0;
            var errors = new System.Collections.Generic.List<string>();

            // One "Invoice created" Activity row for the whole import — suppress per-item rows.
            using var _suppressItemRows = Yakult.Inventory.App.Services.InventoryActivityNotifier.SuppressItemAdded();
            var _activityItems = new System.Collections.Generic.List<Yakult.Inventory.App.Services.InventoryActivityNotifier.ActivityItemDetail>();

            foreach (DataGridViewRow row in _dgvLines.Rows)
            {
                if (row.IsNewRow)
                {
                    continue;
                }

                var rawName = Convert.ToString(row.Cells["ItemName"].Value) ?? string.Empty;
                var rawDescription = Convert.ToString(row.Cells["Description"].Value) ?? string.Empty;
                var name = string.IsNullOrWhiteSpace(rawName) ? rawDescription : rawName;

                if (string.IsNullOrWhiteSpace(name))
                {
                    // Skip completely blank rows
                    continue;
                }

                var qtyText = Convert.ToString(row.Cells["Quantity"].Value) ?? string.Empty;
                if (!int.TryParse(qtyText, out var quantity) || quantity <= 0)
                {
                    errors.Add("Invalid quantity for an item row. Please enter a positive whole number.");
                    continue;
                }

                var unit = Convert.ToString(row.Cells["UnitOfMeasure"].Value);
                if (string.IsNullOrWhiteSpace(unit))
                {
                    unit = "Unit";
                }

                var unitCostText = Convert.ToString(row.Cells["UnitCost"].Value) ?? string.Empty;
                if (!decimal.TryParse(unitCostText, out var unitCost))
                {
                    unitCost = 0m;
                }

                var serialsText = Convert.ToString(row.Cells["SerialNumbers"].Value) ?? string.Empty;
                var serials = serialsText
                    .Split(new[] { '\r', '\n', ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(s => s.Trim())
                    .Where(s => !string.IsNullOrWhiteSpace(s))
                    .ToArray();

                if (serials.Length != quantity)
                {
                    errors.Add($"Item '{name}': quantity is {quantity} but {serials.Length} serial number(s) were entered.");
                    continue;
                }

                foreach (var serial in serials)
                {
                    try
                    {
                        var item = new ItemDto
                        {
                            Name = name,
                            Description = string.IsNullOrWhiteSpace(rawDescription) ? null : rawDescription,
                            // Database column ModelNumber is NOT NULL; default to Name when no separate model is provided
                            ModelNumber = name,
                            CategoryId = selectedCategory.CategoryId,
                            Category = selectedCategory.Name,
                            SerialNumber = serial,
                            StockOnHand = 1,
                            UnitOfMeasure = unit,
                            Amount = unitCost,
                            ItemType = itemType,
                            DateCreated = DateTime.Now,
                            CreatedByUserId = currentUserId,
                            CreatedByName = currentUserName,
                            Active = true
                        };

                        int newItemId = repository.AddItem(item);
                        successCount++;
                        _activityItems.Add(new Yakult.Inventory.App.Services.InventoryActivityNotifier.ActivityItemDetail
                        {
                            id = newItemId, name = name, type = itemType,
                            serial = string.IsNullOrWhiteSpace(serial) ? null : serial,
                        });

                        if (invoiceRepository != null && invoiceSetId > 0)
                        {
                            string invoiceItemDescription = string.IsNullOrWhiteSpace(rawDescription)
                                ? name
                                : rawDescription;

                            try
                            {
                                await invoiceRepository.AddInvoiceItemAsync(
                                    invoiceSetId,
                                    newItemId,
                                    1,
                                    invoiceItemDescription,
                                    currentUserId);
                            }
                            catch (Exception ex)
                            {
                                errors.Add($"Failed to link item '{name}' to invoice: {ex.Message}");
                            }
                        }
                    }
                    catch (SqlException ex)
                    {
                        errors.Add($"Serial '{serial}' for item '{name}': database error {ex.Number} - {ex.Message}");
                    }
                    catch (Exception ex)
                    {
                        errors.Add($"Serial '{serial}' for item '{name}': {ex.Message}");
                    }
                }
            }

            if (successCount > 0)
            {
                ActivityLogger.Log(ActivityLogger.Actions.Import,
                    "Invoice", invoiceSetId > 0 ? invoiceSetId : (int?)null,
                    $"Imported {successCount} item(s) from invoice" +
                    (invoiceSetId > 0 ? $" (Set #{invoiceSetId})" : string.Empty));

                Yakult.Inventory.App.Services.InventoryActivityNotifier.NotifySetCreated(
                    invoiceSetId, successCount, currentUserId, "Invoice", _activityItems);

                MessageBox.Show(
                    $"Successfully imported {successCount} item(s) from invoice.",
                    "Invoice Import",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
            }

            if (errors.Count > 0)
            {
                var preview = string.Join("\n", errors.Take(10));
                if (errors.Count > 10)
                {
                    preview += $"\n... and {errors.Count - 10} more";
                }

                MessageBox.Show(
                    "Some items could not be imported:\n" + preview,
                    "Import Warnings",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
            }

            if (successCount > 0)
            {
                DialogResult = DialogResult.OK;
                Close();
            }
            else if (errors.Count == 0)
            {
                MessageBox.Show(
                    "No items were imported. Please enter at least one line with a name/description, quantity, unit, cost (optional), and matching serial numbers.",
                    "Nothing to Import",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
            }
        }

        private class CategoryItem
        {
            public int CategoryId { get; set; }
            public string Name { get; set; }

            public override string ToString()
            {
                return Name;
            }
        }
    }
}

