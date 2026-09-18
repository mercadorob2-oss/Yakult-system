using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using Yakult.Inventory.App.Models.CallMonitoring;
using Yakult.Inventory.App.Repositories;
using Yakult.Inventory.App.Services;
using Yakult.Inventory.App.Session;
using Yakult.Inventory.App.Forms.CallMonitoring;

namespace Yakult.Inventory.App.Wpf.CallMonitoring
{
    internal sealed class WpfNotificationDrawer : UserControl
    {
        private enum FeedFilter { Attention, Overdue, Unassigned, AssignedToMe, Urgent, Today, All }
        private enum FeedSort   { Risk, Newest, Oldest, SlaNearest }
        private enum FeedGroup  { NeedsActionNow, AssignedToMe, RecentlyUpdated }

        private sealed class TicketNotification
        {
            public CallTicketListItem Ticket { get; set; }
            public int   Score        { get; set; }
            public bool  IsOverdue    { get; set; }
            public bool  IsUnassigned { get; set; }
            public bool  IsUrgent     { get; set; }
            public bool  IsToday      { get; set; }
            public bool  IsAssignedToMe { get; set; }
            public bool  IsEscalated  { get; set; }
            public bool  IsStale      { get; set; }
            public bool  IsUnread     { get; set; }
            public bool  IsPinned     { get; set; }
            public bool  IsMuted      { get; set; }
            public bool  IsSnoozed    { get; set; }
            public DateTime? SnoozedUntil { get; set; }
        }

        // ── Public API ────────────────────────────────────────────────────────
        public event Action       CloseRequested;
        public event Action<int>  AttentionCountChanged;
        public Func<int, Task>    OpenTicketAsync    { get; set; }
        public Func<Task>         OpenTicketListAsync { get; set; }
        public Func<Task>         AfterMutationAsync { get; set; }

        public void Initialize(ICallMonitoringRepository repo) => _repo = repo;

        public async Task RefreshAsync()
        {
            if (_repo == null) return;
            if (Interlocked.Exchange(ref _refreshGate, 1) == 1) return;
            _refreshCts?.Cancel(); _refreshCts?.Dispose();
            _refreshCts = new CancellationTokenSource();
            var token = _refreshCts.Token;
            SetLoading(true);
            try
            {
                if (!await _repo.CallSchemaExistsAsync())
                {
                    _items = new List<TicketNotification>();
                    AttentionCountChanged?.Invoke(0);
                    ShowEmpty("Call Monitoring tables missing.\nRun DB scripts then refresh.");
                    return;
                }
                var t1 = _repo.GetOverdueDaysAsync(defaultDays: 3);
                var t2 = _repo.GetOpenTicketsListAsync(maxRows: 500);
                await Task.WhenAll(t1, t2);
                if (token.IsCancellationRequested) return;
                _overdueDays = t1.Result;
                _items = BuildFeed(t2.Result ?? new List<CallTicketListItem>(), _overdueDays);
                var active = new HashSet<int>(_items.Select(x => x.Ticket?.TicketId ?? 0).Where(id => id > 0));
                _readIds.RemoveWhere(id => !active.Contains(id));
                _pinnedIds.RemoveWhere(id => !active.Contains(id));
                _mutedIds.RemoveWhere(id => !active.Contains(id));
                foreach (var k in _snoozed.Where(x => !active.Contains(x.Key) || x.Value <= DateTime.Now).Select(x => x.Key).ToList())
                    _snoozed.Remove(k);
                ApplyFlags();
                AttentionCountChanged?.Invoke(_items.Count(IsAttention));
                if (!token.IsCancellationRequested) Dispatcher.Invoke(Render);
            }
            catch (Exception ex)
            {
                _items = new List<TicketNotification>();
                AttentionCountChanged?.Invoke(0);
                ShowEmpty("Failed to load.\n" + ex.Message);
            }
            finally { SetLoading(false); Interlocked.Exchange(ref _refreshGate, 0); }
        }

        // ── State ─────────────────────────────────────────────────────────────
        private ICallMonitoringRepository  _repo;
        private int                        _overdueDays = 3;
        private int                        _refreshGate;
        private CancellationTokenSource    _refreshCts;
        private List<TicketNotification>   _items       = new List<TicketNotification>();
        private List<TicketNotification>   _currentView = new List<TicketNotification>();
        private readonly HashSet<int>             _readIds  = new HashSet<int>();
        private readonly HashSet<int>             _pinnedIds = new HashSet<int>();
        private readonly HashSet<int>             _mutedIds  = new HashSet<int>();
        private readonly Dictionary<int,DateTime> _snoozed   = new Dictionary<int,DateTime>();
        private FeedFilter _filter = FeedFilter.Attention;
        private FeedSort   _sort   = FeedSort.Risk;

        // ── UI refs ───────────────────────────────────────────────────────────
        private TextBox    _txtSearch;
        private ComboBox   _cboSort;
        private WrapPanel  _pnlChips;
        private StackPanel _ticketList;
        private TextBlock  _lblEmpty;
        private TextBlock  _lblFooter;

