using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Yakult.Inventory.App.Helpers;

namespace Yakult.Inventory.App.Wpf.RepairPortal.Detail.Views
{
    /// <summary>Normalized view of one evidence file, built from either AttachmentDisplayItem
    /// (ticket-level) or PartAttachmentDisplayItem (part-level) — both expose the same shape but
    /// share no common base.</summary>
    public sealed class EvidenceViewerEntry
    {
        public string FileName { get; }
        public string AttachmentType { get; }
        /// <summary>Full file bytes — either supplied up front, or filled in lazily (and cached
        /// here) the first time this entry is actually displayed, via <see cref="BytesLoader"/>.</summary>
        public byte[] Bytes { get; set; }
        /// <summary>When set (and <see cref="Bytes"/> is still null), ShowCurrent() awaits this the
        /// first time the entry is displayed instead of requiring every entry's bytes to be
        /// fetched up front — opening the viewer for a large gallery would otherwise mean
        /// downloading every image/video in it just to show the first one.</summary>
        public Func<Task<byte[]>> BytesLoader { get; }
        /// <summary>Which Part this evidence belongs to (e.g. "Part #1 - Hand"), or "Whole
        /// Equipment" for ticket-level evidence — shown under the filename so it's clear which
        /// Part a given photo/video documents when browsing the aggregate Evidence Gallery.</summary>
        public string SourceLabel { get; }

        public bool IsImage => string.Equals(AttachmentType, "Image", StringComparison.OrdinalIgnoreCase);
        public bool IsVideo => string.Equals(AttachmentType, "Video", StringComparison.OrdinalIgnoreCase);

        public EvidenceViewerEntry(string fileName, string attachmentType, byte[] bytes, string sourceLabel = null)
        {
            FileName = fileName;
            AttachmentType = attachmentType;
            Bytes = bytes;
            SourceLabel = sourceLabel;
        }

        public EvidenceViewerEntry(string fileName, string attachmentType, Func<Task<byte[]>> bytesLoader, string sourceLabel = null)
        {
            FileName = fileName;
            AttachmentType = attachmentType;
            BytesLoader = bytesLoader;
            SourceLabel = sourceLabel;
        }
    }

    /// <summary>Fullscreen/maximized evidence viewer — Image for images, MediaElement (via a temp
    /// file, since MediaElement needs a Uri not a byte[]) for videos with Play/Pause + a seek
    /// slider, and an "Open externally" button for documents. Next/Prev navigate the whole gallery
    /// passed in, not just the item that was clicked. Modal (ShowDialog) to match this module's
    /// established working pattern for secondary WPF windows (a non-modal owned WPF Window in this
    /// hosted-WPF process never reliably receives real Win32 activation/keyboard input).</summary>
    public partial class EvidenceViewerWindow : Window
    {
        private readonly List<EvidenceViewerEntry> _entries;
        private int _index;
        private string _currentTempFile;
        private string _currentDocTempFile;
        private bool _isPlaying;
        private bool _isDraggingSlider;
        private readonly DispatcherTimer _positionTimer;

        // Segoe MDL2 Assets glyphs for the Play/Pause button, built via code point rather than a
        // literal Unicode character in source to avoid any editor/encoding mangling.
        private static readonly string PlayGlyph = char.ConvertFromUtf32(0xE768);
        private static readonly string PauseGlyph = char.ConvertFromUtf32(0xE769);

        public EvidenceViewerWindow(List<EvidenceViewerEntry> entries, int startIndex)
        {
            InitializeComponent();

            _entries = entries ?? new List<EvidenceViewerEntry>();
            _index = Math.Max(0, Math.Min(startIndex, _entries.Count - 1));

            _positionTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) };
            _positionTimer.Tick += (s, e) => SyncSliderToPosition();

            Closed += (s, e) => { _positionTimer.Stop(); CleanupTempFiles(); };

            SpeedCombo.ItemsSource = new[] { "0.25x", "0.5x", "0.75x", "1x", "1.25x", "1.5x", "2x" };
            SpeedCombo.SelectedItem = "1x";

