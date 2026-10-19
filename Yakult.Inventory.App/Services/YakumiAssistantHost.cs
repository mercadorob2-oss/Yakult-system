using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using Yakult.Inventory.App.Session;
using WinForms=global::System.Windows.Forms;

namespace Yakult.Inventory.App.Services
{
    /// <summary>One reusable chat host for the WinForms/WPF desktop. No top-level WPF overlay is destroyed.</summary>
    public static class YakumiAssistantHost
    {
        internal static readonly IReadOnlyDictionary<string,string> ModuleTypes=new Dictionary<string,string>(StringComparer.Ordinal) {
            {"MainForm","Inventory"},{"WpfMainDashboardShell","Workspace"},{"MainDashboardForm","Workspace"},
            {"WpfCallMonitoringShell","Tickets"},{"CallMonitoringDashboard","Tickets"},{"RequesterPortalForm","Requests"},
            {"ConsumableManagementPortalShell","Consumables"},{"CartridgeManagementPortalForm","Consumables"},
            {"BorrowItemsDashboard","Borrowing"},{"ReportsForm","Reports"},{"AdminPortalForm","Admin"},{"RepairPortalShellWindow","Repairs"}
        };
        private sealed class Registration
        {
            internal object Root;internal string Module;internal WinForms.Button Launcher;
            internal bool Alive {get {var form=Root as WinForms.Form;return form!=null?!form.IsDisposed:Root is Window;}}
            internal IntPtr Handle {get {var form=Root as WinForms.Form;return form!=null?form.Handle:new WindowInteropHelper((Window)Root).EnsureHandle();}}
        }
        private sealed class HandleOwner : WinForms.IWin32Window
        {public IntPtr Handle {get;private set;} internal HandleOwner(IntPtr handle) {Handle=handle;}}
        private sealed class ChatWindow : WinForms.Form
        {
            internal bool ShuttingDown;
            internal ChatWindow()
            {
                Text="Yakumi";ClientSize=new System.Drawing.Size(350,540);FormBorderStyle=WinForms.FormBorderStyle.None;
                ShowInTaskbar=false;StartPosition=WinForms.FormStartPosition.Manual;AutoScaleMode=WinForms.AutoScaleMode.Dpi;
                FormClosing+=(_,e)=> {if(!ShuttingDown && e.CloseReason!=WinForms.CloseReason.ApplicationExitCall && e.CloseReason!=WinForms.CloseReason.WindowsShutDown) {e.Cancel=true;HideAssistant();}};
            }
        }
        private sealed class ShortcutFilter : WinForms.IMessageFilter
        {
            public bool PreFilterMessage(ref WinForms.Message m)
            {
                if(m.Msg==0x100 && m.WParam.ToInt32()==(int)WinForms.Keys.Y && WinForms.Control.ModifierKeys==(WinForms.Keys.Control|WinForms.Keys.Shift) && AppSession.IsLoggedIn)
                {Open();return true;}
                return false;
            }
        }
        private const double LauncherSize=56;
        private sealed class AvatarLauncherButton : WinForms.Button
        {
            private readonly System.Drawing.Image _avatar;
            private readonly WinForms.ToolTip _tip=new WinForms.ToolTip();
            private bool _hover;
            internal AvatarLauncherButton()
            {
                SetStyle(WinForms.ControlStyles.UserPaint|WinForms.ControlStyles.AllPaintingInWmPaint|WinForms.ControlStyles.OptimizedDoubleBuffer|WinForms.ControlStyles.ResizeRedraw,true);
                Size=new System.Drawing.Size((int)LauncherSize,(int)LauncherSize);Text="";AccessibleName="Ask Yakumi";
                AccessibleDescription="Click to open Yakumi. Drag to move.";FlatStyle=WinForms.FlatStyle.Flat;FlatAppearance.BorderSize=0;
                Cursor=WinForms.Cursors.Hand;BackColor=System.Drawing.Color.White;TabStop=true;
                _tip.SetToolTip(this,"Ask Yakumi • Click to open; drag to move (Ctrl+Shift+Y)");
                try
                {
                    string path=System.IO.Path.Combine(System.IO.Path.GetDirectoryName(typeof(YakumiAssistantHost).Assembly.Location),"AI_assets","YakumiAvatar.png");
                    using(var source=System.Drawing.Image.FromFile(path)) _avatar=new System.Drawing.Bitmap(source);
                }
                catch(System.IO.IOException) { }
                catch(ArgumentException) { }
                UpdateShape();
            }
            private void UpdateShape()
            {
                if(Width<=0 || Height<=0) return;
                using(var path=new System.Drawing.Drawing2D.GraphicsPath())
                {
                    path.AddEllipse(0,0,Width,Height);var old=Region;Region=new System.Drawing.Region(path);old?.Dispose();
                }
            }
            protected override void OnSizeChanged(EventArgs e) {base.OnSizeChanged(e);UpdateShape();}
            protected override void OnMouseEnter(EventArgs e) {_hover=true;Invalidate();base.OnMouseEnter(e);}
            protected override void OnMouseLeave(EventArgs e) {_hover=false;Invalidate();base.OnMouseLeave(e);}
            protected override void OnGotFocus(EventArgs e) {base.OnGotFocus(e);Invalidate();}
            protected override void OnLostFocus(EventArgs e) {base.OnLostFocus(e);Invalidate();}
            protected override void OnPaint(WinForms.PaintEventArgs e)
            {
                e.Graphics.SmoothingMode=System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                e.Graphics.InterpolationMode=System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                using(var background=new System.Drawing.SolidBrush(_hover?System.Drawing.Color.FromArgb(255,239,242):System.Drawing.Color.White))
                    e.Graphics.FillEllipse(background,1,1,Width-2,Height-2);
                if(_avatar!=null) e.Graphics.DrawImage(_avatar,new System.Drawing.Rectangle(4,4,Width-8,Height-8));
                else WinForms.TextRenderer.DrawText(e.Graphics,"Y",Font,ClientRectangle,System.Drawing.Color.FromArgb(185,35,53),WinForms.TextFormatFlags.HorizontalCenter|WinForms.TextFormatFlags.VerticalCenter);
                using(var outline=new System.Drawing.Pen(Focused?System.Drawing.Color.FromArgb(37,86,168):System.Drawing.Color.FromArgb(185,35,53),Focused?3:2))
                    e.Graphics.DrawEllipse(outline,2,2,Width-5,Height-5);
            }
            protected override void Dispose(bool disposing)
            {if(disposing) {_avatar?.Dispose();_tip.Dispose();}base.Dispose(disposing);}
        }
        private sealed class AskAdorner : Adorner
        {
            private readonly Button _button;private readonly VisualCollection _visuals;
            private Point? _position;
            private Point Position {get {return ClampLauncherPosition(_position ?? new Point(AdornedElement.RenderSize.Width-LauncherSize-16,AdornedElement.RenderSize.Height-LauncherSize-16),AdornedElement.RenderSize,new Size(LauncherSize,LauncherSize));}}
            internal AskAdorner(UIElement element,Action open) : base(element)
            {
                _button=CreateWpfLauncher();_visuals=new VisualCollection(this) {_button};
                AttachWpfLauncherDrag(_button,element,()=>Position,point=> {_position=point;InvalidateArrange();},open);
            }
            protected override int VisualChildrenCount {get{return _visuals.Count;}}
            protected override Visual GetVisualChild(int index) {return _visuals[index];}
            protected override Size MeasureOverride(Size constraint) {_button.Measure(new Size(LauncherSize,LauncherSize));return new Size(AdornedElement.RenderSize.Width,AdornedElement.RenderSize.Height);}
            protected override Size ArrangeOverride(Size finalSize) {_button.Arrange(new Rect(Position,new Size(LauncherSize,LauncherSize)));return finalSize;}
            protected override HitTestResult HitTestCore(PointHitTestParameters parameters) {return null;}
        }

