using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Yakult.Inventory.App.Helpers;
using Yakult.Inventory.App.Models.CallMonitoring;
using Yakult.Inventory.App.Repositories;
using Yakult.Inventory.App.Session;

namespace Yakult.Inventory.App.Wpf.CallMonitoring
{
    public sealed class WpfFieldWorkReportDetailDialog : Window
    {
        private readonly ICallMonitoringRepository _repo;
        private readonly ICallMonitoringNavigator _navigator;
        private readonly CallFieldVisitReportRow _row;
        private readonly TextBlock _statusLine;
        private readonly StackPanel _timelineHost;
        private readonly StackPanel _detailHost;
        private readonly StackPanel _ticketTimelineHost;
        private readonly DataGrid _photoGrid;
        private readonly Image _photoPreviewImage;
        private readonly TextBlock _photoPreviewLabel;
        private readonly Border _photoPreviewBorder;
        private readonly TextBlock _headerSubtitle;
        private readonly TabControl _tabControl;
        private readonly Button _signOffButton;
        private readonly Button _generatePdfButton;
        private CallTicketListItem _gateTicket;
        private List<CallFieldVisitItem> _gateVisits = new List<CallFieldVisitItem>();
        private List<CallTicketHistoryItem> _gateHistory = new List<CallTicketHistoryItem>();
        private readonly ComboBox _ticketTimelineFilter;
        private readonly TextBlock _photoSummaryLabel;
        private readonly ProgressBar _photoProgress;
        private List<CallTicketHistoryItem> _ticketHistoryAll = new List<CallTicketHistoryItem>();

        public WpfFieldWorkReportDetailDialog(ICallMonitoringRepository repo, ICallMonitoringNavigator navigator, CallFieldVisitReportRow row)
        {
            _repo = repo ?? throw new ArgumentNullException(nameof(repo));
            _navigator = navigator;
            _row = row ?? throw new ArgumentNullException(nameof(row));

            Title = $"Field Visit — {row.TicketCode ?? $"#{row.TicketId}"} • {row.Status}";
            Width = 1100; Height = 760; MinWidth = 900; MinHeight = 600;
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

            // Header
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
            if (accent.Style == null || accent.Style.Setters.Count == 0) { accent.Width = 4; accent.CornerRadius = new CornerRadius(2); accent.Margin = new Thickness(0,0,16,0); accent.Background = new LinearGradientBrush{ StartPoint=new Point(0,0), EndPoint=new Point(0,1), GradientStops={ new GradientStop(Color.FromRgb(0xDC,0x26,0x26),0), new GradientStop(Color.FromRgb(0x25,0x63,0xEB),1)}}; }
            Grid.SetColumn(accent, 0); headerGrid.Children.Add(accent);
            var titleStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            Grid.SetColumn(titleStack, 1); headerGrid.Children.Add(titleStack);
            var titleRow = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            titleStack.Children.Add(titleRow);
            var glyph = new TextBlock { Text = "\uE787", FontFamily = new FontFamily("Segoe MDL2 Assets"), FontSize = 22, Foreground = BrushFromRgb(37,99,235), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0,0,10,0) };
            titleRow.Children.Add(glyph);
            var title = new TextBlock { Text = "Field Work Visit Detail", FontSize = 20, FontWeight = FontWeights.Bold, Foreground = BrushFromRgb(15,23,42), VerticalAlignment = VerticalAlignment.Center };
            titleRow.Children.Add(title);
            var pill = new Border { CornerRadius = new CornerRadius(8), Background = GetStatusBrush(row.Status), Padding = new Thickness(10,4,10,4), Margin = new Thickness(12,0,0,0), VerticalAlignment = VerticalAlignment.Center };
            pill.Child = new TextBlock { Text = (row.Status ?? "-").ToUpperInvariant(), FontSize = 11, FontWeight = FontWeights.Bold, Foreground = Brushes.White };
            titleRow.Children.Add(pill);
            _headerSubtitle = new TextBlock { Text = $"{row.TicketCode ?? $"Ticket #{row.TicketId}"} • Visit #{row.FieldVisitId} • {row.Location}", FontSize = 12.5, Foreground = BrushFromRgb(100,116,139), Margin = new Thickness(2,6,0,0), TextWrapping = TextWrapping.Wrap };
            titleStack.Children.Add(_headerSubtitle);
            var closeBtn = new Button { Content = "Close", MinWidth = 100, Padding = new Thickness(16,9,16,9), Cursor = Cursors.Hand, FontWeight = FontWeights.SemiBold, FontSize=13 };
            var closeStyle = TryFindStyle("RepairSecondaryBtn");
            if (closeStyle != null) closeBtn.Style = closeStyle; else { closeBtn.Background = BrushFromRgb(52,73,94); closeBtn.Foreground = Brushes.White; closeBtn.BorderThickness = new Thickness(0); }
            closeBtn.Click += (_, __) => Close();
            Grid.SetColumn(closeBtn, 2); headerGrid.Children.Add(closeBtn);

            // Footer
            var footer = new DockPanel { Height = 62, Background = Brushes.White, LastChildFill = false };
            DockPanel.SetDock(footer, Dock.Bottom);
            root.Children.Add(footer);
            _statusLine = new TextBlock { Text = "Loading details...", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(20,0,0,0), Foreground = BrushFromRgb(100,116,139), FontSize=12.5 };
            footer.Children.Add(_statusLine);
            var footerActions = new WrapPanel { HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(16,10,16,10) };
            DockPanel.SetDock(footerActions, Dock.Right);
            footer.Children.Add(footerActions);
            var openTicketBtn = CreateActionButton("Open Ticket", BrushFromRgb(37,99,235), "RepairPrimaryBtn", "\uE8A7");
            openTicketBtn.Click += (_, __) =>
            {
                try { _navigator?.OpenTicket(_row.TicketId); } catch { }
            };
            footerActions.Children.Add(openTicketBtn);
            _signOffButton = CreatePillButton("Sign Off", "\uE73E", BrushFromRgb(22,163,74), isPrimary: true);
            _signOffButton.Click += async (_, __) => await SignOffAsync();
            _signOffButton.IsEnabled = false;
            footerActions.Children.Add(_signOffButton);
            _generatePdfButton = CreatePillButton("Sign-Off PDF", "\uE8A7", BrushFromRgb(37,99,235), isPrimary: false);
            _generatePdfButton.Click += async (_, __) => await GenerateSignOffPdfAsync(recordSignOff: false);
            _generatePdfButton.IsEnabled = false;
            footerActions.Children.Add(_generatePdfButton);

            // Tab control with Overview / Timelines / Photos
            _tabControl = new TabControl { Margin = new Thickness(0,0,0,0), Background = BrushFromRgb(241,245,249), BorderThickness = new Thickness(0), Padding = new Thickness(0) };
            root.Children.Add(_tabControl);

            // --- Overview tab: Visit Details + Ticket Context ---
            var overviewScroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Padding = new Thickness(20), Background = BrushFromRgb(241,245,249) };
            var overviewStack = new StackPanel();
            overviewScroll.Content = overviewStack;
            var overviewTab = new TabItem { Header = "Overview", Content = overviewScroll, Padding = new Thickness(14,8,14,8), FontWeight = FontWeights.SemiBold };
            _tabControl.Items.Add(overviewTab);

            var detailCard = NewCard("Visit Details", "\uE77B", "Technician, scheduling, and notes for this visit");
            overviewStack.Children.Add(detailCard);
            _detailHost = new StackPanel { Margin = new Thickness(0,8,0,0) };
            ((StackPanel)detailCard.Child).Children.Add(_detailHost);
            BuildDetailSection();

