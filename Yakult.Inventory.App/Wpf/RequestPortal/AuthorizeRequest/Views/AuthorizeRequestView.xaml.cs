using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Yakult.Inventory.App.WPF.RequestPortal.AuthorizeRequest.ViewModels;

namespace Yakult.Inventory.App.WPF.RequestPortal.AuthorizeRequest.Views
{
    public partial class AuthorizeRequestView : UserControl
    {
        internal AuthorizeRequestViewModel ViewModel => DataContext as AuthorizeRequestViewModel;

        // ── Tour targets — used by WpfPortalTourService to spotlight UI elements ──
        internal System.Windows.FrameworkElement TourTarget_HeaderBar        => tourHeaderBar;
        internal System.Windows.FrameworkElement TourTarget_SearchArea       => tourSearchArea;
        internal System.Windows.FrameworkElement TourTarget_QueueList        => tourQueueList;
        internal System.Windows.FrameworkElement TourTarget_QueuePanel       => tourQueuePanel;
        internal System.Windows.FrameworkElement TourTarget_RequestDetails   => tourRequestDetailsCard;
        internal System.Windows.FrameworkElement TourTarget_CartridgeCard    => tourCartridgeCard;
        internal System.Windows.FrameworkElement TourTarget_PreviewCard      => tourPreviewCard;
        internal System.Windows.FrameworkElement TourTarget_SignatureCard    => tourSignatureCard;
        internal System.Windows.FrameworkElement TourTarget_DrawTabBtn       => tourDrawTabBtn;
        internal System.Windows.FrameworkElement TourTarget_UploadTabBtn     => tourUploadTabBtn;
        internal System.Windows.FrameworkElement TourTarget_ClearBtnRow      => tourClearBtnRow;
        internal System.Windows.FrameworkElement TourTarget_ActionCard       => tourActionCard;
        internal System.Windows.FrameworkElement TourTarget_NotesBox         => tourNotesBox;
        internal System.Windows.FrameworkElement TourTarget_ApproveBtn       => tourApproveBtn;
        internal System.Windows.FrameworkElement TourTarget_RejectBtn        => tourRejectBtn;

        public AuthorizeRequestView()
        {
            InitializeComponent();

            var vm = new AuthorizeRequestViewModel();
            DataContext = vm;

            inkCanvas.StrokesReplaced        += (s, e) => { UpdateDrawHint(); OnStrokesChanged(); };
            inkCanvas.Strokes.StrokesChanged += (s, e) => { UpdateDrawHint(); OnStrokesChanged(); };

            // MouseLeave does NOT fire while InkCanvas holds mouse capture during a stroke.
            // MouseMove DOES fire (capture routes all move events to the capturing element),
            // so we check bounds there and release capture to finalize the stroke at the edge.
            inkCanvas.MouseMove += (s, e) =>
            {
                if (!inkCanvas.IsMouseCaptured) return;
                var pt = e.GetPosition(inkCanvas);
                if (pt.X < 0 || pt.Y < 0 ||
                    pt.X > inkCanvas.ActualWidth || pt.Y > inkCanvas.ActualHeight)
                    inkCanvas.ReleaseMouseCapture();
            };

            // Same check for stylus/pen input.
            inkCanvas.StylusMove += (s, e) =>
            {
                if (!inkCanvas.IsStylusCaptured) return;
                foreach (var pt in e.GetStylusPoints(inkCanvas))
                {
                    if (pt.X < 0 || pt.Y < 0 ||
                        pt.X > inkCanvas.ActualWidth || pt.Y > inkCanvas.ActualHeight)
                    {
                        inkCanvas.ReleaseStylusCapture();
                        break;
                    }
                }
            };

            Loaded += async (s, e) =>
            {
                WireSignatureDelegates(vm);
                await vm.LoadAsync();
                vm.StartPolling();
            };

            Unloaded += (s, e) => vm.StopPolling();
        }

        // Called externally when an approver just submitted a self-request that needs sign-off.
        public async System.Threading.Tasks.Task LoadAndSelectAsync(int authorizationId)
        {
            await ViewModel.SelectByAuthIdAsync(authorizationId);
        }

        private void WireSignatureDelegates(AuthorizeRequestViewModel vm)
        {
            vm.GetSignatureData = () =>
            {
                if (vm.IsDrawTab)
                {
                    if (inkCanvas.Strokes.Count == 0)
                        return (null, null);
                    return (RenderInkCanvasToDataUrl(), "draw");
                }
                else
                {
                    return vm.GetUploadedSig();
                }
            };

            vm.ClearSignatureCanvas = () =>
            {
                inkCanvas.Strokes.Clear();
            };
        }

        private void UpdateDrawHint()
        {
            if (drawHint == null) return;
            drawHint.Visibility = inkCanvas.Strokes.Count == 0
                ? Visibility.Visible
                : Visibility.Collapsed;
        }

        private void OnStrokesChanged()
        {
            var vm = ViewModel;
            if (vm == null || !vm.IsDrawTab) return;

            vm.SetDrawPreview(inkCanvas.Strokes.Count == 0
                ? null
                : RenderInkCanvasToBitmapSource());
        }

        // Renders the InkCanvas strokes as a transparent-background PNG BitmapSource.
        // The transparent background lets the signature float naturally over printed text.
        private BitmapSource RenderInkCanvasToBitmapSource()
        {
            try
            {
                var bounds = GetStrokeBounds();
                if (bounds.IsEmpty) return null;

                // Add a small margin around the tightest bounding box so strokes aren't clipped.
                const double pad = 8;
                int pw = (int)Math.Ceiling(bounds.Width  + pad * 2);
                int ph = (int)Math.Ceiling(bounds.Height + pad * 2);
                if (pw < 1 || ph < 1) return null;

                var rtb = new RenderTargetBitmap(pw, ph, 96, 96, PixelFormats.Pbgra32);

                // Translate a DrawingVisual so only the stroke area is captured.
                var visual = new DrawingVisual();
                using (var ctx = visual.RenderOpen())
                {
                    ctx.PushTransform(new TranslateTransform(
                        -(bounds.X - pad), -(bounds.Y - pad)));
                    foreach (var stroke in inkCanvas.Strokes)
                        stroke.Draw(ctx);
                }
                rtb.Render(visual);
                rtb.Freeze();
                return rtb;
            }
            catch
            {
                return null;
            }
        }

        // Encodes the strokes as a transparent PNG data URL for database storage.
        private string RenderInkCanvasToDataUrl()
        {
            try
            {
                var bmp = RenderInkCanvasToBitmapSource();
                if (bmp == null) return null;

                var encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(bmp));
                using var ms = new MemoryStream();
                encoder.Save(ms);
                return $"data:image/png;base64,{Convert.ToBase64String(ms.ToArray())}";
            }
            catch
            {
                return null;
            }
        }

        private Rect GetStrokeBounds()
        {
            if (inkCanvas.Strokes.Count == 0) return Rect.Empty;
            var bounds = Rect.Empty;
            foreach (var stroke in inkCanvas.Strokes)
            {
                var sb = stroke.GetBounds();
                bounds = bounds.IsEmpty ? sb : Rect.Union(bounds, sb);
            }
            return bounds;
        }
    }
}
