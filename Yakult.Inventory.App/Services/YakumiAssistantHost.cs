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
        private sealed class AskAdorner : Adorner
        {
            private readonly Button _button;private readonly VisualCollection _visuals;
            internal AskAdorner(UIElement element,Action open) : base(element)
            {
                _button=new Button {Content="Ask Yakumi",Width=130,Height=35,Padding=new Thickness(8),ToolTip="Read-only assistant (Ctrl+Shift+Y)",
                    Background=new SolidColorBrush(Color.FromRgb(150,27,44)),Foreground=Brushes.White,Cursor=Cursors.Hand};
                _button.Click+=(_,__)=>open();_visuals=new VisualCollection(this) {_button};
            }
            protected override int VisualChildrenCount {get{return _visuals.Count;}}
            protected override Visual GetVisualChild(int index) {return _visuals[index];}
            protected override Size MeasureOverride(Size constraint) {_button.Measure(new Size(130,35));return new Size(AdornedElement.RenderSize.Width,AdornedElement.RenderSize.Height);}
            protected override Size ArrangeOverride(Size finalSize) {_button.Arrange(new Rect(Math.Max(0,finalSize.Width-146),Math.Max(0,finalSize.Height-51),130,35));return finalSize;}
            protected override HitTestResult HitTestCore(PointHitTestParameters parameters) {return null;}
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
            var button=new WinForms.Button {Text="Ask Yakumi",Width=124,Height=34,Anchor=WinForms.AnchorStyles.Bottom|WinForms.AnchorStyles.Right,
                BackColor=System.Drawing.Color.FromArgb(150,27,44),ForeColor=System.Drawing.Color.White,FlatStyle=WinForms.FlatStyle.Flat,
                TabStop=true,Visible=AppSession.IsLoggedIn};
            registration.Launcher=button;
            Action place=()=> {button.Left=Math.Max(8,root.ClientSize.Width-button.Width-18);button.Top=Math.Max(8,root.ClientSize.Height-button.Height-18);button.BringToFront();};
            button.Click+=(_,__)=>Open(root);root.Controls.Add(button);root.ClientSizeChanged+=(_,__)=>place();
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
                        var button=new Button {Content="Ask Yakumi",HorizontalAlignment=HorizontalAlignment.Right,VerticalAlignment=VerticalAlignment.Bottom,Margin=new Thickness(16),Padding=new Thickness(12,8,12,8)};
                        button.Click+=(_,__)=>Open(root);Grid.SetRowSpan(button,Math.Max(1,grid.RowDefinitions.Count));Grid.SetColumnSpan(button,Math.Max(1,grid.ColumnDefinitions.Count));Panel.SetZIndex(button,1000);grid.Children.Add(button);
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
                _view=new YakumiChatView(CurrentContext);_view.CloseRequested=HideAssistant;
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
            if(ownerForm!=null)
            {
                var point=ownerForm.PointToScreen(new System.Drawing.Point(Math.Max(0,ownerForm.ClientSize.Width-374),Math.Max(0,ownerForm.ClientSize.Height-564)));
                _window.Location=point;
            }
            if(!_window.Visible) _window.Show(new HandleOwner(_active.Handle));
            _window.Activate();_view.FocusInput();
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
                    _window.Controls.Add(new System.Windows.Forms.Integration.ElementHost {Dock=WinForms.DockStyle.Fill,Child=_view});
                    IntPtr handle=_window.Handle;
                    if(IntPtr.Size==8) SetWindowLongPtr(handle,-8,first.Handle);else SetWindowLong(handle,-8,first.Handle.ToInt32());
                    first.Close();
                    if(_window.IsDisposed || _window.Handle!=handle) throw new InvalidOperationException("Owner close destroyed the assistant host.");
                    _active=Registrations[second];
                    if(IntPtr.Size==8) SetWindowLongPtr(handle,-8,second.Handle);else SetWindowLong(handle,-8,second.Handle.ToInt32());
                    _window.Close();
                    if(_window.IsDisposed || _window.Handle!=handle) throw new InvalidOperationException("Normal close destroyed the assistant host.");
                    _view.NewConversation("Synthetic signed-out session.");second.Close();
                    if(_window.IsDisposed || _window.Handle!=handle) throw new InvalidOperationException("Module switch destroyed the reusable host.");
                    return "Isolated host checks passed: owner close, module switch, normal close, conversation reset and final disposal. No application startup or database connection.";
                }
                finally {Shutdown();_window=null;_view=null;}
            }
        }
        [DllImport("user32.dll",EntryPoint="SetWindowLongPtrW")] private static extern IntPtr SetWindowLongPtr(IntPtr handle,int index,IntPtr value);
        [DllImport("user32.dll",EntryPoint="SetWindowLongW")] private static extern int SetWindowLong(IntPtr handle,int index,int value);
    }
}