            var issueCard = NewCard("Ticket Context", "\uE8F2", $"Issue reported for {row.TicketCode ?? $"#{row.TicketId}"}");
            overviewStack.Children.Add(issueCard);
            var issueInner = (StackPanel)issueCard.Child;
            issueInner.Children.Add(new TextBlock { Text = string.IsNullOrWhiteSpace(row.Issue) ? "No issue description." : row.Issue, Foreground = BrushFromRgb(30,41,59), TextWrapping = TextWrapping.Wrap, FontSize = 13, Margin = new Thickness(0,8,0,0) });
            issueInner.Children.Add(new TextBlock { Text = $"Department: {row.Department ?? "—"}    •    Branch: {row.Branch ?? "—"}", Foreground = BrushFromRgb(100,116,139), FontSize=12, Margin = new Thickness(0,8,0,0) });

            // --- Field Timeline tab ---
            var fieldTimelineScroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Padding = new Thickness(20), Background = BrushFromRgb(241,245,249) };
            var fieldTimelineStack = new StackPanel();
            fieldTimelineScroll.Content = fieldTimelineStack;
            var fieldTimelineTab = new TabItem { Header = "Field Timeline", Content = fieldTimelineScroll, Padding = new Thickness(14,8,14,8), FontWeight = FontWeights.SemiBold };
            _tabControl.Items.Add(fieldTimelineTab);
            var timelineCard = NewCard("Field Visit Timeline", "\uE81C", "Status changes for this field visit (Scheduled → Completed / Cancelled, Cancelled → Scheduled reschedule tracked)");
            fieldTimelineStack.Children.Add(timelineCard);
            _timelineHost = new StackPanel { Margin = new Thickness(0,8,0,0) };
            ((StackPanel)timelineCard.Child).Children.Add(_timelineHost);
            _timelineHost.Children.Add(new TextBlock { Text = "Loading field timeline...", Foreground = BrushFromRgb(100,116,139), FontStyle = FontStyles.Italic, Margin = new Thickness(0,4,0,0) });