        // ── Colors ────────────────────────────────────────────────────────────
        private static readonly Color CBorder   = Color.FromRgb(226,232,240);
        private static readonly Color CTextPri  = Color.FromRgb(15, 23, 42);
        private static readonly Color CTextSec  = Color.FromRgb(100,116,139);
        private static readonly Color CBlue     = Color.FromRgb(37, 99, 235);
        private static readonly Color CRed      = Color.FromRgb(239,68, 68);
        private static readonly Color COrange   = Color.FromRgb(249,115,22);
        private static readonly Color CAmber    = Color.FromRgb(245,158,11);
        private static readonly Color CTeal     = Color.FromRgb(20, 184,166);
        private static readonly Color CPurple   = Color.FromRgb(147,51, 234);
        private static readonly Color CGray     = Color.FromRgb(203,213,225);
        private static readonly Color CFooterBg = Color.FromRgb(241,243,247);

        // ── Constructor ───────────────────────────────────────────────────────
        public WpfNotificationDrawer()
        {
            Background = new SolidColorBrush(Color.FromRgb(248,250,252));
            FontFamily = new FontFamily("Segoe UI");
            BuildLayout();
        }

        private void BuildLayout()
        {
            var root = new Grid();
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            // Controls
            var ctrlHost = new Border
            {
                Background = new SolidColorBrush(Colors.White),
                BorderBrush = new SolidColorBrush(CBorder),
                BorderThickness = new Thickness(0,0,0,1),
                Padding = new Thickness(14,10,14,10)
            };
            var searchBorder = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(248,250,252)),
                BorderBrush = new SolidColorBrush(CBorder), BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(8), Margin = new Thickness(0,0,0,8), Height = 34,
                Padding = new Thickness(10,0,10,0)
            };
            var sg = new Grid();
            sg.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            sg.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            sg.Children.Add(new TextBlock { Text="\ud83d\udd0d", FontSize=11, FontFamily=new FontFamily("Segoe UI Emoji"),
                VerticalAlignment=VerticalAlignment.Center, Foreground=new SolidColorBrush(CTextSec), Margin=new Thickness(0,0,6,0) });
            _txtSearch = new TextBox { Background=Brushes.Transparent, BorderThickness=new Thickness(0),
                VerticalAlignment=VerticalAlignment.Center, FontSize=12.5,
                Foreground=new SolidColorBrush(CTextSec), Text="Search tickets...", Padding=new Thickness(0) };
            _txtSearch.GotFocus  += (_,__) => { if (_txtSearch.Text=="Search tickets...") { _txtSearch.Text=""; _txtSearch.Foreground=new SolidColorBrush(CTextPri); } };
            _txtSearch.LostFocus += (_,__) => { if (string.IsNullOrWhiteSpace(_txtSearch.Text)) { _txtSearch.Text="Search tickets..."; _txtSearch.Foreground=new SolidColorBrush(CTextSec); } };
            _txtSearch.TextChanged += (_,__) => Render();
            Grid.SetColumn(_txtSearch, 1); sg.Children.Add(_txtSearch);
            searchBorder.Child = sg;

            _cboSort = new ComboBox { Margin=new Thickness(0,0,0,8), FontSize=12, Padding=new Thickness(8,4,8,4) };
            _cboSort.Items.Add("Sort: Risk (default)"); _cboSort.Items.Add("Sort: Newest");
            _cboSort.Items.Add("Sort: Oldest");         _cboSort.Items.Add("Sort: SLA nearest");
            _cboSort.SelectedIndex = 0;
            _cboSort.SelectionChanged += (_,__) => { _sort=(FeedSort)Math.Max(0,Math.Min(3,_cboSort.SelectedIndex)); Render(); };

            _pnlChips = new WrapPanel { Margin=new Thickness(0,0,0,8) };

            var bulkBtn = new Button { Content="Bulk actions \u25be", FontSize=11,
                Background=new SolidColorBrush(Colors.White), BorderBrush=new SolidColorBrush(CBorder),
                BorderThickness=new Thickness(1), Padding=new Thickness(10,4,10,4),
                Cursor=Cursors.Hand, HorizontalAlignment=HorizontalAlignment.Left };
            var bm = new ContextMenu();
            var ma = new MenuItem { Header="Assign to me" };      ma.Click += async (_,__) => await BulkAssignAsync();
            var mb = new MenuItem { Header="Set In Progress" };   mb.Click += async (_,__) => await BulkInProgressAsync();
            var mc = new MenuItem { Header="Mark Read" };         mc.Click += (_,__) => BulkMarkRead();
            var md = new MenuItem { Header="Snooze 2h" };         md.Click += (_,__) => BulkSnooze(TimeSpan.FromHours(2));
            bm.Items.Add(ma); bm.Items.Add(mb); bm.Items.Add(mc); bm.Items.Add(md);
            bulkBtn.Click += (_,__) => { bm.PlacementTarget=bulkBtn; bm.IsOpen=true; };

            var cs = new StackPanel();
            cs.Children.Add(searchBorder); cs.Children.Add(_cboSort);
            cs.Children.Add(_pnlChips);    cs.Children.Add(bulkBtn);
            ctrlHost.Child = cs;
            Grid.SetRow(ctrlHost, 0); root.Children.Add(ctrlHost);