        private static Button CreateWpfLauncher()
        {
            var surface=new FrameworkElementFactory(typeof(Border),"Circle");
            surface.SetValue(Border.CornerRadiusProperty,new CornerRadius(LauncherSize/2));
            surface.SetValue(Border.BackgroundProperty,Brushes.White);surface.SetValue(Border.BorderBrushProperty,new SolidColorBrush(Color.FromRgb(185,35,53)));
            surface.SetValue(Border.BorderThicknessProperty,new Thickness(2));
            var content=new FrameworkElementFactory(typeof(ContentPresenter));content.SetValue(FrameworkElement.HorizontalAlignmentProperty,HorizontalAlignment.Center);
            content.SetValue(FrameworkElement.VerticalAlignmentProperty,VerticalAlignment.Center);surface.AppendChild(content);
            var template=new ControlTemplate(typeof(Button)) {VisualTree=surface};
            var hover=new Trigger {Property=UIElement.IsMouseOverProperty,Value=true};hover.Setters.Add(new Setter(Border.BackgroundProperty,new SolidColorBrush(Color.FromRgb(255,239,242)),"Circle"));template.Triggers.Add(hover);
            var focus=new Trigger {Property=UIElement.IsKeyboardFocusedProperty,Value=true};focus.Setters.Add(new Setter(Border.BorderBrushProperty,new SolidColorBrush(Color.FromRgb(37,86,168)),"Circle"));focus.Setters.Add(new Setter(Border.BorderThicknessProperty,new Thickness(3),"Circle"));template.Triggers.Add(focus);
            var button=new Button {Width=LauncherSize,Height=LauncherSize,Template=template,
                ToolTip="Ask Yakumi • Click to open; drag to move (Ctrl+Shift+Y)",Cursor=Cursors.Hand};
            var avatar=YakumiChatView.YakumiAvatarImage();
            button.Content=avatar==null?(object)new TextBlock {Text="Y",FontSize=24,Foreground=new SolidColorBrush(Color.FromRgb(185,35,53))}:
                new Image {Source=avatar,Width=LauncherSize-8,Height=LauncherSize-8,Stretch=Stretch.Uniform,IsHitTestVisible=false};
            System.Windows.Automation.AutomationProperties.SetName(button,"Ask Yakumi");
            return button;
        }
        private static Point ClampLauncherPosition(Point point,Size bounds,Size button)
        {
            double maxX=Math.Max(0,bounds.Width-button.Width-8),maxY=Math.Max(0,bounds.Height-button.Height-8);
            return new Point(Math.Max(Math.Min(8,maxX),Math.Min(maxX,point.X)),Math.Max(Math.Min(8,maxY),Math.Min(maxY,point.Y)));
        }
        private static void AttachWpfLauncherDrag(Button button,UIElement surface,Func<Point> position,Action<Point> move,Action open)
        {
            Point start=new Point(),origin=new Point();bool tracking=false,dragging=false;
            button.Click+=(_,__)=> {if(!dragging) open();};
            button.PreviewMouseLeftButtonDown+=(_,e)=>
            {
                // Let ButtonBase own capture and generate Click for an ordinary press/release.
                start=e.GetPosition(surface);origin=position();dragging=false;tracking=true;
            };
            button.PreviewMouseMove+=(_,e)=>
            {
                if(!tracking || e.LeftButton!=MouseButtonState.Pressed) return;
                Vector delta=e.GetPosition(surface)-start;
                if(!dragging && Math.Abs(delta.X)<SystemParameters.MinimumHorizontalDragDistance && Math.Abs(delta.Y)<SystemParameters.MinimumVerticalDragDistance) return;
                dragging=true;button.Cursor=Cursors.SizeAll;move(origin+delta);e.Handled=true;
            };
            button.PreviewMouseLeftButtonUp+=(_,e)=>
            {
                if(!tracking) return;
                tracking=false;button.Cursor=Cursors.Hand;
                if(dragging) {e.Handled=true;button.ReleaseMouseCapture();}
            };
            button.LostMouseCapture+=(_,__)=> {tracking=false;dragging=false;button.Cursor=Cursors.Hand;};
            button.PreviewKeyDown+=(_,e)=> {if(e.Key==Key.Escape && tracking) {tracking=false;button.ReleaseMouseCapture();e.Handled=true;}};
        }
        private static void AttachWinFormsLauncherDrag(WinForms.Button button,WinForms.Form root,Action<Point> move,Action open)
        {
            System.Drawing.Point start=new System.Drawing.Point(),origin=new System.Drawing.Point();bool tracking=false,dragging=false;int press=0;
            button.Click+=(_,__)=> {if(!dragging) open();};
            button.MouseDown+=(_,e)=>
            {
                if(e.Button!=WinForms.MouseButtons.Left) return;
                press++;tracking=true;dragging=false;start=button.PointToScreen(e.Location);origin=button.Location;
            };
            button.MouseMove+=(_,e)=>
            {
                if(!tracking || e.Button!=WinForms.MouseButtons.Left) return;
                var point=button.PointToScreen(e.Location);int x=point.X-start.X,y=point.Y-start.Y;
                if(!dragging && Math.Abs(x)<WinForms.SystemInformation.DragSize.Width && Math.Abs(y)<WinForms.SystemInformation.DragSize.Height) return;
                dragging=true;button.Cursor=WinForms.Cursors.SizeAll;move(new Point(origin.X+x,origin.Y+y));
            };
            button.MouseUp+=(_,e)=>
            {
                if(e.Button!=WinForms.MouseButtons.Left) return;
                // Releasing capture here prevents the native Button from raising Click.
                tracking=false;button.Cursor=WinForms.Cursors.Hand;
                // Native Click can run before or after MouseUp. Keep suppression through this mouse message.
                int completedPress=press;
                if(button.IsHandleCreated) button.BeginInvoke(new Action(()=> {if(press==completedPress) dragging=false;}));
            };
            button.MouseCaptureChanged+=(_,__)=> {if(!button.Capture) {tracking=false;button.Cursor=WinForms.Cursors.Hand;}};
            button.KeyDown+=(_,__)=>dragging=false;
            root.Deactivate+=(_,__)=> {tracking=false;button.Capture=false;button.Cursor=WinForms.Cursors.Hand;};
        }