            // --- Ticket Timeline tab (full ticket history) ---
            var ticketTimelineScroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Padding = new Thickness(20), Background = BrushFromRgb(241,245,249) };
            var ticketTimelineStack = new StackPanel();
            ticketTimelineScroll.Content = ticketTimelineStack;
            var ticketTimelineTab = new TabItem { Header = "Ticket Timeline", Content = ticketTimelineScroll, Padding = new Thickness(14,8,14,8), FontWeight = FontWeights.SemiBold };
            _tabControl.Items.Add(ticketTimelineTab);
            var ticketCard = NewCard("Ticket Timeline", "\uE8A7", "Full ticket history: status, assignment, notes and field visit changes");
            ticketTimelineStack.Children.Add(ticketCard);
            var ticketInner = (StackPanel)ticketCard.Child;
            var filterRow = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0,8,0,0) };
            filterRow.Children.Add(new TextBlock { Text = "Show:", FontSize = 11.5, FontWeight = FontWeights.SemiBold, Foreground = BrushFromRgb(100,116,139), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0,0,8,0) });
            _ticketTimelineFilter = new ComboBox { MinWidth = 150, MinHeight = 30, Padding = new Thickness(8,4,8,4), Background = Brushes.White, BorderBrush = BrushFromRgb(203,213,225), BorderThickness = new Thickness(1) };
            _ticketTimelineFilter.Items.Add("All");
            _ticketTimelineFilter.Items.Add("Field visits");
            _ticketTimelineFilter.Items.Add("Notes");
            _ticketTimelineFilter.Items.Add("Status");
            _ticketTimelineFilter.SelectedIndex = 0;
            _ticketTimelineFilter.SelectionChanged += (_, __) => ApplyTicketTimelineFilter();
            filterRow.Children.Add(_ticketTimelineFilter);
            ticketInner.Children.Add(filterRow);
            _ticketTimelineHost = new StackPanel { Margin = new Thickness(0,8,0,0) };
            ticketInner.Children.Add(_ticketTimelineHost);
            _ticketTimelineHost.Children.Add(new TextBlock { Text = "Loading ticket timeline...", Foreground = BrushFromRgb(100,116,139), FontStyle = FontStyles.Italic, Margin = new Thickness(0,4,0,0) });

            // --- Photos tab with viewer ---
            var photosScroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Padding = new Thickness(20), Background = BrushFromRgb(241,245,249) };
            var photosStack = new StackPanel();
            photosScroll.Content = photosStack;
            var photosTab = new TabItem { Header = "Photos", Content = photosScroll, Padding = new Thickness(14,8,14,8), FontWeight = FontWeights.SemiBold };
            _tabControl.Items.Add(photosTab);
            var photoCard = NewCard("Evidence", "\uEB9F", "Photos and customer signature — double-click a row to preview, or select and view below");
            photosStack.Children.Add(photoCard);
            var photoInner = (StackPanel)photoCard.Child;
            var photoSummaryRow = new DockPanel { LastChildFill = false, Margin = new Thickness(0,8,0,0) };
            photoInner.Children.Add(photoSummaryRow);
            _photoSummaryLabel = new TextBlock { Text = "Loading photos...", FontSize = 12, FontWeight = FontWeights.SemiBold, Foreground = BrushFromRgb(71,85,105), VerticalAlignment = VerticalAlignment.Center };
            DockPanel.SetDock(_photoSummaryLabel, Dock.Left);
            photoSummaryRow.Children.Add(_photoSummaryLabel);
            _photoProgress = new ProgressBar { Minimum = 0, Maximum = 20, Height = 6, Width = 160, Margin = new Thickness(12,0,0,0), VerticalAlignment = VerticalAlignment.Center, Foreground = BrushFromRgb(37,99,235), Background = BrushFromRgb(226,232,240), BorderThickness = new Thickness(0) };
            DockPanel.SetDock(_photoProgress, Dock.Right);
            photoSummaryRow.Children.Add(_photoProgress);
            // Polished Evidence table — same treatment as WpfCallFieldWorkDialog (rounded wrapper, header pills, icons)
            var tableBorder = new Border { CornerRadius = new CornerRadius(12), BorderBrush = BrushFromRgb(226,232,240), BorderThickness = new Thickness(1), Background = Brushes.White, Margin = new Thickness(0,10,0,0), ClipToBounds = true, SnapsToDevicePixels = true };
            try { tableBorder.Effect = new System.Windows.Media.Effects.DropShadowEffect { BlurRadius = 14, ShadowDepth = 2, Opacity = 0.06, Direction = 270, Color = Colors.Black }; } catch { }
            _photoGrid = new DataGrid { AutoGenerateColumns = false, IsReadOnly = true, CanUserAddRows = false, CanUserDeleteRows = false, HeadersVisibility = DataGridHeadersVisibility.Column, GridLinesVisibility = DataGridGridLinesVisibility.None, BorderThickness = new Thickness(0), RowHeaderWidth = 0, Background = Brushes.White, AlternatingRowBackground = BrushFromRgb(248,250,252), MinHeight = 140, MaxHeight = 220, FontSize = 12.5, Cursor = Cursors.Hand, ColumnWidth = new DataGridLength(1, DataGridLengthUnitType.Auto), CanUserResizeColumns = true, CanUserSortColumns = true, SelectionMode = DataGridSelectionMode.Single, SelectionUnit = DataGridSelectionUnit.FullRow, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
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
            sizeTextFactory.SetBinding(TextBlock.TextProperty, new System.Windows.Data.Binding("FileSizeBytes") { Converter = new FileSizeConverter() });
            sizeBorderFactory.AppendChild(sizeTextFactory);
            sizeCol.CellTemplate = new DataTemplate { VisualTree = sizeBorderFactory };
            _photoGrid.Columns.Add(sizeCol);
            var uploadedCol = new DataGridTemplateColumn { Header = "Uploaded", Width = new DataGridLength(170), SortMemberPath = "UploadedAt" };
            var uploadedFactory = new FrameworkElementFactory(typeof(TextBlock));
            uploadedFactory.SetBinding(TextBlock.TextProperty, new System.Windows.Data.Binding("UploadedAt") { StringFormat = "MMM d, yyyy h:mm tt" });
            uploadedFactory.SetValue(TextBlock.ForegroundProperty, BrushFromRgb(71,85,105));
            uploadedFactory.SetValue(TextBlock.FontSizeProperty, 12.0);
            uploadedCol.CellTemplate = new DataTemplate { VisualTree = uploadedFactory };
            _photoGrid.Columns.Add(uploadedCol);
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
            _photoGrid.MouseDoubleClick += async (_, __) => await PreviewSelectedPhotoAsync();
            _photoGrid.SelectionChanged += async (_, __) => await PreviewSelectedPhotoAsync();
            tableBorder.Child = _photoGrid;
            photoInner.Children.Add(tableBorder);
            // Cleaner pill buttons — match the Field Work dialog toolbar
            var photoActions = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0,10,0,0), HorizontalAlignment = HorizontalAlignment.Right };
            photoInner.Children.Add(photoActions);
            var viewBtn = CreatePillButton("View", "\uE8A7", BrushFromRgb(37,99,235), isPrimary:true);
            viewBtn.Click += async (_, __) => await PreviewSelectedPhotoAsync();
            var openFullBtn = CreatePillButton("Open Full", "\uE8B0", BrushFromRgb(51,65,85), isPrimary:true);
            openFullBtn.Click += async (_, __) => await OpenSelectedPhotoFullAsync();
            photoActions.Children.Add(viewBtn); photoActions.Children.Add(openFullBtn);
            _photoPreviewLabel = new TextBlock { Text = "Select a photo to preview", Foreground = BrushFromRgb(100,116,139), FontStyle = FontStyles.Italic, Margin = new Thickness(0,10,0,6), FontSize=12 };
            photoInner.Children.Add(_photoPreviewLabel);
            _photoPreviewBorder = new Border { Background = BrushFromRgb(248,250,252), CornerRadius = new CornerRadius(12), BorderBrush = BrushFromRgb(226,232,240), BorderThickness = new Thickness(1), Padding = new Thickness(12), MinHeight = 220, Visibility = Visibility.Collapsed };
            photoInner.Children.Add(_photoPreviewBorder);
            _photoPreviewImage = new Image { Stretch = Stretch.Uniform, MaxHeight = 420, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
            var previewScroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, Content = _photoPreviewImage, MaxHeight = 420 };
            _photoPreviewBorder.Child = previewScroll;

            Loaded += async (_, __) => await LoadAsync();
        }

        private void BuildDetailSection()
        {
            var r = _row;
            _detailHost.Children.Clear();

            // Status pill row
            var pillRow = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0,4,0,0) };
            _detailHost.Children.Add(pillRow);
            var pill = new Border { CornerRadius = new CornerRadius(10), Padding = new Thickness(12,5,12,5), HorizontalAlignment = HorizontalAlignment.Left, Background = GetStatusBrush(r.Status) };
            pill.Child = new TextBlock { Text = (r.Status ?? "-").ToUpperInvariant(), Foreground = Brushes.White, FontWeight = FontWeights.SemiBold, FontSize = 11.5 };
            pillRow.Children.Add(pill);
            pillRow.Children.Add(new TextBlock { Text = $"Visit #{r.FieldVisitId}  •  Ticket #{r.TicketId}", Foreground = BrushFromRgb(100,116,139), FontSize = 12, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(10,0,0,0) });
            if (r.HasSignature)
            {
                var sigPill = new Border { CornerRadius = new CornerRadius(8), Background = BrushFromRgb(220,252,231), Padding = new Thickness(8,3,8,3), Margin = new Thickness(10,0,0,0), VerticalAlignment = VerticalAlignment.Center, BorderBrush = BrushFromRgb(134,239,172), BorderThickness = new Thickness(1) };
                sigPill.Child = new TextBlock { Text = "✓ Signature", FontSize = 11, FontWeight = FontWeights.SemiBold, Foreground = BrushFromRgb(22,101,52) };
                pillRow.Children.Add(sigPill);
            }
            if (r.AttachmentCount > 0)
            {
                var attPill = new Border { CornerRadius = new CornerRadius(8), Background = BrushFromRgb(219,234,254), Padding = new Thickness(8,3,8,3), Margin = new Thickness(8,0,0,0), VerticalAlignment = VerticalAlignment.Center, BorderBrush = BrushFromRgb(147,197,253), BorderThickness = new Thickness(1) };
                attPill.Child = new TextBlock { Text = $"{r.AttachmentCount} photo(s)", FontSize = 11, FontWeight = FontWeights.SemiBold, Foreground = BrushFromRgb(29,78,216) };
                pillRow.Children.Add(attPill);
            }

            // Grid of key values
            var grid = new Grid { Margin = new Thickness(0,14,0,0) };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            _detailHost.Children.Add(grid);
            grid.Children.Add(MakeDetailCell("Technician", r.TechnicianName ?? "—", 0, 0));
            // Stored UTC: convert back for display (matches the schedule dialog).
            grid.Children.Add(MakeDetailCell("Scheduled", r.ScheduledAt.HasValue ? r.ScheduledAt.Value.ToLocalTime().ToString("MMM d, yyyy h:mm tt") : "—", 1, 0));
            grid.Children.Add(MakeDetailCell("Completed", r.CompletedAt.HasValue ? r.CompletedAt.Value.ToLocalTime().ToString("MMM d, yyyy h:mm tt") : "Not yet", 2, 0));
            grid.Children.Add(MakeDetailCell("Created", r.CreatedAt.ToLocalTime().ToString("MMM d, yyyy h:mm tt"), 0, 1));
            grid.Children.Add(MakeDetailCell("Ticket", r.TicketCode ?? $"#{r.TicketId}", 1, 1));
            grid.Children.Add(MakeDetailCell("Location", string.IsNullOrWhiteSpace(r.Location) ? "—" : r.Location, 2, 1));

            if (!string.IsNullOrWhiteSpace(r.Notes))
            {
                var noteCard = new Border { Background = Brushes.White, CornerRadius = new CornerRadius(10), Padding = new Thickness(14), Margin = new Thickness(0,12,0,0), BorderBrush = BrushFromRgb(226,232,240), BorderThickness = new Thickness(1) };
                var noteStack = new StackPanel();
                noteCard.Child = noteStack;
                noteStack.Children.Add(new TextBlock { Text = "Notes", FontSize = 11, FontWeight = FontWeights.Bold, Foreground = BrushFromRgb(71,85,105), Margin = new Thickness(0,0,0,6) });
                noteStack.Children.Add(new TextBlock { Text = r.Notes, Foreground = BrushFromRgb(30,41,59), TextWrapping = TextWrapping.Wrap, FontSize = 13 });
                _detailHost.Children.Add(noteCard);
            }

            // Hint for cancelled reschedulable
            if (string.Equals(r.Status, "Cancelled", StringComparison.OrdinalIgnoreCase))
            {
                _detailHost.Children.Add(new Border { Background = BrushFromRgb(254,242,242), BorderBrush = BrushFromRgb(252,165,165), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(10), Padding = new Thickness(12,10,12,10), Margin = new Thickness(0,12,0,0), Child = new TextBlock { Text = "Cancelled — this visit can be rescheduled (Cancelled → Scheduled). Use the Field Work dialog for the ticket to reschedule.", Foreground = BrushFromRgb(127,29,29), FontWeight = FontWeights.SemiBold, TextWrapping = TextWrapping.Wrap, FontSize=12 } });
            }

            UpdateSignOffBanner();
        }

        private void UpdateSignOffBanner()
        {
            var lastSignOff = _gateHistory
                .Where(h => string.Equals(h.FieldName, "SignOff", StringComparison.OrdinalIgnoreCase) && string.Equals(h.NewValue, "Signed", StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(h => h.ChangedAt).FirstOrDefault();
            if (lastSignOff == null) return;
            var st = (_gateTicket?.Status ?? "").Trim();
            var ticketFinal = st.Equals("Solved", StringComparison.OrdinalIgnoreCase)
                || st.Equals("Closed", StringComparison.OrdinalIgnoreCase)
                || st.Equals("Resolved (Temporary)", StringComparison.OrdinalIgnoreCase);
            var text = ticketFinal
                ? $"Signed off {lastSignOff.ChangedAt:MMM d, yyyy h:mm tt} by {lastSignOff.ChangedByName ?? "—"}. Reprinting uses current photos and signature."
                : "Signed — ticket Reopened (re-sign required). Prior sign-off stays in history.";
            _detailHost.Children.Add(new Border { Background = ticketFinal ? BrushFromRgb(220,252,231) : BrushFromRgb(254,243,199), BorderBrush = ticketFinal ? BrushFromRgb(134,239,172) : BrushFromRgb(252,211,77), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(10), Padding = new Thickness(12,10,12,10), Margin = new Thickness(0,12,0,0), Child = new TextBlock { Text = "✓ " + text, Foreground = ticketFinal ? BrushFromRgb(22,101,52) : BrushFromRgb(146,64,14), FontWeight = FontWeights.SemiBold, TextWrapping = TextWrapping.Wrap, FontSize = 12 } });
        }

        private Border MakeDetailCell(string label, string value, int col, int row)
        {
            var panel = new StackPanel { Margin = new Thickness(0,0,12,8) };
            panel.Children.Add(new TextBlock { Text = label, FontSize = 11, FontWeight = FontWeights.SemiBold, Foreground = BrushFromRgb(100,116,139), Margin = new Thickness(0,0,0,4) });
            panel.Children.Add(new TextBlock { Text = value ?? "—", Foreground = BrushFromRgb(15,23,42), FontWeight = FontWeights.SemiBold, FontSize = 13, TextWrapping = TextWrapping.Wrap });
            var border = new Border { Child = panel, Background = BrushFromRgb(248,250,252), CornerRadius = new CornerRadius(10), Padding = new Thickness(12,10,12,10), Margin = new Thickness(0,0,8,0), BorderBrush = BrushFromRgb(226,232,240), BorderThickness = new Thickness(1) };
            Grid.SetColumn(border, col); Grid.SetRow(border, row);
            return border;
        }

        private async Task LoadAsync()
        {
            _statusLine.Text = "Loading timelines and photos...";
            try
            {
                // Histories
                var history = await _repo.GetTicketHistoryAsync(_row.TicketId, 200);
                var fieldHistory = history.Where(h => string.Equals(h.FieldName, "FieldVisitStatus", StringComparison.OrdinalIgnoreCase)).OrderBy(h => h.ChangedAt).ToList();
                RenderTimeline(fieldHistory);
                RenderTicketTimeline(history.OrderByDescending(h => h.ChangedAt).Take(60).ToList());

                // Sign-off gate data
                try
                {
                    _gateHistory = history;
                    _gateTicket = await _repo.GetTicketByIdAsync(_row.TicketId);
                    _gateVisits = await _repo.GetFieldVisitsAsync(_row.TicketId) ?? new List<CallFieldVisitItem>();
                    // Refresh the constructor snapshot so the header, grid,
                    // and photo summary agree with the gates below.
                    var fresh = _gateVisits.FirstOrDefault(v => v.FieldVisitId == _row.FieldVisitId);
                    if (fresh != null)
                    {
                        _row.Status = fresh.Status;
                        _row.HasSignature = fresh.HasSignature;
                        _row.ScheduledAt = fresh.ScheduledAt;
                        _row.CompletedAt = fresh.CompletedAt;
                        _row.Notes = fresh.Notes;
                        _row.TechnicianName = fresh.TechnicianName ?? _row.TechnicianName;
                    }
                }
                catch { }
                BuildDetailSection();
                UpdateSignOffButtons();

                // Attachments (keyed by visit, not ticket)
                var atts = await _repo.GetFieldVisitAttachmentsByVisitAsync(_row.FieldVisitId);
                var filtered = atts;
                _photoGrid.ItemsSource = filtered;
                if (filtered.Count > 0)
                {
                    _photoGrid.SelectedIndex = 0;
                    await PreviewSelectedPhotoAsync();
                }
                else
                {
                    _photoPreviewBorder.Visibility = Visibility.Collapsed;
                    _photoPreviewLabel.Text = "No photos attached to this visit.";
                }
                if (_photoSummaryLabel != null)
                {
                    var sigTxt = _row.HasSignature ? "signature on file" : "no signature";
                    _photoSummaryLabel.Text = $"{filtered.Count}/20 photos • {sigTxt}";
                    _photoSummaryLabel.Foreground = filtered.Count >= 20 ? BrushFromRgb(185, 28, 28) : BrushFromRgb(71, 85, 105);
                }
                if (_photoProgress != null) _photoProgress.Value = Math.Min(20, filtered.Count);
                _statusLine.Text = $"{filtered.Count} photo(s) • {history.Count} ticket events • {(string.Equals(_row.Status, "Cancelled", StringComparison.OrdinalIgnoreCase) ? "Cancelled — reschedulable" : _row.Status)} • Visit #{_row.FieldVisitId}";
            }
            catch (Exception ex)
            {
                _timelineHost.Children.Clear();
                _timelineHost.Children.Add(new TextBlock { Text = "Timeline failed to load: " + ex.Message, Foreground = BrushFromRgb(220,38,38), TextWrapping = TextWrapping.Wrap });
                _ticketTimelineHost.Children.Clear();
                _ticketTimelineHost.Children.Add(new TextBlock { Text = "Ticket timeline failed: " + ex.Message, Foreground = BrushFromRgb(220,38,38), TextWrapping = TextWrapping.Wrap });
                _statusLine.Text = "Timeline load failed.";
            }
        }

        private void UpdateSignOffButtons()
        {
            var ok = CanSignOffQuick(out _);
            if (_generatePdfButton != null) _generatePdfButton.IsEnabled = ok;
            if (_signOffButton != null) _signOffButton.IsEnabled = ok;
        }

        private bool CanSignOffQuick(out string reason)
        {
            reason = null;
            if (!AppSession.IsLoggedIn || AppSession.CurrentUserId <= 0) { reason = "You must be logged in to sign off."; return false; }
            if (_gateTicket == null) { reason = "Ticket not found."; return false; }
            var st = (_gateTicket.Status ?? string.Empty).Trim();
            if (!st.Equals("Solved", StringComparison.OrdinalIgnoreCase) && !st.Equals("Closed", StringComparison.OrdinalIgnoreCase)
                && !st.Equals("Resolved (Temporary)", StringComparison.OrdinalIgnoreCase)) { reason = "Ticket must be Solved or Closed (current: " + st + ")."; return false; }
            var visit = _gateVisits.FirstOrDefault(v => v.FieldVisitId == _row.FieldVisitId) ?? _gateVisits.FirstOrDefault();
            if (visit == null) { reason = "No field visit found."; return false; }
            if (!string.Equals(visit.Status, "Completed", StringComparison.OrdinalIgnoreCase)) { reason = "Field visit must be Completed (current: " + (visit.Status ?? "-") + ")."; return false; }
            if (!visit.HasSignature) { reason = "Customer signature is required before sign-off. Capture it on the completed visit, then sign off."; return false; }
            if (_gateVisits.Any(v => string.Equals(v.Status, "Scheduled", StringComparison.OrdinalIgnoreCase))) { reason = "An open (Scheduled) field visit exists."; return false; }
            return true;
        }

        private async Task<bool> CheckTemporaryReturnAsync()
        {
            try
            {
                if (_gateTicket != null && string.Equals((_gateTicket.Status ?? string.Empty).Trim(), "Resolved (Temporary)", StringComparison.OrdinalIgnoreCase))
                {
                    var preview = await _repo.GetTemporaryReplacementReturnPreviewAsync(_row.TicketId);
                    // Service-Only temporaries issue no parts - nothing to return.
                    if (preview.NewItemId > 0 && !preview.AlreadyReturned)
                    {
                        MessageBox.Show(this, "Temporary replacement has not been returned yet.", "Sign Off", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return false;
                    }
                }
            }
            catch { }
            return true;
        }

        private async Task<TicketSignOffReportModel> BuildReportModelAsync()
        {
            var ticket = _gateTicket;
            CallTicketNotificationData notify = null;
            try { notify = await _repo.GetTicketNotificationDataAsync(_row.TicketId); } catch { }
            var history = (_gateHistory ?? new List<CallTicketHistoryItem>()).OrderByDescending(h => h.ChangedAt).Take(60).ToList();
            List<CallTicketNoteItem> notes = new List<CallTicketNoteItem>();
            try { notes = await _repo.GetTicketNotesAsync(_row.TicketId, 200) ?? notes; } catch { }
            var visits = _gateVisits ?? new List<CallFieldVisitItem>();
            List<CallFieldVisitAttachmentItem> allAtts = new List<CallFieldVisitAttachmentItem>();
            try { allAtts = await _repo.GetFieldVisitAttachmentsAsync(_row.TicketId) ?? allAtts; } catch { }

            var model = new TicketSignOffReportModel
            {
                TicketId = _row.TicketId,
                TicketCode = _row.TicketCode ?? ticket?.TicketCode ?? ("#" + _row.TicketId),
                Status = ticket?.Status ?? _row.Status,
                Priority = ticket?.Priority,
                Caller = notify?.CallerName ?? ticket?.CallerName,
                Department = notify?.Department ?? ticket?.Department ?? _row.Department,
                Branch = notify?.Branch ?? ticket?.Branch ?? _row.Branch,
                Assignee = notify?.AssignedTo ?? ticket?.ResponsiblePerson ?? _row.TechnicianName,
                Issue = notify?.Issue ?? ticket?.Issue ?? _row.Issue,
                Solution = notify?.ProvidedSolution,
                CreatedAt = notify?.CreatedAt ?? ticket?.CreatedAt ?? _row.CreatedAt,
                UpdatedAt = notify?.UpdatedAt ?? ticket?.UpdatedAt ?? DateTime.UtcNow,
                SolvedAt = notify?.SolvedAt ?? ticket?.SolvedAt,
                AgeDays = ticket?.TicketAgeDays ?? 0,
                GeneratedAt = DateTime.Now,
                GeneratedBy = AppSession.IsLoggedIn ? AppSession.CurrentUserName : null
            };

            foreach (var h in history)
            {
                var old = (h.OldValue ?? string.Empty).Trim();
                var @new = (h.NewValue ?? string.Empty).Trim();
                // Drop: both values empty and no note.
                if (string.IsNullOrWhiteSpace(old) && string.IsNullOrWhiteSpace(@new) && string.IsNullOrWhiteSpace(h.Note)) continue;
                var change = string.IsNullOrWhiteSpace(old) ? (string.IsNullOrWhiteSpace(@new) ? "Updated" : @new) : (string.IsNullOrWhiteSpace(@new) ? old : old + " → " + @new);
                // Drop pure-noise: tiny change text or bare numbers (e.g. "139", "1412", "High" handled below by key-event filter).
                var changeTrimmed = (change ?? string.Empty).Trim();
                if (changeTrimmed.Length < 3) continue;
                if (System.Text.RegularExpressions.Regex.IsMatch(changeTrimmed, @"^\d+$")) continue;
                // Keep key events only: status/sign-off fields, or a Completed/Cancelled/Scheduled/Solved/Closed transition.
                var field = (h.FieldName ?? string.Empty).Trim();
                var isKeyField = string.Equals(field, "Status", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(field, "FieldVisitStatus", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(field, "SignOff", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(field, "FieldVisitSignature", StringComparison.OrdinalIgnoreCase);
                var newLower = @new.ToLowerInvariant();
                var newIsStatusWord = newLower.Contains("completed") || newLower.Contains("cancelled") || newLower.Contains("scheduled") || newLower.Contains("solved") || newLower.Contains("closed");
                var isKeyTransition = change.Contains("→") && newIsStatusWord;
                if (!isKeyField && !isKeyTransition) continue;
                var line = (h.ChangedAt.HasValue ? h.ChangedAt.Value.ToString("MMM d, h:mm tt") : "-") + " — " + change + " (" + (h.ChangedByName ?? "System") + ")";
                if (!string.IsNullOrWhiteSpace(h.Note)) line += " — " + h.Note.Trim();
                // Dedup identical final lines (case-insensitive), keep order (newest first).
                if (model.HistoryLines.Any(existing => string.Equals(existing, line, StringComparison.OrdinalIgnoreCase))) continue;
                if (model.HistoryLines.Count >= 10) continue;
                model.HistoryLines.Add(line);
                // Structured twin for the PDF timeline table (STATUS = resulting
                // state; visit transitions render Old → New; cap 15 rows).
                if (model.HistoryRows.Count < 15)
                {
                    var by = (h.ChangedByName ?? "System").Trim();
                    var noteText = (h.Note ?? string.Empty).Trim();
                    string rowStatus;
                    if (string.IsNullOrWhiteSpace(@new)) rowStatus = old;
                    else if (string.IsNullOrWhiteSpace(old)) rowStatus = @new;
                    else rowStatus = old + " → " + @new;
                    model.HistoryRows.Add(new TicketSignOffHistoryRow
                    {
                        ChangedAt = h.ChangedAt,
                        Status = rowStatus,
                        Details = string.IsNullOrWhiteSpace(noteText) ? "(" + by + ")" : noteText + " (" + by + ")"
                    });
                }
            }
            model.HistoryTotalCount = history.Count;
            // Resolution card label for the PDF (TEMPORARY SERVICE vs others).
            var lastRes = history.FirstOrDefault(h => string.Equals((h.FieldName ?? string.Empty).Trim(), "ResolutionType", StringComparison.OrdinalIgnoreCase));
            var resVal = (lastRes?.NewValue ?? string.Empty).Trim();
            var tempNow = string.Equals((model.Status ?? string.Empty).Trim(), "Resolved (Temporary)", StringComparison.OrdinalIgnoreCase);
            model.ResolutionTypeLabel =
                tempNow && resVal.Equals("Service Only", StringComparison.OrdinalIgnoreCase) ? "TEMPORARY SERVICE" :
                tempNow && string.IsNullOrWhiteSpace(resVal) ? "TEMPORARY" :
                tempNow ? "TEMPORARY REPLACEMENT" :
                string.IsNullOrWhiteSpace(resVal) ? "SERVICE ONLY" : resVal.ToUpperInvariant();
            if (model.HistoryLines.Count == 0)
            {
                model.HistoryLines.Add("No key status changes on file.");
            }
            else if (history.Count > model.HistoryLines.Count)
            {
                model.HistoryLines.Add("+" + (history.Count - model.HistoryLines.Count) + " earlier events on file.");
            }
            foreach (var n in notes)
                model.NotesLines.Add("[" + (n.NoteType ?? "Note") + "] " + (n.NoteText ?? string.Empty).Trim() + " (" + (n.CreatedByName ?? "—") + (n.CreatedAt.HasValue ? ", " + n.CreatedAt.Value.ToString("MMM d, h:mm tt") : string.Empty) + ")");

            foreach (var v in visits)
            {
                var vm = new TicketSignOffVisit
                {
                    VisitId = v.FieldVisitId,
                    Status = v.Status,
                    TechnicianName = v.TechnicianName,
                    ScheduledAt = v.ScheduledAt,
                    CompletedAt = v.CompletedAt,
                    Location = v.Location,
                    Notes = v.Notes,
                    HasSignature = v.HasSignature
                };
                foreach (var a in allAtts.Where(a => a.FieldVisitId == v.FieldVisitId).Take(20))
                {
                    byte[] thumb = null;
                    try { thumb = await _repo.GetFieldVisitAttachmentThumbnailBytesAsync(a.AttachmentId); } catch { }
                    if (thumb == null || thumb.Length == 0)
                    {
                        try { thumb = await _repo.GetFieldVisitAttachmentBytesAsync(a.AttachmentId); } catch { }
                    }
                    vm.Photos.Add(new TicketSignOffPhoto { FileName = a.FileName, SizeBytes = a.FileSizeBytes ?? 0, UploadedBy = a.UploadedByName, UploadedAt = a.UploadedAt, ThumbBytes = thumb });
                }
                model.Visits.Add(vm);
            }

            model.ResolutionLines.AddRange(history
                .Where(h => (h.FieldName ?? string.Empty).StartsWith("Resolution", StringComparison.OrdinalIgnoreCase))
                .Select(h => (h.FieldName ?? "").Trim() + ": " + (h.NewValue ?? "").Trim()));
            model.ResolutionLines.AddRange(notes
                .Where(n => string.Equals(n.NoteType, "Resolution", StringComparison.OrdinalIgnoreCase) || string.Equals(n.NoteType, "Replacement", StringComparison.OrdinalIgnoreCase))
                .Select(n => "[" + n.NoteType + "] " + (n.NoteText ?? string.Empty).Trim()));
            if (model.ResolutionLines.Count == 0) model.ResolutionLines.Add("Status: " + (model.Status ?? "-"));

            try
            {
                var emails = await _repo.GetEmailLogPageAsync(_row.TicketId.ToString(), null, 1, 20) ?? new List<CallEmailLogItem>();
                foreach (var e in emails)
                    model.EmailLines.Add(e.DateSent.ToString("MMM d, h:mm tt") + " " + (e.EmailType ?? "-") + " → " + (e.Recipient ?? "—") + " [" + (e.Status ?? "-") + "]");
            }
            catch { }

            var currentVisit = visits.FirstOrDefault(v => v.FieldVisitId == _row.FieldVisitId) ?? visits.FirstOrDefault();
            if (currentVisit != null && currentVisit.HasSignature)
            {
                try { model.CustomerSignatureBytes = await _repo.GetFieldVisitSignatureBytesAsync(currentVisit.FieldVisitId); } catch { }
            }
            model.CustomerName = model.Caller;
            model.TechnicianName = currentVisit?.TechnicianName ?? model.Assignee;
            model.TechnicianSignedAt = currentVisit?.CompletedAt;
            using (var sha = SHA256.Create())
            {
                var sigFlag = model.CustomerSignatureBytes != null && model.CustomerSignatureBytes.Length > 0 ? "sig1" : "sig0";
                var rawStr = model.TicketId + ":" + _row.FieldVisitId + ":" + (model.SolvedAt.HasValue ? model.SolvedAt.Value.Ticks.ToString() : "nosolve")
                    + ":" + model.HistoryLines.Count + ":" + model.Visits.Sum(v => v.Photos.Count) + ":" + sigFlag
                    + ":" + string.Join("|", model.HistoryLines.Take(60));
                var raw = Encoding.UTF8.GetBytes(rawStr);
                model.ReportHash = BitConverter.ToString(sha.ComputeHash(raw)).Replace("-", string.Empty).Substring(0, 8).ToLowerInvariant();
            }
            return model;
        }

        private bool _signOffRunning;

        private async Task GenerateSignOffPdfAsync(bool recordSignOff)
        {
            // Re-entrancy guard: double-clicks must not duplicate PDFs or
            // sign-off rows (the repo insert is additionally de-duped).
            if (_signOffRunning) return;
            _signOffRunning = true;
            try
            {
            if (!CanSignOffQuick(out var reason))
            {
                MessageBox.Show(this, reason ?? "Sign-off is not available for this visit.", "Sign-Off PDF", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            if (!await CheckTemporaryReturnAsync()) return;
            var blockReason2 = await _repo.ValidateTicketSignOffAsync(_row.TicketId, _row.FieldVisitId);
            if (!string.IsNullOrEmpty(blockReason2))
            {
                MessageBox.Show(this, blockReason2, "Sign Off", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            var atts = new List<CallFieldVisitAttachmentItem>();
            try { atts = await _repo.GetFieldVisitAttachmentsByVisitAsync(_row.FieldVisitId) ?? new List<CallFieldVisitAttachmentItem>(); } catch { }
            if (atts.Count == 0)
            {
                var cont = MessageBox.Show(this, "No photos attached to this visit. Continue anyway?", "Sign-Off PDF", MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (cont != MessageBoxResult.Yes) return;
            }
            try
            {
                _statusLine.Text = "Building sign-off report...";
                var model = await BuildReportModelAsync();
                var dlg = new Microsoft.Win32.SaveFileDialog
                {
                    FileName = (model.TicketCode ?? ("TCK-" + model.TicketId)).Replace(" ", string.Empty) + "_SignOff_" + DateTime.Now.ToString("yyyyMMdd") + ".pdf",
                    Filter = "PDF files|*.pdf",
                    Title = "Save Sign-Off Report"
                };
                if (dlg.ShowDialog() != true) { _statusLine.Text = "Sign-off report cancelled."; return; }
                FieldVisitCompletionPdfGenerator.Generate(model, dlg.FileName);
                if (recordSignOff)
                {
                    var userId = AppSession.IsLoggedIn && AppSession.CurrentUserId > 0 ? (int?)AppSession.CurrentUserId : null;
                    await _repo.RecordTicketSignOffAsync(_row.TicketId, _row.FieldVisitId, model.ReportHash, model.CustomerName, model.TechnicianName, userId);
                    await LoadAsync();
                }
                _statusLine.Text = "Sign-off report saved.";
                FieldVisitCompletionPdfGenerator.TryOpen(dlg.FileName);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "Sign-Off PDF", MessageBoxButton.OK, MessageBoxImage.Error);
                _statusLine.Text = "Sign-off report failed.";
            }
            }
            finally
            {
                _signOffRunning = false;
            }
        }

        private async Task SignOffAsync()
        {
            await GenerateSignOffPdfAsync(recordSignOff: true);
        }

        private async Task PreviewSelectedPhotoAsync()
        {
            var item = _photoGrid.SelectedItem as CallFieldVisitAttachmentItem;
            if (item == null)
            {
                _photoPreviewBorder.Visibility = Visibility.Collapsed;
                _photoPreviewLabel.Text = "Select a photo to preview";
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
                    _photoPreviewLabel.Text = $"No preview available for {item.FileName}";
                    return;
                }
                using (var ms = new MemoryStream(bytes))
                {
                    var bmp = new BitmapImage();
                    bmp.BeginInit();
                    bmp.CacheOption = BitmapCacheOption.OnLoad;
                    bmp.StreamSource = ms;
                    bmp.EndInit();
                    bmp.Freeze();
                    _photoPreviewImage.Source = bmp;
                    _photoPreviewLabel.Text = $"{item.FileName} • {(item.FileSizeBytes ?? bytes.Length)/1024} KB • {item.UploadedByName ?? "—"} • {item.UploadedAt:MMM d, yyyy h:mm tt}";
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
                using (var ms = new MemoryStream(bytes))
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

        private void RenderTimeline(List<CallTicketHistoryItem> items)
        {
            _timelineHost.Children.Clear();
            if (items == null || items.Count == 0)
            {
                _timelineHost.Children.Add(new Border { Background = BrushFromRgb(248,250,252), CornerRadius = new CornerRadius(10), Padding = new Thickness(14), BorderBrush = BrushFromRgb(226,232,240), BorderThickness = new Thickness(1), Child = new TextBlock { Text = "No status history yet — this visit was just scheduled.", Foreground = BrushFromRgb(100,116,139), FontStyle = FontStyles.Italic } });
                // Show at least current status as timeline entry from row
                items = new List<CallTicketHistoryItem>
                {
                    new CallTicketHistoryItem { FieldName = "FieldVisitStatus", OldValue = null, NewValue = _row.Status, ChangedAt = _row.CreatedAt, ChangedByName = "System", Note = _row.Notes }
                };
            }

            // Summary hero (mirrors mobile Field Timeline header)
            var reschedules = items.Count(h => string.Equals(h.OldValue, "Cancelled", StringComparison.OrdinalIgnoreCase) && string.Equals(h.NewValue, "Scheduled", StringComparison.OrdinalIgnoreCase));
            var summary = new Border { CornerRadius = new CornerRadius(10), Background = BrushFromRgb(239,246,255), BorderBrush = BrushFromRgb(147,197,253), BorderThickness = new Thickness(1), Padding = new Thickness(12,10,12,10), Margin = new Thickness(0,0,0,12) };
            var summaryRow = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            summary.Child = summaryRow;
            var statusPill = new Border { CornerRadius = new CornerRadius(8), Background = GetStatusBrush(_row.Status), Padding = new Thickness(10,4,10,4), VerticalAlignment = VerticalAlignment.Center };
            statusPill.Child = new TextBlock { Text = (_row.Status ?? "-").ToUpperInvariant(), Foreground = Brushes.White, FontWeight = FontWeights.Bold, FontSize = 11 };
            summaryRow.Children.Add(statusPill);
            summaryRow.Children.Add(new TextBlock { Text = $"{items.Count} change(s) • {reschedules} reschedule(s) • newest first", Foreground = BrushFromRgb(29,78,216), FontWeight = FontWeights.SemiBold, FontSize = 12, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(10,0,0,0) });
            _timelineHost.Children.Add(summary);

            // Day-grouped vertical timeline (newest first, like mobile)
            var groups = items.OrderByDescending(h => h.ChangedAt).GroupBy(h => h.ChangedAt.HasValue ? h.ChangedAt.Value.ToString("MMM d, yyyy") : "Unknown date").ToList();
            foreach (var g in groups)
            {
                _timelineHost.Children.Add(new TextBlock { Text = $"{g.Key} • {g.Count()}", FontSize = 11, FontWeight = FontWeights.Bold, Foreground = BrushFromRgb(100,116,139), Margin = new Thickness(0,8,0,8) });
                var list = g.ToList();
                for (int i = 0; i < list.Count; i++)
                {
                    var h = list[i];
                    var isLast = i == list.Count - 1;
                    var isReschedule = string.Equals(h.OldValue, "Cancelled", StringComparison.OrdinalIgnoreCase) && string.Equals(h.NewValue, "Scheduled", StringComparison.OrdinalIgnoreCase);
                    var row = new Grid { Margin = new Thickness(0,0,0, isLast ? 0 : 14) };
                    row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(28) });
                    row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                    // Dot + line
                    var dotCol = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center };
                    Grid.SetColumn(dotCol, 0);
                    row.Children.Add(dotCol);
                    var dot = new Border { Width = 12, Height = 12, CornerRadius = new CornerRadius(6), Background = GetHistoryDotBrush(h.NewValue), BorderBrush = Brushes.White, BorderThickness = new Thickness(2), HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(0,4,0,0) };
                    try { dot.Effect = new System.Windows.Media.Effects.DropShadowEffect { BlurRadius = 6, ShadowDepth = 1, Opacity = 0.15, Color = Colors.Black }; } catch { }
                    dotCol.Children.Add(dot);
                    if (!isLast)
                    {
                        var line = new Border { Width = 2, Background = BrushFromRgb(226,232,240), Margin = new Thickness(0,2,0,0), HorizontalAlignment = HorizontalAlignment.Center, Height = 44 };
                        dotCol.Children.Add(line);
                    }
                    // Card
                    var card = new Border { CornerRadius = new CornerRadius(12), Padding = new Thickness(14,12,14,12), Background = isReschedule ? BrushFromRgb(254,249,195) : Brushes.White, BorderBrush = isReschedule ? BrushFromRgb(253,224,71) : BrushFromRgb(226,232,240), BorderThickness = new Thickness(1) };
                    Grid.SetColumn(card, 1);
                    row.Children.Add(card);
                    var cardStack = new StackPanel();
                    card.Child = cardStack;
                    var header = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
                    cardStack.Children.Add(header);
                    var newPill = new Border { CornerRadius = new CornerRadius(8), Background = GetStatusBrush(h.NewValue), Padding = new Thickness(8,3,8,3), VerticalAlignment = VerticalAlignment.Center };
                    newPill.Child = new TextBlock { Text = (h.NewValue ?? "-").ToUpperInvariant(), Foreground = Brushes.White, FontWeight = FontWeights.Bold, FontSize = 11 };
                    header.Children.Add(newPill);
                    if (!string.IsNullOrWhiteSpace(h.OldValue))
                    {
                        header.Children.Add(new TextBlock { Text = $"from {h.OldValue}", Foreground = BrushFromRgb(100,116,139), FontSize=11, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8,0,0,0) });
                    }
                    if (isReschedule)
                    {
                        var rs = new Border { CornerRadius = new CornerRadius(6), Background = BrushFromRgb(250,204,21), Padding = new Thickness(6,2,6,2), Margin = new Thickness(8,0,0,0), VerticalAlignment = VerticalAlignment.Center };
                        rs.Child = new TextBlock { Text = "RESCHEDULE", FontSize=10, FontWeight = FontWeights.Bold, Foreground = BrushFromRgb(113,63,18) };
                        header.Children.Add(rs);
                    }
                    var timeLine = $"{h.ChangedAt:MMM d, yyyy h:mm tt} • {h.ChangedByName ?? "System"}";
                    cardStack.Children.Add(new TextBlock { Text = timeLine, Foreground = BrushFromRgb(100,116,139), FontSize=11, Margin = new Thickness(0,6,0,0) });
                    if (!string.IsNullOrWhiteSpace(h.Note))
                    {
                        cardStack.Children.Add(new TextBlock { Text = h.Note, Foreground = BrushFromRgb(30,41,59), FontSize=12, FontWeight = FontWeights.SemiBold, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0,6,0,0) });
                    }
                    _timelineHost.Children.Add(row);
                }
            }
        }

        private void RenderTicketTimeline(List<CallTicketHistoryItem> items)
        {
            _ticketHistoryAll = (items ?? new List<CallTicketHistoryItem>()).OrderByDescending(x => x.ChangedAt).Take(60).ToList();
            ApplyTicketTimelineFilter();
        }

        private void ApplyTicketTimelineFilter()
        {
            if (_ticketTimelineHost == null) return;
            _ticketTimelineHost.Children.Clear();
            var all = _ticketHistoryAll ?? new List<CallTicketHistoryItem>();
            if (all.Count == 0)
            {
                _ticketTimelineHost.Children.Add(new Border { Background = BrushFromRgb(248,250,252), CornerRadius = new CornerRadius(10), Padding = new Thickness(14), BorderBrush = BrushFromRgb(226,232,240), BorderThickness = new Thickness(1), Child = new TextBlock { Text = "No ticket history yet.", Foreground = BrushFromRgb(100,116,139), FontStyle = FontStyles.Italic } });
                return;
            }
            var mode = (_ticketTimelineFilter?.SelectedItem as string) ?? "All";
            var filtered = all.Where(h =>
                string.Equals(mode, "All", StringComparison.OrdinalIgnoreCase) ? true
                : string.Equals(mode, "Field visits", StringComparison.OrdinalIgnoreCase) ? (h.FieldName.Equals("FieldVisitStatus", StringComparison.OrdinalIgnoreCase) || h.FieldName.Equals("FieldVisitSignature", StringComparison.OrdinalIgnoreCase))
                : string.Equals(mode, "Notes", StringComparison.OrdinalIgnoreCase) ? h.FieldName.Equals("NoteAdded", StringComparison.OrdinalIgnoreCase)
                : string.Equals(mode, "Status", StringComparison.OrdinalIgnoreCase) ? h.FieldName.Equals("Status", StringComparison.OrdinalIgnoreCase)
                : true).ToList();
            var summary = new Border { CornerRadius = new CornerRadius(10), Background = BrushFromRgb(239,246,255), BorderBrush = BrushFromRgb(147,197,253), BorderThickness = new Thickness(1), Padding = new Thickness(12,10,12,10), Margin = new Thickness(0,0,0,12) };
            summary.Child = new TextBlock { Text = $"{filtered.Count} of {all.Count} events • newest first • {mode}", Foreground = BrushFromRgb(29,78,216), FontWeight = FontWeights.SemiBold, FontSize = 12 };
            _ticketTimelineHost.Children.Add(summary);
            if (filtered.Count == 0)
            {
                _ticketTimelineHost.Children.Add(new TextBlock { Text = "No events match this filter.", Foreground = BrushFromRgb(100,116,139), FontStyle = FontStyles.Italic, Margin = new Thickness(0,4,0,0) });
                return;
            }
            var groups = filtered.GroupBy(h => h.ChangedAt.HasValue ? h.ChangedAt.Value.ToString("MMM d, yyyy") : "Unknown date").ToList();
            foreach (var g in groups)
            {
                _ticketTimelineHost.Children.Add(new TextBlock { Text = $"{g.Key} • {g.Count()}", FontSize = 11, FontWeight = FontWeights.Bold, Foreground = BrushFromRgb(100,116,139), Margin = new Thickness(0,8,0,8) });
                var list = g.ToList();
                for (int i = 0; i < list.Count; i++)
                {
                    var h = list[i];
                    var isLast = i == list.Count - 1;
                    var isReschedule = string.Equals(h.FieldName, "FieldVisitStatus", StringComparison.OrdinalIgnoreCase)
                        && string.Equals(h.OldValue, "Cancelled", StringComparison.OrdinalIgnoreCase)
                        && string.Equals(h.NewValue, "Scheduled", StringComparison.OrdinalIgnoreCase);
                    var accent = GetTimelineAccent(h.FieldName, h.NewValue);
                    var row = new Grid { Margin = new Thickness(0,0,0, isLast ? 0 : 14) };
                    row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(28) });
                    row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                    var dotCol = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center };
                    Grid.SetColumn(dotCol, 0);
                    row.Children.Add(dotCol);
                    var dot = new Border { Width = 12, Height = 12, CornerRadius = new CornerRadius(6), Background = accent, BorderBrush = Brushes.White, BorderThickness = new Thickness(2), HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(0,4,0,0) };
                    dotCol.Children.Add(dot);
                    if (!isLast)
                    {
                        dotCol.Children.Add(new Border { Width = 2, Background = BrushFromRgb(226,232,240), Margin = new Thickness(0,2,0,0), HorizontalAlignment = HorizontalAlignment.Center, Height = 44 });
                    }
                    var card = new Border { CornerRadius = new CornerRadius(12), Padding = new Thickness(14,12,14,12), Background = isReschedule ? BrushFromRgb(254,249,195) : Brushes.White, BorderBrush = isReschedule ? BrushFromRgb(253,224,71) : BrushFromRgb(226,232,240), BorderThickness = new Thickness(1) };
                    Grid.SetColumn(card, 1);
                    row.Children.Add(card);
                    var stack = new StackPanel();
                    card.Child = stack;
                    var header = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
                    stack.Children.Add(header);
                    var fieldPill = new Border { CornerRadius = new CornerRadius(6), Background = accent, Padding = new Thickness(7,3,7,3), VerticalAlignment = VerticalAlignment.Center };
                    fieldPill.Child = new TextBlock { Text = PrettyFieldName(h.FieldName), Foreground = Brushes.White, FontWeight = FontWeights.Bold, FontSize = 10 };
                    header.Children.Add(fieldPill);
                    if (isReschedule)
                    {
                        var rs = new Border { CornerRadius = new CornerRadius(6), Background = BrushFromRgb(250,204,21), Padding = new Thickness(6,2,6,2), Margin = new Thickness(8,0,0,0), VerticalAlignment = VerticalAlignment.Center };
                        rs.Child = new TextBlock { Text = "RESCHEDULE", FontSize = 10, FontWeight = FontWeights.Bold, Foreground = BrushFromRgb(113,63,18) };
                        header.Children.Add(rs);
                    }
                    var oldVal = (h.OldValue ?? string.Empty).Trim();
                    var newVal = (h.NewValue ?? string.Empty).Trim();
                    var changeText = string.IsNullOrWhiteSpace(oldVal) ? (string.IsNullOrWhiteSpace(newVal) ? "Updated" : newVal) : (string.IsNullOrWhiteSpace(newVal) ? oldVal : oldVal + " → " + newVal);
                    stack.Children.Add(new TextBlock { Text = changeText, Foreground = BrushFromRgb(15,23,42), FontWeight = FontWeights.SemiBold, FontSize = 12, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0,6,0,0) });
                    stack.Children.Add(new TextBlock { Text = $"{h.ChangedAt:MMM d, yyyy h:mm tt} • {h.ChangedByName ?? "System"}", Foreground = BrushFromRgb(100,116,139), FontSize = 11, Margin = new Thickness(0,4,0,0) });
                    if (!string.IsNullOrWhiteSpace(h.Note)) stack.Children.Add(new TextBlock { Text = h.Note, Foreground = BrushFromRgb(30,41,59), FontSize = 11, FontWeight = FontWeights.SemiBold, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0,4,0,0) });
                    _ticketTimelineHost.Children.Add(row);
                }
            }
        }

        private static string PrettyFieldName(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return "UPDATE";
            var spaced = System.Text.RegularExpressions.Regex.Replace(raw.Trim(), "(?<=[a-z0-9])(?=[A-Z])", " ").Replace('_', ' ').Trim();
            return System.Text.RegularExpressions.Regex.Replace(spaced, @"\s+", " ").ToUpperInvariant();
        }

        private static Brush GetTimelineAccent(string field, string newValue)
        {
            if (string.Equals(field, "FieldVisitStatus", StringComparison.OrdinalIgnoreCase) && string.Equals(newValue, "Completed", StringComparison.OrdinalIgnoreCase))
                return BrushFromRgb(22, 163, 74);
            if (string.Equals(field, "FieldVisitStatus", StringComparison.OrdinalIgnoreCase) && string.Equals(newValue, "Cancelled", StringComparison.OrdinalIgnoreCase))
                return BrushFromRgb(220, 38, 38);
            if (string.Equals(field, "FieldVisitStatus", StringComparison.OrdinalIgnoreCase))
                return BrushFromRgb(37, 99, 235);
            if (string.Equals(field, "FieldVisitSignature", StringComparison.OrdinalIgnoreCase))
                return BrushFromRgb(22, 163, 74);
            if (string.Equals(field, "NoteAdded", StringComparison.OrdinalIgnoreCase))
                return BrushFromRgb(100, 116, 139);
            if (string.Equals(field, "Status", StringComparison.OrdinalIgnoreCase))
                return BrushFromRgb(163, 106, 114);
            if (string.Equals(field, "Priority", StringComparison.OrdinalIgnoreCase))
                return BrushFromRgb(198, 106, 0);
            if (string.Equals(field, "Created", StringComparison.OrdinalIgnoreCase))
                return BrushFromRgb(95, 107, 122);
            return BrushFromRgb(100, 116, 139);
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

        private static Button CreateActionButton(string text, Brush bg, string styleKey, string glyph = null)
        {
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

        private static Brush GetStatusBrush(string status)
        {
            switch((status??"").Trim().ToLowerInvariant()){
                case "scheduled": return BrushFromRgb(37,99,235);
                case "completed": return BrushFromRgb(22,163,74);
                case "cancelled": return BrushFromRgb(220,38,38);
                default: return BrushFromRgb(100,116,139);
            }
        }
        private static Brush GetHistoryDotBrush(string status)
        {
            switch((status??"").Trim().ToLowerInvariant()){
                case "scheduled": return BrushFromRgb(37,99,235);
                case "completed": return BrushFromRgb(22,163,74);
                case "cancelled": return BrushFromRgb(220,38,38);
                default: return BrushFromRgb(148,163,184);
            }
        }
        private Style TryFindStyle(string key){ try{ return Application.Current?.TryFindResource(key) as Style ?? Resources[key] as Style; }catch{ return null; } }
        private static SolidColorBrush BrushFromRgb(byte r, byte g, byte b){ var br=new SolidColorBrush(Color.FromRgb(r,g,b)); br.Freeze(); return br; }
    }
}