            // List
            var scroll = new ScrollViewer { VerticalScrollBarVisibility=ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled,
                Background=new SolidColorBrush(Color.FromRgb(248,250,252)) };
            scroll.Resources.MergedDictionaries.Add(WpfThemeResources.GetScrollBarStyle());
            var lo = new StackPanel { Margin=new Thickness(12,8,12,8) };
            _lblEmpty = new TextBlock { Text="No notifications.", FontSize=13,
                Foreground=new SolidColorBrush(CTextSec), HorizontalAlignment=HorizontalAlignment.Center,
                TextAlignment=TextAlignment.Center, Margin=new Thickness(0,40,0,0),
                Visibility=Visibility.Collapsed, TextWrapping=TextWrapping.Wrap };
            _ticketList = new StackPanel();
            lo.Children.Add(_lblEmpty); lo.Children.Add(_ticketList);
            scroll.Content = lo;
            Grid.SetRow(scroll, 1); root.Children.Add(scroll);

            // Footer
            var footer = new Border { Background=new SolidColorBrush(CFooterBg),
                BorderBrush=new SolidColorBrush(CBorder), BorderThickness=new Thickness(0,1,0,0),
                Padding=new Thickness(14,8,14,8) };
            var fg = new Grid();
            fg.ColumnDefinitions.Add(new ColumnDefinition { Width=new GridLength(1,GridUnitType.Star) });
            fg.ColumnDefinitions.Add(new ColumnDefinition { Width=GridLength.Auto });
            fg.ColumnDefinitions.Add(new ColumnDefinition { Width=GridLength.Auto });
            _lblFooter = new TextBlock { Text="Stateless feed \u2022 refresh for latest", FontSize=11,
                Foreground=new SolidColorBrush(CTextSec), VerticalAlignment=VerticalAlignment.Center };
            Grid.SetColumn(_lblFooter, 0);
            var bva = new Button { Content="View ticket list \u2192", FontSize=11,
                Foreground=new SolidColorBrush(CBlue), Background=Brushes.Transparent,
                BorderThickness=new Thickness(0), Cursor=Cursors.Hand, Margin=new Thickness(8,0,8,0), Padding=new Thickness(0) };
            bva.Click += async (_,__) => { if (OpenTicketListAsync!=null) await OpenTicketListAsync(); };
            Grid.SetColumn(bva, 1);
            var bma = new Button { Content="Mark all read", FontSize=11,
                Foreground=new SolidColorBrush(CBlue), Background=Brushes.Transparent,
                BorderThickness=new Thickness(0), Cursor=Cursors.Hand, Padding=new Thickness(0) };
            bma.Click += (_,__) => MarkAllAsRead();
            Grid.SetColumn(bma, 2);
            fg.Children.Add(_lblFooter); fg.Children.Add(bva); fg.Children.Add(bma);
            footer.Child = fg;
            Grid.SetRow(footer, 2); root.Children.Add(footer);

