using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace Yakult.Inventory.App.Pages
{
    internal class SimpleCropForm : Form
    {
        private readonly Image _sourceImage;
        private PictureBox _pictureBox;
        private Button _btnOk;
        private Button _btnCancel;

        private bool _isDragging;
        private Point _dragStartPoint;
        private Rectangle _selectionImageRect;
        private bool _hasSelection;

        public SimpleCropForm(Image source)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));

            _sourceImage = (Image)source.Clone();

            BuildUi();
        }

        private void BuildUi()
        {
            Text = "Crop Invoice";
            StartPosition = FormStartPosition.CenterParent;
            Width = 800;
            Height = 600;

            _pictureBox = new PictureBox
            {
                Dock = DockStyle.Fill,
                Image = _sourceImage,
                SizeMode = PictureBoxSizeMode.Zoom,
                BackColor = Color.DimGray
            };

            _pictureBox.MouseDown += PictureBox_MouseDown;
            _pictureBox.MouseMove += PictureBox_MouseMove;
            _pictureBox.MouseUp += PictureBox_MouseUp;
            _pictureBox.Paint += PictureBox_Paint;

            _btnOk = new Button
            {
                Text = "OK",
                DialogResult = DialogResult.OK,
                Dock = DockStyle.Right,
                Width = 80
            };

            _btnCancel = new Button
            {
                Text = "Cancel",
                DialogResult = DialogResult.Cancel,
                Dock = DockStyle.Right,
                Width = 80
            };

            var bottomPanel = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 36
            };

            bottomPanel.Controls.Add(_btnCancel);
            bottomPanel.Controls.Add(_btnOk);

            Controls.Add(_pictureBox);
            Controls.Add(bottomPanel);
        }

        private void PictureBox_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left || _pictureBox.Image == null)
            {
                return;
            }

            var imgPoint = TranslateToImagePoint(e.Location);
            if (imgPoint == null)
            {
                return;
            }

            _isDragging = true;
            _dragStartPoint = imgPoint.Value;
            _selectionImageRect = new Rectangle(_dragStartPoint, Size.Empty);
            _hasSelection = false;
            _pictureBox.Invalidate();
        }

        private void PictureBox_MouseMove(object sender, MouseEventArgs e)
        {
            if (!_isDragging || _pictureBox.Image == null)
            {
                return;
            }

            var imgPoint = TranslateToImagePoint(e.Location);
            if (imgPoint == null)
            {
                return;
            }

            var current = imgPoint.Value;

            int x = Math.Min(_dragStartPoint.X, current.X);
            int y = Math.Min(_dragStartPoint.Y, current.Y);
            int w = Math.Abs(_dragStartPoint.X - current.X);
            int h = Math.Abs(_dragStartPoint.Y - current.Y);

            _selectionImageRect = new Rectangle(x, y, w, h);
            _hasSelection = _selectionImageRect.Width > 5 && _selectionImageRect.Height > 5;

            _pictureBox.Invalidate();
        }

        private void PictureBox_MouseUp(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left)
            {
                return;
            }

            _isDragging = false;
        }

        private void PictureBox_Paint(object sender, PaintEventArgs e)
        {
            if (!_hasSelection || _pictureBox.Image == null)
            {
                return;
            }

            var displayRect = GetImageDisplayRectangle();
            if (displayRect.Width <= 0 || displayRect.Height <= 0)
            {
                return;
            }

            float scaleX = (float)displayRect.Width / _sourceImage.Width;
            float scaleY = (float)displayRect.Height / _sourceImage.Height;

            var sel = _selectionImageRect;
            var rectOnControl = new Rectangle(
                displayRect.Left + (int)(sel.Left * scaleX),
                displayRect.Top + (int)(sel.Top * scaleY),
                (int)(sel.Width * scaleX),
                (int)(sel.Height * scaleY));

            using (var pen = new Pen(Color.Lime, 2))
            {
                e.Graphics.DrawRectangle(pen, rectOnControl);
            }

            using (var brush = new SolidBrush(Color.FromArgb(64, Color.Lime)))
            {
                e.Graphics.FillRectangle(brush, rectOnControl);
            }
        }

        private Rectangle GetImageDisplayRectangle()
        {
            if (_pictureBox.Image == null)
            {
                return Rectangle.Empty;
            }

            var img = _pictureBox.Image;
            var box = _pictureBox.ClientRectangle;
            if (box.Width <= 0 || box.Height <= 0)
            {
                return Rectangle.Empty;
            }

            float imageAspect = (float)img.Width / img.Height;
            float boxAspect = (float)box.Width / box.Height;

            int width, height, x, y;

            if (imageAspect > boxAspect)
            {
                width = box.Width;
                height = (int)(width / imageAspect);
                x = 0;
                y = (box.Height - height) / 2;
            }
            else
            {
                height = box.Height;
                width = (int)(height * imageAspect);
                y = 0;
                x = (box.Width - width) / 2;
            }

            return new Rectangle(x, y, width, height);
        }

        private Point? TranslateToImagePoint(Point controlPoint)
        {
            if (_pictureBox.Image == null)
            {
                return null;
            }

            var img = _pictureBox.Image;
            var displayRect = GetImageDisplayRectangle();
            if (!displayRect.Contains(controlPoint) || displayRect.Width <= 0 || displayRect.Height <= 0)
            {
                return null;
            }

            float scaleX = (float)img.Width / displayRect.Width;
            float scaleY = (float)img.Height / displayRect.Height;

            int x = (int)((controlPoint.X - displayRect.Left) * scaleX);
            int y = (int)((controlPoint.Y - displayRect.Top) * scaleY);

            x = Math.Max(0, Math.Min(img.Width - 1, x));
            y = Math.Max(0, Math.Min(img.Height - 1, y));

            return new Point(x, y);
        }

        public Image GetCroppedImage()
        {
            if (!_hasSelection || _selectionImageRect.Width <= 0 || _selectionImageRect.Height <= 0)
            {
                return null;
            }

            var rect = Rectangle.Intersect(new Rectangle(Point.Empty, _sourceImage.Size), _selectionImageRect);
            if (rect.Width <= 0 || rect.Height <= 0)
            {
                return null;
            }

            var bmp = new Bitmap(rect.Width, rect.Height);
            using (var g = Graphics.FromImage(bmp))
            {
                g.DrawImage(_sourceImage, new Rectangle(0, 0, rect.Width, rect.Height), rect, GraphicsUnit.Pixel);
            }

            return bmp;
        }
    }
}