            ShowCurrent();
        }

        private int _loadToken;

        private async void ShowCurrent()
        {
            if (_entries.Count == 0) return;

            StopVideo();

            var entry = _entries[_index];
            var myToken = ++_loadToken;

            ImageHost.Visibility = Visibility.Collapsed;
            VideoHost.Visibility = Visibility.Collapsed;
            DocumentHost.Visibility = Visibility.Collapsed;
            VideoControlsBar.Visibility = Visibility.Collapsed;
            LoadingIndicator.Visibility = Visibility.Collapsed;

            Title = string.IsNullOrWhiteSpace(entry.FileName) ? "Evidence Viewer" : entry.FileName;
            FileNameText.Text = entry.FileName;
            SourceLabelText.Text = entry.SourceLabel;
            SourceLabelText.Visibility = string.IsNullOrWhiteSpace(entry.SourceLabel) ? Visibility.Collapsed : Visibility.Visible;
            PositionText.Text = $"{_index + 1} of {_entries.Count}";

            if (entry.Bytes == null && entry.BytesLoader != null)
            {
                LoadingIndicator.Visibility = Visibility.Visible;
                try
                {
                    var bytes = await entry.BytesLoader();
                    if (myToken != _loadToken) return; // user navigated away before this finished
                    entry.Bytes = bytes;
                }
                catch
                {
                    if (myToken != _loadToken) return;
                    LoadingIndicator.Visibility = Visibility.Collapsed;
                    return; // leave the pane blank rather than show a half-loaded state
                }
            }

            if (myToken != _loadToken) return;
            LoadingIndicator.Visibility = Visibility.Collapsed;

            if (entry.IsImage)
            {
                ImageHost.Visibility = Visibility.Visible;
                ImageHost.Source = BytesToImage(entry.Bytes);
            }
            else if (entry.IsVideo)
            {
                VideoHost.Visibility = Visibility.Visible;
                VideoControlsBar.Visibility = Visibility.Visible;
                PlayVideo(entry);
            }
            else
            {
                DocumentHost.Visibility = Visibility.Visible;
                DocumentNameText.Text = string.IsNullOrWhiteSpace(entry.FileName) ? "Document" : entry.FileName;
            }
        }

        private static BitmapImage BytesToImage(byte[] bytes)
        {
            if (bytes == null || bytes.Length == 0) return null;

            var bitmap = new BitmapImage();
            using (var stream = new MemoryStream(bytes))
            {
                bitmap.BeginInit();
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.StreamSource = stream;
                bitmap.EndInit();
            }
            bitmap.Freeze();
            return bitmap;
        }

        private void PlayVideo(EvidenceViewerEntry entry)
        {
            if (entry.Bytes == null || entry.Bytes.Length == 0) return;

            try
            {
                var ext = Path.GetExtension(entry.FileName);
                if (string.IsNullOrWhiteSpace(ext)) ext = ".mp4";

                _currentTempFile = Path.Combine(Path.GetTempPath(), "RepairEvidence_" + Guid.NewGuid().ToString("N") + ext);
                File.WriteAllBytes(_currentTempFile, entry.Bytes);

                // Phones record portrait video without physically rotating the sensor pixels —
                // the correction is a rotation flag in the container instead. MediaElement doesn't
                // apply that automatically (unlike mobile OS players), so read it ourselves.
                var rotation = VideoRotationReader.GetRotationDegrees(entry.Bytes);
                VideoHost.LayoutTransform = rotation == 0 ? Transform.Identity : new RotateTransform(rotation);

                SeekSlider.Value = 0;
                ElapsedTimeText.Text = "00:00";
                TotalTimeText.Text = "00:00";
                SpeedCombo.SelectedItem = "1x";
                VideoHost.SpeedRatio = 1.0;

                VideoHost.Source = new Uri(_currentTempFile);
                VideoHost.Play();
                _isPlaying = true;
                PlayPauseButton.Content = PauseGlyph;
                _positionTimer.Start();
            }
            catch
            {
                // Non-critical — falls back to a blank video area if the temp file couldn't be
                // written/played.
            }
        }

        private void VideoHost_MediaOpened(object sender, RoutedEventArgs e)
        {
            if (VideoHost.NaturalDuration.HasTimeSpan)
            {
                var total = VideoHost.NaturalDuration.TimeSpan;
                SeekSlider.Maximum = total.TotalSeconds;
                TotalTimeText.Text = FormatTime(total);
            }
        }

        private void VideoHost_MediaEnded(object sender, RoutedEventArgs e)
        {
            _positionTimer.Stop();
            _isPlaying = false;
            PlayPauseButton.Content = PlayGlyph;
            VideoHost.Stop();
            SeekSlider.Value = 0;
            ElapsedTimeText.Text = "00:00";
        }

        private void PlayPause_Click(object sender, RoutedEventArgs e)
        {
            if (VideoHost.Source == null) return;

            if (_isPlaying)
            {
                VideoHost.Pause();
                _positionTimer.Stop();
                _isPlaying = false;
                PlayPauseButton.Content = PlayGlyph;
            }
            else
            {
                VideoHost.Play();
                _positionTimer.Start();
                _isPlaying = true;
                PlayPauseButton.Content = PauseGlyph;
            }
        }

        private void SyncSliderToPosition()
        {
            if (_isDraggingSlider) return;

            SeekSlider.ValueChanged -= SeekSlider_ValueChanged;
            SeekSlider.Value = VideoHost.Position.TotalSeconds;
            SeekSlider.ValueChanged += SeekSlider_ValueChanged;

            ElapsedTimeText.Text = FormatTime(VideoHost.Position);
        }

        private void SeekSlider_PreviewMouseDown(object sender, MouseButtonEventArgs e) => _isDraggingSlider = true;

        private void SeekSlider_PreviewMouseUp(object sender, MouseButtonEventArgs e)
        {
            _isDraggingSlider = false;
            VideoHost.Position = TimeSpan.FromSeconds(SeekSlider.Value);
        }

        private void SeekSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_isDraggingSlider)
                ElapsedTimeText.Text = FormatTime(TimeSpan.FromSeconds(e.NewValue));
        }

        private void SpeedCombo_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (!(SpeedCombo.SelectedItem is string label)) return;
            if (VideoHost.Source == null) return;

            // "0.25x" -> 0.25 etc.
            var numeric = label.TrimEnd('x', 'X');
            if (double.TryParse(numeric, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var ratio))
                VideoHost.SpeedRatio = ratio;
        }

        private static string FormatTime(TimeSpan t) => t.TotalHours >= 1
            ? t.ToString(@"h\:mm\:ss")
            : t.ToString(@"mm\:ss");

        private void StopVideo()
        {
            _positionTimer.Stop();
            _isPlaying = false;

            try
            {
                VideoHost.Stop();
                // Close() (not just Stop()) fully releases the underlying DirectShow media session —
                // leaving that half-torn-down after repeated open/close of video entries is a
                // contributing factor to the render thread eventually failing with
                // COMException 0x88980406 (UCEERR_RENDERTHREADFAILURE) on later, unrelated WPF
                // operations (see Program.cs's RenderOptions.ProcessRenderMode for the other half
                // of this fix).
                VideoHost.Close();
                VideoHost.Source = null;
            }
            catch
            {
                // Non-critical.
            }

            if (_currentTempFile != null)
            {
                try { File.Delete(_currentTempFile); } catch { /* best-effort cleanup */ }
                _currentTempFile = null;
            }
        }

        private void CleanupTempFiles()
        {
            StopVideo();

            if (_currentDocTempFile != null)
            {
                try { File.Delete(_currentDocTempFile); } catch { /* best-effort cleanup */ }
                _currentDocTempFile = null;
            }
        }

        private void OpenExternally_Click(object sender, RoutedEventArgs e)
        {
            if (_entries.Count == 0) return;
            var entry = _entries[_index];
            if (entry.Bytes == null || entry.Bytes.Length == 0) return;

            try
            {
                var ext = Path.GetExtension(entry.FileName);
                if (string.IsNullOrWhiteSpace(ext)) ext = ".dat";

                _currentDocTempFile = Path.Combine(Path.GetTempPath(), "RepairEvidence_" + Guid.NewGuid().ToString("N") + ext);
                File.WriteAllBytes(_currentDocTempFile, entry.Bytes);

                Process.Start(new ProcessStartInfo(_currentDocTempFile) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                System.Windows.Forms.MessageBox.Show("Couldn't open this file: " + ex.Message, "Open Failed",
                    System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Error);
            }
        }

        private void Prev_Click(object sender, RoutedEventArgs e) => MovePrev();
        private void Next_Click(object sender, RoutedEventArgs e) => MoveNext();

        private void MovePrev()
        {
            if (_index <= 0) return;
            _index--;
            ShowCurrent();
        }

        private void MoveNext()
        {
            if (_index >= _entries.Count - 1) return;
            _index++;
            ShowCurrent();
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            switch (e.Key)
            {
                case Key.Escape: Close(); break;
                case Key.Left: MovePrev(); break;
                case Key.Right: MoveNext(); break;
                case Key.Space:
                    if (VideoHost.Visibility == Visibility.Visible) PlayPause_Click(this, null);
                    break;
            }
        }
    }
}