        private static readonly Dictionary<object,Registration> Registrations=new Dictionary<object,Registration>();
        private static Registration _active;
        private static object _contextRoot;
        private static ChatWindow _window;
        private static YakumiChatView _view;
        private static Grid _embeddedGrid;
        private static System.Windows.Forms.Integration.ElementHost _elementHost;
        private static Registration _viewOwner;
        private static DispatcherTimer _timer;
        private static ShortcutFilter _shortcut;
        private static bool _shutdown;
        private static bool _installed;
        private static Action _sessionCleared;
        public static void Install(WinForms.Form main)
        {
            if(_installed) return;_installed=true;_shutdown=false;
            _shortcut=new ShortcutFilter();WinForms.Application.AddMessageFilter(_shortcut);
            Register(main,"Inventory");
            _timer=new DispatcherTimer(DispatcherPriority.Background) {Interval=TimeSpan.FromMilliseconds(350)};
            _timer.Tick+=(_,__)=>Poll();_timer.Start();
            _sessionCleared=()=>
            {
                if(_shutdown) return;
                Action clear=()=> {HideAssistant();if(_view!=null) _view.NewConversation("Signed out. Previous conversation was cleared.");};
                if(_timer.Dispatcher.CheckAccess()) clear();else _timer.Dispatcher.BeginInvoke(clear);
            };
            AppSession.SessionCleared+=_sessionCleared;
            WinForms.Application.ApplicationExit+=(_,__)=>Shutdown();
        }
        public static void Register(WinForms.Form root,string module)
        {
            if(root==null || Registrations.ContainsKey(root)) return;
            var registration=new Registration {Root=root,Module=module};Registrations.Add(root,registration);
            root.Activated+=(_,__)=> {_active=registration;_contextRoot=root;};
            root.FormClosing+=(_,__)=>OwnerClosing(registration);
            root.FormClosed+=(_,__)=> {Registrations.Remove(root);if(ReferenceEquals(_active,registration)) _active=null;};
            var button=new AvatarLauncherButton {Anchor=WinForms.AnchorStyles.Top|WinForms.AnchorStyles.Left,Visible=AppSession.IsLoggedIn};
            registration.Launcher=button;
            Point? movedPosition=null;
            Action place=()=>
            {
                Point point=ClampLauncherPosition(movedPosition ?? new Point(root.ClientSize.Width-button.Width-18,root.ClientSize.Height-button.Height-18),
                    new Size(root.ClientSize.Width,root.ClientSize.Height),new Size(button.Width,button.Height));
                button.Location=new System.Drawing.Point((int)Math.Round(point.X),(int)Math.Round(point.Y));button.BringToFront();
            };
            AttachWinFormsLauncherDrag(button,root,point=> {movedPosition=point;place();},()=>Open(root));
            root.Controls.Add(button);root.ClientSizeChanged+=(_,__)=>place();
            root.VisibleChanged+=(_,__)=> {button.Visible=AppSession.IsLoggedIn;if(root.Visible) place();};place();
        }
        public static void Register(Window root,string module)
        {
            if(root==null || Registrations.ContainsKey(root)) return;
            var registration=new Registration {Root=root,Module=module};Registrations.Add(root,registration);
            root.Activated+=(_,__)=> {_active=registration;_contextRoot=root;};
            root.Closing+=(_,__)=>OwnerClosing(registration);
            root.Closed+=(_,__)=> {Registrations.Remove(root);if(ReferenceEquals(_active,registration)) _active=null;};
            root.PreviewKeyDown+=(_,e)=> {if(e.Key==Key.Y && Keyboard.Modifiers==(ModifierKeys.Control|ModifierKeys.Shift) && AppSession.IsLoggedIn) {e.Handled=true;Open(root);} };
            if(module=="Workspace") return; // The workspace already has its Ask AI header entry.
            Action add=()=>
            {
                var content=root.Content as UIElement;if(content==null) return;
                var layer=AdornerLayer.GetAdornerLayer(content);
                if(layer!=null) layer.Add(new AskAdorner(content,()=>Open(root)));
                else
                {
                    // Add to an existing Grid without replacing or re-loading the module's visual tree.
                    var grid=content as Grid;
                    if(grid!=null)
                    {
                        var button=CreateWpfLauncher();button.HorizontalAlignment=HorizontalAlignment.Left;button.VerticalAlignment=VerticalAlignment.Top;
                        Point? movedPosition=null;
                        Func<Point> position=()=>ClampLauncherPosition(movedPosition ?? new Point(grid.ActualWidth-LauncherSize-16,grid.ActualHeight-LauncherSize-16),grid.RenderSize,new Size(LauncherSize,LauncherSize));
                        Action place=()=> {Point point=position();button.Margin=new Thickness(point.X,point.Y,0,0);};
                        AttachWpfLauncherDrag(button,grid,position,point=> {movedPosition=point;place();},()=>Open(root));
                        grid.SizeChanged+=(_,__)=>place();Grid.SetRowSpan(button,Math.Max(1,grid.RowDefinitions.Count));Grid.SetColumnSpan(button,Math.Max(1,grid.ColumnDefinitions.Count));Panel.SetZIndex(button,1000);grid.Children.Add(button);place();
                    }
                }
            };
            if(root.IsLoaded) add();else root.Loaded+=(_,__)=>add();
        }
        private static void Poll()
        {
            if(_shutdown) return;
            foreach(var registration in Registrations.Values)
                if(registration.Launcher!=null && !registration.Launcher.IsDisposed) registration.Launcher.Visible=AppSession.IsLoggedIn;
            if(!AppSession.IsLoggedIn) {if(_window!=null && _window.Visible) HideAssistant();return;}
            foreach(var form in WinForms.Application.OpenForms.Cast<WinForms.Form>().ToArray())
            {string module;if(ModuleTypes.TryGetValue(form.GetType().Name,out module)) Register(form,module);}
            foreach(var source in PresentationSource.CurrentSources.OfType<HwndSource>().ToArray())
            {var window=source.RootVisual as Window;string module;if(window!=null && ModuleTypes.TryGetValue(window.GetType().Name,out module)) Register(window,module);}
            if(_view!=null && _view.Visibility==Visibility.Visible && (_embeddedGrid!=null || (_window!=null && _window.Visible)))
            {
                try {_view.RefreshContext(CurrentContext());}
                catch(Exception ex) {HideAssistant();try {Core.Logger.LogError("[Yakumi] Could not refresh assistant context.",ex);} catch {}}
            }
        }
        private static AssistantContext CurrentContext()
        {
            var dialog=WinForms.Form.ActiveForm;
            if(dialog!=null && !ReferenceEquals(dialog,_window) && !Registrations.ContainsKey(dialog))
            {
                _contextRoot=dialog;
                var selected=AssistantScreens.Resolve(dialog);
                return AssistantContext.Capture(_active==null?"Workspace":_active.Module,selected.Page,selected.Capability,selected.RecordId);
            }
            var activeWindow=PresentationSource.CurrentSources.OfType<HwndSource>().Select(s=>s.RootVisual as Window).FirstOrDefault(w=>w!=null && w.IsActive);
            if(activeWindow!=null && !Registrations.ContainsKey(activeWindow))
            {
                _contextRoot=activeWindow;
                var selected=AssistantScreens.Resolve(activeWindow);
                return AssistantContext.Capture(_active==null?"Workspace":_active.Module,selected.Page,selected.Capability,selected.RecordId);
            }
            // Keep the caller's modal context while the modeless chat itself has focus.
            // Never fall back to an underlying grid selection while an editor is still open.
            var contextForm=_contextRoot as WinForms.Form;var contextWindow=_contextRoot as Window;
            if(_contextRoot!=null && !Registrations.ContainsKey(_contextRoot) &&
                ((contextForm!=null && !contextForm.IsDisposed && contextForm.Visible) || (contextWindow!=null && contextWindow.IsVisible)))
            {
                var selected=AssistantScreens.Resolve(_contextRoot);
                return AssistantContext.Capture(_active==null?"Workspace":_active.Module,selected.Page,selected.Capability,selected.RecordId);
            }
            var active=Registrations.Values.FirstOrDefault(r=>r.Root is Window && ((Window)r.Root).IsActive) ??
                Registrations.Values.FirstOrDefault(r=>ReferenceEquals(r.Root,WinForms.Form.ActiveForm)) ?? _active;
            if(active!=null && active.Alive)
            {
                _active=active;var selected=AssistantScreens.Resolve(active.Root);
                return AssistantContext.Capture(active.Module,selected.Page,selected.Capability,selected.RecordId);
            }
            return AssistantContext.Capture();
        }
        public static void Open(object root=null)
        {
            if(_shutdown || !AppSession.IsLoggedIn) return;
            if(root!=null)
            {
                if(!Registrations.ContainsKey(root))
                {var form=root as WinForms.Form;var window=root as Window;string module;if(ModuleTypes.TryGetValue(root.GetType().Name,out module)) {if(form!=null) Register(form,module);else if(window!=null) Register(window,module);}}
                Registration registration;if(Registrations.TryGetValue(root,out registration)) _active=registration;
            }
            if(_active==null || !_active.Alive) {Poll();if(_active==null) return;}
            bool visible=_view!=null && _view.Visibility==Visibility.Visible && (_embeddedGrid!=null || (_window!=null && _window.Visible));
            if(visible && ReferenceEquals(_viewOwner,_active)) {HideAssistant();return;}
            if(_view==null)
            {
                _view=new YakumiChatView(CurrentContext);_view.CloseRequested=HideAssistant;_view.HeightChanged=ResizeFloatingHost;
                _view.Loaded+=(_,__)=>ResizeFloatingHost(_view.Height);
            }
            _viewOwner=_active;
            DetachView();
            _view.RefreshContext(CurrentContext());
            _view.Visibility=Visibility.Visible;
            var wpf=_active.Root as Window;
            var grid=wpf==null?null:wpf.Content as Grid;
            if(grid!=null)
            {
                if(_window!=null) _window.Hide();
                _view.HorizontalAlignment=HorizontalAlignment.Right;_view.VerticalAlignment=VerticalAlignment.Bottom;
                _view.Margin=new Thickness(0,0,24,24);Panel.SetZIndex(_view,2000);
                Grid.SetColumn(_view,0);Grid.SetColumnSpan(_view,Math.Max(1,grid.ColumnDefinitions.Count));
                Grid.SetRow(_view,_active.Module=="Workspace" && grid.RowDefinitions.Count>1?1:0);
                Grid.SetRowSpan(_view,_active.Module=="Workspace"?1:Math.Max(1,grid.RowDefinitions.Count));
                grid.Children.Add(_view);_embeddedGrid=grid;_view.FocusInput();return;
            }
            if(_window==null || _window.IsDisposed)
            {
                _window=new ChatWindow();_elementHost=new System.Windows.Forms.Integration.ElementHost {Dock=WinForms.DockStyle.Fill};_window.Controls.Add(_elementHost);
            }
            _view.Margin=new Thickness(0);_elementHost.Child=_view;
            var ownerForm=_active.Root as WinForms.Form;
            if(!_window.Visible) _window.Show(new HandleOwner(_active.Handle));
            ResizeFloatingHost(_view.Height);
            if(ownerForm!=null)
            {
                var dpi=VisualTreeHelper.GetDpi(_view);
                var point=ownerForm.PointToScreen(new System.Drawing.Point(Math.Max(0,ownerForm.ClientSize.Width-_window.Width-(int)(24*dpi.DpiScaleX)),Math.Max(0,ownerForm.ClientSize.Height-_window.Height-(int)(24*dpi.DpiScaleY))));
                _window.Location=point;
            }
            _window.Activate();_view.FocusInput();
        }
        private static void ResizeFloatingHost(double height)
        {
            if(_window==null || _window.IsDisposed || _elementHost==null || !ReferenceEquals(_elementHost.Child,_view)) return;
            var dpi=VisualTreeHelper.GetDpi(_view);
            int bottom=_window.Bottom;
            _window.ClientSize=new System.Drawing.Size((int)Math.Ceiling(350*dpi.DpiScaleX),(int)Math.Ceiling(height*dpi.DpiScaleY));
            _window.Top=bottom-_window.Height;
        }
        private static void OwnerClosing(Registration owner)
        {if(ReferenceEquals(owner,_viewOwner) || ReferenceEquals(owner,_active)) {HideAssistant();DetachView();}}
        private static void DetachView()
        {
            if(_view==null) return;
            if(_view.IsKeyboardFocusWithin) Keyboard.ClearFocus();
            if(_embeddedGrid!=null) {_embeddedGrid.Children.Remove(_view);_embeddedGrid=null;}
            if(_elementHost!=null && !_elementHost.IsDisposed && ReferenceEquals(_elementHost.Child,_view)) _elementHost.Child=null;
        }
        public static void HideAssistant()
        {
            if(_view!=null) {_view.Cancel();_view.Visibility=Visibility.Collapsed;}
            if(_window==null || _window.IsDisposed) return;
            _window.Hide();_window.Owner=null;
            if(_window.IsHandleCreated)
            {if(IntPtr.Size==8) SetWindowLongPtr(_window.Handle,-8,IntPtr.Zero);else SetWindowLong(_window.Handle,-8,0);}
        }
        private static void Shutdown()
        {
            _shutdown=true;if(_timer!=null) _timer.Stop();
            if(_sessionCleared!=null) AppSession.SessionCleared-=_sessionCleared;
            if(_shortcut!=null) WinForms.Application.RemoveMessageFilter(_shortcut);
            if(_view!=null) _view.Dispose();
            if(_window!=null && !_window.IsDisposed) {_window.ShuttingDown=true;_window.Close();_window.Dispose();}
            Registrations.Clear();_active=null;_contextRoot=null;_viewOwner=null;
        }
        // Called only by the isolated offline test runner, never by the running desktop application.
        internal static string VerifyIsolatedLifecycle(AssistantContext context,YakumiAssistantService service)
        {
            if(_installed || _window!=null) throw new InvalidOperationException("Host lifecycle checks require a separate process.");
            using(var first=new WinForms.Form())
            using(var second=new WinForms.Form())
            {
                try
                {
                    Register(first,"Inventory");Register(second,"Tickets");_active=Registrations[first];
                    _window=new ChatWindow();_view=new YakumiChatView(()=>context,service);
                    _elementHost=new System.Windows.Forms.Integration.ElementHost {Dock=WinForms.DockStyle.Fill,Child=_view};_window.Controls.Add(_elementHost);
                    _view.HeightChanged=ResizeFloatingHost;
                    IntPtr handle=_window.Handle;
                    _view.Measure(new Size(350,540));_view.Arrange(new Rect(0,0,350,540));_view.UpdateLayout();ResizeFloatingHost(540);
                    int expandedHeight=_window.ClientSize.Height;int bottom=_window.Bottom;
                    typeof(YakumiChatView).GetMethod("ToggleYakumiMinimized",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).Invoke(_view,null);
                    if(_window.ClientSize.Height>=expandedHeight || _window.Bottom!=bottom || _window.Handle!=handle) throw new InvalidOperationException("Minimize did not shrink the reusable host at the same anchor.");
                    typeof(YakumiChatView).GetMethod("ToggleYakumiMinimized",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).Invoke(_view,null);
                    if(_window.ClientSize.Height!=expandedHeight || _window.Bottom!=bottom || _window.Handle!=handle) throw new InvalidOperationException("Restore did not preserve the reusable host.");
                    if(IntPtr.Size==8) SetWindowLongPtr(handle,-8,first.Handle);else SetWindowLong(handle,-8,first.Handle.ToInt32());
                    first.Close();
                    if(_window.IsDisposed || _window.Handle!=handle) throw new InvalidOperationException("Owner close destroyed the assistant host.");
                    _active=Registrations[second];
                    if(IntPtr.Size==8) SetWindowLongPtr(handle,-8,second.Handle);else SetWindowLong(handle,-8,second.Handle.ToInt32());
                    _window.Close();
                    if(_window.IsDisposed || _window.Handle!=handle) throw new InvalidOperationException("Normal close destroyed the assistant host.");
                    _view.NewConversation("Synthetic signed-out session.");second.Close();
                    if(_window.IsDisposed || _window.Handle!=handle) throw new InvalidOperationException("Module switch destroyed the reusable host.");
                    return "Isolated host checks passed: header-only minimize/restore with anchored size and same HWND, owner close, module switch, normal close, conversation reset and final disposal. No application startup or database connection.";
                }
                finally {Shutdown();_window=null;_view=null;_elementHost=null;}
            }
        }
        [DllImport("user32.dll",EntryPoint="SetWindowLongPtrW")] private static extern IntPtr SetWindowLongPtr(IntPtr handle,int index,IntPtr value);
        [DllImport("user32.dll",EntryPoint="SetWindowLongW")] private static extern int SetWindowLong(IntPtr handle,int index,int value);
    }
}
