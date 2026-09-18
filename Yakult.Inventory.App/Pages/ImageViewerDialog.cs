using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Drawing.Printing;
using System.IO;
using System.Windows.Forms;
using PdfSharp.Drawing;
using PdfSharp.Pdf;

namespace Yakult.Inventory.App.Pages
{
    public partial class ImageViewerDialog : Form
    {
        private readonly string _imagePath;
        private readonly byte[] _imageData;
        private readonly string _displayName;
        private Panel scrollPanel;
        private PictureBox pictureBox;
        private Panel topPanel;
        private Button closeButton;
        private Label titleLabel;
        private TrackBar zoomSlider;
        private Button resetZoomButton;
        private Label zoomLabel;
        private Button printButton;
        private Button saveButton;
        private Size _originalImageSize;
        private float zoomFactor = 1.0f;
        private const float ZoomStep = 0.1f;
        private const float MinZoom = 0.05f;
        private const float MaxZoom = 3.0f;

        public ImageViewerDialog(string imagePath)
        {
            _imagePath = imagePath;
            _displayName = Path.GetFileName(imagePath);
            InitializeComponent();
            LoadImage();
        }

        public ImageViewerDialog(byte[] imageData, string displayName)
        {
            _imageData = imageData;
            _displayName = displayName;
            InitializeComponent();
            LoadImage();
        }

        private void InitializeComponent()
        {
            this.Text = "Image Viewer";
            this.Size = new Size(900, 700);
            this.StartPosition = FormStartPosition.CenterParent;
            this.BackColor = Color.FromArgb(32, 33, 36);
            this.KeyPreview = true;

            // Top panel for controls
            topPanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 60,
                BackColor = Color.FromArgb(45, 45, 48),
                Padding = new Padding(10)
            };

            // Title label
            titleLabel = new Label
            {
                Text = _displayName,
                Font = new Font("Segoe UI", 12F, FontStyle.Bold),
                ForeColor = Color.White,
                Location = new Point(10, 15),
                AutoSize = true
            };

            // All the action controls flow right-to-left in a single row so they never overlap
            // or get clipped regardless of window width -- the old fixed pixel Locations (no
            // Anchor) broke down whenever the window wasn't exactly the default size, which is
            // what made the zoom buttons feel unresponsive/broken.
            var controlsFlow = new FlowLayoutPanel
            {
                FlowDirection = FlowDirection.RightToLeft,
                Dock = DockStyle.Right,
                Width = 620,
                Height = 45,
                Location = new Point(0, 8),
                WrapContents = false,
                AutoSize = false,
                BackColor = Color.Transparent
            };

            var toolTip = new ToolTip();

            // Close button
            closeButton = new Button
            {
                Text = "✕ Close",
                Size = new Size(80, 35),
                Margin = new Padding(4, 0, 0, 0),
                BackColor = Color.FromArgb(198, 52, 52),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand,
                Font = new Font("Segoe UI", 10F)
            };
            closeButton.FlatAppearance.BorderSize = 0;
            closeButton.Click += (s, e) => this.Close();

            // Save button
            saveButton = new Button
            {
                Text = "💾 Save As...",
                Size = new Size(110, 35),
                Margin = new Padding(4, 0, 0, 0),
                BackColor = Color.FromArgb(40, 167, 69),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand,
                Font = new Font("Segoe UI", 9.5F)
            };
            saveButton.FlatAppearance.BorderSize = 0;
            saveButton.Click += (s, e) => SaveImageAs();

            // Print button
            printButton = new Button
            {
                Text = "🖨 Print",
                Size = new Size(90, 35),
                Margin = new Padding(4, 0, 0, 0),
                BackColor = Color.FromArgb(61, 90, 128),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand,
                Font = new Font("Segoe UI", 9.5F)
            };
            printButton.FlatAppearance.BorderSize = 0;
            printButton.Click += (s, e) => PrintImage();

            resetZoomButton = new Button
            {
                Text = "⤢",
                Size = new Size(36, 35),
                Margin = new Padding(12, 0, 0, 0),
                BackColor = Color.FromArgb(61, 90, 128),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand,
                Font = new Font("Segoe UI", 12F),
                TextAlign = ContentAlignment.MiddleCenter
            };
            resetZoomButton.FlatAppearance.BorderSize = 0;
            resetZoomButton.Click += (s, e) => FitToWindow();
            toolTip.SetToolTip(resetZoomButton, "Fit to Window");

            zoomLabel = new Label
            {
                Text = "100%",
                Size = new Size(46, 35),
                Margin = new Padding(4, 5, 0, 0),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleCenter
            };

            zoomSlider = new TrackBar
            {
                Minimum = (int)(MinZoom * 100),
                Maximum = (int)(MaxZoom * 100),
                Value = 100,
                TickFrequency = 20,
                TickStyle = TickStyle.None,
                Size = new Size(160, 35),
                Margin = new Padding(4, 0, 0, 0)
            };
            zoomSlider.Scroll += (s, e) =>
            {
                zoomFactor = zoomSlider.Value / 100f;
                ApplyZoom();
            };
            toolTip.SetToolTip(zoomSlider, "Zoom");