            Content = root;
            RebuildChips();
        }

        // ── Render ────────────────────────────────────────────────────────────
        private void Render()
        {
            if (!Dispatcher.CheckAccess()) { Dispatcher.Invoke(Render); return; }
            var q = (_txtSearch?.Text ?? "").Trim();
            var hasQ = !string.IsNullOrWhiteSpace(q) && q != "Search tickets...";
            ApplyFlags();

            IEnumerable<TicketNotification> view = _items ?? Enumerable.Empty<TicketNotification>();
            if (_filter != FeedFilter.All) view = view.Where(x => !x.IsSnoozed);
            switch (_filter)
            {
                case FeedFilter.Attention:    view = view.Where(IsAttention); break;
                case FeedFilter.Overdue:      view = view.Where(x => x.IsOverdue); break;
                case FeedFilter.Unassigned:   view = view.Where(x => x.IsUnassigned); break;
                case FeedFilter.AssignedToMe: view = view.Where(x => x.IsAssignedToMe); break;
                case FeedFilter.Urgent:       view = view.Where(x => x.IsUrgent); break;
                case FeedFilter.Today:        view = view.Where(x => x.IsToday); break;
            }
            if (hasQ)
                view = view.Where(x => { var t=x?.Ticket; return t!=null&&(Ci(t.TicketCode,q)||Ci(t.Issue,q)||Ci(t.Company,q)||Ci(t.Branch,q)||Ci(t.Department,q)||Ci(t.CallerName,q)||Ci(t.ResponsiblePerson,q)); });

            var rows = ApplySort(view).Take(120).ToList();
            _currentView = rows;
            _ticketList.Children.Clear();

            if (rows.Count == 0)
            {
                _lblEmpty.Text = _items.Count==0 ? "No notifications." : "No matches for current filter.";
                _lblEmpty.Visibility = Visibility.Visible;
            }
            else
            {
                _lblEmpty.Visibility = Visibility.Collapsed;
                AddGroup("NEEDS ACTION NOW", rows.Where(x=>ResolveGroup(x)==FeedGroup.NeedsActionNow).ToList());
                AddGroup("ASSIGNED TO ME",   rows.Where(x=>ResolveGroup(x)==FeedGroup.AssignedToMe).ToList());
                AddGroup("RECENTLY UPDATED", rows.Where(x=>ResolveGroup(x)==FeedGroup.RecentlyUpdated).ToList());
            }
            UpdateFooter(rows);
            RebuildChips();
        }

        private void AddGroup(string title, List<TicketNotification> gr)
        {
            if (gr==null||gr.Count==0) return;
            _ticketList.Children.Add(new TextBlock { Text=$"{title} ({gr.Count})", FontSize=10.5,
                FontWeight=FontWeights.Bold, Foreground=new SolidColorBrush(CTextSec), Margin=new Thickness(0,12,0,6) });
            foreach (var n in gr) _ticketList.Children.Add(BuildTicketRow(n));
        }

        private Border BuildTicketRow(TicketNotification n)
        {
            var t = n?.Ticket;
            var card = new Border { Background=new SolidColorBrush(Colors.White),
                BorderBrush=new SolidColorBrush(CBorder), BorderThickness=new Thickness(1),
                CornerRadius=new CornerRadius(10), Margin=new Thickness(0,0,0,8), Cursor=Cursors.Hand, ClipToBounds=true };
            card.MouseEnter += (_,__) => { card.Effect=new DropShadowEffect{Color=Color.FromRgb(15,23,42),BlurRadius=10,ShadowDepth=0,Opacity=0.12}; card.BorderBrush=new SolidColorBrush(Color.FromRgb(148,163,184)); };
            card.MouseLeave += (_,__) => { card.Effect=null; card.BorderBrush=new SolidColorBrush(CBorder); };

            var row = new Grid();
            row.ColumnDefinitions.Add(new ColumnDefinition { Width=new GridLength(4) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width=new GridLength(1,GridUnitType.Star) });
            var stripe = new Border { Background=new SolidColorBrush(StripeColor(n)) };
            Grid.SetColumn(stripe, 0); row.Children.Add(stripe);

            var body = new StackPanel { Margin=new Thickness(12,10,12,10) };

            // Title
            var tg = new Grid();
            tg.ColumnDefinitions.Add(new ColumnDefinition { Width=new GridLength(1,GridUnitType.Star) });
            tg.ColumnDefinitions.Add(new ColumnDefinition { Width=GridLength.Auto });
            tg.Children.Add(new TextBlock { Text=t?.TicketCode??"TCK-?", FontSize=13, FontWeight=FontWeights.Bold, Foreground=new SolidColorBrush(CTextPri) });
            if (n.IsUnread)
            {
                var dot = new Border { Width=8,Height=8,CornerRadius=new CornerRadius(4),
                    Background=new SolidColorBrush(CBlue), VerticalAlignment=VerticalAlignment.Center };
                Grid.SetColumn(dot,1); tg.Children.Add(dot);
            }
            body.Children.Add(tg);

            // Issue
            if (!string.IsNullOrWhiteSpace(t?.Issue))
                body.Children.Add(new TextBlock { Text=t.Issue, FontSize=11.5, Foreground=new SolidColorBrush(CTextSec),
                    TextTrimming=TextTrimming.CharacterEllipsis, Margin=new Thickness(0,2,0,6), TextWrapping=TextWrapping.NoWrap });

            // Tags
            var tags = new WrapPanel { Margin=new Thickness(0,0,0,6) };
            if (n.IsUnread)    tags.Children.Add(BuildTag("Unread",   CBlue));
            if (n.IsEscalated) tags.Children.Add(BuildTag("Escalated",CRed));
            var pri = (t?.Priority??"").Trim();
            if (!string.IsNullOrEmpty(pri)) tags.Children.Add(BuildTag(pri, PriColor(pri)));
            var sta = (t?.Status??"").Trim();
            if (!string.IsNullOrEmpty(sta)) tags.Children.Add(BuildTag(sta, StaColor(sta)));
            var idle = t!=null ? IdleDays(t) : 0;
            if (n.IsToday)   tags.Children.Add(BuildTag("Due today",   COrange));
            if (n.IsOverdue && _overdueDays>0) tags.Children.Add(BuildTag($"Breached {idle}d", CRed));
            if (n.IsPinned)  tags.Children.Add(BuildTag("\ud83d\udccc Pinned", CGray));
            if (n.IsMuted)   tags.Children.Add(BuildTag("\ud83d\udd07 Muted",  CGray));
            if (tags.Children.Count>0) body.Children.Add(tags);

            // Actions
            var acts = new WrapPanel { Margin=new Thickness(0,0,0,6) };
            var tid = t?.TicketId ?? 0;
            var bo = ActionBtn("Open \u2192", CBlue);   bo.Click += async (_,__) => await SafeOpen(tid);
            var bm2 = ActionBtn("Modify",   CTextSec);  bm2.Click += async (_,__) => await SafeOpen(tid);
            var bp = ActionBtn(n.IsPinned?"Unpin":"\ud83d\udccc Pin", CTextSec);   bp.Click += (_,__) => TogglePin(tid);
            var bmt = ActionBtn(n.IsMuted?"Unmute":"\ud83d\udd07 Mute", CTextSec); bmt.Click += (_,__) => ToggleMute(tid);
            acts.Children.Add(bo); acts.Children.Add(bm2); acts.Children.Add(bp); acts.Children.Add(bmt);
            body.Children.Add(acts);

            // Meta
            var meta = new List<string>();
            if (!string.IsNullOrWhiteSpace(t?.Company))    meta.Add(t.Company);
            if (!string.IsNullOrWhiteSpace(t?.Branch))     meta.Add(t.Branch);
            if (!string.IsNullOrWhiteSpace(t?.Department)) meta.Add(t.Department);
            if (meta.Any()) body.Children.Add(new TextBlock { Text=string.Join(" \u2022 ",meta), FontSize=10.5,
                Foreground=new SolidColorBrush(CTextSec), TextTrimming=TextTrimming.CharacterEllipsis, Margin=new Thickness(0,0,0,2) });
            var assignee = string.IsNullOrWhiteSpace(t?.ResponsiblePerson) ? "Unassigned" : t.ResponsiblePerson;
            var ago = t!=null ? Ago(t.UpdatedAt!=default?t.UpdatedAt:t.CreatedAt) : "";
            body.Children.Add(new TextBlock { Text=$"{assignee} \u2022 Updated {ago}", FontSize=10.5,
                Foreground=new SolidColorBrush(CTextSec), TextTrimming=TextTrimming.CharacterEllipsis });

            Grid.SetColumn(body,1); row.Children.Add(body);
            card.Child = row;
            return card;
        }

        // ── Chip helpers ──────────────────────────────────────────────────────
        private void RebuildChips()
        {
            if (_pnlChips==null) return;
            _pnlChips.Children.Clear();
            Chip($"Attention ({Count(FeedFilter.Attention)})", FeedFilter.Attention);
            Chip($"Overdue ({Count(FeedFilter.Overdue)})",     FeedFilter.Overdue);
            Chip($"Unassigned ({Count(FeedFilter.Unassigned)})",FeedFilter.Unassigned);
            Chip($"Assigned to me ({Count(FeedFilter.AssignedToMe)})", FeedFilter.AssignedToMe);
            Chip($"Critical/High ({Count(FeedFilter.Urgent)})",FeedFilter.Urgent);
            Chip($"Today ({Count(FeedFilter.Today)})",         FeedFilter.Today);
            Chip($"All ({Count(FeedFilter.All)})",             FeedFilter.All);
        }
        private void Chip(string label, FeedFilter f)
        {
            var active = _filter==f;
            var btn = new Button { Content=label, FontSize=11,
                FontWeight=active?FontWeights.Bold:FontWeights.Normal,
                Foreground=new SolidColorBrush(active?Colors.White:CTextPri),
                Background=new SolidColorBrush(active?CBlue:Colors.White),
                BorderBrush=new SolidColorBrush(active?CBlue:CBorder),
                BorderThickness=new Thickness(1), Padding=new Thickness(8,3,8,3),
                Margin=new Thickness(0,0,4,4), Cursor=Cursors.Hand };
            btn.Click += (_,__) => { _filter=f; Render(); };
            _pnlChips.Children.Add(btn);
        }

        // ── Tag / button factories ─────────────────────────────────────────────
        private static Border BuildTag(string text, Color c) => new Border
        {
            Background=new SolidColorBrush(Color.FromArgb(28,c.R,c.G,c.B)),
            BorderBrush=new SolidColorBrush(Color.FromArgb(80,c.R,c.G,c.B)),
            BorderThickness=new Thickness(1), CornerRadius=new CornerRadius(4),
            Padding=new Thickness(6,2,6,2), Margin=new Thickness(0,0,4,4),
            Child=new TextBlock { Text=text, FontSize=10, FontWeight=FontWeights.SemiBold, Foreground=new SolidColorBrush(c) }
        };

        private static Button ActionBtn(string text, Color c)
        {
            var btn = new Button { Content=text, FontSize=11,
                Foreground=new SolidColorBrush(c),
                Background=new SolidColorBrush(Color.FromArgb(20,c.R,c.G,c.B)),
                BorderBrush=new SolidColorBrush(Color.FromArgb(60,c.R,c.G,c.B)),
                BorderThickness=new Thickness(1), Padding=new Thickness(8,3,8,3),
                Margin=new Thickness(0,0,6,0), Cursor=Cursors.Hand };
            btn.MouseEnter += (_,__) => btn.Background=new SolidColorBrush(Color.FromArgb(40,c.R,c.G,c.B));
            btn.MouseLeave += (_,__) => btn.Background=new SolidColorBrush(Color.FromArgb(20,c.R,c.G,c.B));
            return btn;
        }

        // ── Data helpers ──────────────────────────────────────────────────────
        private static List<TicketNotification> BuildFeed(List<CallTicketListItem> tickets, int overdueDays)
        {
            var now = DateTime.Now;
            var list = new List<TicketNotification>();
            foreach (var t in tickets.Where(x=>x!=null))
            {
                var status = (t.Status??"").Trim();
                var prio   = (t.Priority??"").Trim();
                var isEsc  = status.Equals("Escalated",StringComparison.OrdinalIgnoreCase);
                var idle   = IdleDays(t);
                var isOvd  = overdueDays>0 && idle>=overdueDays;
                var isUna  = string.IsNullOrWhiteSpace(t.ResponsiblePerson)||(t.AssignedToEmpId==null||t.AssignedToEmpId<=0);
                var isUrg  = prio.Equals("Critical",StringComparison.OrdinalIgnoreCase)||prio.Equals("High",StringComparison.OrdinalIgnoreCase);
                var isToday= t.CreatedAt!=default && t.CreatedAt.Date==DateTime.Today;
                var upd    = t.UpdatedAt!=default?t.UpdatedAt:t.CreatedAt;
                var isStale= upd!=default&&(now-upd).TotalDays>=2.0;
                var score  = 0;
                if (isEsc)  score+=1000; if (isOvd) score+=800;
                if (prio.Equals("Critical",StringComparison.OrdinalIgnoreCase)) score+=450;
                if (prio.Equals("High",StringComparison.OrdinalIgnoreCase))     score+=300;
                if (isUna)  score+=260;  if (isStale) score+=160; if (isToday) score+=80;
                if (score<=0) score=20;
                list.Add(new TicketNotification { Ticket=t, Score=score, IsEscalated=isEsc,
                    IsOverdue=isOvd, IsUnassigned=isUna, IsUrgent=isUrg, IsToday=isToday, IsStale=isStale });
            }
            return list.OrderByDescending(x=>x.Score).ThenByDescending(x=>x.Ticket?.UpdatedAt??x.Ticket?.CreatedAt??DateTime.MinValue).Take(120).ToList();
        }

        private void ApplyFlags()
        {
            if (_items==null||_items.Count==0) return;
            var now = DateTime.Now;
            foreach (var k in _snoozed.Where(x=>x.Value<=now).Select(x=>x.Key).ToList()) _snoozed.Remove(k);
            foreach (var item in _items)
            {
                var id = item.Ticket?.TicketId??0;
                item.IsAssignedToMe = IsMe(item.Ticket);
                item.IsUnread  = id>0 && !_readIds.Contains(id);
                item.IsPinned  = id>0 && _pinnedIds.Contains(id);
                item.IsMuted   = id>0 && _mutedIds.Contains(id);
                if (id>0 && _snoozed.TryGetValue(id,out var u)) { item.IsSnoozed=u>now; item.SnoozedUntil=u; }
                else { item.IsSnoozed=false; item.SnoozedUntil=null; }
            }
        }

        private static bool IsAttention(TicketNotification n)
        {
            if (n==null||n.IsMuted) return false;
            if (n.IsSnoozed&&n.SnoozedUntil.HasValue&&n.SnoozedUntil.Value>DateTime.Now) return false;
            return n.IsEscalated||n.IsOverdue||n.IsUnassigned||n.IsUrgent;
        }

        private static bool IsMe(CallTicketListItem t)
        {
            if (t==null) return false;
            var empId = AppSession.CurrentEmployeeId;
            if (empId.HasValue&&empId.Value>0&&t.AssignedToEmpId.HasValue&&t.AssignedToEmpId.Value>0)
                return t.AssignedToEmpId.Value==empId.Value;
            var name=(AppSession.CurrentEmployeeName??"").Trim();
            if (!string.IsNullOrWhiteSpace(name)&&!string.IsNullOrWhiteSpace(t.ResponsiblePerson))
                return string.Equals(t.ResponsiblePerson.Trim(),name,StringComparison.OrdinalIgnoreCase);
            return false;
        }

        private static FeedGroup ResolveGroup(TicketNotification n)
        {
            if (n==null) return FeedGroup.RecentlyUpdated;
            if (IsAttention(n)) return FeedGroup.NeedsActionNow;
            if (n.IsAssignedToMe) return FeedGroup.AssignedToMe;
            return FeedGroup.RecentlyUpdated;
        }

        private int Count(FeedFilter f)
        {
            var s = _items??new List<TicketNotification>();
            switch (f)
            {
                case FeedFilter.Attention:    return s.Count(IsAttention);
                case FeedFilter.Overdue:      return s.Count(x=>x.IsOverdue);
                case FeedFilter.Unassigned:   return s.Count(x=>x.IsUnassigned);
                case FeedFilter.AssignedToMe: return s.Count(x=>x.IsAssignedToMe);
                case FeedFilter.Urgent:       return s.Count(x=>x.IsUrgent);
                case FeedFilter.Today:        return s.Count(x=>x.IsToday);
                default:                      return s.Count;
            }
        }

        private IEnumerable<TicketNotification> ApplySort(IEnumerable<TicketNotification> src)
        {
            src = src??Enumerable.Empty<TicketNotification>();
            Func<TicketNotification,object> pin = x => !x.IsPinned;
            Func<TicketNotification,object> mute = x => x.IsMuted;
            Func<TicketNotification,object> snz  = x => x.IsSnoozed;
            switch (_sort)
            {
                case FeedSort.Newest: return src.OrderBy(pin).ThenBy(mute).ThenBy(snz).ThenByDescending(x=>x.Ticket?.UpdatedAt??DateTime.MinValue);
                case FeedSort.Oldest: return src.OrderBy(pin).ThenBy(mute).ThenBy(snz).ThenBy(x=>x.Ticket?.CreatedAt??DateTime.MaxValue);
                case FeedSort.SlaNearest: return src.OrderBy(pin).ThenBy(mute).ThenBy(snz).ThenBy(x=>_overdueDays-IdleDays(x.Ticket));
                default: return src.OrderBy(pin).ThenBy(mute).ThenBy(snz).ThenByDescending(x=>x.Score).ThenByDescending(x=>x.Ticket?.UpdatedAt??DateTime.MinValue);
            }
        }

        // ── Actions ───────────────────────────────────────────────────────────
        private void TogglePin(int id)  { if(id<=0)return; if(_pinnedIds.Contains(id))_pinnedIds.Remove(id);else _pinnedIds.Add(id); Render(); }
        private void ToggleMute(int id) { if(id<=0)return; if(_mutedIds.Contains(id))_mutedIds.Remove(id);else _mutedIds.Add(id); Render(); }

        private void MarkAllAsRead()
        {
            foreach (var item in _items) { var id=item.Ticket?.TicketId??0; if(id>0)_readIds.Add(id); item.IsUnread=false; }
            Render();
        }

        private async Task SafeOpen(int id)
        {
            if (id<=0||OpenTicketAsync==null) return;
            _readIds.Add(id);
            var item = _items.FirstOrDefault(x=>(x.Ticket?.TicketId??0)==id);
            if (item!=null) item.IsUnread=false;
            try { await OpenTicketAsync(id); }
            catch (Exception ex) { System.Windows.MessageBox.Show(ex.Message,"Open Ticket Failed",MessageBoxButton.OK,MessageBoxImage.Error); }
        }

        private async Task BulkAssignAsync()
        {
            if (_repo==null||(AppSession.CurrentEmployeeId??0)<=0)
            { System.Windows.MessageBox.Show("User not linked to an employee record.","Bulk Assign",MessageBoxButton.OK,MessageBoxImage.Information); return; }
            var ids = _currentView.Where(x=>!x.IsSnoozed&&!Final(x.Ticket?.Status)).Select(x=>x.Ticket?.TicketId??0).Where(id=>id>0).Distinct().Take(25).ToList();
            if (ids.Count==0||System.Windows.MessageBox.Show($"Assign {ids.Count} ticket(s) to you?","Bulk Assign",MessageBoxButton.YesNo,MessageBoxImage.Question)!=MessageBoxResult.Yes) return;
            foreach (var id in ids) try { await new CallEmailNotificationService(_repo).AssignTicketAndNotifyAsync(id,AppSession.CurrentEmployeeId,AppSession.CurrentUserId); } catch {}
            await RefreshAsync(); if (AfterMutationAsync!=null) await AfterMutationAsync();
        }

        private async Task BulkInProgressAsync()
        {
            if (_repo==null||!AppSession.IsLoggedIn) return;
            var ids = _currentView.Where(x=>!x.IsSnoozed&&!Final(x.Ticket?.Status)).Select(x=>x.Ticket?.TicketId??0).Where(id=>id>0).Distinct().Take(25).ToList();
            if (ids.Count==0||System.Windows.MessageBox.Show($"Set {ids.Count} ticket(s) to In Progress?","Bulk Update",MessageBoxButton.YesNo,MessageBoxImage.Question)!=MessageBoxResult.Yes) return;
            var statusById = _currentView.Where(x=>x?.Ticket!=null).GroupBy(x=>x.Ticket.TicketId).ToDictionary(x=>x.Key, x=>x.First().Ticket);
            var ok=0; var skipped=new List<string>();
            foreach (var id in ids)
            {
                try
                {
                    statusById.TryGetValue(id, out var current);
                    var currentStatus = current?.Status ?? string.Empty;
                    // Same workflow guard as the main dropdown: no backward
                    // moves, no writes on final tickets.
                    if (!TicketWorkflow.IsStatusTransitionAllowed(currentStatus, "In Progress") || Final(currentStatus))
                    {
                        skipped.Add((current?.TicketCode ?? ("#"+id)) + " (" + currentStatus + ")");
                        continue;
                    }
                    await _repo.SetTicketStatusAsync(id,"In Progress",AppSession.CurrentUserId,"Bulk update from drawer");
                    ok++;
                }
                catch (Exception ex)
                {
                    statusById.TryGetValue(id, out var failed);
                    skipped.Add((failed?.TicketCode ?? ("#"+id)) + ": " + ex.Message);
                }
            }
            await RefreshAsync(); if (AfterMutationAsync!=null) await AfterMutationAsync();
            var summary = $"Updated {ok}/{ids.Count} ticket(s).";
            if (skipped.Count>0) summary += "\n\nSkipped:\n- " + string.Join("\n- ", skipped.Take(10).ToArray()) + (skipped.Count>10 ? $"\n...and {skipped.Count-10} more." : string.Empty);
            System.Windows.MessageBox.Show(summary, "Bulk Update", MessageBoxButton.OK, skipped.Count>0 ? MessageBoxImage.Warning : MessageBoxImage.Information);
        }

        private void BulkMarkRead()  { foreach(var n in _currentView){var id=n.Ticket?.TicketId??0;if(id>0)_readIds.Add(id);n.IsUnread=false;} Render(); }
        private void BulkSnooze(TimeSpan d) { var u=DateTime.Now.Add(d); foreach(var n in _currentView){var id=n.Ticket?.TicketId??0;if(id>0)_snoozed[id]=u;} Render(); }

        // ── Utility ───────────────────────────────────────────────────────────
        private static bool Final(string s) => !string.IsNullOrWhiteSpace(s)&&(s.Equals("Solved",StringComparison.OrdinalIgnoreCase)||s.Equals("Resolved (Temporary)",StringComparison.OrdinalIgnoreCase)||s.Equals("Closed",StringComparison.OrdinalIgnoreCase));
        private static bool Ci(string h,string n) => !string.IsNullOrWhiteSpace(h)&&!string.IsNullOrWhiteSpace(n)&&h.IndexOf(n,StringComparison.OrdinalIgnoreCase)>=0;

        private static int IdleDays(CallTicketListItem t)
        {
            if (t==null) return 0;
            var last = t.LastContactAt.HasValue ? DateTime.SpecifyKind(t.LastContactAt.Value,DateTimeKind.Utc)
                : DateTime.SpecifyKind(t.UpdatedAt!=default?t.UpdatedAt:t.CreatedAt,DateTimeKind.Utc);
            return Math.Max(0,(int)Math.Floor((DateTime.UtcNow-last).TotalDays));
        }

        private Color StripeColor(TicketNotification n)
        {
            if (n==null) return CGray;
            if (n.IsEscalated) return CRed;
            if (n.IsOverdue)   return COrange;
            var p=(n.Ticket?.Priority??"").Trim();
            if (p.Equals("Critical",StringComparison.OrdinalIgnoreCase)) return CRed;
            if (p.Equals("High",StringComparison.OrdinalIgnoreCase))     return COrange;
            if (n.IsUnassigned) return CPurple;
            if (n.IsToday)      return CTeal;
            return CGray;
        }

        private Color PriColor(string p)
        {
            if (p.Equals("Critical",StringComparison.OrdinalIgnoreCase)) return CRed;
            if (p.Equals("High",StringComparison.OrdinalIgnoreCase))     return COrange;
            if (p.Equals("Medium",StringComparison.OrdinalIgnoreCase))   return CAmber;
            return CTextSec;
        }

        private Color StaColor(string s)
        {
            if (s.Equals("Escalated",StringComparison.OrdinalIgnoreCase))  return CRed;
            if (s.Equals("Open",StringComparison.OrdinalIgnoreCase))        return CTeal;
            if (s.Equals("In Progress",StringComparison.OrdinalIgnoreCase)) return CBlue;
            if (s.Equals("Solved",StringComparison.OrdinalIgnoreCase)||s.Equals("Closed",StringComparison.OrdinalIgnoreCase)) return Color.FromRgb(34,197,94);
            return CTextSec;
        }

        private static string Ago(DateTime dt)
        {
            if (dt==default) return "\u2014";
            var span = DateTime.UtcNow - DateTime.SpecifyKind(dt,DateTimeKind.Utc);
            if (span.TotalMinutes<2)  return "just now";
            if (span.TotalHours<1)    return $"{(int)span.TotalMinutes}m ago";
            if (span.TotalDays<1)     return $"{(int)span.TotalHours}h ago";
            return $"{(int)span.TotalDays}d ago";
        }

        private void SetLoading(bool on)
        {
            if (!Dispatcher.CheckAccess()) { Dispatcher.Invoke(()=>SetLoading(on)); return; }
            _cboSort.IsEnabled = !on; _txtSearch.IsEnabled = !on; _pnlChips.IsEnabled = !on;
            _lblFooter.Text = on ? "Loading\u2026" : "Stateless feed \u2022 refresh for latest";
        }

        private void ShowEmpty(string msg)
        {
            if (!Dispatcher.CheckAccess()) { Dispatcher.Invoke(()=>ShowEmpty(msg)); return; }
            _ticketList.Children.Clear();
            _lblEmpty.Text = msg; _lblEmpty.Visibility = Visibility.Visible;
        }

        private void UpdateFooter(List<TicketNotification> vis)
        {
            var total   = _items?.Count??0;
            var muted   = _items?.Count(x=>x.IsMuted)??0;
            var snoozed = _items?.Count(x=>x.IsSnoozed)??0;
            _lblFooter.Text = total<=0 ? "Stateless feed \u2022 refresh for latest"
                : $"Showing {vis?.Count??0} of {total} \u2022 muted {muted} \u2022 snoozed {snoozed}";
        }
    }
}
