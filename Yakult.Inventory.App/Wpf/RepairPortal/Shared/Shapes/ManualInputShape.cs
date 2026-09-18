using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Shapes;

namespace Yakult.Inventory.App.Wpf.RepairPortal.Shared.Shapes
{
    /// <summary>Which corner is cut by the diagonal edge. TopLeft/BottomRight are a mirror-symmetric
    /// pair — used for the two decorative accents in RepairPortalShellWindow so their diagonals lean
    /// toward each other instead of matching.</summary>
    public enum ManualInputSlantCorner
    {
        TopLeft,
        TopRight,
        BottomLeft,
        BottomRight
    }

    /// <summary>Vector reproduction of Word/PowerPoint's "Flowchart: Manual Input" shape — a
    /// rectangle with one corner replaced by a diagonal edge. Derives from Shape (not a static
    /// image), so Fill/Stroke/StrokeThickness are inherited for free and it resizes without
    /// distortion like any other WPF Shape (Rectangle, Ellipse, etc.). SlantWidth controls how far
    /// the diagonal cut extends from the corner; the remainder of that edge stays flat, matching the
    /// "short diagonal, then flat" look of the reference shape rather than a corner-to-corner shear.</summary>
    public sealed class ManualInputShape : Shape
    {
        public static readonly DependencyProperty SlantWidthProperty =
            DependencyProperty.Register(nameof(SlantWidth), typeof(double), typeof(ManualInputShape),
                new FrameworkPropertyMetadata(28.0, FrameworkPropertyMetadataOptions.AffectsRender));

        public double SlantWidth
        {
            get => (double)GetValue(SlantWidthProperty);
            set => SetValue(SlantWidthProperty, value);
        }

        public static readonly DependencyProperty SlantCornerProperty =
            DependencyProperty.Register(nameof(SlantCorner), typeof(ManualInputSlantCorner), typeof(ManualInputShape),
                new FrameworkPropertyMetadata(ManualInputSlantCorner.TopLeft, FrameworkPropertyMetadataOptions.AffectsRender));

        public ManualInputSlantCorner SlantCorner
        {
            get => (ManualInputSlantCorner)GetValue(SlantCornerProperty);
            set => SetValue(SlantCornerProperty, value);
        }

        protected override Geometry DefiningGeometry
        {
            get
            {
                double w = Math.Max(RenderSize.Width, 0);
                double h = Math.Max(RenderSize.Height, 0);
                double slant = Math.Max(0, Math.Min(SlantWidth, w));

                var figure = new PathFigure { IsClosed = true, IsFilled = true };

                switch (SlantCorner)
                {
                    case ManualInputSlantCorner.TopRight:
                        // Flat top-left -> diagonal down into the top-right corner -> full rectangle.
                        figure.StartPoint = new Point(0, 0);
                        figure.Segments.Add(new LineSegment(new Point(w - slant, 0), true));
                        figure.Segments.Add(new LineSegment(new Point(w, h), true));
                        figure.Segments.Add(new LineSegment(new Point(0, h), true));
                        break;

                    case ManualInputSlantCorner.BottomLeft:
                        // Full rectangle top/right, diagonal cutting the bottom-left corner.
                        figure.StartPoint = new Point(0, 0);
                        figure.Segments.Add(new LineSegment(new Point(w, 0), true));
                        figure.Segments.Add(new LineSegment(new Point(w, h), true));
                        figure.Segments.Add(new LineSegment(new Point(slant, h), true));
                        break;

                    case ManualInputSlantCorner.BottomRight:
                        // Full rectangle top/left, diagonal cutting the bottom-right corner.
                        figure.StartPoint = new Point(0, 0);
                        figure.Segments.Add(new LineSegment(new Point(w, 0), true));
                        figure.Segments.Add(new LineSegment(new Point(w - slant, h), true));
                        figure.Segments.Add(new LineSegment(new Point(0, h), true));
                        break;

                    case ManualInputSlantCorner.TopLeft:
                    default:
                        // Diagonal rising from the bottom-left up into a flat top, then full rectangle.
                        figure.StartPoint = new Point(0, h);
                        figure.Segments.Add(new LineSegment(new Point(slant, 0), true));
                        figure.Segments.Add(new LineSegment(new Point(w, 0), true));
                        figure.Segments.Add(new LineSegment(new Point(w, h), true));
                        break;
                }

                var geometry = new PathGeometry();
                geometry.Figures.Add(figure);
                return geometry;
            }
        }
    }
}