            // FlowDirection.RightToLeft lays these out left-appearing-last, so add in the visual
            // order we want reading left-to-right: [slider][100%][fit][print][save][close]
            controlsFlow.Controls.Add(closeButton);
            controlsFlow.Controls.Add(saveButton);
            controlsFlow.Controls.Add(printButton);
            controlsFlow.Controls.Add(resetZoomButton);
            controlsFlow.Controls.Add(zoomLabel);
            controlsFlow.Controls.Add(zoomSlider);

            topPanel.Controls.AddRange(new Control[] { titleLabel, controlsFlow });

            // Scrollable container so a zoomed-in image (larger than the window) can be panned
            scrollPanel = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(32, 33, 36),
                AutoScroll = true
            };

            // Picture box for image — sized explicitly to the current zoom level (see ApplyZoom)
            pictureBox = new PictureBox
            {
                SizeMode = PictureBoxSizeMode.StretchImage,
                Cursor = Cursors.Hand
            };
            pictureBox.MouseDown += PictureBox_MouseDown;
            pictureBox.MouseWheel += PictureBox_MouseWheel;

            scrollPanel.Controls.Add(pictureBox);
            this.Controls.AddRange(new Control[] { topPanel, scrollPanel });

            // Keyboard shortcuts
            this.KeyDown += ImageViewerDialog_KeyDown;
        }

        private void LoadImage()
        {
            try
            {
                if (_imageData != null && _imageData.Length > 0)
                {
                    using (var ms = new MemoryStream(_imageData))
                    using (var originalImage = Image.FromStream(ms))
                    {
                        pictureBox.Image = new Bitmap(originalImage);
                    }
                }
                else if (File.Exists(_imagePath))
                {
                    using (var originalImage = Image.FromFile(_imagePath))
                    {
                        pictureBox.Image = new Bitmap(originalImage);
                    }
                }
                else
                {
                    ShowError("Image file not found.");
                    return;
                }

                _originalImageSize = pictureBox.Image.Size;
                FitToWindow();
            }
            catch (Exception ex)
            {
                ShowError($"Error loading image: {ex.Message}");
            }
        }

        private void ShowError(string message)
        {
            scrollPanel.BackColor = Color.FromArgb(45, 45, 48);
            var errorLabel = new Label
            {
                Text = message,
                Font = new Font("Segoe UI", 12F),
                ForeColor = Color.White,
                TextAlign = ContentAlignment.MiddleCenter,
                Dock = DockStyle.Fill
            };
            scrollPanel.Controls.Add(errorLabel);
            saveButton.Enabled = false;
        }

        private void ZoomIn()
        {
            if (zoomFactor < MaxZoom)
            {
                zoomFactor = Math.Min(MaxZoom, zoomFactor + ZoomStep);
                ApplyZoom();
            }
        }

        private void ZoomOut()
        {
            if (zoomFactor > MinZoom)
            {
                zoomFactor = Math.Max(MinZoom, zoomFactor - ZoomStep);
                ApplyZoom();
            }
        }

        /// <summary>Resets to whatever zoom level makes the whole image visible in the window.</summary>
        private void FitToWindow()
        {
            if (pictureBox.Image == null || _originalImageSize.Width <= 0 || _originalImageSize.Height <= 0)
                return;

            var containerSize = scrollPanel.ClientSize;
            float scaleX = containerSize.Width  / (float)_originalImageSize.Width;
            float scaleY = containerSize.Height / (float)_originalImageSize.Height;
            zoomFactor = Math.Max(MinZoom, Math.Min(MaxZoom, Math.Min(scaleX, scaleY)));
            ApplyZoom();
        }

        private void ApplyZoom()
        {
            if (pictureBox.Image == null)
                return;

            var newSize = new Size(
                Math.Max(1, (int)(_originalImageSize.Width * zoomFactor)),
                Math.Max(1, (int)(_originalImageSize.Height * zoomFactor))
            );
            pictureBox.Size = newSize;

            // Center within the viewport when smaller than it; otherwise let AutoScroll handle panning.
            var viewportSize = scrollPanel.ClientSize;
            pictureBox.Location = new Point(
                Math.Max(0, (viewportSize.Width  - newSize.Width)  / 2),
                Math.Max(0, (viewportSize.Height - newSize.Height) / 2)
            );

            if (zoomLabel != null)
                zoomLabel.Text = $"{(int)Math.Round(zoomFactor * 100)}%";

            // Keep the slider in sync when zoom changes via keyboard shortcuts or mouse wheel
            // (Scroll only fires on user drag, so setting Value here doesn't cause a feedback loop).
            if (zoomSlider != null)
            {
                int clamped = Math.Max(zoomSlider.Minimum, Math.Min(zoomSlider.Maximum, (int)Math.Round(zoomFactor * 100)));
                if (zoomSlider.Value != clamped)
                    zoomSlider.Value = clamped;
            }
        }

        private void SaveImageAs()
        {
            if (pictureBox.Image == null)
            {
                MessageBox.Show(this, "No image loaded to save.", "Nothing to Save",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            string baseName = string.IsNullOrWhiteSpace(_displayName)
                ? "image"
                : Path.GetFileNameWithoutExtension(_displayName);

            using (var dlg = new SaveFileDialog
            {
                Filter = "PNG Image (*.png)|*.png|JPEG Image (*.jpg)|*.jpg|PDF Document (*.pdf)|*.pdf",
                FileName = baseName,
                Title = "Save Image As"
            })
            {
                if (dlg.ShowDialog(this) != DialogResult.OK)
                    return;

                try
                {
                    string ext = Path.GetExtension(dlg.FileName).ToLowerInvariant();
                    switch (ext)
                    {
                        case ".jpg":
                        case ".jpeg":
                            SaveAsJpeg(dlg.FileName);
                            break;
                        case ".pdf":
                            SaveAsPdf(dlg.FileName);
                            break;
                        default:
                            pictureBox.Image.Save(dlg.FileName, ImageFormat.Png);
                            break;
                    }

                    MessageBox.Show(this, $"Saved to:\n{dlg.FileName}", "Saved",
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(this, $"Failed to save image:\n\n{ex.Message}", "Error",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private void PrintImage()
        {
            if (pictureBox.Image == null)
            {
                MessageBox.Show(this, "No image loaded to print.", "Nothing to Print",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            using (var printDocument = new PrintDocument())
            {
                printDocument.DocumentName = string.IsNullOrWhiteSpace(_displayName) ? "Image" : _displayName;
                // Print the original full-resolution image, not the current on-screen zoom level.
                printDocument.PrintPage += (s, e) => PrintDocument_PrintPage(e, pictureBox.Image);

                using (var printDialog = new PrintDialog { Document = printDocument, AllowSomePages = false, AllowSelection = false })
                {
                    if (printDialog.ShowDialog(this) != DialogResult.OK)
                        return;

                    try
                    {
                        printDocument.Print();
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show(this, $"Failed to print:\n\n{ex.Message}", "Error",
                            MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
            }
        }

        // Fixed physical print size (2x2 in), independent of the image's pixel dimensions and the
        // viewer's on-screen zoom level -- suited to a QR code meant for a label/sticker.
        // PrintPageEventArgs coordinates are in hundredths of an inch, so 200 units = 2 inches.
        private const int PrintSizeHundredthsInch = 200;

        private static void PrintDocument_PrintPage(PrintPageEventArgs e, Image image)
        {
            var printable = e.MarginBounds;
            int x = printable.X + (printable.Width - PrintSizeHundredthsInch) / 2;
            int y = printable.Y + (printable.Height - PrintSizeHundredthsInch) / 2;
            e.Graphics.DrawImage(image, x, y, PrintSizeHundredthsInch, PrintSizeHundredthsInch);
        }

        private void SaveAsJpeg(string path)
        {
            // JPEG has no alpha channel — flatten onto a white background first.
            using (var flattened = new Bitmap(pictureBox.Image.Width, pictureBox.Image.Height))
            {
                using (var g = Graphics.FromImage(flattened))
                {
                    g.Clear(Color.White);
                    g.DrawImage(pictureBox.Image, 0, 0, flattened.Width, flattened.Height);
                }
                flattened.Save(path, ImageFormat.Jpeg);
            }
        }

        private void SaveAsPdf(string path)
        {
            using (var document = new PdfDocument())
            {
                var page = document.AddPage();
                using (var gfx = XGraphics.FromPdfPage(page))
                using (var ms = new MemoryStream())
                {
                    pictureBox.Image.Save(ms, ImageFormat.Png);
                    ms.Position = 0;
                    using (var ximg = XImage.FromStream(ms))
                    {
                        const double margin = 40;
                        double maxW = page.Width.Point - margin * 2;
                        double maxH = page.Height.Point - margin * 2;
                        double scale = Math.Min(maxW / ximg.PixelWidth, maxH / ximg.PixelHeight);
                        double w = ximg.PixelWidth * scale;
                        double h = ximg.PixelHeight * scale;
                        double x = (page.Width.Point - w) / 2;
                        double y = (page.Height.Point - h) / 2;
                        gfx.DrawImage(ximg, x, y, w, h);
                    }
                }
                document.Save(path);
            }
        }

        private void PictureBox_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                // Pan functionality could be added here
            }
        }

        private void PictureBox_MouseWheel(object sender, MouseEventArgs e)
        {
            if (e.Delta > 0)
            {
                ZoomIn();
            }
            else
            {
                ZoomOut();
            }
        }

        private void ImageViewerDialog_KeyDown(object sender, KeyEventArgs e)
        {
            switch (e.KeyCode)
            {
                case Keys.Escape:
                    this.Close();
                    break;
                case Keys.Add:
                case Keys.Oemplus:
                    ZoomIn();
                    break;
                case Keys.Subtract:
                case Keys.OemMinus:
                    ZoomOut();
                    break;
                case Keys.D0:
                case Keys.NumPad0:
                    FitToWindow();
                    break;
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                pictureBox?.Image?.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
