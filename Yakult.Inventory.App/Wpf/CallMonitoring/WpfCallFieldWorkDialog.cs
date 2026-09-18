using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using Yakult.Inventory.App.Models;
using Yakult.Inventory.App.Models.CallMonitoring;
using Yakult.Inventory.App.Repositories;
using Yakult.Inventory.App.Session;

namespace Yakult.Inventory.App.Wpf.CallMonitoring
{
    public sealed partial class WpfCallFieldWorkDialog : Window
    {
        private readonly ICallMonitoringRepository _repo;
        private readonly int _ticketId;
        private readonly TextBlock _statusLine;
        private readonly StackPanel _visitHost;
        private readonly DataGrid _photoGrid;
        private readonly Image _photoPreviewImage;
        private readonly TextBlock _photoPreviewLabel;
        private readonly Border _photoPreviewBorder;
        private readonly Image _signaturePreviewImage;
        private readonly Border _signaturePreviewBorder;
        private readonly TextBlock _signaturePreviewLabel;
        private readonly Button _scheduleBtn;
        private readonly Button _editFinishBtn;
        private readonly Button _completeBtn;
        private readonly Button _cancelBtn;
        private readonly Button _rescheduleBtn;
        private readonly Button _uploadPhotoBtn;
        private readonly Button _signatureBtn;
        private readonly Button _viewPhotoBtn;
        private readonly Button _viewSignatureBtn;
        private readonly TextBox _notesBox;
        private readonly ComboBox _techCombo;
        private Border _photoCapHintBorder;
        private TextBlock _photoCapHintText;
        private TextBlock _photoCountLabel;
        private CallFieldVisitItem _currentVisit;
        private List<CallFieldVisitAttachmentItem> _photos = new List<CallFieldVisitAttachmentItem>();
        public WpfCallFieldWorkDialog(ICallMonitoringRepository repo, int ticketId, string ticketCode)
        {
            _repo = repo ?? throw new ArgumentNullException(nameof(repo));
            _ticketId = ticketId;
            Title = "Field Work — " + (ticketCode ?? "Ticket #" + ticketId);
            Width = 1180; Height = 780; MinWidth = 960; MinHeight = 620;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            ShowInTaskbar = false;
            Background = BrushFromRgb(241, 245, 249);
            try
            {
                Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri("/Yakult.Inventory.App;component/Wpf/RepairPortal/RepairPortalStyles.xaml", UriKind.Relative) });
                Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri("/Yakult.Inventory.App;component/Wpf/Shared/Styles/ListPageStyles.xaml", UriKind.Relative) });
            }
            catch { }
            var root = new DockPanel();
            Content = root;
            // Header — Tier 2 popup with red/blue accent, like Repair Portal detail
            var header = new Border { Style = TryFindStyle("PopupHeaderBar") ?? new Style(typeof(Border)), MinHeight = 84 };
            if (header.Style == null || header.Style.Setters.Count == 0) { header.Background = Brushes.White; header.BorderBrush = BrushFromRgb(226,232,240); header.BorderThickness = new Thickness(0,0,0,1); header.Padding = new Thickness(24,18,24,18); }
            DockPanel.SetDock(header, Dock.Top);
            root.Children.Add(header);
            var headerGrid = new Grid();
            header.Child = headerGrid;
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(4) });
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var accent = new Border { Style = TryFindStyle("PopupTitleAccentBar") ?? new Style(typeof(Border)), VerticalAlignment = VerticalAlignment.Stretch };
            if (accent.Style == null || accent.Style.Setters.Count == 0) { accent.Width = 4; accent.CornerRadius = new CornerRadius(2); accent.Margin = new Thickness(0,0,16,0); accent.Background = (Brush)FindResourceOrDefault("RepairIdentityAccentBrush", new LinearGradientBrush{ StartPoint=new Point(0,0), EndPoint=new Point(0,1), GradientStops={ new GradientStop(Color.FromRgb(0xDC,0x26,0x26),0), new GradientStop(Color.FromRgb(0x25,0x63,0xEB),1)}}); }
            Grid.SetColumn(accent, 0); headerGrid.Children.Add(accent);
            var titleStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            Grid.SetColumn(titleStack, 1); headerGrid.Children.Add(titleStack);
            var titleRow = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            titleStack.Children.Add(titleRow);
            var glyph = new TextBlock { Text = "\uE80F", FontFamily = new FontFamily("Segoe MDL2 Assets"), FontSize = 22, Foreground = BrushFromRgb(37,99,235), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0,0,10,0) };
            titleRow.Children.Add(glyph);
            var title = new TextBlock { Text = "Field Work", FontSize = 20, FontWeight = FontWeights.Bold, Foreground = BrushFromRgb(15,23,42), VerticalAlignment = VerticalAlignment.Center };
            titleRow.Children.Add(title);
            var badge = new Border { CornerRadius = new CornerRadius(8), Background = BrushFromRgb(219,234,254), Padding = new Thickness(8,3,8,3), Margin = new Thickness(12,0,0,0), VerticalAlignment = VerticalAlignment.Center };
            badge.Child = new TextBlock { Text = ticketCode ?? $"#{ticketId}", FontSize = 11, FontWeight = FontWeights.SemiBold, Foreground = BrushFromRgb(29,78,216) };
            titleRow.Children.Add(badge);
            var subtitle = new TextBlock { Text = "One visit per ticket • Schedule → Complete/Cancel  •  Photos + customer signature", FontSize = 12.5, Foreground = BrushFromRgb(100,116,139), Margin = new Thickness(2,6,0,0), TextWrapping = TextWrapping.Wrap };
            titleStack.Children.Add(subtitle);
            var closeBtn = new Button { Content = "Close", MinWidth = 100, Padding = new Thickness(16,9,16,9), Cursor = Cursors.Hand, FontWeight = FontWeights.SemiBold, FontSize=13 };
            var closeStyle = TryFindStyle("RepairSecondaryBtn");
            if (closeStyle != null) closeBtn.Style = closeStyle; else { closeBtn.Background = BrushFromRgb(52,73,94); closeBtn.Foreground = Brushes.White; closeBtn.BorderThickness = new Thickness(0); }
            closeBtn.Click += (_, __) => Close();
            Grid.SetColumn(closeBtn, 2); headerGrid.Children.Add(closeBtn);
            var footer = new DockPanel { Height = 62, Background = Brushes.White, LastChildFill = false };
            DockPanel.SetDock(footer, Dock.Bottom);
            root.Children.Add(footer);
            _statusLine = new TextBlock { Text = "Loading field work...", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(20,0,0,0), Foreground = BrushFromRgb(100,116,139), FontSize=12.5 };
            footer.Children.Add(_statusLine);
            var buildTag = new TextBlock { Text = "footer v3", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8,0,0,0), Foreground = BrushFromRgb(180,190,205), FontSize = 10 };
            footer.Children.Add(buildTag);
            var actions = new WrapPanel { HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(16,10,16,10) };
            DockPanel.SetDock(actions, Dock.Right);
            footer.Children.Add(actions);
            _scheduleBtn = CreateFooterButton("+ Schedule", Color.FromRgb(37, 99, 235));
            _editFinishBtn = CreateFooterButton("Edit Finish", Color.FromRgb(124, 58, 237));
            _completeBtn = CreateFooterButton("Complete", Color.FromRgb(22, 163, 74));
            _cancelBtn = CreateFooterButton("Cancel", Color.FromRgb(220, 38, 38));
            _rescheduleBtn = CreateFooterButton("Reschedule", Color.FromRgb(245, 158, 11));
            _scheduleBtn.Click += async (_, __) => await ScheduleAsync();
            _editFinishBtn.Click += (_, __) => OpenFinishTimeEditor();
            _completeBtn.Click += async (_, __) => await SetStatusAsync("Completed");
            _cancelBtn.Click += async (_, __) => await SetStatusAsync("Cancelled");
            _rescheduleBtn.Click += async (_, __) => await SetStatusAsync("Scheduled");
            _scheduleBtn.ToolTip = "Create the one field visit for this ticket.";
            _editFinishBtn.ToolTip = "View or correct the recorded finish time.";
            _completeBtn.ToolTip = "Mark the scheduled visit as Completed.";
            _cancelBtn.ToolTip = "Cancel the scheduled visit.";
            _rescheduleBtn.ToolTip = "Reopen a cancelled visit with a new slot.";
            actions.Children.Add(_scheduleBtn);
            actions.Children.Add(_editFinishBtn);
            actions.Children.Add(_completeBtn);
            actions.Children.Add(_cancelBtn);
            actions.Children.Add(_rescheduleBtn);
            var bodyScroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Padding = new Thickness(20), Background = BrushFromRgb(241,245,249) };
            root.Children.Add(bodyScroll);
            var bodyStack = new StackPanel();
            bodyScroll.Content = bodyStack;
            // Schedule card — FeedCardStyle with icon header
            var scheduleCard = NewCard("Schedule / Notes", "\uE77B", "Pick technician, add notes, then Schedule — one visit per ticket");
            bodyStack.Children.Add(scheduleCard);
            var scheduleInner = (StackPanel)scheduleCard.Child;
            // Use extracted ScheduleSection (now 3-col: tech + date + time, notes below)
            _techCombo = new ComboBox { MinHeight = 36, DisplayMemberPath = "Name", Padding = new Thickness(8,6,8,6), Background = Brushes.White, BorderBrush = BrushFromRgb(203,213,225), BorderThickness = new Thickness(1), Foreground = BrushFromRgb(15,23,42), FontSize = 13 };
            _notesBox = new TextBox { MinHeight = 36, AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, Padding = new Thickness(10,8,10,8), BorderBrush = BrushFromRgb(203,213,225), BorderThickness = new Thickness(1), Background = Brushes.White, Foreground = BrushFromRgb(30,41,59), FontSize = 13 };
            var schedForm = BuildScheduleSection();
            scheduleInner.Children.Add(schedForm);
            _scheduleModeHint = new TextBlock { FontSize = 12, FontWeight = FontWeights.SemiBold, TextWrapping = TextWrapping.Wrap, VerticalAlignment = VerticalAlignment.Center };
            _scheduleModeHintBorder = new Border { CornerRadius = new CornerRadius(10), Padding = new Thickness(12,8,12,8), Margin = new Thickness(0,10,0,0), BorderThickness = new Thickness(1), Child = _scheduleModeHint };
            scheduleInner.Children.Add(_scheduleModeHintBorder);
            // Notes below the 3-col grid (as in original 2-col, but now stacked)
            var notesStack2 = new StackPanel { Margin = new Thickness(0,12,0,0) };
            notesStack2.Children.Add(new TextBlock { Text = "Notes (optional)", FontSize = 11, FontWeight = FontWeights.SemiBold, Foreground = BrushFromRgb(100,116,139), Margin = new Thickness(0,0,0,6) });
            notesStack2.Children.Add(_notesBox);
            scheduleInner.Children.Add(notesStack2);
            // Visit card — main focus, with status pill + simplified info
            var visitCard = NewCard("Visit Status", "\uE787", "Scheduled → Completed/Cancelled");
            bodyStack.Children.Add(visitCard);
            _visitHost = new StackPanel { Margin = new Thickness(0,8,0,0) };
            ((StackPanel)visitCard.Child).Children.Add(_visitHost);
            // Evidence card — photos + signature (polished WPF table + cleaner buttons)
            var photoCard = NewCard("Evidence", "\uEB9F", "Photos + customer signature (up to 20 photos, PNG/JPG ≤20 MB)");
            bodyStack.Children.Add(photoCard);
            var photoInner = (StackPanel)photoCard.Child;
            var photoHeader = new DockPanel { LastChildFill = false, Margin = new Thickness(0,10,0,0) };
            photoInner.Children.Add(photoHeader);
            var photoBtns = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
            DockPanel.SetDock(photoBtns, Dock.Right); photoHeader.Children.Add(photoBtns);
            // Cleaner segmented toolbar — ghost secondary for add, solid primary for view
            _uploadPhotoBtn = CreatePillButton("Add Photo", "\uEB9F", BrushFromRgb(37,99,235), isPrimary:true);
            _uploadPhotoBtn.Click += async (_, __) => await UploadPhotoAsync();
            _signatureBtn = CreatePillButton("Signature", "\uE70B", BrushFromRgb(124,58,237), isPrimary:false);
            _signatureBtn.Click += async (_, __) => await SaveSignatureAsync();
            _viewPhotoBtn = CreatePillButton("View", "\uE8A7", BrushFromRgb(37,99,235), isPrimary:true);
            _viewPhotoBtn.Click += async (_, __) => await PreviewSelectedPhotoAsync();
            _viewSignatureBtn = CreatePillButton("View Sig", "\uE73E", BrushFromRgb(16,185,129), isPrimary:true);
            _viewSignatureBtn.Click += async (_, __) => await PreviewSignatureAsync();
            photoBtns.Children.Add(_uploadPhotoBtn); photoBtns.Children.Add(_signatureBtn); photoBtns.Children.Add(_viewPhotoBtn); photoBtns.Children.Add(_viewSignatureBtn);
            // Modern table container — rounded, subtle shadow, header pill styling
            var tableBorder = new Border { CornerRadius = new CornerRadius(12), BorderBrush = BrushFromRgb(226,232,240), BorderThickness = new Thickness(1), Background = Brushes.White, Margin = new Thickness(0,10,0,0), ClipToBounds = true, SnapsToDevicePixels = true };
            try { tableBorder.Effect = new System.Windows.Media.Effects.DropShadowEffect { BlurRadius = 14, ShadowDepth = 2, Opacity = 0.06, Direction = 270, Color = Colors.Black }; } catch { }
            _photoGrid = new DataGrid { AutoGenerateColumns = false, IsReadOnly = true, CanUserAddRows = false, CanUserDeleteRows = false, HeadersVisibility = DataGridHeadersVisibility.Column, GridLinesVisibility = DataGridGridLinesVisibility.None, BorderThickness = new Thickness(0), RowHeaderWidth = 0, Background = Brushes.White, AlternatingRowBackground = BrushFromRgb(248,250,252), MinHeight = 160, MaxHeight = 260, FontSize = 12.5, ColumnWidth = new DataGridLength(1, DataGridLengthUnitType.Auto), CanUserResizeColumns = true, CanUserSortColumns = true, SelectionMode = DataGridSelectionMode.Single, SelectionUnit = DataGridSelectionUnit.FullRow, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
            // Header style — pill-like, muted slate, semibold
            var headerStyle = new Style(typeof(System.Windows.Controls.Primitives.DataGridColumnHeader));
            headerStyle.Setters.Add(new Setter(Control.BackgroundProperty, BrushFromRgb(248,250,252)));
            headerStyle.Setters.Add(new Setter(Control.ForegroundProperty, BrushFromRgb(71,85,105)));
            headerStyle.Setters.Add(new Setter(Control.FontWeightProperty, FontWeights.SemiBold));
            headerStyle.Setters.Add(new Setter(Control.FontSizeProperty, 11.0));
            headerStyle.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(14,10,14,10)));
            headerStyle.Setters.Add(new Setter(Control.BorderBrushProperty, BrushFromRgb(226,232,240)));
            headerStyle.Setters.Add(new Setter(Control.BorderThicknessProperty, new Thickness(0,0,0,1)));
            headerStyle.Setters.Add(new Setter(Control.HorizontalContentAlignmentProperty, HorizontalAlignment.Left));
            _photoGrid.ColumnHeaderStyle = headerStyle;
            var cellStyle = new Style(typeof(DataGridCell));
            cellStyle.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(12,9,12,9)));
            cellStyle.Setters.Add(new Setter(Control.BorderThicknessProperty, new Thickness(0)));
            cellStyle.Setters.Add(new Setter(Control.VerticalContentAlignmentProperty, VerticalAlignment.Center));
            cellStyle.Setters.Add(new Setter(Control.FontSizeProperty, 12.5));
            _photoGrid.CellStyle = cellStyle;
            var rowStyle = new Style(typeof(DataGridRow));
            rowStyle.Setters.Add(new Setter(Control.MinHeightProperty, 38.0));
            rowStyle.Setters.Add(new Setter(Control.BorderBrushProperty, BrushFromRgb(241,245,249)));
            rowStyle.Setters.Add(new Setter(Control.BorderThicknessProperty, new Thickness(0,0,0,1)));
            _photoGrid.RowStyle = rowStyle;
            // File column — icon + name with ellipsis
            var fileCol = new DataGridTemplateColumn { Header = "File", Width = new DataGridLength(1, DataGridLengthUnitType.Star), MinWidth = 180, SortMemberPath = "FileName" };
            var fileFactory = new FrameworkElementFactory(typeof(StackPanel));
            fileFactory.SetValue(StackPanel.OrientationProperty, Orientation.Horizontal);
            fileFactory.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
            var iconFactory = new FrameworkElementFactory(typeof(TextBlock));
            iconFactory.SetValue(TextBlock.TextProperty, "\uE91B");
            iconFactory.SetValue(TextBlock.FontFamilyProperty, new FontFamily("Segoe MDL2 Assets"));
            iconFactory.SetValue(TextBlock.ForegroundProperty, BrushFromRgb(100,116,139));
            iconFactory.SetValue(TextBlock.FontSizeProperty, 14.0);
            iconFactory.SetValue(TextBlock.VerticalAlignmentProperty, VerticalAlignment.Center);
            iconFactory.SetValue(FrameworkElement.MarginProperty, new Thickness(0,0,8,0));
            fileFactory.AppendChild(iconFactory);
            var nameFactory = new FrameworkElementFactory(typeof(TextBlock));
            nameFactory.SetBinding(TextBlock.TextProperty, new System.Windows.Data.Binding("FileName"));
            nameFactory.SetValue(TextBlock.TextTrimmingProperty, TextTrimming.CharacterEllipsis);
            nameFactory.SetValue(TextBlock.VerticalAlignmentProperty, VerticalAlignment.Center);
            nameFactory.SetValue(TextBlock.ForegroundProperty, BrushFromRgb(15,23,42));
            nameFactory.SetValue(TextBlock.FontWeightProperty, FontWeights.Medium);
            fileFactory.AppendChild(nameFactory);
            fileCol.CellTemplate = new DataTemplate { VisualTree = fileFactory };
            _photoGrid.Columns.Add(fileCol);
            // Size column — pill badge
            var sizeCol = new DataGridTemplateColumn { Header = "Size", Width = new DataGridLength(110), SortMemberPath = "FileSizeBytes" };
            var sizeBorderFactory = new FrameworkElementFactory(typeof(Border));
            sizeBorderFactory.SetValue(Border.CornerRadiusProperty, new CornerRadius(8));
            sizeBorderFactory.SetValue(Border.BackgroundProperty, BrushFromRgb(241,245,249));
            sizeBorderFactory.SetValue(Border.PaddingProperty, new Thickness(8,3,8,3));
            sizeBorderFactory.SetValue(Border.HorizontalAlignmentProperty, HorizontalAlignment.Left);
            var sizeTextFactory = new FrameworkElementFactory(typeof(TextBlock));
            sizeTextFactory.SetValue(TextBlock.FontSizeProperty, 11.5);
            sizeTextFactory.SetValue(TextBlock.ForegroundProperty, BrushFromRgb(51,65,85));
            sizeTextFactory.SetValue(TextBlock.FontWeightProperty, FontWeights.SemiBold);
            var sizeBinding = new System.Windows.Data.Binding("FileSizeBytes") { Converter = new FileSizeConverter() };
            sizeTextFactory.SetBinding(TextBlock.TextProperty, sizeBinding);
            sizeBorderFactory.AppendChild(sizeTextFactory);
            sizeCol.CellTemplate = new DataTemplate { VisualTree = sizeBorderFactory };
            _photoGrid.Columns.Add(sizeCol);
            // Uploaded column
            var uploadedCol = new DataGridTemplateColumn { Header = "Uploaded", Width = new DataGridLength(170), SortMemberPath = "UploadedAt" };
            var uploadedFactory = new FrameworkElementFactory(typeof(TextBlock));
            uploadedFactory.SetBinding(TextBlock.TextProperty, new System.Windows.Data.Binding("UploadedAt") { StringFormat = "MMM d, yyyy h:mm tt" });
            uploadedFactory.SetValue(TextBlock.ForegroundProperty, BrushFromRgb(71,85,105));
            uploadedFactory.SetValue(TextBlock.FontSizeProperty, 12.0);
            uploadedCol.CellTemplate = new DataTemplate { VisualTree = uploadedFactory };
            _photoGrid.Columns.Add(uploadedCol);
            // By column — avatar initial + name
            var byCol = new DataGridTemplateColumn { Header = "By", Width = new DataGridLength(140), SortMemberPath = "UploadedByName" };
            var byPanelFactory = new FrameworkElementFactory(typeof(StackPanel));
            byPanelFactory.SetValue(StackPanel.OrientationProperty, Orientation.Horizontal);
            byPanelFactory.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
            var avatarFactory = new FrameworkElementFactory(typeof(Border));
            avatarFactory.SetValue(Border.WidthProperty, 24.0);
            avatarFactory.SetValue(Border.HeightProperty, 24.0);
            avatarFactory.SetValue(Border.CornerRadiusProperty, new CornerRadius(12));
            avatarFactory.SetValue(Border.BackgroundProperty, BrushFromRgb(219,234,254));
            avatarFactory.SetValue(FrameworkElement.MarginProperty, new Thickness(0,0,8,0));
            var avatarTextFactory = new FrameworkElementFactory(typeof(TextBlock));
            avatarTextFactory.SetBinding(TextBlock.TextProperty, new System.Windows.Data.Binding("UploadedByName") { Converter = new InitialConverter() });
            avatarTextFactory.SetValue(TextBlock.FontSizeProperty, 10.0);
            avatarTextFactory.SetValue(TextBlock.FontWeightProperty, FontWeights.Bold);
            avatarTextFactory.SetValue(TextBlock.ForegroundProperty, BrushFromRgb(37,99,235));
            avatarTextFactory.SetValue(TextBlock.HorizontalAlignmentProperty, HorizontalAlignment.Center);
            avatarTextFactory.SetValue(TextBlock.VerticalAlignmentProperty, VerticalAlignment.Center);
            avatarTextFactory.SetValue(TextBlock.TextAlignmentProperty, TextAlignment.Center);
            avatarFactory.AppendChild(avatarTextFactory);
            byPanelFactory.AppendChild(avatarFactory);
            var byNameFactory = new FrameworkElementFactory(typeof(TextBlock));
            byNameFactory.SetBinding(TextBlock.TextProperty, new System.Windows.Data.Binding("UploadedByName"));
            byNameFactory.SetValue(TextBlock.ForegroundProperty, BrushFromRgb(51,65,85));
            byNameFactory.SetValue(TextBlock.TextTrimmingProperty, TextTrimming.CharacterEllipsis);
            byPanelFactory.AppendChild(byNameFactory);
            byCol.CellTemplate = new DataTemplate { VisualTree = byPanelFactory };
            _photoGrid.Columns.Add(byCol);
            _photoGrid.SelectionChanged += async (_, __) => await PreviewSelectedPhotoAsync();
            _photoGrid.MouseDoubleClick += async (_, __) => await OpenSelectedPhotoFullAsync();
            tableBorder.Child = _photoGrid;
            photoInner.Children.Add(tableBorder);
            var photoMetaRow = new DockPanel { LastChildFill = false, Margin = new Thickness(0,8,0,0) };
            photoInner.Children.Add(photoMetaRow);
            _photoCountLabel = new TextBlock { Text = "0/20 photos", Foreground = BrushFromRgb(100,116,139), FontSize = 11, FontWeight = FontWeights.SemiBold, VerticalAlignment = VerticalAlignment.Center };
            DockPanel.SetDock(_photoCountLabel, Dock.Left);
            photoMetaRow.Children.Add(_photoCountLabel);
            _photoCapHintBorder = new Border { CornerRadius = new CornerRadius(8), Background = BrushFromRgb(254,242,242), BorderBrush = BrushFromRgb(252,165,165), BorderThickness = new Thickness(1), Padding = new Thickness(10,6,10,6), Margin = new Thickness(12,0,0,0), Visibility = Visibility.Collapsed, VerticalAlignment = VerticalAlignment.Center };
            _photoCapHintText = new TextBlock { Text = "Limit reached (20/20)", Foreground = BrushFromRgb(185,28,28), FontWeight = FontWeights.SemiBold, FontSize = 11 };
            _photoCapHintBorder.Child = _photoCapHintText;
            DockPanel.SetDock(_photoCapHintBorder, Dock.Right);
            photoMetaRow.Children.Add(_photoCapHintBorder);
            _photoPreviewLabel = new TextBlock { Text = "Select a photo to preview", Foreground = BrushFromRgb(100,116,139), FontStyle = FontStyles.Italic, Margin = new Thickness(0,8,0,4), FontSize=11 };
            photoInner.Children.Add(_photoPreviewLabel);
            _photoPreviewBorder = new Border { Background = BrushFromRgb(248,250,252), CornerRadius = new CornerRadius(10), BorderBrush = BrushFromRgb(226,232,240), BorderThickness = new Thickness(1), Padding = new Thickness(8), MinHeight = 140, Visibility = Visibility.Collapsed, Margin = new Thickness(0,0,0,8) };
            _photoPreviewImage = new Image { Stretch = Stretch.Uniform, MaxHeight = 220, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
            _photoPreviewBorder.Child = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, Content = _photoPreviewImage, MaxHeight = 220 };
            photoInner.Children.Add(_photoPreviewBorder);
            _signaturePreviewLabel = new TextBlock { Text = "No signature yet — capture before completing visit", Foreground = BrushFromRgb(100,116,139), FontStyle = FontStyles.Italic, Margin = new Thickness(0,8,0,4), FontSize=11 };
            photoInner.Children.Add(_signaturePreviewLabel);
            _signaturePreviewBorder = new Border { Background = Brushes.White, CornerRadius = new CornerRadius(10), BorderBrush = BrushFromRgb(203,213,225), BorderThickness = new Thickness(1), Padding = new Thickness(8), MinHeight = 100, Visibility = Visibility.Collapsed, Margin = new Thickness(0,0,0,4) };
            _signaturePreviewImage = new Image { Stretch = Stretch.Uniform, MaxHeight = 120, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
            _signaturePreviewBorder.Child = _signaturePreviewImage;
            photoInner.Children.Add(_signaturePreviewBorder);
            var emptyPhotoHint = new Border { Background = BrushFromRgb(248,250,252), CornerRadius = new CornerRadius(10), Padding = new Thickness(14), Margin = new Thickness(0,10,0,0), BorderBrush = BrushFromRgb(226,232,240), BorderThickness = new Thickness(1) };
            emptyPhotoHint.Child = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            ((StackPanel)emptyPhotoHint.Child).Children.Add(new TextBlock { Text = "\uE91B", FontFamily = new FontFamily("Segoe MDL2 Assets"), FontSize = 16, Foreground = BrushFromRgb(148,163,184), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0,0,8,0) });
            ((StackPanel)emptyPhotoHint.Child).Children.Add(new TextBlock { Text = "No photos yet — add photos and signature before completing the visit.", Foreground = BrushFromRgb(100,116,139), FontSize = 12, VerticalAlignment = VerticalAlignment.Center, TextWrapping = TextWrapping.Wrap });
            photoInner.Children.Add(emptyPhotoHint);
            Loaded += async (_, __) => await LoadAsync();
        }
        private Border NewCard(string title, string glyph, string subtitle)
        {
            var card = new Border { CornerRadius = new CornerRadius(16), Padding = new Thickness(18), Margin = new Thickness(0,0,0,16), Background = Brushes.White, BorderBrush = BrushFromRgb(226,232,240), BorderThickness = new Thickness(1) };
            var style = TryFindStyle("FeedCardStyle");
            if (style != null) card.Style = style;
            try { card.Effect = new System.Windows.Media.Effects.DropShadowEffect { BlurRadius = 12, ShadowDepth = 3, Opacity = 0.08, Direction = 270, Color = Colors.Black }; } catch { }
            var stack = new StackPanel();
            card.Child = stack;
            var header = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0,0,0,4) };
            stack.Children.Add(header);
            header.Children.Add(new TextBlock { Text = glyph, FontFamily = new FontFamily("Segoe MDL2 Assets"), FontSize = 18, Foreground = BrushFromRgb(37,99,235), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0,0,10,0) });
            header.Children.Add(new TextBlock { Text = title, FontSize = 14, FontWeight = FontWeights.Bold, Foreground = BrushFromRgb(15,23,42), VerticalAlignment = VerticalAlignment.Center });
            if (!string.IsNullOrWhiteSpace(subtitle))
                stack.Children.Add(new TextBlock { Text = subtitle, FontSize = 11.5, Foreground = BrushFromRgb(100,116,139), Margin = new Thickness(2,2,0,0), TextWrapping = TextWrapping.Wrap });
            return card;
        }
        private async Task LoadAsync()
        {
            _statusLine.Text = "Loading...";
            try
            {
                var techs = (await _repo.GetItEmployeesByDepartmentNameAsync("Information Technology") ?? new List<LookupItem>()).ToList();
                _techCombo.ItemsSource = techs;
                _techCombo.SelectedIndex = -1;
                var visits = await _repo.GetFieldVisitsAsync(_ticketId);
                _currentVisit = visits.FirstOrDefault();
                // Default technician to the ticket's current assignee (AssignedToEmpId) when no visit / no technician yet
                try
                {
                    int? defaultEmpId = _currentVisit?.TechnicianEmpId;
                    if (!defaultEmpId.HasValue)
                    {
                        defaultEmpId = await _repo.GetTicketAssignedEmployeeIdAsync(_ticketId);
                        if (!defaultEmpId.HasValue)
                        {
                            var t = await _repo.GetTicketByIdAsync(_ticketId);
                            defaultEmpId = t?.AssignedToEmpId;
                        }
                    }
                    if (defaultEmpId.HasValue)
                    {
                        var match = techs.FirstOrDefault(x => x.Id == defaultEmpId.Value);
                        if (match == null)
                        {
                            // Assigned tech not in IT list — still show them so field work isn't blank
                            try
                            {
                                var prof = await _repo.GetEmployeeProfileLookupByEmpIdAsync(defaultEmpId.Value);
                                var profName = prof?.EmployeeName;
                                if (prof != null && !string.IsNullOrWhiteSpace(profName))
                                {
                                    var injected = new LookupItem { Id = prof.EmpId, Name = profName, DisplayName = profName };
                                    techs.Insert(0, injected);
                                    _techCombo.ItemsSource = null;
                                    _techCombo.ItemsSource = techs;
                                    match = injected;
                                }
                                else
                                {
                                    // Fallback: at least show EmpId so user sees assignment
                                    var fallback = new LookupItem { Id = defaultEmpId.Value, Name = $"Emp #{defaultEmpId.Value}", DisplayName = $"Emp #{defaultEmpId.Value}" };
                                    techs.Insert(0, fallback);
                                    _techCombo.ItemsSource = null;
                                    _techCombo.ItemsSource = techs;
                                    match = fallback;
                                }
                            }
                            catch { }
                        }
                        if (match != null)
                            _techCombo.SelectedItem = match;
                    }
                }
                catch { }
                RenderVisit();
                _photos = _currentVisit == null
                    ? new System.Collections.Generic.List<CallFieldVisitAttachmentItem>()
                    : await _repo.GetFieldVisitAttachmentsByVisitAsync(_currentVisit.FieldVisitId);
                _photoGrid.ItemsSource = _photos;
                if (_photos.Count > 0)
                {
                    _photoGrid.SelectedIndex = 0;
                    await PreviewSelectedPhotoAsync();
                }
                else
                {
                    _photoPreviewBorder.Visibility = Visibility.Collapsed;
                    _photoPreviewLabel.Text = "No photos attached to this visit.";
                    _photoPreviewImage.Source = null;
                }
                await PreviewSignatureAsync();
                UpdatePhotoCapState();
                _statusLine.Text = _currentVisit == null ? "No field visit yet — Schedule one (one per ticket)." : $"Status: {(_currentVisit.Status ?? "-")} • {visits.Count} visit(s) • {(_currentVisit.HasSignature ? "Signature on file" : "No signature")} • {_photos.Count}/20 photo(s)";
            }
            catch (Exception ex)
            {
                _statusLine.Text = "Load failed: " + ex.Message;
                Yakult.Inventory.App.Core.Logger.LogError($"FieldWork dialog LoadAsync failed (TicketId={_ticketId}).", ex);
                MessageBox.Show(this, ex.ToString(), "Field Work Load Failed", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        private void RenderVisit()
        {
            _visitHost.Children.Clear();
            if (_currentVisit == null)
            {
                var empty = new Border { Background = BrushFromRgb(248,250,252), CornerRadius = new CornerRadius(12), Padding = new Thickness(16), Margin = new Thickness(0,12,0,0), BorderBrush = BrushFromRgb(226,232,240), BorderThickness = new Thickness(1) };
                var row = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
                empty.Child = row;
                row.Children.Add(new TextBlock { Text = "\uE946", FontFamily = new FontFamily("Segoe MDL2 Assets"), FontSize = 20, Foreground = BrushFromRgb(148,163,184), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0,0,12,0) });
                row.Children.Add(new TextBlock { Text = "No field visit scheduled. Pick a technician, add a note, and click Schedule — one visit per ticket.", Foreground = BrushFromRgb(100,116,139), FontStyle = FontStyles.Italic, TextWrapping = TextWrapping.Wrap, VerticalAlignment = VerticalAlignment.Center, FontSize = 13 });
                _visitHost.Children.Add(empty);
                UpdateButtons(null);
                return;
            }
            var v = _currentVisit;
            _visitHost.Children.Add(BuildOverviewPillRow(v));
            _visitHost.Children.Add(BuildOverviewGrid(v));
            // Status card (centered)
            var statusCard = new Border { Background = BrushFromRgb(248,250,252), CornerRadius = new CornerRadius(12), Padding = new Thickness(14,12,14,12), Margin = new Thickness(0,14,0,0), BorderBrush = BrushFromRgb(226,232,240), BorderThickness = new Thickness(1) };
            _visitHost.Children.Add(statusCard);
            var statusStack = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center };
            statusCard.Child = statusStack;
            statusStack.Children.Add(new TextBlock { Text = "Current Status", FontSize = 11, Foreground = BrushFromRgb(100,116,139), TextAlignment = TextAlignment.Center, Margin = new Thickness(0,0,0,8) });
            var statusPill = new Border { CornerRadius = new CornerRadius(16), Padding = new Thickness(20,8,20,8), HorizontalAlignment = HorizontalAlignment.Center, Background = GetFieldWorkStatusBrush(v.Status) };
            statusPill.Child = new TextBlock { Text = (v.Status ?? "-").ToUpperInvariant(), Foreground = Brushes.White, FontWeight = FontWeights.Bold, FontSize = 14 };
            statusStack.Children.Add(statusPill);
            statusStack.Children.Add(new TextBlock { Text = GetStatusHint(v.Status), FontSize = 11, Foreground = BrushFromRgb(100,116,139), TextAlignment = TextAlignment.Center, Margin = new Thickness(0,8,0,0), TextWrapping = TextWrapping.Wrap });
            if (v.Status != null && v.Status.Equals("Cancelled", StringComparison.OrdinalIgnoreCase))
                _visitHost.Children.Add(BuildCancelledHint());
            var notesCard = BuildNotesCard(v.Notes);
            if (notesCard != null) _visitHost.Children.Add(notesCard);
            _visitHost.Children.Add(BuildSignatureRow(v.HasSignature));
            UpdateButtons(v.Status);
        }
        private static Button CreateFooterButton(string text, Color baseColor)
        {
            var normal = new SolidColorBrush(baseColor); normal.Freeze();
            var hover = new SolidColorBrush(ShadeFooterButton(baseColor, 0.88)); hover.Freeze();
            var pressed = new SolidColorBrush(ShadeFooterButton(baseColor, 0.76)); pressed.Freeze();
            // Unmistakable disabled look: pale fill + slate text, nothing like
            // the colored enabled state.
            var disabledBg = new SolidColorBrush(Color.FromRgb(226, 232, 240)); disabledBg.Freeze();
            var disabledFg = new SolidColorBrush(Color.FromRgb(148, 163, 184)); disabledFg.Freeze();

            var template = new ControlTemplate(typeof(Button));
            var border = new FrameworkElementFactory(typeof(Border));
            border.SetValue(Border.CornerRadiusProperty, new CornerRadius(9));
            border.SetValue(Border.BackgroundProperty, new TemplateBindingExtension(Button.BackgroundProperty));
            border.SetValue(Border.PaddingProperty, new Thickness(16, 0, 16, 0));
            var presenter = new FrameworkElementFactory(typeof(ContentPresenter));
            presenter.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Center);
            presenter.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
            border.AppendChild(presenter);
            template.VisualTree = border;

            var style = new Style(typeof(Button));
            style.Setters.Add(new Setter(Control.BackgroundProperty, normal));
            style.Setters.Add(new Setter(Control.ForegroundProperty, Brushes.White));
            style.Setters.Add(new Setter(Control.HeightProperty, 36.0));
            style.Setters.Add(new Setter(Control.MinWidthProperty, 112.0));
            style.Setters.Add(new Setter(FrameworkElement.MarginProperty, new Thickness(8, 0, 0, 0)));
            style.Setters.Add(new Setter(Control.FontWeightProperty, FontWeights.SemiBold));
            style.Setters.Add(new Setter(Control.FontSizeProperty, 13.0));
            style.Setters.Add(new Setter(Control.CursorProperty, Cursors.Hand));
            style.Setters.Add(new Setter(Control.TemplateProperty, template));
            style.Setters.Add(new Setter(UIElement.FocusableProperty, true));
            var hoverTrigger = new Trigger { Property = UIElement.IsMouseOverProperty, Value = true };
            hoverTrigger.Setters.Add(new Setter(Control.BackgroundProperty, hover));
            style.Triggers.Add(hoverTrigger);
            var pressedTrigger = new Trigger { Property = Button.IsPressedProperty, Value = true };
            pressedTrigger.Setters.Add(new Setter(Control.BackgroundProperty, pressed));
            style.Triggers.Add(pressedTrigger);
            var disabledTrigger = new Trigger { Property = UIElement.IsEnabledProperty, Value = false };
            disabledTrigger.Setters.Add(new Setter(Control.BackgroundProperty, disabledBg));
            disabledTrigger.Setters.Add(new Setter(Control.ForegroundProperty, disabledFg));
            style.Triggers.Add(disabledTrigger);

            var btn = new Button { Content = text, Style = style };
            btn.ToolTip = text;
            return btn;
        }

        private static Color ShadeFooterButton(Color c, double factor)
        {
            return Color.FromRgb(
                (byte)Math.Max(0, Math.Min(255, c.R * factor)),
                (byte)Math.Max(0, Math.Min(255, c.G * factor)),
                (byte)Math.Max(0, Math.Min(255, c.B * factor)));
        }

        private void UpdateButtons(string status)
        {
            var s = (status ?? string.Empty).Trim();
            bool none = string.IsNullOrEmpty(s);
            bool isCompleted = s.Equals("Completed", StringComparison.OrdinalIgnoreCase);
            bool isCancelled = s.Equals("Cancelled", StringComparison.OrdinalIgnoreCase);
            bool isScheduled = s.Equals("Scheduled", StringComparison.OrdinalIgnoreCase);

            _scheduleBtn.IsEnabled = none;
            // Finish-time editor: available for any loaded visit. For open
            // visits it stages the timestamp for Complete; for Completed
            // visits it corrects the recorded finish time in place.
            _editFinishBtn.IsEnabled = !none;
            _editFinishBtn.ToolTip = !none
                ? "View or correct the recorded finish time."
                : "Schedule a visit first.";
            _completeBtn.IsEnabled = isScheduled;
            _cancelBtn.IsEnabled = isScheduled;
            _rescheduleBtn.IsEnabled = isCancelled;
            _rescheduleBtn.Visibility = isCancelled ? Visibility.Visible : Visibility.Collapsed;
            _uploadPhotoBtn.IsEnabled = !none && !isCompleted;
            _signatureBtn.IsEnabled = !none && !isCompleted;
            // F4: tech/date pickers editable only when scheduling or rescheduling
            var canPickSchedule = none || isCancelled;
            if (_techCombo != null) { _techCombo.IsEnabled = canPickSchedule; _techCombo.Opacity = canPickSchedule ? 1.0 : 0.55; }
            if (_scheduleDatePicker != null) { _scheduleDatePicker.IsEnabled = canPickSchedule; _scheduleDatePicker.Opacity = canPickSchedule ? 1.0 : 0.55; }
            if (_scheduleTimeBox != null) { _scheduleTimeBox.IsEnabled = canPickSchedule; _scheduleTimeBox.Opacity = canPickSchedule ? 1.0 : 0.55; }
            UpdateScheduleModeHint(s);
        }
        private async Task ScheduleAsync()
        {
            Yakult.Inventory.App.Core.Logger.LogInfo($"FieldWork Schedule clicked (TicketId={_ticketId}).");
            try
            {
                int? techId = null;
                if (_techCombo.SelectedItem is Yakult.Inventory.App.Models.LookupItem li && li.Id > 0) techId = li.Id;
                var notes = string.IsNullOrWhiteSpace(_notesBox.Text) ? null : _notesBox.Text.Trim();
                var userId = Session.AppSession.CurrentUserId > 0 ? (int?)Session.AppSession.CurrentUserId : null;
                DateTime? scheduledAt = GetScheduledAtOrNull();
                _statusLine.Text = "Scheduling...";
                var visit = await _repo.ScheduleFieldVisitAsync(_ticketId, techId, scheduledAt, notes, userId);
                await LoadAsync();
                _statusLine.Text = "Scheduled visit #" + visit.FieldVisitId;
                Yakult.Inventory.App.Core.Logger.LogInfo($"FieldWork scheduled (TicketId={_ticketId}, VisitId={visit.FieldVisitId}).");
            }
            catch (Exception ex)
            {
                Yakult.Inventory.App.Core.Logger.LogError($"FieldWork Schedule failed (TicketId={_ticketId}).", ex);
                MessageBox.Show(this, ex.Message, "Schedule Field Work", MessageBoxButton.OK, MessageBoxImage.Error);
                _statusLine.Text = "Schedule failed.";
            }
        }
        private async Task SetStatusAsync(string newStatus)
        {
            Yakult.Inventory.App.Core.Logger.LogInfo($"FieldWork status action '{newStatus}' clicked (TicketId={_ticketId}, VisitId={_currentVisit?.FieldVisitId}, VisitStatus={_currentVisit?.Status}).");
            if (_currentVisit == null)
            {
                _statusLine.Text = "No field visit loaded — Schedule one first.";
                return;
            }
            // Evidence gate: sign-off requires a customer signature, so a
            // visit cannot be Completed without one. Photos stay optional but
            // get an explicit confirm so techs don't complete bare by habit.
            DateTime? backdatedCompletedAt = null;
            if (string.Equals(newStatus, "Completed", StringComparison.OrdinalIgnoreCase))
            {
                if (!_currentVisit.HasSignature)
                {
                    MessageBox.Show(this, "Capture the customer signature before completing the visit.\n\nSign-off is blocked for unsigned visits.", "Signature Required", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                if ((_photos == null || _photos.Count == 0)
                    && MessageBox.Show(this, "No photos are attached to this visit.\n\nComplete anyway?", "No Photos", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
                {
                    return;
                }
                // Backdate mode: confirm the exact finish timestamp being
                // recorded, since it becomes the permanent CompletedAt.
                if (_backdateCheck != null && _backdateCheck.IsChecked == true)
                {
                    DateTime finishAt;
                    try
                    {
                        var parsed = GetCompletedAtOrNull();
                        if (!parsed.HasValue)
                            throw new InvalidOperationException("Pick the completed date for the backdated visit.");
                        finishAt = parsed.Value;
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show(this, ex.Message, "Backdated Completion", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }
                    var confirmDlg = new BackdatedCompletionConfirmDialog(
                        _currentVisit.ScheduledAt,
                        finishAt,
                        _currentVisit.TechnicianName,
                        _currentVisit.Notes)
                    {
                        Owner = this
                    };
                    if (confirmDlg.ShowDialog() != true)
                        return;
                    // The finish time is editable inside the dialog: honor
                    // whatever passed validation there.
                    backdatedCompletedAt = confirmDlg.SelectedFinishAtUtc;
                }
            }
            try
            {
                var userId = Session.AppSession.CurrentUserId > 0 ? (int?)Session.AppSession.CurrentUserId : null;
                var notes = string.IsNullOrWhiteSpace(_notesBox.Text) ? null : _notesBox.Text.Trim();
                int? techId = null;
                DateTime? scheduledAt = null;
                DateTime? completedAt = null;
                if (string.Equals(newStatus, "Scheduled", StringComparison.OrdinalIgnoreCase))
                {
                    // F1+F4: reschedule carries newly picked tech/date
                    if (_techCombo.SelectedItem is Yakult.Inventory.App.Models.LookupItem rli && rli.Id > 0) techId = rli.Id;
                    scheduledAt = GetScheduledAtOrNull();
                    _statusLine.Text = "Rescheduling...";
                }
                else
                {
                    _statusLine.Text = "Updating to " + newStatus + "...";
                }
                if (string.Equals(newStatus, "Completed", StringComparison.OrdinalIgnoreCase))
                {
                    // Backdate mode supplies the actual finish time; otherwise
                    // the server stamps now.
                    completedAt = backdatedCompletedAt ?? GetCompletedAtOrNull();
                }
                await _repo.SetFieldVisitStatusAsync(_currentVisit.FieldVisitId, newStatus, userId, notes, techId, scheduledAt, completedAt);
                await LoadAsync();
                Yakult.Inventory.App.Core.Logger.LogInfo($"FieldWork status set to '{newStatus}' (TicketId={_ticketId}, VisitId={_currentVisit?.FieldVisitId}).");
            }
            catch (Exception ex)
            {
                Yakult.Inventory.App.Core.Logger.LogError($"FieldWork status update to '{newStatus}' failed (TicketId={_ticketId}).", ex);
                MessageBox.Show(this, ex.Message, "Field Work", MessageBoxButton.OK, MessageBoxImage.Error);
                _statusLine.Text = "Update failed.";
            }
        }
        private void UpdatePhotoCapState()
        {
            var count = _photos != null ? _photos.Count : 0;
            var atCap = count >= 20;
            var isCompleted = string.Equals(_currentVisit?.Status, "Completed", StringComparison.OrdinalIgnoreCase);
            if (_uploadPhotoBtn != null)
            {
                _uploadPhotoBtn.IsEnabled = _currentVisit != null && !isCompleted && !atCap;
                _uploadPhotoBtn.ToolTip = atCap ? "Maximum 20 photos reached (20/20)" : $"Add a field photo (PNG/JPG, max 20 MB) — {count}/20 used";
                _uploadPhotoBtn.Opacity = _uploadPhotoBtn.IsEnabled ? 1.0 : 0.55;
            }
            if (_photoCountLabel != null)
            {
                _photoCountLabel.Text = $"{count}/20 photos";
                _photoCountLabel.Foreground = atCap ? BrushFromRgb(185, 28, 28) : BrushFromRgb(100, 116, 139);
            }
            if (_photoCapHintBorder != null)
                _photoCapHintBorder.Visibility = atCap ? Visibility.Visible : Visibility.Collapsed;
        }

        private async Task UploadPhotoAsync()
        {
            if (_currentVisit == null) { MessageBox.Show(this, "Schedule a visit first.", "Field Work", MessageBoxButton.OK, MessageBoxImage.Information); return; }
            if (_photos != null && _photos.Count >= 20) { _statusLine.Text = "Photo limit reached (20/20)."; MessageBox.Show(this, "Maximum 20 photos per visit (20/20).", "Field Work", MessageBoxButton.OK, MessageBoxImage.Warning); UpdatePhotoCapState(); return; }
            var dlg = new OpenFileDialog { Filter="Images|*.jpg;*.jpeg;*.png;*.bmp|All files|*.*", Title="Select field photo" };
            if (dlg.ShowDialog(this) != true) return;
            try
            {
                var bytes = System.IO.File.ReadAllBytes(dlg.FileName);
                if (bytes.Length > 20*1024*1024) { MessageBox.Show(this, "File too large (max 20 MB).", "Field Work", MessageBoxButton.OK, MessageBoxImage.Warning); return; }
                var mime = dlg.FileName.EndsWith(".png",StringComparison.OrdinalIgnoreCase)?"image/png":"image/jpeg";
                var userId = Session.AppSession.CurrentUserId > 0 ? (int?)Session.AppSession.CurrentUserId : null;
                _statusLine.Text = "Uploading photo...";
                await _repo.UploadFieldVisitPhotoAsync(_currentVisit.FieldVisitId, System.IO.Path.GetFileName(dlg.FileName), mime, bytes, userId);
                _photos = await _repo.GetFieldVisitAttachmentsByVisitAsync(_currentVisit.FieldVisitId);
                _photoGrid.ItemsSource = _photos;
                UpdatePhotoCapState();
                _statusLine.Text = $"Photo uploaded ({_photos.Count}/20).";
            }
            catch (Exception ex) { MessageBox.Show(this, ex.Message, "Upload Photo", MessageBoxButton.OK, MessageBoxImage.Error); }
        }
        private async Task SaveSignatureAsync()
        {
            if (_currentVisit == null) { MessageBox.Show(this, "Schedule a visit first.", "Field Work", MessageBoxButton.OK, MessageBoxImage.Information); return; }
            var sigDlg = new Window { Title="Customer Signature", Width=640, Height=380, WindowStartupLocation=WindowStartupLocation.CenterOwner, Owner=this, Background=Brushes.White };
            var grid = new Grid { Margin=new Thickness(16) };
            grid.RowDefinitions.Add(new RowDefinition { Height=new GridLength(1, GridUnitType.Star) });
            grid.RowDefinitions.Add(new RowDefinition { Height=GridLength.Auto });
            sigDlg.Content=grid;
            var hint = new TextBlock { Text="Sign in the box below — single stroke, press Save when done", Foreground=BrushFromRgb(100,116,139), FontSize=12, Margin=new Thickness(0,0,0,8) };
            var stack = new StackPanel();
            Grid.SetRow(stack,0); grid.Children.Add(stack);
            stack.Children.Add(hint);
            var ink = new InkCanvas { Background=Brushes.White, Height=180, DefaultDrawingAttributes=new System.Windows.Ink.DrawingAttributes{ Color=Colors.Black, Width=2.2, Height=2.2 }};
            var border = new Border { BorderBrush=BrushFromRgb(203,213,225), BorderThickness=new Thickness(1), CornerRadius=new CornerRadius(10), Child=ink, Padding=new Thickness(8), Background=Brushes.White, Margin=new Thickness(0,4,0,0) };
            stack.Children.Add(border);
            var btns = new StackPanel { Orientation=Orientation.Horizontal, HorizontalAlignment=HorizontalAlignment.Right, Margin=new Thickness(0,12,0,0) };
            Grid.SetRow(btns,1); grid.Children.Add(btns);
            var clearBtn = CreateActionButton("Clear", BrushFromRgb(100,116,139), "RepairSecondaryBtn"); clearBtn.Click+=(_,__)=> ink.Strokes.Clear();
            var saveBtn = CreateActionButton("Save Signature", BrushFromRgb(22,163,74), "RepairPrimaryBtn");
            btns.Children.Add(clearBtn); btns.Children.Add(saveBtn);
            byte[] pngBytes=null;
            saveBtn.Click+=(_,__)=>{ if(ink.Strokes.Count==0){ MessageBox.Show(sigDlg,"Please sign first.","Signature",MessageBoxButton.OK,MessageBoxImage.Information); return; } var bounds=ink.Strokes.GetBounds(); var rtb=new RenderTargetBitmap((int)Math.Max(1,bounds.Width+24),(int)Math.Max(1,bounds.Height+24),96,96,PixelFormats.Pbgra32); var dv=new DrawingVisual(); using(var dc=dv.RenderOpen()){ dc.DrawRectangle(Brushes.White,null,new Rect(0,0,rtb.PixelWidth,rtb.PixelHeight)); foreach(var s in ink.Strokes) s.Draw(dc); } rtb.Render(dv); var enc=new PngBitmapEncoder(); enc.Frames.Add(BitmapFrame.Create(rtb)); using(var ms=new System.IO.MemoryStream()){ enc.Save(ms); pngBytes=ms.ToArray(); } sigDlg.DialogResult=true; sigDlg.Close(); };
            if(sigDlg.ShowDialog()!=true || pngBytes==null) return;
            try{ var userId=Session.AppSession.CurrentUserId>0?(int?)Session.AppSession.CurrentUserId:null; _statusLine.Text="Saving signature..."; await _repo.SaveFieldVisitSignatureAsync(_currentVisit.FieldVisitId,pngBytes,userId); await LoadAsync(); _statusLine.Text="Signature saved."; } catch(Exception ex){ MessageBox.Show(this,ex.Message,"Signature",MessageBoxButton.OK,MessageBoxImage.Error); }
        }
        private async Task PreviewSelectedPhotoAsync()
        {
            var item = _photoGrid.SelectedItem as CallFieldVisitAttachmentItem;
            if (item == null)
            {
                _photoPreviewBorder.Visibility = Visibility.Collapsed;
                _photoPreviewLabel.Text = "Select a photo to preview";
                _photoPreviewImage.Source = null;
                return;
            }
            _photoPreviewLabel.Text = $"Loading {item.FileName}...";
            _photoPreviewBorder.Visibility = Visibility.Visible;
            _photoPreviewImage.Source = null;
            try
            {
                var bytes = await _repo.GetFieldVisitAttachmentThumbnailBytesAsync(item.AttachmentId) ?? await _repo.GetFieldVisitAttachmentBytesAsync(item.AttachmentId);
                if (bytes == null || bytes.Length == 0)
                {
                    _photoPreviewLabel.Text = $"No preview for {item.FileName}";
                    return;
                }
                using (var ms = new System.IO.MemoryStream(bytes))
                {
                    var bmp = new BitmapImage();
                    bmp.BeginInit();
                    bmp.CacheOption = BitmapCacheOption.OnLoad;
                    bmp.StreamSource = ms;
                    bmp.EndInit();
                    bmp.Freeze();
                    _photoPreviewImage.Source = bmp;
                    _photoPreviewLabel.Text = $"{item.FileName} • {item.FileSizeBytes/1024} KB • {item.UploadedByName ?? "—"} • {item.UploadedAt:MMM d, yyyy h:mm tt}";
                }
            }
            catch (Exception ex)
            {
                _photoPreviewLabel.Text = $"Preview failed: {ex.Message}";
            }
        }

        private async Task OpenSelectedPhotoFullAsync()
        {
            var item = _photoGrid.SelectedItem as CallFieldVisitAttachmentItem;
            if (item == null) return;
            try
            {
                var bytes = await _repo.GetFieldVisitAttachmentBytesAsync(item.AttachmentId);
                if (bytes == null || bytes.Length == 0) { MessageBox.Show(this, "No image data.", "Photos", MessageBoxButton.OK, MessageBoxImage.Information); return; }
                var win = new Window { Title = item.FileName ?? $"Photo #{item.AttachmentId}", Width = 900, Height = 700, WindowStartupLocation = WindowStartupLocation.CenterOwner, Owner = this, Background = Brushes.Black };
                var img = new Image { Stretch = Stretch.Uniform };
                using (var ms = new System.IO.MemoryStream(bytes))
                {
                    var bmp = new BitmapImage();
                    bmp.BeginInit();
                    bmp.CacheOption = BitmapCacheOption.OnLoad;
                    bmp.StreamSource = ms;
                    bmp.EndInit();
                    bmp.Freeze();
                    img.Source = bmp;
                }
                win.Content = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Auto, Content = img, Background = Brushes.Black };
                win.ShowDialog();
            }
            catch (Exception ex) { MessageBox.Show(this, ex.Message, "Photos", MessageBoxButton.OK, MessageBoxImage.Error); }
        }

        private async Task PreviewSignatureAsync()
        {
            if (_currentVisit == null || !_currentVisit.HasSignature)
            {
                _signaturePreviewBorder.Visibility = Visibility.Collapsed;
                _signaturePreviewLabel.Text = "No signature yet — capture before completing visit";
                _signaturePreviewImage.Source = null;
                return;
            }
            _signaturePreviewLabel.Text = "Loading signature...";
            _signaturePreviewBorder.Visibility = Visibility.Visible;
            _signaturePreviewImage.Source = null;
            try
            {
                var bytes = await _repo.GetFieldVisitSignatureBytesAsync(_currentVisit.FieldVisitId);
                if (bytes == null || bytes.Length == 0)
                {
                    _signaturePreviewLabel.Text = "No signature on file";
                    _signaturePreviewBorder.Visibility = Visibility.Collapsed;
                    return;
                }
                using (var ms = new System.IO.MemoryStream(bytes))
                {
                    var bmp = new BitmapImage();
                    bmp.BeginInit();
                    bmp.CacheOption = BitmapCacheOption.OnLoad;
                    bmp.StreamSource = ms;
                    bmp.EndInit();
                    bmp.Freeze();
                    _signaturePreviewImage.Source = bmp;
                    _signaturePreviewLabel.Text = $"Signature on file • Visit #{_currentVisit.FieldVisitId}";
                }
            }
            catch (Exception ex)
            {
                _signaturePreviewLabel.Text = $"Signature preview failed: {ex.Message}";
            }
        }

        private static Button CreatePillButton(string text, string glyph, Brush accent, bool isPrimary)
        {
            var panel = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            panel.Children.Add(new TextBlock { Text = glyph, FontFamily = new FontFamily("Segoe MDL2 Assets"), FontSize = 13, Foreground = isPrimary ? Brushes.White : accent, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0,0,6,0) });
            panel.Children.Add(new TextBlock { Text = text, Foreground = isPrimary ? Brushes.White : accent, FontWeight = FontWeights.SemiBold, VerticalAlignment = VerticalAlignment.Center, FontSize = 12.5 });
            var btn = new Button { Content = panel, MinWidth = 102, Height = 34, Margin = new Thickness(6,0,0,0), Padding = new Thickness(14,0,14,0), Background = isPrimary ? accent : Brushes.White, Foreground = isPrimary ? Brushes.White : accent, BorderBrush = accent, BorderThickness = new Thickness(isPrimary ? 0 : 1), FontWeight = FontWeights.SemiBold, Cursor = Cursors.Hand, FontSize = 12.5 };
            btn.SetValue(Control.TemplateProperty, CreatePillButtonTemplate());
            try { btn.Effect = new System.Windows.Media.Effects.DropShadowEffect { BlurRadius = 8, ShadowDepth = 1, Opacity = 0.08, Direction = 270, Color = Colors.Black }; } catch { }
            return btn;
        }

        private static ControlTemplate CreatePillButtonTemplate()
        {
            var borderFactory = new FrameworkElementFactory(typeof(Border));
            borderFactory.SetValue(Border.CornerRadiusProperty, new CornerRadius(9));
            borderFactory.SetValue(Border.BackgroundProperty, new System.Windows.Data.Binding("Background") { RelativeSource = new System.Windows.Data.RelativeSource(System.Windows.Data.RelativeSourceMode.TemplatedParent) });
            borderFactory.SetValue(Border.BorderBrushProperty, new System.Windows.Data.Binding("BorderBrush") { RelativeSource = new System.Windows.Data.RelativeSource(System.Windows.Data.RelativeSourceMode.TemplatedParent) });
            borderFactory.SetValue(Border.BorderThicknessProperty, new System.Windows.Data.Binding("BorderThickness") { RelativeSource = new System.Windows.Data.RelativeSource(System.Windows.Data.RelativeSourceMode.TemplatedParent) });
            borderFactory.SetValue(Border.PaddingProperty, new System.Windows.Data.Binding("Padding") { RelativeSource = new System.Windows.Data.RelativeSource(System.Windows.Data.RelativeSourceMode.TemplatedParent) });
            borderFactory.SetValue(Border.SnapsToDevicePixelsProperty, true);
            var cp = new FrameworkElementFactory(typeof(ContentPresenter));
            cp.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Center);
            cp.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
            cp.SetValue(ContentPresenter.ContentProperty, new System.Windows.Data.Binding("Content") { RelativeSource = new System.Windows.Data.RelativeSource(System.Windows.Data.RelativeSourceMode.TemplatedParent) });
            borderFactory.AppendChild(cp);
            return new ControlTemplate(typeof(Button)) { VisualTree = borderFactory };
        }

        private sealed class FileSizeConverter : System.Windows.Data.IValueConverter
        {
            public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
            {
                if (value is int b) { if (b < 1024) return $"{b} B"; if (b < 1024*1024) return $"{b/1024} KB"; return $"{b/(1024.0*1024):0.0} MB"; }
                if (value is long l) { if (l < 1024) return $"{l} B"; if (l < 1024*1024) return $"{l/1024} KB"; return $"{l/(1024.0*1024):0.0} MB"; }
                return "—";
            }
            public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture) => throw new NotSupportedException();
        }

        private sealed class InitialConverter : System.Windows.Data.IValueConverter
        {
            public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
            {
                var s = value as string;
                if (string.IsNullOrWhiteSpace(s)) return "?";
                return s.Trim().Substring(0,1).ToUpperInvariant();
            }
            public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture) => throw new NotSupportedException();
        }

        // Opens the finish-time editor for the current visit. Saving checks
        // backdate automatically, since an edited finish time is by
        // definition a backdated completion. For an already-Completed
        // visit, saving applies the correction immediately (with confirm).
        private async void OpenFinishTimeEditor()
        {
            Yakult.Inventory.App.Core.Logger.LogInfo($"FieldWork Edit Finish opened (TicketId={_ticketId}, VisitId={_currentVisit?.FieldVisitId}).");
            if (_currentVisit == null)
            {
                MessageBox.Show(this, "Schedule a visit first.", "Edit Finish Time", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            var initial = _completedDatePicker != null && _completedDatePicker.SelectedDate.HasValue
                ? _completedDatePicker.SelectedDate.Value.Date
                : DateTime.Today;
            var initialTime = _completedTimeBox != null && !string.IsNullOrWhiteSpace(_completedTimeBox.Text)
                ? _completedTimeBox.Text.Trim()
                : DateTime.Now.ToString("HH:mm");
            var dlg = new FinishTimeEditorDialog(initial, initialTime) { Owner = this };
            if (dlg.ShowDialog() != true || !dlg.SelectedFinishLocal.HasValue)
                return;
            var picked = dlg.SelectedFinishLocal.Value;
            if (_completedDatePicker != null) _completedDatePicker.SelectedDate = picked.Date;
            if (_completedTimeBox != null) _completedTimeBox.Text = picked.ToString("HH:mm");
            if (_backdateCheck != null) _backdateCheck.IsChecked = true;
            UpdateBackdateMode();
            DateTime pickedUtc;
            try
            {
                pickedUtc = DateTime.SpecifyKind(picked, DateTimeKind.Local).ToUniversalTime();
            }
            catch
            {
                pickedUtc = picked;
            }

            if (string.Equals((_currentVisit.Status ?? string.Empty).Trim(), "Completed", StringComparison.OrdinalIgnoreCase))
            {
                if (MessageBox.Show(this,
                    "Update the recorded finish time to " + picked.ToString("MMM d, yyyy h:mm tt") + "?",
                    "Correct Completion Time", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
                    return;
                try
                {
                    var userId = Session.AppSession.CurrentUserId > 0 ? (int?)Session.AppSession.CurrentUserId : null;
                    await _repo.SetFieldVisitStatusAsync(_currentVisit.FieldVisitId, "Completed", userId, "Completion time corrected.", null, null, pickedUtc);
                    await LoadAsync();
                    _statusLine.Text = "Finish time corrected to " + picked.ToString("MMM d, yyyy h:mm tt") + ".";
                    Yakult.Inventory.App.Core.Logger.LogInfo($"FieldWork finish time corrected (TicketId={_ticketId}, VisitId={_currentVisit?.FieldVisitId}).");
                }
                catch (Exception ex)
                {
                    Yakult.Inventory.App.Core.Logger.LogError($"FieldWork finish time correction failed (TicketId={_ticketId}).", ex);
                    MessageBox.Show(this, ex.Message, "Edit Finish Time", MessageBoxButton.OK, MessageBoxImage.Error);
                }
                return;
            }

            _statusLine.Text = "Finish time set to " + picked.ToString("MMM d, yyyy h:mm tt") + " — press Complete to record it.";
            Yakult.Inventory.App.Core.Logger.LogInfo($"FieldWork finish time edited (TicketId={_ticketId}, VisitId={_currentVisit?.FieldVisitId}, FinishLocal={picked:yyyy-MM-dd HH:mm}).");
        }

        // Small date/time editor used by the Edit button next to Schedule.
        private sealed class FinishTimeEditorDialog : Window
        {
            private readonly DatePicker _datePicker;
            private readonly TextBox _timeBox;
            private readonly TextBlock _error;

            public DateTime? SelectedFinishLocal { get; private set; }

            public FinishTimeEditorDialog(DateTime initialDate, string initialTime)
            {
                Title = "Edit Finish Time";
                Width = 400;
                SizeToContent = SizeToContent.Height;
                MinHeight = 240;
                WindowStartupLocation = WindowStartupLocation.CenterOwner;
                ResizeMode = ResizeMode.NoResize;
                ShowInTaskbar = false;
                Background = BrushFromRgb(241, 245, 249);

                var root = new StackPanel { Margin = new Thickness(20) };
                Content = root;
                var card = new Border
                {
                    CornerRadius = new CornerRadius(12),
                    Background = Brushes.White,
                    BorderBrush = BrushFromRgb(226, 232, 240),
                    BorderThickness = new Thickness(1),
                    Padding = new Thickness(18, 14, 18, 14)
                };
                root.Children.Add(card);
                var body = new StackPanel();
                card.Child = body;
                body.Children.Add(new TextBlock
                {
                    Text = "When did this visit finish?",
                    FontSize = 15,
                    FontWeight = FontWeights.Bold,
                    Foreground = BrushFromRgb(15, 23, 42),
                    TextWrapping = TextWrapping.Wrap,
                    Margin = new Thickness(0, 0, 0, 10)
                });
                var row = new StackPanel { Orientation = Orientation.Horizontal };
                _datePicker = new DatePicker
                {
                    SelectedDate = initialDate,
                    Width = 190,
                    Height = 34,
                    Background = Brushes.White,
                    BorderBrush = BrushFromRgb(203, 213, 225),
                    Foreground = BrushFromRgb(15, 23, 42),
                    FontSize = 13
                };
                _timeBox = new TextBox
                {
                    Text = initialTime,
                    Width = 100,
                    Height = 34,
                    Margin = new Thickness(8, 0, 0, 0),
                    Padding = new Thickness(8, 6, 8, 6),
                    Background = Brushes.White,
                    BorderBrush = BrushFromRgb(203, 213, 225),
                    Foreground = BrushFromRgb(15, 23, 42),
                    FontSize = 13
                };
                row.Children.Add(_datePicker);
                row.Children.Add(_timeBox);
                body.Children.Add(row);
                body.Children.Add(new TextBlock
                {
                    Text = "Date + 24-hour time (HH:mm). Saving checks backdate automatically.",
                    FontSize = 12,
                    Foreground = BrushFromRgb(100, 116, 139),
                    TextWrapping = TextWrapping.Wrap,
                    Margin = new Thickness(0, 8, 0, 0)
                });
                _error = new TextBlock
                {
                    FontSize = 12,
                    Foreground = BrushFromRgb(220, 38, 38),
                    TextWrapping = TextWrapping.Wrap,
                    Visibility = Visibility.Collapsed,
                    Margin = new Thickness(0, 6, 0, 0)
                };
                body.Children.Add(_error);

                var buttons = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    HorizontalAlignment = HorizontalAlignment.Right,
                    Margin = new Thickness(0, 16, 0, 0)
                };
                var cancelBtn = new Button
                {
                    Content = "Cancel",
                    MinWidth = 96,
                    Height = 36,
                    Margin = new Thickness(0, 0, 8, 0),
                    Background = Brushes.White,
                    Foreground = BrushFromRgb(71, 85, 105),
                    BorderBrush = BrushFromRgb(203, 213, 225),
                    BorderThickness = new Thickness(1),
                    FontWeight = FontWeights.SemiBold,
                    Cursor = Cursors.Hand,
                    IsCancel = true
                };
                cancelBtn.Click += (_, __) => { DialogResult = false; Close(); };
                var okBtn = new Button
                {
                    Content = "Save",
                    MinWidth = 110,
                    Height = 36,
                    Background = BrushFromRgb(124, 58, 237),
                    Foreground = Brushes.White,
                    BorderThickness = new Thickness(0),
                    FontWeight = FontWeights.SemiBold,
                    Cursor = Cursors.Hand,
                    IsDefault = true
                };
                okBtn.Click += (_, __) =>
                {
                    if (_datePicker.SelectedDate == null)
                    {
                        _error.Text = "Pick the finish date.";
                        _error.Visibility = Visibility.Visible;
                        return;
                    }
                    if (!TryParseHhMm((_timeBox.Text ?? string.Empty).Trim(), out var t))
                    {
                        _error.Text = "Finish time must be HH:mm in 24-hour format (e.g. 09:30).";
                        _error.Visibility = Visibility.Visible;
                        return;
                    }
                    DateTime local;
                    try
                    {
                        local = _datePicker.SelectedDate.Value.Date + t;
                    }
                    catch
                    {
                        _error.Text = "Finish time must be HH:mm in 24-hour format (e.g. 09:30).";
                        _error.Visibility = Visibility.Visible;
                        return;
                    }
                    if (local > DateTime.Now.AddMinutes(1))
                    {
                        _error.Text = "Finish time cannot be in the future.";
                        _error.Visibility = Visibility.Visible;
                        return;
                    }
                    SelectedFinishLocal = local;
                    DialogResult = true;
                    Close();
                };
                buttons.Children.Add(cancelBtn);
                buttons.Children.Add(okBtn);
                root.Children.Add(buttons);
            }

            private static bool TryParseHhMm(string text, out TimeSpan value)
            {
                value = TimeSpan.Zero;
                var parts = (text ?? string.Empty).Split(':');
                if (parts.Length != 2) return false;
                if (!int.TryParse(parts[0], out var h) || !int.TryParse(parts[1], out var m)) return false;
                if (h < 0 || h > 23 || m < 0 || m > 59) return false;
                value = new TimeSpan(h, m, 0);
                return true;
            }
        }

        // Dedicated confirm surface for backdated completion: shows exactly
        // what timestamp becomes permanent, since it differs from "now".
        // The finish date/time is editable in place.
        private sealed class BackdatedCompletionConfirmDialog : Window
        {
            private readonly DatePicker _finishDatePicker;
            private readonly TextBox _finishTimeBox;
            private readonly TextBlock _finishError;

            public DateTime? SelectedFinishAtUtc { get; private set; }

            public BackdatedCompletionConfirmDialog(DateTime? scheduledAt, DateTime finishAtUtc, string technicianName, string notes)
            {
                Title = "Confirm Backdated Completion";
                Width = 480;
                SizeToContent = SizeToContent.Height;
                MinHeight = 380;
                WindowStartupLocation = WindowStartupLocation.CenterOwner;
                ResizeMode = ResizeMode.NoResize;
                ShowInTaskbar = false;
                Background = BrushFromRgb(241, 245, 249);

                DateTime initialLocal;
                try
                {
                    var asUtc = finishAtUtc.Kind == DateTimeKind.Utc
                        ? finishAtUtc
                        : DateTime.SpecifyKind(finishAtUtc, DateTimeKind.Utc);
                    initialLocal = asUtc.ToLocalTime();
                }
                catch
                {
                    initialLocal = finishAtUtc;
                }

                var root = new StackPanel { Margin = new Thickness(20) };
                Content = root;

                var card = new Border
                {
                    CornerRadius = new CornerRadius(12),
                    Background = Brushes.White,
                    BorderBrush = BrushFromRgb(226, 232, 240),
                    BorderThickness = new Thickness(1),
                    Padding = new Thickness(18, 14, 18, 14)
                };
                root.Children.Add(card);
                var body = new StackPanel();
                card.Child = body;

                body.Children.Add(new TextBlock
                {
                    Text = "Complete this visit with a past finish time?",
                    FontSize = 15,
                    FontWeight = FontWeights.Bold,
                    Foreground = BrushFromRgb(15, 23, 42),
                    TextWrapping = TextWrapping.Wrap,
                    Margin = new Thickness(0, 0, 0, 10)
                });
                AddConfirmRow(body, "Scheduled slot",
                    scheduledAt.HasValue ? ToLocalStr(scheduledAt.Value) : "—");
                body.Children.Add(new TextBlock
                {
                    Text = "Record finished as (editable)",
                    FontSize = 11,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = BrushFromRgb(100, 116, 139),
                    Margin = new Thickness(0, 8, 0, 2)
                });
                var finishRow = new StackPanel { Orientation = Orientation.Horizontal };
                _finishDatePicker = new DatePicker
                {
                    SelectedDate = initialLocal.Date,
                    Width = 170,
                    Height = 34,
                    Background = Brushes.White,
                    BorderBrush = BrushFromRgb(203, 213, 225),
                    Foreground = BrushFromRgb(15, 23, 42),
                    FontSize = 13
                };
                _finishTimeBox = new TextBox
                {
                    Text = initialLocal.ToString("HH:mm"),
                    Width = 100,
                    Height = 34,
                    Margin = new Thickness(8, 0, 0, 0),
                    Padding = new Thickness(8, 6, 8, 6),
                    Background = Brushes.White,
                    BorderBrush = BrushFromRgb(203, 213, 225),
                    Foreground = BrushFromRgb(146, 64, 14),
                    FontSize = 13,
                    FontWeight = FontWeights.Bold
                };
                finishRow.Children.Add(_finishDatePicker);
                finishRow.Children.Add(_finishTimeBox);
                body.Children.Add(finishRow);
                _finishError = new TextBlock
                {
                    FontSize = 12,
                    Foreground = BrushFromRgb(220, 38, 38),
                    TextWrapping = TextWrapping.Wrap,
                    Visibility = Visibility.Collapsed,
                    Margin = new Thickness(0, 4, 0, 0)
                };
                body.Children.Add(_finishError);
                AddConfirmRow(body, "Technician",
                    string.IsNullOrWhiteSpace(technicianName) ? "—" : technicianName.Trim());
                if (!string.IsNullOrWhiteSpace(notes))
                    AddConfirmRow(body, "Notes", notes.Trim());
                body.Children.Add(new TextBlock
                {
                    Text = "This timestamp becomes the permanent completion record and appears on the sign-off report.",
                    FontSize = 12,
                    Foreground = BrushFromRgb(146, 64, 14),
                    TextWrapping = TextWrapping.Wrap,
                    Margin = new Thickness(0, 10, 0, 0)
                });

                var buttons = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    HorizontalAlignment = HorizontalAlignment.Right,
                    Margin = new Thickness(0, 16, 0, 0)
                };
                var cancelBtn = new Button
                {
                    Content = "Cancel",
                    MinWidth = 96,
                    Height = 36,
                    Margin = new Thickness(0, 0, 8, 0),
                    Background = Brushes.White,
                    Foreground = BrushFromRgb(71, 85, 105),
                    BorderBrush = BrushFromRgb(203, 213, 225),
                    BorderThickness = new Thickness(1),
                    FontWeight = FontWeights.SemiBold,
                    Cursor = Cursors.Hand,
                    IsCancel = true
                };
                cancelBtn.Click += (_, __) => { DialogResult = false; Close(); };
                var okBtn = new Button
                {
                    Content = "Complete Visit",
                    MinWidth = 130,
                    Height = 36,
                    Background = BrushFromRgb(22, 163, 74),
                    Foreground = Brushes.White,
                    BorderThickness = new Thickness(0),
                    FontWeight = FontWeights.SemiBold,
                    Cursor = Cursors.Hand,
                    IsDefault = true
                };
                okBtn.Click += (_, __) =>
                {
                    DateTime local;
                    try
                    {
                        local = ParseFinishDateTime();
                    }
                    catch (Exception ex)
                    {
                        _finishError.Text = ex.Message;
                        _finishError.Visibility = Visibility.Visible;
                        return;
                    }
                    if (local > DateTime.Now.AddMinutes(1))
                    {
                        _finishError.Text = "Finish time cannot be in the future.";
                        _finishError.Visibility = Visibility.Visible;
                        return;
                    }
                    try
                    {
                        SelectedFinishAtUtc = DateTime.SpecifyKind(local, DateTimeKind.Local).ToUniversalTime();
                    }
                    catch
                    {
                        SelectedFinishAtUtc = local;
                    }
                    DialogResult = true;
                    Close();
                };
                buttons.Children.Add(cancelBtn);
                buttons.Children.Add(okBtn);
                root.Children.Add(buttons);
            }

            private DateTime ParseFinishDateTime()
            {
                if (_finishDatePicker.SelectedDate == null)
                    throw new InvalidOperationException("Pick the finish date.");
                if (!TryParseHhMm((_finishTimeBox.Text ?? string.Empty).Trim(), out var t))
                    throw new InvalidOperationException("Finish time must be HH:mm in 24-hour format (e.g. 09:30).");
                try
                {
                    return _finishDatePicker.SelectedDate.Value.Date + t;
                }
                catch
                {
                    throw new InvalidOperationException("Finish time must be HH:mm in 24-hour format (e.g. 09:30).");
                }
            }

            private static bool TryParseHhMm(string text, out TimeSpan value)
            {
                value = TimeSpan.Zero;
                var parts = (text ?? string.Empty).Split(':');
                if (parts.Length != 2) return false;
                if (!int.TryParse(parts[0], out var h) || !int.TryParse(parts[1], out var m)) return false;
                if (h < 0 || h > 23 || m < 0 || m > 59) return false;
                value = new TimeSpan(h, m, 0);
                return true;
            }

            private static void AddConfirmRow(StackPanel body, string label, string value, bool highlight = false)
            {
                body.Children.Add(new TextBlock
                {
                    Text = label,
                    FontSize = 11,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = BrushFromRgb(100, 116, 139),
                    Margin = new Thickness(0, 8, 0, 2)
                });
                body.Children.Add(new TextBlock
                {
                    Text = string.IsNullOrWhiteSpace(value) ? "—" : value,
                    FontSize = 13.5,
                    FontWeight = highlight ? FontWeights.Bold : FontWeights.SemiBold,
                    Foreground = highlight ? BrushFromRgb(146, 64, 14) : BrushFromRgb(15, 23, 42),
                    TextWrapping = TextWrapping.Wrap
                });
            }

            private static string ToLocalStr(DateTime d)
            {
                try
                {
                    var utc = d.Kind == DateTimeKind.Utc ? d : DateTime.SpecifyKind(d, DateTimeKind.Utc);
                    return utc.ToLocalTime().ToString("MMM d, yyyy h:mm tt");
                }
                catch
                {
                    return d.ToString("MMM d, yyyy h:mm tt");
                }
            }
        }

        private static Button CreateActionButton(string text, Brush bg, string styleKey, string glyph = null)        {
            Button btn;
            if (!string.IsNullOrEmpty(glyph))
            {
                var panel = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
                panel.Children.Add(new TextBlock { Text = glyph, FontFamily = new FontFamily("Segoe MDL2 Assets"), FontSize = 13, Foreground = Brushes.White, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0,0,6,0) });
                panel.Children.Add(new TextBlock { Text = text, Foreground = Brushes.White, FontWeight = FontWeights.SemiBold, VerticalAlignment = VerticalAlignment.Center });
                btn = new Button { Content = panel, MinWidth = 96, Margin = new Thickness(6,0,0,0), Padding = new Thickness(12,7,12,7), Background = bg, Foreground = Brushes.White, BorderThickness = new Thickness(0), FontWeight = FontWeights.SemiBold, Cursor = Cursors.Hand, FontSize = 12.5 };
            }
            else
            {
                btn = new Button { Content=text, MinWidth=96, Margin=new Thickness(6,0,0,0), Padding=new Thickness(12,7,12,7), Background=bg, Foreground=Brushes.White, BorderThickness=new Thickness(0), FontWeight=FontWeights.SemiBold, Cursor=Cursors.Hand, FontSize=12.5 };
            }
            try{ var res=Application.Current?.TryFindResource(styleKey) as Style; if(res!=null) btn.Style=res; }catch{}
            return btn;
        }
        private static Brush GetFieldWorkStatusBrush(string status)
        {
            switch((status??"").Trim().ToLowerInvariant()){
                case "scheduled": return BrushFromRgb(37,99,235);
                case "completed": return BrushFromRgb(22,163,74);
                case "cancelled": return BrushFromRgb(220,38,38);
                default: return BrushFromRgb(100,116,139);
            }
        }
        
        private static string GetStatusHint(string status)
        {
            switch((status??"").Trim().ToLowerInvariant()){
                case "scheduled": return "Ready for field work — click Complete when done or Cancel to abort";
                case "completed": return "✓ Field visit completed successfully";
                case "cancelled": return "✗ Field visit cancelled — click Reschedule to reopen (Cancelled → Scheduled)";
                default: return "Unknown status";
            }
        }
        private Style TryFindStyle(string key){ try{ return Application.Current?.TryFindResource(key) as Style ?? Resources[key] as Style; }catch{ return null; } }
        private object FindResourceOrDefault(string key, object fallback){ try{ return Application.Current?.TryFindResource(key) ?? Resources[key] ?? fallback; }catch{ return fallback; } }
        private static SolidColorBrush BrushFromRgb(byte r, byte g, byte b){ var br=new SolidColorBrush(Color.FromRgb(r,g,b)); br.Freeze(); return br; }
    }
}
