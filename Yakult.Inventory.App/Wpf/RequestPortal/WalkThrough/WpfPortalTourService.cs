using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using Yakult.Inventory.App.Models;
using Yakult.Inventory.App.Pages.RequestPortal;
using Yakult.Inventory.App.Services;
using Yakult.Inventory.App.WPF.RequestPortal.NewRequest.Views;
using Yakult.Inventory.App.WPF.RequestPortal.RequestHistory.Views;
using Yakult.Inventory.App.WPF.RequestPortal.AuthorizationHistory.Views;
using Yakult.Inventory.App.WPF.RequestPortal.AuthorizeRequest.Views;
using Yakult.Inventory.App.WPF.RequestPortal.AssistedRequest.Views;

namespace Yakult.Inventory.App.WPF.RequestPortal.WalkThrough
{
    /// <summary>
    /// Manages the three guided tours for the Requester Portal:
    ///   • New Request
    ///   • Request History
    ///   • Authorization History
    ///
    /// Call StartTour(section) to launch a specific section, or
    /// LaunchIfFirstTime() from the portal's Load event.
    /// Wire the "?" help button to StartTourForCurrentTab().
    /// </summary>
    public class WpfPortalTourService : IDisposable
    {
        // ── Constants ────────────────────────────────────────────────────────
        private const string FlagFileName = "requester-portal-tour-v1.flag";

        public enum TourSection { NewRequest, AssistedRequest, RequestHistory, AuthorizationHistory, SignedApprovals, ApprovalQueue, ApproverHomepage }

        // ── State ────────────────────────────────────────────────────────────
        private WpfPortalTourWindow _window;
        private List<WpfPortalTourStep> _steps;
        private int _currentIndex;
        private bool _active;
        private bool _suspended;

        // True while we are in the middle of showing/hiding the overlay window itself.
        // The WinForms host form fires Deactivated when the WPF overlay takes focus, which
        // would immediately re-suspend the tour. This flag lets the Activated/Deactivated
        // handlers skip those internally-triggered events.
        private bool _suppressHostEvents;
        public  bool  SuppressHostEvents => _suppressHostEvents;

        // ── References set by the host form ─────────────────────────────────
        private NewRequestView _newRequestView;
        private RequestHistoryView _requestHistoryView;
        private AuthorizationHistoryView _authHistoryView;
        private AuthorizeRequestView _authorizeRequestView;
        private AssistedRequestView  _assistedRequestView;
        // Approver landing page card references
        private Control _landingCardAuthorize;
        private Control _landingCardSubmit;
        private Control _landingCardHistory;
        private Control _notifBellHost;       // WinForms ElementHost for the bell
        private Control _tabControlWinForms;  // WinForms TabControl
        private Form    _hostForm;            // RequesterPortalForm — owner for tour dialogs

        // ── Section currently running ────────────────────────────────────────
        private TourSection _activeSection;

        // ── Dummy data tracking ──────────────────────────────────────────────
        private bool _newRequestDummyInjected;
        private bool _historyDummyInjected;
        private bool _authDummyInjected;
        private bool _approvalDummyInjected;

        // ── Dialog tour tracking ─────────────────────────────────────────────
        private Form _activeTourDialog;
        public  bool  DialogStepsActive  { get; private set; }
        private int   _dialogStartIndex = -1; // step index that called OpenTourDialog

        // Tracks the pending BeginInvoke that resets _suppressHostEvents = false.
        // Cancelled by CloseTourWindow() to prevent the reset from firing during teardown.
        private DispatcherOperation _pendingSuppressReset;

        // No global exception handlers are needed.
        // Earlier revisions subscribed to Dispatcher.CurrentDispatcher.UnhandledException to
        // absorb WPF focus-restoration exceptions that fired after _window.Close() destroyed the
        // overlay HWND.  With the singleton overlay (never closed during normal tour termination)
        // the HWND is never destroyed mid-session, so the WM_ACTIVATE/focus-restoration race
        // cannot occur.  See WpfPortalTourWindow.HideTour() for the full explanation.

        // ── Win32 z-order helper ─────────────────────────────────────────────
        // Used to raise the overlay above the tour dialog WITHOUT activating it.
        // Calling _window.Activate() can set Application.Current.MainWindow = _window;
        // then closing the overlay triggers Application.Shutdown() (OnMainWindowClose
        // default mode), which kills WPF and can freeze or close the host application.
        [DllImport("user32.dll", SetLastError = false)]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter,
            int X, int Y, int cx, int cy, uint uFlags);
        private static readonly IntPtr HWND_TOP     = IntPtr.Zero;
        private const uint SWP_NOMOVE     = 0x0002;
        private const uint SWP_NOSIZE     = 0x0001;
        private const uint SWP_NOACTIVATE = 0x0010;

        // ────────────────────────────────────────────────────────────────────
        // Initialise
        // ────────────────────────────────────────────────────────────────────

        public void SetViews(
            NewRequestView           newRequestView,
            RequestHistoryView       requestHistoryView,
            AuthorizationHistoryView authHistoryView,
            Control                  notifBellHost,
            Control                  tabControl,
            AuthorizeRequestView     authorizeRequestView  = null,
            Control                  landingCardAuthorize  = null,
            Control                  landingCardSubmit     = null,
            Control                  landingCardHistory    = null,
            AssistedRequestView      assistedRequestView   = null)
        {
            _newRequestView         = newRequestView;
            _requestHistoryView     = requestHistoryView;
            _authHistoryView        = authHistoryView;
            _authorizeRequestView   = authorizeRequestView;
            _assistedRequestView    = assistedRequestView;
            _landingCardAuthorize   = landingCardAuthorize;
            _landingCardSubmit      = landingCardSubmit;
            _landingCardHistory     = landingCardHistory;
            _notifBellHost          = notifBellHost;
            _tabControlWinForms     = tabControl;
        }

        /// <summary>
        /// Resolves the host WinForms form that owns the overlay window, using a three-tier
        /// fallback strategy so that the overlay always has a Win32 owner.
        ///
        /// An ownerless overlay is problematic: without an owner the window can appear as a
        /// separate entry in the taskbar and Alt+Tab switcher regardless of ShowInTaskbar=false.
        /// Win32 owns the ShowInTaskbar suppression at the owner chain level — a top-level
        /// window with no owner always gets a taskbar button.
        ///
        /// Tier 1 — derive from the bell ElementHost registered via SetViews().
        ///          This is the authoritative source and will succeed in all normal flows.
        /// Tier 2 — search Application.OpenForms for RequesterPortalForm by type name.
        ///          Fallback for the case where SetViews() was called but FindForm() returned
        ///          null (e.g. the host control had not been parented yet).
        /// Tier 3 — use Form.ActiveForm.
        ///          Last resort; may return null or a wrong form if the portal is not focused.
        /// </summary>
        private Form ResolveHostForm()
        {
            // Tier 1: standard path — bell control knows its parent form.
            if (_notifBellHost != null && !_notifBellHost.IsDisposed)
            {
                var f = _notifBellHost.FindForm() as Form;
                if (f != null && !f.IsDisposed && f.IsHandleCreated)
                {
                    System.Diagnostics.Debug.WriteLine(
                        $"[Tour] ResolveHostForm — Tier 1 succeeded: {f.GetType().Name}");
                    return f;
                }
            }

            // Tier 2: scan open forms for the portal window by type name.
            // Using type name avoids a circular assembly dependency on RequesterPortalForm.
            foreach (Form openForm in System.Windows.Forms.Application.OpenForms)
            {
                if (openForm == null || openForm.IsDisposed || !openForm.IsHandleCreated) continue;
                if (openForm.GetType().Name == "RequesterPortalForm")
                {
                    System.Diagnostics.Debug.WriteLine(
                        $"[Tour] ResolveHostForm — Tier 2 succeeded via OpenForms: {openForm.GetType().Name}");
                    return openForm;
                }
            }

            // Tier 3: last resort — use the currently active form.
            var active = System.Windows.Forms.Form.ActiveForm;
            if (active != null && !active.IsDisposed && active.IsHandleCreated)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[Tour] ResolveHostForm — Tier 3 (ActiveForm): {active.GetType().Name}. " +
                    "This may not be the portal — owner assignment could be incorrect.");
                return active;
            }

            System.Diagnostics.Debug.WriteLine(
                "[Tour] ResolveHostForm — WARNING: all three resolution tiers failed. " +
                "SetViews() may not have been called, or no WinForms form is currently open. " +
                "Overlay creation will be deferred until a valid owner is available.");
            return null;
        }

        // ────────────────────────────────────────────────────────────────────
        // Public launch API
        // ────────────────────────────────────────────────────────────────────

        public void LaunchIfFirstTime()
        {
            if (!HasCompletedTour())
                StartTour(TourSection.NewRequest);
        }

        public void StartTourForCurrentTab(TabControl tc, TabPage newReqPage, TabPage assistedPage, TabPage histPage, TabPage authPage)
        {
            var selected = tc?.SelectedTab;
            if (selected == histPage)
                StartTour(TourSection.RequestHistory);
            else if (selected == authPage)
            {
                // Check which inner sub-tab is active
                if (_authHistoryView?.ViewModel.ShowSignedTab == true)
                    StartTour(TourSection.SignedApprovals);
                else
                    StartTour(TourSection.AuthorizationHistory);
            }
            else if (selected == assistedPage)
                StartTour(TourSection.AssistedRequest);
            else
                StartTour(TourSection.NewRequest);
        }

        public void StartTour(TourSection section)
        {
            // Tear down any tour that is already running (hides overlay, closes dialog, cleans data).
            CloseTourWindow();

            // Resolve host form using the three-tier strategy so that EnsureOverlayCreated()
            // always has a pre-validated owner reference to work with.
            _hostForm = ResolveHostForm();

            // Create the singleton overlay on the first call; reuse it on every subsequent call.
            // Returns false (and logs a warning) if no valid Win32 owner exists.
            if (!EnsureOverlayCreated())
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[Tour] StartTour({section}) — aborted: overlay could not be created " +
                    "because no valid host form was found. Call SetViews() before StartTour().");
                return;
            }

            // Wire delegates — simple assignment; no event accumulation across tours.
            _window.OnNextClicked = OnNext;
            _window.OnPrevClicked = OnPrev;
            _window.OnSkipClicked = OnSkip;

            _activeSection = section;
            _steps         = BuildSteps(section);
            _currentIndex  = 0;
            _active        = true;

            InjectDummyDataForSection(section);

            // Size to current screen (covers resolution or monitor changes between tours).
            _window.Left   = 0;
            _window.Top    = 0;
            _window.Width  = SystemParameters.PrimaryScreenWidth;
            _window.Height = SystemParameters.PrimaryScreenHeight;

            _window.ResetVisuals();
            ShowOverlay();

            _steps[0].OnEnter?.Invoke();
            ApplyCurrentStep();
        }

        public bool IsActive => _active;

        // ────────────────────────────────────────────────────────────────────
        // Navigation
        // ────────────────────────────────────────────────────────────────────

        private void OnNext()
        {
            if (_currentIndex < _steps.Count - 1)
            {
                _steps[_currentIndex].OnLeave?.Invoke();
                _currentIndex++;
                _steps[_currentIndex].OnEnter?.Invoke();
                ApplyCurrentStep();
            }
            else
            {
                _steps[_currentIndex].OnLeave?.Invoke();
                FinishTour();
            }
        }

        private void OnPrev()
        {
            if (_currentIndex > 0)
            {
                _steps[_currentIndex].OnLeave?.Invoke();
                _currentIndex--;
                // If the user backs out before the step that opened the dialog, close it.
                if (_dialogStartIndex >= 0 && _currentIndex < _dialogStartIndex)
                    CloseTourDialog();
                _steps[_currentIndex].OnEnter?.Invoke();
                ApplyCurrentStep();
            }
        }

        private void OnSkip()
        {
            // Do NOT call OnLeave here — CloseTourWindow() handles all teardown.
            // Calling OnLeave first would close the WinForms dialog while the WPF overlay is
            // still visible, causing a double WM_ACTIVATE storm through ElementHost.
            FinishTour();
        }

        /// <summary>Closes the tour overlay without marking it as completed (e.g. on tab switch).</summary>
        public void StopTour() => CloseTourWindow();

        /// <summary>
        /// Hides the overlay while the host application loses focus (Alt+Tab, minimize).
        /// Uses HideTour() (Opacity=0, not Hide()) to avoid the WM_ACTIVATE/focus-restoration
        /// crash.  The tour step is preserved; call ResumeTour() to restore.
        /// </summary>
        public void SuspendTour()
        {
            if (!_active || _window == null || _window.IsClosed() || _suspended) return;
            _suspended = true;
            _window.HideTour();
        }

        /// <summary>
        /// Restores the overlay after the host application regains focus.
        /// Re-applies the current step so spotlight and card are repositioned correctly.
        /// </summary>
        public void ResumeTour()
        {
            if (!_active || _window == null || _window.IsClosed() || !_suspended) return;
            _suspended = false;
            ShowOverlay();
            ApplyCurrentStep();
        }

        // Shows the overlay and gives it keyboard focus.
        // Sets _suppressHostEvents for the duration of the activation cycle: the WinForms host
        // form briefly fires Deactivated when the WPF overlay steals focus, and that event must
        // be ignored or it would immediately re-suspend the tour.
        private void ShowOverlay()
        {
            // ShutdownMode guard: EnsureOverlayCreated() sets this on the first Show() call.
            // Repeated here as a defensive measure in case Application.Current was replaced.
            if (System.Windows.Application.Current != null)
                System.Windows.Application.Current.ShutdownMode =
                    System.Windows.ShutdownMode.OnExplicitShutdown;

            _suppressHostEvents = true;
            _window.ShowTour();   // Opacity=1, IsHitTestVisible=true; calls Show() only on first use

            // Ensure ShutdownMode after ShowTour() in case WPF created Application.Current
            // on the very first Show() call and reset the mode to OnMainWindowClose.
            if (System.Windows.Application.Current != null)
                System.Windows.Application.Current.ShutdownMode =
                    System.Windows.ShutdownMode.OnExplicitShutdown;

            _window.Activate();

            // Reset the flag after the current message-pump cycle has processed the activation
            // events that fire synchronously during Activate().  Track the operation so
            // CloseTourWindow() can cancel it if the tour ends before this delegate fires.
            CancelPendingSuppressReset();
            _pendingSuppressReset = _window.Dispatcher.BeginInvoke(
                new Action(() => { _pendingSuppressReset = null; _suppressHostEvents = false; }),
                DispatcherPriority.Background);
        }

        private void CancelPendingSuppressReset()
        {
            if (_pendingSuppressReset != null)
            {
                _pendingSuppressReset.Abort();
                _pendingSuppressReset = null;
            }
        }

        /// <summary>
        /// Creates the overlay window exactly once for the lifetime of this service instance.
        /// Subsequent calls are no-ops. Must be called on the WinForms UI thread.
        ///
        /// Returns true if the overlay is ready to use, false if creation was deferred because
        /// no valid Win32 owner could be found (StartTour must abort in that case).
        ///
        /// Why show-then-hide on creation:
        ///   WPF does not materialise the HWND until Show() is called. We need the HWND so we
        ///   can set the Win32 owner (interop.Owner) and so PhysicalToLogical() can read
        ///   PresentationSource. We set Opacity=0 immediately so the blank window is never
        ///   visible. ShutdownMode is forced to OnExplicitShutdown AFTER Show() because WPF may
        ///   create Application.Current on the first Show() and default it to OnMainWindowClose.
        ///
        /// Thread safety:
        ///   Not thread-safe by design. This class is single-threaded: all callers are WinForms
        ///   UI-thread event handlers (button click, form load, form closed). WPF windows cannot
        ///   be created on background threads in a WinForms-hosted interop app. A debug assertion
        ///   guards against accidental off-thread calls during development.
        /// </summary>
        private bool EnsureOverlayCreated()
        {
            // ── Duplicate-instance guard ─────────────────────────────────────
            if (_window != null)
            {
                // Window already exists and is valid — nothing to do.
                if (!_window.IsClosed()) return true;

                // Defensive: the window was closed externally (should only happen via Dispose).
                // Drop the dead reference so we fall through and recreate below.
                System.Diagnostics.Debug.WriteLine(
                    "[Tour] EnsureOverlayCreated — stale window detected (IsClosed=true); recreating.");
                _window = null;
            }

            // ── Thread assertion (debug builds only) ─────────────────────────
            System.Diagnostics.Debug.Assert(
                System.Windows.Forms.Application.MessageLoop,
                "WpfPortalTourService: EnsureOverlayCreated() must be called on the WinForms UI thread. " +
                "WPF windows cannot be created on background threads in a WinForms-hosted interop app.");

            // ── Owner resolution ─────────────────────────────────────────────
            // The overlay MUST have a Win32 owner.  Without one, Windows ignores ShowInTaskbar=false
            // and the overlay appears as a top-level taskbar entry, which is confusing and wrong.
            if (_hostForm == null || _hostForm.IsDisposed || !_hostForm.IsHandleCreated)
                _hostForm = ResolveHostForm();

            if (_hostForm == null)
            {
                // All three resolution tiers failed. ResolveHostForm() has already logged the
                // reason. Defer overlay creation: StartTour() will abort cleanly.
                return false;
            }

            // ── Create overlay ───────────────────────────────────────────────
            _window               = new WpfPortalTourWindow();
            _window.WindowState   = WindowState.Normal;
            _window.ShowInTaskbar = false;
            _window.Left          = 0;
            _window.Top           = 0;
            _window.Width         = SystemParameters.PrimaryScreenWidth;
            _window.Height        = SystemParameters.PrimaryScreenHeight;

            // Assign Win32 owner BEFORE Show() so the HWND is created in the correct owner chain.
            var interop = new WindowInteropHelper(_window);
            interop.EnsureHandle();
            interop.Owner = _hostForm.Handle;
            System.Diagnostics.Debug.WriteLine(
                $"[Tour] EnsureOverlayCreated — owner assigned: " +
                $"{_hostForm.GetType().Name} HWND=0x{_hostForm.Handle:X}");

            // Materialise the HWND, lock down ShutdownMode, then immediately hide.
            _window.Show();
            if (System.Windows.Application.Current != null)
                System.Windows.Application.Current.ShutdownMode =
                    System.Windows.ShutdownMode.OnExplicitShutdown;
            _window.HideTour();

            System.Diagnostics.Debug.WriteLine("[Tour] EnsureOverlayCreated — overlay ready.");
            return true;
        }

        private void FinishTour()
        {
            MarkTourCompleted();
            CloseTourWindow();
        }

        private void CloseTourWindow()
        {
            _active             = false;
            _suppressHostEvents = true;
            CancelPendingSuppressReset();

            // Hide overlay BEFORE closing the dialog.
            //
            // When CloseTourDialog() destroys the WinForms dialog, Win32 must activate a new
            // window.  If the overlay is still visible at that moment Win32 picks it (the topmost
            // visible window) and sends WM_ACTIVATE to it; then our HideTour() deactivates it
            // and Win32 sends WM_ACTIVATE again to RequesterPortalForm — a double-activation
            // storm through WPF's ElementHost interop that can corrupt WPF focus state.
            // With the overlay hidden first the dialog's WM_DESTROY activates RequesterPortalForm
            // directly: one clean, predictable activation.
            if (_window != null && !_window.IsClosed())
            {
                // Detach all delegates so that button clicks arriving after teardown are no-ops.
                _window.OnNextClicked = null;
                _window.OnPrevClicked = null;
                _window.OnSkipClicked = null;
                _window.HideTour();   // Opacity=0 — no HWND destruction, no WM_ACTIVATE
            }

            CloseTourDialog();   // detaches FormClosing handler, calls ExitTourMode(), disposes dialog
            CleanupDummyData();  // removes injected tour rows from view models

            // Drop the step list so all OnEnter/OnLeave/TargetWpf lambda closures are released.
            // Steps are rebuilt fresh each time StartTour() is called, so nothing is lost.
            _steps        = null;
            _currentIndex = 0;

            _suspended          = false;
            _suppressHostEvents = false;
            // _window is intentionally kept alive — same instance is reused by the next StartTour().
        }

        // ── Dialog tour helpers ──────────────────────────────────────────────

        /// <summary>
        /// Opens a detail dialog non-modally for the tour walkthrough, then brings the
        /// overlay back above it using SetWindowPos(NOACTIVATE) rather than Activate().
        ///
        /// Why SetWindowPos instead of _window.Activate():
        ///   Calling Activate() can cause WPF to assign Application.Current.MainWindow = _window.
        ///   With the default ShutdownMode.OnMainWindowClose, closing the overlay later would
        ///   trigger Application.Shutdown(), which kills WPF in the host process and can
        ///   minimize or close the entire application. SetWindowPos with SWP_NOACTIVATE raises
        ///   the overlay in Z-order without sending WM_ACTIVATE, eliminating that side-effect.
        ///   The ShowOverlay() call in StartTour already activated the window (giving it
        ///   keyboard focus), so re-activating here is unnecessary.
        /// </summary>
        internal void OpenTourDialog(Form dlg)
        {
            CloseTourDialog();
            _activeTourDialog  = dlg;
            DialogStepsActive  = true;
            _dialogStartIndex  = _currentIndex;

            // Left-align the dialog so the card (Side="right") fits to its right without
            // being clamped into the dialog. At 125–150% DPI the overlay's logical width is
            // ~911–1093 px; CenterScreen would push the dialog right far enough that the card
            // clamp (ActualWidth - 400 - 12) sits inside the dialog, hiding ~40% of its content.
            // Pinning the dialog to x=12 (physical) keeps its right edge clear of the clamp.
            dlg.StartPosition = System.Windows.Forms.FormStartPosition.Manual;
            var workScreen = _hostForm != null
                ? System.Windows.Forms.Screen.FromHandle(_hostForm.Handle)
                : System.Windows.Forms.Screen.PrimaryScreen;
            var wa = workScreen.WorkingArea;
            int safeH    = Math.Min(dlg.Height, wa.Height - 10);
            dlg.Height   = safeH;
            dlg.Location = new System.Drawing.Point(wa.Left + 12, wa.Top + Math.Max(0, (wa.Height - safeH) / 2));

            // _suppressHostEvents prevents the host form's Deactivated handler from calling
            // SuspendTour() while the activation cycle from dlg.Show() is in flight.
            _suppressHostEvents = true;
            // Cancel any previously scheduled reset so it does not fire mid-show.
            CancelPendingSuppressReset();

            // Show with the host form as owner: dialog appears above RequesterPortalForm,
            // and WM_ACTIVATE is routed through it for proper suspend/resume on Alt+Tab.
            if (_hostForm != null)
                dlg.Show(_hostForm);
            else
                dlg.Show();
            dlg.PerformLayout();
            dlg.Refresh();

            // Block user interaction with the tour dialog while the tour is running:
            //   • FormClosing is cancelled so clicking the title-bar X or the Close button
            //     does not remove the dialog mid-tour (leaving later steps with no target).
            //   • EnterTourMode() disables the scroll panel and custom Close button.
            dlg.FormClosing += OnTourDialogFormClosing;
            if (dlg is RequestDetailDialog rdd)    rdd.EnterTourMode();
            if (dlg is AuthorizationDetailDialog add) add.EnterTourMode();

            // Raise the overlay above the dialog WITHOUT activating it (SWP_NOACTIVATE).
            // This avoids the WM_ACTIVATE → Application.MainWindow assignment that would
            // cause Application.Shutdown() when the overlay is later closed.
            if (_window != null && !_window.IsClosed())
            {
                var overlayInterop = new WindowInteropHelper(_window);
                if (overlayInterop.Handle != IntPtr.Zero)
                    SetWindowPos(overlayInterop.Handle, HWND_TOP, 0, 0, 0, 0,
                        SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);
            }

            // Reset _suppressHostEvents after the current pump cycle.
            // Track the operation so CloseTourWindow() can cancel it if the tour ends before
            // this Background-priority delegate fires.
            _pendingSuppressReset = _window?.Dispatcher.BeginInvoke(
                new Action(() => { _pendingSuppressReset = null; _suppressHostEvents = false; }),
                DispatcherPriority.Background);
        }

        private void OnTourDialogFormClosing(object sender, System.Windows.Forms.FormClosingEventArgs e)
        {
            // Cancel any attempt to close the tour dialog while the tour is active.
            // This prevents the user clicking the title-bar X or the in-dialog Close button
            // from removing the dialog and leaving subsequent tour steps with no target.
            if (DialogStepsActive)
                e.Cancel = true;
        }

        /// <summary>Closes the currently open tour dialog and resets dialog-step state.</summary>
        internal void CloseTourDialog()
        {
            if (_activeTourDialog != null && !_activeTourDialog.IsDisposed)
            {
                // Clear WPF keyboard focus before closing the WinForms dialog.
                // CloseTourWindow() has already called HideTour() (Opacity=0) at this point, but
                // clearing focus here too ensures WPF's internal focus state is clean so that
                // the WM_ACTIVATE routed to RequesterPortalForm after dialog destruction does not
                // try to restore focus to an element inside the hidden overlay.
                _window?.Dispatcher.Invoke(() => System.Windows.Input.Keyboard.ClearFocus());

                // Detach the FormClosing guard so the Close() call below is not cancelled.
                _activeTourDialog.FormClosing -= OnTourDialogFormClosing;
                // Restore interactive state before closing (controls re-enabled, close btn re-enabled).
                if (_activeTourDialog is RequestDetailDialog rdd)    rdd.ExitTourMode();
                if (_activeTourDialog is AuthorizationDetailDialog add) add.ExitTourMode();
                _activeTourDialog.Close();
                _activeTourDialog.Dispose();
            }
            _activeTourDialog  = null;
            DialogStepsActive  = false;
            _dialogStartIndex  = -1;
        }

        // ────────────────────────────────────────────────────────────────────
        // Rendering
        // ────────────────────────────────────────────────────────────────────

        private void ApplyCurrentStep()
        {
            if (_window == null || !_active) return;

            var step = _steps[_currentIndex];

            Rect? targetRect = null;
            // Secondary highlight: the blue ring and card anchor point, separate from the spotlight hole.
            // Used for dialog sub-steps so the full dialog stays visible (spotlight = dialog) while
            // the blue ring and tour card still pinpoint the specific section being explained.
            Rect? ringRect   = null;

            if (step.TargetWpf != null)
            {
                try
                {
                    var element = step.TargetWpf();
                    if (element != null && element.IsLoaded && element.IsVisible)
                    {
                        FrameworkElement scrollAnchor = null;
                        if (step.ScrollAnchorWpf != null)
                        {
                            var a = step.ScrollAnchorWpf();
                            if (a != null && a.IsLoaded && a.IsVisible)
                                scrollAnchor = a;
                        }
                        targetRect = GetWpfElementRect(element, scrollAnchor);
                    }
                }
                catch { /* element not ready — render without spotlight */ }
            }
            else if (step.TargetWinForms != null)
            {
                try
                {
                    var ctrl = step.TargetWinForms();
                    if (ctrl != null && ctrl.Visible)
                    {
                        var ctrlRect = GetWinFormsControlRect(ctrl);

                        // Dialog sub-step: ctrl is a control INSIDE the tour dialog (not the dialog itself).
                        // Spotlight the full dialog so the entire popup stays visible in the clear area,
                        // and use the sub-control as the secondary ring so the blue highlight ring and
                        // the tour card still pinpoint exactly which section is being explained.
                        if (DialogStepsActive && _activeTourDialog != null &&
                            !(ctrl is Form) && !_activeTourDialog.IsDisposed)
                        {
                            targetRect = GetWinFormsControlRect(_activeTourDialog); // spotlight = whole dialog
                            ringRect   = ctrlRect;                                   // ring      = sub-control
                        }
                        else
                        {
                            targetRect = ctrlRect; // step 7: spotlight IS the dialog; no ring split needed
                        }
                    }
                }
                catch { /* control not ready */ }
            }

            // Demo banner only shows when dummy data could not be injected (e.g. real data already exists
            // but we still want to flag that the grid values shown are live, not tour examples).
            // For sections where we injected dummy rows, no banner is needed.
            bool showDemoBanner = false;
            if (_activeSection == TourSection.RequestHistory)
                showDemoBanner = _historyDummyInjected;
            else if (_activeSection == TourSection.AuthorizationHistory)
                showDemoBanner = _authDummyInjected;
            else if (_activeSection == TourSection.SignedApprovals)
                showDemoBanner = _signedDummyInjected;
            else if (_activeSection == TourSection.NewRequest)
                showDemoBanner = _newRequestDummyInjected;
            else if (_activeSection == TourSection.ApprovalQueue)
                showDemoBanner = _approvalDummyInjected;

            _window.ApplyStep(step, _currentIndex, _steps.Count, targetRect, ringRect, showDemoBanner);
        }

        // ── Dummy data helpers ───────────────────────────────────────────────

        private bool _signedDummyInjected;

        private void InjectDummyDataForSection(TourSection section)
        {
            _newRequestDummyInjected = false;
            _historyDummyInjected    = false;
            _authDummyInjected       = false;
            _signedDummyInjected     = false;
            _approvalDummyInjected   = false;

            switch (section)
            {
                case TourSection.NewRequest:
                    if (_newRequestView != null)
                        _newRequestDummyInjected = _newRequestView.ViewModel.AddTourDummyItems();
                    break;

                case TourSection.RequestHistory:
                    if (_requestHistoryView != null && !_requestHistoryView.ViewModel.HasRows)
                    {
                        _requestHistoryView.ViewModel.AddTourDummyRows();
                        _historyDummyInjected = true;
                    }
                    break;

                case TourSection.AuthorizationHistory:
                    // Ensure the My History sub-tab is active so phantom zones are visible.
                    _authHistoryView?.ViewModel.SwitchToMyHistoryCommand.Execute(null);
                    if (_authHistoryView != null && !_authHistoryView.ViewModel.HasRows)
                    {
                        _authHistoryView.ViewModel.AddTourDummyRows();
                        _authDummyInjected = true;
                    }
                    break;

                case TourSection.SignedApprovals:
                    // Ensure the Signed sub-tab is active so its phantom zones are visible.
                    _authHistoryView?.ViewModel.SwitchToSignedCommand.Execute(null);
                    if (_authHistoryView != null && !_authHistoryView.ViewModel.HasSignedRows)
                    {
                        _authHistoryView.ViewModel.AddTourDummySignedRows();
                        _signedDummyInjected = true;
                    }
                    break;

                case TourSection.ApprovalQueue:
                    if (_authorizeRequestView?.ViewModel != null)
                    {
                        _authorizeRequestView.ViewModel.AddTourDummyItems();
                        _approvalDummyInjected = true;
                    }
                    break;
            }
        }

        private void CleanupDummyData()
        {
            if (_newRequestDummyInjected)
            {
                _newRequestView?.ViewModel.RemoveTourDummyItems();
                _newRequestDummyInjected = false;
            }
            if (_historyDummyInjected)
            {
                _requestHistoryView?.ViewModel.RemoveTourDummyRows();
                _historyDummyInjected = false;
            }
            if (_authDummyInjected)
            {
                _authHistoryView?.ViewModel.RemoveTourDummyRows();
                _authDummyInjected = false;
            }
            if (_signedDummyInjected)
            {
                _authHistoryView?.ViewModel.RemoveTourDummySignedRows();
                _signedDummyInjected = false;
            }
            if (_approvalDummyInjected)
            {
                _authorizeRequestView?.ViewModel.RemoveTourDummyItems();
                _approvalDummyInjected = false;
            }
        }

        // ── Coordinate helpers ───────────────────────────────────────────────

        private Rect GetWpfElementRect(FrameworkElement element, FrameworkElement scrollAnchor = null)
        {
            // Walk the visual tree to find qualifying ScrollViewer ancestors.
            // ScrollViewers with ViewportHeight/Width < 150 are embedded template scrollers
            // (TextBox PART_ContentHost, DataGrid internals, ComboBox popup) — skip them.
            var allSvs = new System.Collections.Generic.List<System.Windows.Controls.ScrollViewer>();
            DependencyObject node = VisualTreeHelper.GetParent(element);
            while (node != null)
            {
                if (node is System.Windows.Controls.ScrollViewer sv)
                {
                    if (!sv.IsMeasureValid) sv.UpdateLayout();
                    allSvs.Add(sv);
                }
                node = VisualTreeHelper.GetParent(node);
            }

            System.Windows.Controls.ScrollViewer scrollViewer = null;
            foreach (var candidate in allSvs)
            {
                if (candidate.ViewportHeight >= 150 && candidate.ViewportWidth >= 150)
                {
                    scrollViewer = candidate;
                    break;
                }
            }

            // scrollAnchor: a small child element (e.g. section header) used as the scroll
            // position reference when the spotlight target is too tall to fit above the popover.
            // The viewport scrolls to show the anchor; the spotlight still covers the full element.
            if (scrollViewer != null)
                ScrollToShowElement(scrollViewer, scrollAnchor ?? element,
                                    scrollAnchor != null ? element : null);

            // Flush the WPF render pipeline before reading screen coordinates.
            // ScrollToVerticalOffset marks layout dirty; UpdateLayout() processes layout
            // transforms but does NOT guarantee the render pass has completed.  PointToScreen
            // reports the RENDERED position — in WinForms/ElementHost interop the render can
            // lag behind the layout pass, returning pre-scroll coordinates.  Invoking an empty
            // delegate at Render priority drains all higher-priority work (Layout → Render)
            // so the scroll has fully settled before we read the position.
            element.Dispatcher.Invoke(
                System.Windows.Threading.DispatcherPriority.Render,
                new Action(() => { }));

            var topLeft     = element.PointToScreen(new Point(0, 0));
            var bottomRight = element.PointToScreen(new Point(element.ActualWidth, element.ActualHeight));
            return _window.PhysicalToLogical(topLeft.X, topLeft.Y,
                                              bottomRight.X - topLeft.X,
                                              bottomRight.Y - topLeft.Y);
        }

        /// <summary>
        /// Scrolls <paramref name="sv"/> so the target element's top sits at
        /// <c>topMargin</c> logical pixels from the viewport top, leaving headroom
        /// for the popover card above the spotlight.
        ///
        /// Uses a two-pass approach:
        ///   Pass 1 — compute contentY (absolute Y in scrollable content, stable across
        ///            scroll positions), derive idealOffset, apply it.
        ///   Pass 2 — re-measure element position; if the offset did not settle correctly
        ///            (can happen with deferred layout in WinForms/WPF interop), apply a
        ///            correction based on the observed error.
        /// </summary>
        private static void ScrollToShowElement(
            System.Windows.Controls.ScrollViewer sv, FrameworkElement element,
            FrameworkElement fullElement = null)
        {
            // ── Pass 1 ───────────────────────────────────────────────────────
            sv.UpdateLayout();

            double viewportH   = sv.ViewportHeight;
            double scrollableH = sv.ScrollableHeight;
            double currentOff  = sv.VerticalOffset;
            double elementH    = element.ActualHeight;

            if (viewportH <= 0) return;

            // vpY:     element top in the ScrollViewer's viewport coordinate space.
            // contentY: element top in the full scrollable content (invariant across scrolls).
            double vpY      = element.TransformToAncestor(sv).Transform(new Point(0, 0)).Y;
            double contentY = vpY + currentOff;

            // topMargin:    headroom reserved above the element for the popover card.
            // bottomMargin: clearance below.
            const double topMargin    = 300.0;
            const double bottomMargin = 24.0;

            double idealOffset = contentY - topMargin;
            double newOffset   = Math.Max(0, Math.Min(idealOffset, scrollableH));

            // When the scroll anchor is a small child (e.g. section header) but the spotlight
            // covers a larger parent card, also ensure the card's bottom edge is visible.
            // If the card overflows the viewport after anchor-based positioning, scroll down
            // by the overshoot amount so the card bottom lands at (viewportH - bottomMargin).
            bool bottomFitApplied = false;
            if (fullElement != null && fullElement.IsLoaded)
            {
                double fullVpY      = fullElement.TransformToAncestor(sv).Transform(new Point(0, 0)).Y;
                double fullContentY = fullVpY + currentOff;
                double fullElementH = fullElement.ActualHeight;
                double bottomFitOff = fullContentY + fullElementH - (viewportH - bottomMargin);
                if (bottomFitOff > newOffset)
                {
                    newOffset        = Math.Max(0, Math.Min(bottomFitOff, scrollableH));
                    bottomFitApplied = true;
                }
            }

            sv.ScrollToVerticalOffset(newOffset);
            sv.UpdateLayout();

            // ── Pass 2: verify and auto-correct ─────────────────────────────
            // Skipped when bottomFitApplied: the anchor intentionally lands above topMargin
            // (scroll was pushed further to keep the full card's bottom in view); Pass 2 would
            // otherwise see the anchor as "too high" and pull the offset back.
            double actualVpY = element.TransformToAncestor(sv).Transform(new Point(0, 0)).Y;
            double error     = actualVpY - topMargin;

            if (Math.Abs(error) > 2.0 && !bottomFitApplied)
            {
                // Offset did not settle as expected (deferred layout / WinForms interop lag).
                double correctedOffset = Math.Max(0, Math.Min(sv.VerticalOffset + error, scrollableH));
                sv.ScrollToVerticalOffset(correctedOffset);
                sv.UpdateLayout();
            }
        }

        private Rect GetWinFormsControlRect(Control ctrl)
        {
            // Access Handle to force creation if the control hasn't been painted yet.
            _ = ctrl.Handle;
            System.Drawing.Rectangle bounds;
            if (ctrl is Form)
                // For a top-level Form, Bounds already is in screen coordinates and includes title bar.
                bounds = ctrl.Bounds;
            else
                bounds = ctrl.RectangleToScreen(ctrl.ClientRectangle);
            return _window.PhysicalToLogical(bounds.Left, bounds.Top, bounds.Width, bounds.Height);
        }

        // ────────────────────────────────────────────────────────────────────
        // First-launch persistence
        // ────────────────────────────────────────────────────────────────────

        private static string FlagFilePath()
        {
            var dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Yakult");
            Directory.CreateDirectory(dir);
            return Path.Combine(dir, FlagFileName);
        }

        public static bool HasCompletedTour()
        {
            try { return File.Exists(FlagFilePath()); }
            catch { return true; } // if we can't read the flag, don't spam tours
        }

        private static void MarkTourCompleted()
        {
            try { File.WriteAllText(FlagFilePath(), DateTime.UtcNow.ToString("O")); }
            catch { /* non-critical */ }
        }

        // ────────────────────────────────────────────────────────────────────
        // Step definitions
        // ────────────────────────────────────────────────────────────────────

        private List<WpfPortalTourStep> BuildSteps(TourSection section)
        {
            switch (section)
            {
                case TourSection.AssistedRequest:      return BuildAssistedRequestSteps();
                case TourSection.RequestHistory:       return BuildRequestHistorySteps();
                case TourSection.AuthorizationHistory: return BuildAuthHistorySteps();
                case TourSection.SignedApprovals:      return BuildSignedApprovalsSteps();
                case TourSection.ApprovalQueue:        return BuildApprovalQueueSteps();
                case TourSection.ApproverHomepage:     return BuildApproverHomepageSteps();
                default:                               return BuildNewRequestSteps();
            }
        }

        // ── New Request Tour (13 steps) ──────────────────────────────────────

        private List<WpfPortalTourStep> BuildNewRequestSteps()
        {
            return new List<WpfPortalTourStep>
            {
                // 1 · Welcome
                new WpfPortalTourStep
                {
                    Title       = "Welcome to the Request Portal",
                    Description = "This short tour walks you through everything you need to submit and track cartridge requests.\n\nUse the buttons below to move through each step, or press Escape / Skip Tour at any time.",
                    Side        = "over"
                },

                // 2 · Notification bell
                new WpfPortalTourStep
                {
                    Title         = "Notification Bell",
                    Description   = "You will receive real-time alerts here whenever your request is approved, rejected, or fulfilled. A red badge shows how many unread notifications you have.\n\nClick the bell to open your notification panel.",
                    TargetWinForms = () => _notifBellHost,
                    Side          = "bottom",
                    Align         = "end"
                },

                // 3 · Tab bar
                new WpfPortalTourStep
                {
                    Title         = "Page Navigation",
                    Description   = "These tabs are your main navigation:\n• New Request — submit a new cartridge request\n• Request History — track your submission history\n• Authorization History — view supervisor approval status\n\nYou can switch between them at any time.",
                    TargetWinForms = () => _tabControlWinForms,
                    Side          = "bottom",
                    Align         = "start",
                    SpotlightPadding = 0
                },

                // 4 · Request Details card
                new WpfPortalTourStep
                {
                    Title       = "Request Details",
                    Description = "This section identifies who is making the request.\n\nFor standard accounts, your name, position, company, branch, and department are filled in automatically.\n\nFor shared department accounts, a Select Employee dropdown will appear here — the employee must select their name before submitting.",
                    TargetWpf   = () => _newRequestView?.TourTarget_RequestDetailsCard,
                    Side        = "bottom",
                    Align       = "start"
                },

                // 5 · Requester identity panel (or dept selector in dept-account mode)
                new WpfPortalTourStep
                {
                    Title       = "Your Identity",
                    Description = "For a standard account this panel is read-only — it shows your name, position, and organisation details pulled from your account profile.\n\nFor a shared department account, a dropdown appears here instead. Select your name from the list before adding cartridges to the request.",
                    TargetWpf   = () => _newRequestView?.TourTarget_RequesterIdentityPanel,
                    Side        = "bottom",
                    Align       = "start"
                },

                // 6 · Cartridge Selection card
                new WpfPortalTourStep
                {
                    Title           = "Cartridge Selection",
                    Description     = "Use this section to choose the cartridge model you need, set the quantity, and record how many empty cartridges you are returning.\n\nYou can add multiple models to one request — each model is listed separately.",
                    TargetWpf       = () => _newRequestView?.TourTarget_CartridgeSelectionCard,
                    ScrollAnchorWpf = () => _newRequestView?.TourTarget_CartridgeSelectionHeader,
                    Side            = "top",
                    Align           = "start"
                },

                // 7 · Cartridge Model dropdown
                new WpfPortalTourStep
                {
                    Title       = "Cartridge Model",
                    Description = "Select or type the cartridge model number here — for example HP CF280A or Canon 051H.\n\nThe dropdown shows all models the IT department currently stocks. If your model is not listed, you may still type it in directly.",
                    TargetWpf   = () => _newRequestView?.TourTarget_CartridgeModelCombo,
                    Side        = "top",
                    Align       = "start"
                },

                // 8 · Qty Requested
                new WpfPortalTourStep
                {
                    Title       = "Quantity Requested",
                    Description = "Enter how many cartridges you need for this model using the up / down arrows.\n\nThe maximum is 3 per model per submission. If no stock is currently available, you may still submit — IT will fulfil the request once stock arrives.",
                    TargetWpf   = () => _newRequestView?.TourTarget_QtySpinner,
                    Side        = "top",
                    Align       = "start"
                },

                // 9 · Returned Empty Cartridges
                new WpfPortalTourStep
                {
                    Title       = "Returned Empty Cartridges",
                    Description = "When collecting new cartridges, you are expected to return your used empties.\n\n• Good — cartridges that are intact and undamaged.\n• Damaged — cartridges that are cracked, leaking, or broken.\n\nThe combined total of Good + Damaged cannot exceed your requested quantity.",
                    TargetWpf   = () => _newRequestView?.TourTarget_ReturnedEmptyRow,
                    Side        = "top",
                    Align       = "start"
                },

                // 10 · Add to Request button
                new WpfPortalTourStep
                {
                    Title       = "Add to Request",
                    Description = "Once you have filled in the model, quantity, and returned empties, click + Add to Request to add this cartridge model to your submission.\n\nYou can then select another model and add it too — each model appears as a separate row in the list below.",
                    TargetWpf   = () => _newRequestView?.TourTarget_AddToRequest,
                    Side        = "top",
                    Align       = "start"
                },

                // 11 · Request Items grid
                new WpfPortalTourStep
                {
                    Title           = "Request Items",
                    Description     = "All the cartridge models you have added appear here. The highlighted list shows two demo entries (HP CF280A and HP CE285A) so you can see how your request will look.\n\nEach row shows the model name, quantity requested, and how many empties you are returning (Good and Damaged).\n\nYou can remove an item using the Remove button in its row.",
                    TargetWpf       = () => _newRequestView?.TourTarget_RequestItemsCard,
                    ScrollAnchorWpf = () => _newRequestView?.TourTarget_RequestItemsHeader,
                    Side            = "top",
                    Align           = "start"
                },

                // 12 · Fulfillment & Delivery card
                new WpfPortalTourStep
                {
                    Title           = "Fulfillment & Delivery",
                    Description     = "Choose how you will receive your cartridges:\n\n• **PICKUP** — you or a representative collects them at the IT office. Use the filters to narrow the list, then search by name in the To Be Received By field:\n    ◦ **Company**\n    ◦ **Branch**\n    ◦ **Department**\n    Click × to clear all filters.\n• **DELIVERY** — IT delivers directly to your branch.\n\nUse Additional Remarks for any special instructions.",
                    TargetWpf       = () => _newRequestView?.TourTarget_FulfillmentCard,
                    ScrollAnchorWpf = () => _newRequestView?.TourTarget_FulfillmentHeader,
                    Side            = "top",
                    Align           = "start",
                    PopoverWidth    = 580
                },

                // 13 · Action bar (Clear + Submit)
                new WpfPortalTourStep
                {
                    Title       = "Submit Your Request",
                    Description = "When everything is filled in:\n\n• Click Submit Request to send your request for supervisor authorization. You will be able to track its progress on the Request History and Authorization History tabs.\n• Click Clear to reset the form and start over.",
                    TargetWpf   = () => _newRequestView?.TourTarget_ActionBar,
                    Side        = "top",
                    Align       = "end"
                },

                // 14 · Finish
                new WpfPortalTourStep
                {
                    Title       = "You are Ready to Submit!",
                    Description = "That covers the New Request form.\n\nTo see your submitted requests and their approval status, click the Request History and Authorization History tabs.\n\nYou can replay this guide at any time by clicking the  ❓  button in the top-right corner of the portal.",
                    Side        = "over"
                }
            };
        }

        // ── Request History Tour (9 steps) ───────────────────────────────────

        private List<WpfPortalTourStep> BuildRequestHistorySteps()
        {
            return new List<WpfPortalTourStep>
            {
                // 1 · Welcome
                new WpfPortalTourStep
                {
                    Title       = "Request History — Your Submissions",
                    Description = "This tab shows every cartridge request you have submitted and its current approval status. Let us walk through what each part means.",
                    Side        = "over"
                },

                // 2 · Header bar
                new WpfPortalTourStep
                {
                    Title       = "Request History Header",
                    Description = "The blue badge next to the title shows the total number of request batches you have submitted.\n\nClick Refresh to reload the list and see the latest status on all your requests — approvals happen in real time.",
                    TargetWpf   = () => _requestHistoryView?.TourTarget_HistoryHeaderBar,
                    Side        = "bottom",
                    Align       = "start"
                },

                // 3 · History grid — spotlight the two demo rows
                new WpfPortalTourStep
                {
                    Title       = "Your Request History Table",
                    Description = "Your submissions appear here, most recent first. Two demo entries have been loaded into the table for this walkthrough so you can see what it looks like — they will be removed when the guide closes.\n\nEach row is one batch submission. A Set Code groups all the cartridge models you requested together into a single submission.",
                    TargetWpf   = () => _requestHistoryView?.TourTarget_HistoryDemoRows,
                    Side        = "bottom",
                    Align       = "start"
                },

                // 4 · Column explanation — spotlight the header row
                new WpfPortalTourStep
                {
                    Title       = "Understanding the Columns",
                    Description = "• Set Code — unique batch ID for this submission.\n• Date — when the request was submitted.\n• Cartridge Model(s) — what was requested.\n• Qty — total units across all models.\n• Empties — returned cartridges (Good / Damaged).\n• Fulfillment — PICKUP or DELIVERY.\n• Branch — your branch at time of submission.\n• Status — current approval state (see next step).",
                    TargetWpf   = () => _requestHistoryView?.TourTarget_HistoryColumnHeaders,
                    Side        = "bottom",
                    Align       = "start"
                },

                // 5 · Status colours — spotlight the Status column (far right)
                new WpfPortalTourStep
                {
                    Title       = "Request Status",
                    Description = "Track where each request stands using the Status column:",
                    ColoredBullets = new System.Collections.Generic.List<TourBulletItem>
                    {
                        TourBulletItem.Make("#5A6A7E", "Awaiting Authorization — submitted; waiting for supervisor to authorize."),
                        TourBulletItem.Make("#3A8EF6", "Under Review — supervisor is reviewing the request."),
                        TourBulletItem.Make("#1E9E5E", "Fulfilled — request completed and cartridges are ready."),
                        TourBulletItem.Make("#FF9900", "Partially Fulfilled — some items completed, others not."),
                        TourBulletItem.Make("#E03C31", "Unfulfilled — request was declined or cancelled."),
                    },
                    TargetWpf   = () => _requestHistoryView?.TourTarget_HistoryStatusColumn,
                    Side        = "left",
                    Align       = "start"
                },

                // 6 · Double-click — spotlight the demo rows (dialog NOT open yet)
                new WpfPortalTourStep
                {
                    Title       = "View Full Request Details",
                    Description = "Double-click any row to open a complete breakdown of that request.\n\nClick Next to see what the Request Details dialog looks like.",
                    TargetWpf   = () => _requestHistoryView?.TourTarget_HistoryDemoRows,
                    Side        = "bottom",
                    Align       = "start"
                },

                // 7 · Open the dialog and spotlight the entire form so it is visible
                new WpfPortalTourStep
                {
                    Title          = "Request Details Dialog",
                    Description    = "This is the Request Details dialog. It shows a complete breakdown of a submission — the set code, status, cartridge models, requester info, and delivery details.\n\nClick Next to walk through each section.",
                    TargetWinForms = () => _activeTourDialog,
                    Side           = "right",
                    Align          = "start",
                    OnEnter        = () =>
                    {
                        var items = new System.Collections.Generic.List<PortalRequestStatusDto>
                        {
                            new PortalRequestStatusDto
                            {
                                SetCode                     = "REQ-DEMO-001",
                                CartridgeName               = "HP CF280A",
                                Quantity                    = 2,
                                GoodEmptyQty                = 1,
                                DamagedEmptyQty             = 0,
                                Status                      = "Awaiting Authorization",
                                DestinationEmployeeName     = "Sample Employee",
                                DestinationEmployeePosition = "Staff",
                                DestinationCompany          = "Yakult Philippines",
                                DestinationBranch           = "Manila Liaison Office",
                                DestinationDepartment       = "Sales",
                                FulfillmentMethod           = "PICKUP",
                                ReceivedByName              = "Sample Employee"
                            },
                            new PortalRequestStatusDto
                            {
                                SetCode                     = "REQ-DEMO-001",
                                CartridgeName               = "HP CE285A",
                                Quantity                    = 1,
                                GoodEmptyQty                = 1,
                                DamagedEmptyQty             = 0,
                                Status                      = "Awaiting Authorization",
                                DestinationEmployeeName     = "Sample Employee",
                                DestinationEmployeePosition = "Staff",
                                DestinationCompany          = "Yakult Philippines",
                                DestinationBranch           = "Manila Liaison Office",
                                DestinationDepartment       = "Sales",
                                FulfillmentMethod           = "PICKUP",
                                ReceivedByName              = "Sample Employee"
                            }
                        };
                        OpenTourDialog(new RequestDetailDialog(items));
                    },
                    OnLeave        = null
                },

                // 8 · Status Banner — spotlight the banner panel inside the dialog
                new WpfPortalTourStep
                {
                    Title           = "Status Banner",
                    Description     = "Look at the banner at the top of the dialog.\n\nIt shows the Set Code that groups all your requested cartridge models into one submission batch. The colored stripe on the left reflects the overall status at a glance.",
                    TargetWinForms  = () => (_activeTourDialog as RequestDetailDialog)?.TourTarget_StatusBanner,
                    Side            = "right",
                    Align           = "start",
                    OnEnter         = () => (_activeTourDialog as RequestDetailDialog)?.ScrollIntoView(
                                          (_activeTourDialog as RequestDetailDialog).TourTarget_StatusBanner)
                },

                // 9 · Request Details section
                new WpfPortalTourStep
                {
                    Title           = "Request Details Section",
                    Description     = "Below the banner is the REQUEST DETAILS section.\n\nIt lists every cartridge model you requested with its quantity and how many empty cartridges were returned.\n\n• Returned (Good) — empties in reusable condition.\n• Returned (Damaged) — empties that could not be refilled.\n\nThe Status field shows the current approval state.",
                    TargetWinForms  = () => (_activeTourDialog as RequestDetailDialog)?.TourTarget_RequestDetails,
                    Side            = "right",
                    Align           = "start",
                    OnEnter         = () => (_activeTourDialog as RequestDetailDialog)?.ScrollIntoView(
                                          (_activeTourDialog as RequestDetailDialog).TourTarget_RequestDetails)
                },

                // 10 · Requester Information
                new WpfPortalTourStep
                {
                    Title           = "Requester Information",
                    Description     = "The REQUESTER INFORMATION section identifies who submitted the request — the employee's name and position as recorded at the time of submission.",
                    TargetWinForms  = () => (_activeTourDialog as RequestDetailDialog)?.TourTarget_RequesterInfo,
                    Side            = "right",
                    Align           = "start",
                    OnEnter         = () => (_activeTourDialog as RequestDetailDialog)?.ScrollIntoView(
                                          (_activeTourDialog as RequestDetailDialog).TourTarget_RequesterInfo)
                },

                // 11 · Destination & Fulfillment
                new WpfPortalTourStep
                {
                    Title           = "Destination & Fulfillment",
                    Description     = "The DESTINATION FULFILLMENT section shows where the cartridges should go and how they will be delivered:\n\n• Company, Branch, Department — the destination.\n• Fulfillment Method — PICKUP (collected at IT office) or DELIVERY (sent to your branch).\n• Received By — who will collect the cartridges on PICKUP.",
                    TargetWinForms  = () => (_activeTourDialog as RequestDetailDialog)?.TourTarget_Destination,
                    Side            = "right",
                    Align           = "start",
                    OnEnter         = () => (_activeTourDialog as RequestDetailDialog)?.ScrollIntoView(
                                          (_activeTourDialog as RequestDetailDialog).TourTarget_Destination)
                },

                // 12 · Close button — spotlight the close button, OnLeave closes dialog
                new WpfPortalTourStep
                {
                    Title           = "Close the Dialog",
                    Description     = "When you are done reviewing, click the Close button at the bottom of the dialog to dismiss it and return to your request list.",
                    TargetWinForms  = () => (_activeTourDialog as RequestDetailDialog)?.TourTarget_CloseButton,
                    Side            = "right",
                    Align           = "start",
                    OnLeave         = () => CloseTourDialog()
                },

                // 13 · Pagination
                new WpfPortalTourStep
                {
                    Title       = "Pagination",
                    Description = "If you have many requests, they are split across pages.\n\nUse the left and right arrows at the bottom of the list to move between pages. The page indicator shows your current position.",
                    TargetWpf   = () => _requestHistoryView?.TourTarget_PaginationBar,
                    Side        = "top",
                    Align       = "center",
                    SpotlightPadding = 4
                },

                // 14 · Finish
                new WpfPortalTourStep
                {
                    Title       = "You are All Set!",
                    Description = "You now know how to track your cartridge requests.\n\nFor supervisor authorization details, check the Authorization History tab.\n\nClick the  ❓  button at any time to replay this guide.",
                    Side        = "over"
                }
            };
        }

        // ── Authorization History Tour (7 steps) ─────────────────────────────

        private List<WpfPortalTourStep> BuildAuthHistorySteps()
        {
            return new List<WpfPortalTourStep>
            {
                // 1 · Welcome
                new WpfPortalTourStep
                {
                    Title       = "Authorization History",
                    Description = "This tab tracks the supervisor authorization status of every cartridge request you have submitted.\n\nAuthorization is a required step before IT can fulfil a request. Let us walk through what each part means.",
                    Side        = "over"
                },

                // 2 · Header bar
                new WpfPortalTourStep
                {
                    Title       = "Authorization History Header",
                    Description = "The badge shows the total number of authorization records linked to your requests.\n\nClick Refresh to reload and see the latest approvals in real time.",
                    TargetWpf   = () => _authHistoryView?.TourTarget_AuthHeaderBar,
                    Side        = "bottom",
                    Align       = "start"
                },

                // 3 · Auth grid — spotlight the two demo rows
                new WpfPortalTourStep
                {
                    Title       = "Your Authorization Records",
                    Description = "Each row is one authorization linked to a cartridge request submission. Two demo entries have been loaded into the table for this walkthrough so you can see what it looks like — they will be removed when the guide closes.\n\nWhen you submit a new request, the system creates an authorization record that your supervisor must sign off before IT processes it.",
                    TargetWpf   = () => _authHistoryView?.TourTarget_AuthDemoRows,
                    Side        = "bottom",
                    Align       = "start"
                },

                // 4 · Column explanation — spotlight the header row
                new WpfPortalTourStep
                {
                    Title       = "Understanding the Columns",
                    Description = "• Auth ID — unique number for this authorization.\n• Status — current approval state.\n• Department — your department at time of request.\n• Cartridge Models — what was requested.\n• Signed By — which supervisor reviewed it.\n• Date Signed — when it was reviewed.\n• Requested On — when you originally submitted.",
                    TargetWpf   = () => _authHistoryView?.TourTarget_AuthColumnHeaders,
                    Side        = "bottom",
                    Align       = "start"
                },

                // 5 · Status badges — spotlight the Status column (2nd from left)
                new WpfPortalTourStep
                {
                    Title       = "Authorization Status",
                    Description = "Each authorization record has one of these statuses:",
                    ColoredBullets = new System.Collections.Generic.List<TourBulletItem>
                    {
                        TourBulletItem.Make("#E08A00", "Pending — awaiting your supervisor's review and signature."),
                        TourBulletItem.Make("#1E9E5E", "Approved — authorized by supervisor; IT can now process your request."),
                        TourBulletItem.Make("#E03C31", "Rejected — declined by your supervisor."),
                    },
                    TargetWpf   = () => _authHistoryView?.TourTarget_AuthStatusColumn,
                    Side        = "right",
                    Align       = "start"
                },

                // 6 · Row click — spotlight the demo rows (dialog NOT open yet)
                new WpfPortalTourStep
                {
                    Title       = "View Authorization Details",
                    Description = "Click any row to open a detailed view of that authorization record.\n\nClick Next to see what the Authorization Details dialog looks like.",
                    TargetWpf   = () => _authHistoryView?.TourTarget_AuthDemoRows,
                    Side        = "bottom",
                    Align       = "start"
                },

                // 7 · Open the dialog and spotlight the entire form so it is visible
                new WpfPortalTourStep
                {
                    Title          = "Authorization Details Dialog",
                    Description    = "This is the Authorization Details dialog. It shows the full approval record — who signed off, when, the cartridge models requested, and your employee details.\n\nClick Next to walk through each section.",
                    TargetWinForms = () => _activeTourDialog,
                    Side           = "right",
                    Align          = "start",
                    OnEnter        = () =>
                    {
                        var model = new CartridgeAuthorizationModel
                        {
                            AuthorizationId   = 1001,
                            Status            = "Approved",
                            SignedByName      = "Demo Manager",
                            SignedDate        = DateTime.Today.AddDays(-1),
                            CreatedDate       = DateTime.Today.AddDays(-2),
                            FulfillmentMethod = "Pickup",
                            ReceivedByName    = "Sample Employee",
                            RequestedModels   = "[{\"model\":\"HP CF280A\",\"qty\":2,\"good\":1,\"damaged\":0},{\"model\":\"HP CE285A\",\"qty\":1,\"good\":1,\"damaged\":0}]",
                            EmployeeName      = "Sample Employee",
                            EmployeePosition  = "Staff",
                            DepartmentName    = "Sales",
                            BranchName        = "Manila Liaison Office",
                            CompanyName       = "Yakult Philippines"
                        };
                        OpenTourDialog(new AuthorizationDetailDialog(model));
                    },
                    OnLeave        = null
                },

                // 8 · Banner — spotlight the banner panel inside the dialog
                new WpfPortalTourStep
                {
                    Title          = "Authorization Details — Banner",
                    Description    = "Look at the banner at the top of the dialog.\n\nIt displays the Authorization ID — the unique reference number for this supervisor approval record.",
                    TargetWinForms = () => (_activeTourDialog as AuthorizationDetailDialog)?.TourTarget_StatusBanner,
                    Side           = "right",
                    Align          = "start",
                    OnEnter        = () => (_activeTourDialog as AuthorizationDetailDialog)?.ScrollIntoView(
                                         (_activeTourDialog as AuthorizationDetailDialog).TourTarget_StatusBanner)
                },

                // 9 · Authorization Details section
                new WpfPortalTourStep
                {
                    Title          = "Authorization Details Section",
                    Description    = "The AUTHORIZATION DETAILS section shows the full approval record:\n\n• Status — Pending, Approved, or Rejected.\n• Signed By — the supervisor who reviewed your request.\n• Signed Date — when the decision was made.\n• Submitted — when you originally sent the request.\n• Distribution Method — PICKUP or DELIVERY.",
                    TargetWinForms = () => (_activeTourDialog as AuthorizationDetailDialog)?.TourTarget_AuthDetails,
                    Side           = "right",
                    Align          = "start",
                    OnEnter        = () => (_activeTourDialog as AuthorizationDetailDialog)?.ScrollIntoView(
                                         (_activeTourDialog as AuthorizationDetailDialog).TourTarget_AuthDetails)
                },

                // 10 · Cartridge Models section
                new WpfPortalTourStep
                {
                    Title          = "Cartridge Models Requested",
                    Description    = "The CARTRIDGE MODELS section lists every cartridge model included in the authorization — the model name, quantity requested, and how many empty cartridges were returned (Good and Damaged).",
                    TargetWinForms = () => (_activeTourDialog as AuthorizationDetailDialog)?.TourTarget_CartridgeModels,
                    Side           = "right",
                    Align          = "start",
                    OnEnter        = () => (_activeTourDialog as AuthorizationDetailDialog)?.ScrollIntoView(
                                         (_activeTourDialog as AuthorizationDetailDialog).TourTarget_CartridgeModels)
                },

                // 11 · Employee section
                new WpfPortalTourStep
                {
                    Title          = "Employee Section",
                    Description    = "The EMPLOYEE section shows your details as recorded at the time of submission: name, position, department, branch, and company.",
                    TargetWinForms = () => (_activeTourDialog as AuthorizationDetailDialog)?.TourTarget_Employee,
                    Side           = "right",
                    Align          = "start",
                    OnEnter        = () => (_activeTourDialog as AuthorizationDetailDialog)?.ScrollIntoView(
                                         (_activeTourDialog as AuthorizationDetailDialog).TourTarget_Employee)
                },

                // 12 · Close button — spotlight the close button, OnLeave closes dialog
                new WpfPortalTourStep
                {
                    Title          = "Close the Dialog",
                    Description    = "When you are done reviewing, click the Close button at the bottom of the dialog to dismiss it and return to your authorization list.",
                    TargetWinForms = () => (_activeTourDialog as AuthorizationDetailDialog)?.TourTarget_CloseButton,
                    Side           = "right",
                    Align          = "start",
                    OnLeave        = () => CloseTourDialog()
                },

                // 12 · Finish
                new WpfPortalTourStep
                {
                    Title       = "You are All Set!",
                    Description = "You now know how to track your authorization requests.\n\nIf you need a refresher on any tab, click the  ❓  button in the top-right corner of the portal to replay the guide for that tab.",
                    Side        = "over"
                }
            };
        }

        // ── Approvals I've Signed Tour (13 steps) ────────────────────────────

        private List<WpfPortalTourStep> BuildSignedApprovalsSteps()
        {
            return new List<WpfPortalTourStep>
            {
                // 1 · Welcome
                new WpfPortalTourStep
                {
                    Title       = "Approvals I've Signed",
                    Description = "This tab shows every cartridge authorization you have signed as a supervisor.\n\nEach row represents a request you reviewed and approved on behalf of an employee. Let us walk through each part.",
                    Side        = "over"
                },

                // 2 · Header bar
                new WpfPortalTourStep
                {
                    Title       = "Authorization History Header",
                    Description = "The badge at the top shows the total number of your own authorization records.\n\nClick Refresh at any time to reload and see the latest data.",
                    TargetWpf   = () => _authHistoryView?.TourTarget_AuthHeaderBar,
                    Side        = "bottom",
                    Align       = "start"
                },

                // 3 · Signed grid — spotlight demo rows
                new WpfPortalTourStep
                {
                    Title       = "Your Signed Approvals",
                    Description = "Each row is a cartridge authorization that you signed. Two demo entries have been loaded for this walkthrough — they will be removed when the guide closes.\n\nWhen you approve a cartridge request in the Approval Queue, a record appears here.",
                    TargetWpf   = () => _authHistoryView?.TourTarget_SignedDemoRows,
                    Side        = "bottom",
                    Align       = "start"
                },

                // 4 · Column explanation
                new WpfPortalTourStep
                {
                    Title       = "Understanding the Columns",
                    Description = "• Auth ID — unique number for this authorization.\n• Status — the approval state at the time you signed.\n• Employee — the staff member who submitted the request.\n• Department — their department at time of request.\n• Cartridge Models — what was requested.\n• Date Signed — when you signed the authorization.",
                    TargetWpf   = () => _authHistoryView?.TourTarget_SignedColumnHeaders,
                    Side        = "bottom",
                    Align       = "start"
                },

                // 5 · Status column
                new WpfPortalTourStep
                {
                    Title       = "Authorization Status",
                    Description = "Records in this tab can have one of two statuses:",
                    ColoredBullets = new System.Collections.Generic.List<TourBulletItem>
                    {
                        TourBulletItem.Make("#1E9E5E", "Approved — you signed and authorized this request."),
                        TourBulletItem.Make("#E03C31", "Rejected — you reviewed and declined this request."),
                    },
                    TargetWpf   = () => _authHistoryView?.TourTarget_SignedStatusColumn,
                    Side        = "right",
                    Align       = "start"
                },

                // 6 · Row click
                new WpfPortalTourStep
                {
                    Title       = "View Authorization Details",
                    Description = "Click any row to open the full authorization record — the cartridge models requested, the employee details, and the signature record.\n\nClick Next to preview what this looks like.",
                    TargetWpf   = () => _authHistoryView?.TourTarget_SignedDemoRows,
                    Side        = "bottom",
                    Align       = "start"
                },

                // 7 · Open dialog
                new WpfPortalTourStep
                {
                    Title          = "Authorization Details Dialog",
                    Description    = "This is the Authorization Details dialog. It shows the full approval record — the cartridge models requested, the employee who submitted, and your approval signature.\n\nClick Next to walk through each section.",
                    TargetWinForms = () => _activeTourDialog,
                    Side           = "right",
                    Align          = "start",
                    OnEnter        = () =>
                    {
                        var model = new CartridgeAuthorizationModel
                        {
                            AuthorizationId   = 2001,
                            Status            = "Approved",
                            SignedByName      = "Demo Manager",
                            SignedDate        = DateTime.Today.AddDays(-3),
                            CreatedDate       = DateTime.Today.AddDays(-4),
                            FulfillmentMethod = "Pickup",
                            ReceivedByName    = "JUAN DELA CRUZ",
                            RequestedModels   = "[{\"model\":\"HP CF280A\",\"qty\":2,\"good\":2,\"damaged\":0}]",
                            EmployeeName      = "JUAN DELA CRUZ",
                            EmployeePosition  = "Sales Staff",
                            DepartmentName    = "Sales",
                            BranchName        = "Manila Liaison Office",
                            CompanyName       = "Yakult Philippines"
                        };
                        OpenTourDialog(new AuthorizationDetailDialog(model));
                    },
                    OnLeave = null
                },

                // 8 · Banner
                new WpfPortalTourStep
                {
                    Title          = "Authorization Details — Banner",
                    Description    = "The banner shows the Authorization ID — the unique reference number for this approval record.",
                    TargetWinForms = () => (_activeTourDialog as AuthorizationDetailDialog)?.TourTarget_StatusBanner,
                    Side           = "right",
                    Align          = "start",
                    OnEnter        = () => (_activeTourDialog as AuthorizationDetailDialog)?.ScrollIntoView(
                                         (_activeTourDialog as AuthorizationDetailDialog).TourTarget_StatusBanner)
                },

                // 9 · Authorization Details section
                new WpfPortalTourStep
                {
                    Title          = "Authorization Details Section",
                    Description    = "The AUTHORIZATION DETAILS section shows the full approval record:\n\n• Status — the decision you made.\n• Signed By — your name as the approving supervisor.\n• Signed Date — when you signed it.\n• Submitted — when the employee originally sent the request.\n• Distribution Method — PICKUP or DELIVERY.",
                    TargetWinForms = () => (_activeTourDialog as AuthorizationDetailDialog)?.TourTarget_AuthDetails,
                    Side           = "right",
                    Align          = "start",
                    OnEnter        = () => (_activeTourDialog as AuthorizationDetailDialog)?.ScrollIntoView(
                                         (_activeTourDialog as AuthorizationDetailDialog).TourTarget_AuthDetails)
                },

                // 10 · Cartridge Models section
                new WpfPortalTourStep
                {
                    Title          = "Cartridge Models Requested",
                    Description    = "The CARTRIDGE MODELS section lists every cartridge model included in the authorization — the model name, quantity, and how many empty cartridges were returned.",
                    TargetWinForms = () => (_activeTourDialog as AuthorizationDetailDialog)?.TourTarget_CartridgeModels,
                    Side           = "right",
                    Align          = "start",
                    OnEnter        = () => (_activeTourDialog as AuthorizationDetailDialog)?.ScrollIntoView(
                                         (_activeTourDialog as AuthorizationDetailDialog).TourTarget_CartridgeModels)
                },

                // 11 · Employee section
                new WpfPortalTourStep
                {
                    Title          = "Employee Section",
                    Description    = "The EMPLOYEE section shows the details of the staff member who submitted the request: name, position, department, branch, and company.",
                    TargetWinForms = () => (_activeTourDialog as AuthorizationDetailDialog)?.TourTarget_Employee,
                    Side           = "right",
                    Align          = "start",
                    OnEnter        = () => (_activeTourDialog as AuthorizationDetailDialog)?.ScrollIntoView(
                                         (_activeTourDialog as AuthorizationDetailDialog).TourTarget_Employee)
                },

                // 12 · Close button
                new WpfPortalTourStep
                {
                    Title          = "Close the Dialog",
                    Description    = "When you are done reviewing, click the Close button to dismiss the dialog and return to the list.",
                    TargetWinForms = () => (_activeTourDialog as AuthorizationDetailDialog)?.TourTarget_CloseButton,
                    Side           = "right",
                    Align          = "start",
                    OnLeave        = () => CloseTourDialog()
                },

                // 13 · Finish
                new WpfPortalTourStep
                {
                    Title       = "You are All Set!",
                    Description = "You now know how to review the approvals you have signed.\n\nIf you need a refresher on any tab, click the  ❓  button in the top-right corner of the portal to replay the guide for that tab.",
                    Side        = "over"
                }
            };
        }

        // ── IT Assisted Request Tour (8 steps) ───────────────────────────────

        private List<WpfPortalTourStep> BuildAssistedRequestSteps()
        {
            return new List<WpfPortalTourStep>
            {
                // 1 · Welcome
                new WpfPortalTourStep
                {
                    Title       = "IT Assisted Request",
                    Description = "This tab lets IT staff create a cartridge request on behalf of any employee. The request is filed under the selected employee's name and goes through the normal authorization process.\n\nUse the buttons below to walk through each section.",
                    Side        = "over"
                },

                // 2 · IT Assisted Request Mode banner
                new WpfPortalTourStep
                {
                    Title        = "IT Assisted Request Mode",
                    Description  = "This yellow banner is a reminder that you are acting on behalf of another employee — not for yourself. Always double-check the selected employee before submitting.",
                    TargetWpf    = () => _assistedRequestView?.TourTarget_AssistedBanner,
                    ScrollAnchorWpf = () => _assistedRequestView?.TourTarget_AssistedBanner,
                    Side         = "bottom",
                    Align        = "start"
                },

                // 3 · Employee filter dropdowns
                new WpfPortalTourStep
                {
                    Title        = "Filter by Company, Branch & Department",
                    Description  = "Use these dropdowns to narrow the employee list before searching. Select a Company, Branch, or Department to limit the results — useful when there are many employees to choose from.\n\nClick × to clear all filters at once.",
                    TargetWpf    = () => _assistedRequestView?.TourTarget_EmpFilterRow,
                    ScrollAnchorWpf = () => _assistedRequestView?.TourTarget_EmpFilterRow,
                    Side         = "bottom",
                    Align        = "start"
                },

                // 4 · Target Employee
                new WpfPortalTourStep
                {
                    Title        = "Target Employee",
                    Description  = "Search for and select the employee you are creating this request for. You can type part of their name to filter the list.\n\nOnce selected, their department and position are shown below the dropdown as confirmation.",
                    TargetWpf    = () => _assistedRequestView?.TourTarget_TargetEmployeeCard,
                    ScrollAnchorWpf = () => _assistedRequestView?.TourTarget_TargetEmployeeCard,
                    Side         = "bottom",
                    Align        = "start"
                },

                // 5 · Cartridge Selection
                new WpfPortalTourStep
                {
                    Title        = "Cartridge Selection",
                    Description  = "Choose the cartridge model and set the quantity requested.\n\nIf the employee is returning empty cartridges, enter the count under Returned Empty Cartridges — split between Good (intact) and Damaged. Then click + Add to Request to add the item to the list below.",
                    TargetWpf    = () => _assistedRequestView?.TourTarget_CartridgeSelection,
                    ScrollAnchorWpf = () => _assistedRequestView?.TourTarget_CartridgeSelection,
                    Side         = "bottom",
                    Align        = "start"
                },

                // 5 · Request Items list
                new WpfPortalTourStep
                {
                    Title        = "Request Items",
                    Description  = "All cartridge models added for this request appear here. You can add multiple different models in a single request.\n\nTo remove a line, click the Remove button on that row.",
                    TargetWpf    = () => _assistedRequestView?.TourTarget_RequestItemsCard,
                    ScrollAnchorWpf = () => _assistedRequestView?.TourTarget_RequestItemsCard,
                    Side         = "top",
                    Align        = "start"
                },

                // 6 · Destination & Fulfillment
                new WpfPortalTourStep
                {
                    Title        = "Destination & Fulfillment",
                    Description  = "Set the Company, Branch, and Department where the cartridges should go.\n\nFor Fulfillment Method, choose PICKUP if the employee will collect the cartridge in person, or DELIVERY if it will be sent to them. If PICKUP is selected, you must also specify who will receive it.",
                    TargetWpf    = () => _assistedRequestView?.TourTarget_DestinationCard,
                    ScrollAnchorWpf = () => _assistedRequestView?.TourTarget_DestinationCard,
                    Side         = "top",
                    Align        = "start"
                },

                // 7 · Received By filter dropdowns
                new WpfPortalTourStep
                {
                    Title        = "Filter by Company, Branch & Department",
                    Description  = "Use these dropdowns to narrow down the list of people who can receive the cartridges. Filter by Company, Branch, or Department to quickly find the right person.\n\nClick × to clear all filters at once.",
                    TargetWpf    = () => _assistedRequestView?.TourTarget_RbFilterRow,
                    ScrollAnchorWpf = () => _assistedRequestView?.TourTarget_RbFilterRow,
                    Side         = "top",
                    Align        = "start"
                },

                // 9 · Manual Authorization toggle
                new WpfPortalTourStep
                {
                    Title        = "Manual Authorization",
                    Description  = "Use this toggle to decide how authorization is handled for this request.\n\n• Toggle OFF (default) — the request is submitted as Pending Approval. The approver reviews and signs it digitally through the portal at their convenience.\n\n• Toggle ON — use this when the approver cannot access the portal right now (no internet, broken PC, network outage, etc.). Select the approver, choose Approved or Rejected to record their verbal or offline decision, and add remarks if required.\n\nYou — not the system — decide which flow is appropriate based on the current situation.",
                    TargetWpf    = () => _assistedRequestView?.TourTarget_ManualAuthCard,
                    ScrollAnchorWpf = () => _assistedRequestView?.TourTarget_ManualAuthCard,
                    Side         = "top",
                    Align        = "start",
                    OnEnter      = () => { if (_assistedRequestView?.ViewModel != null) _assistedRequestView.ViewModel.IsManualAuthEnabled = true; }
                },

                // 10 · Authorized By
                new WpfPortalTourStep
                {
                    Title        = "Authorized By",
                    Description  = "Select the supervisor or manager who gave the authorization for this request.\n\nOnly approvers linked to the target employee's department appear in this list.",
                    TargetWpf    = () => _assistedRequestView?.TourTarget_AuthorizedByRow,
                    ScrollAnchorWpf = () => _assistedRequestView?.TourTarget_AuthorizedByRow,
                    Side         = "top",
                    Align        = "start"
                },

                // 11 · Decision
                new WpfPortalTourStep
                {
                    Title        = "Decision",
                    Description  = "Choose the outcome of the authorization:\n\n• Approved — the approver confirmed the request.\n• Rejected — the approver declined it.",
                    TargetWpf    = () => _assistedRequestView?.TourTarget_DecisionRow,
                    ScrollAnchorWpf = () => _assistedRequestView?.TourTarget_DecisionRow,
                    Side         = "top",
                    Align        = "start"
                },

                // 12 · Auth Remarks
                new WpfPortalTourStep
                {
                    Title        = "Auth Remarks",
                    Description  = "Add any relevant notes about the authorization — for example the reason for rejection or additional context.\n\nRemarks are optional when Approved but required when Rejected.",
                    TargetWpf    = () => _assistedRequestView?.TourTarget_AuthRemarksRow,
                    ScrollAnchorWpf = () => _assistedRequestView?.TourTarget_AuthRemarksRow,
                    Side         = "top",
                    Align        = "start",
                    OnLeave      = () => { if (_assistedRequestView?.ViewModel != null) _assistedRequestView.ViewModel.IsManualAuthEnabled = false; }
                },

                // 13 · Action buttons

                new WpfPortalTourStep
                {
                    Title        = "Submit or Clear",
                    Description  = "When the form is complete, click Submit Request to file the request under the selected employee's name.\n\nIf you need to start over, click Clear to reset all fields without submitting.",
                    TargetWpf    = () => _assistedRequestView?.TourTarget_ActionBar,
                    ScrollAnchorWpf = () => _assistedRequestView?.TourTarget_ActionBar,
                    Side         = "top",
                    Align        = "end"
                },

                // 15 · Finish
                new WpfPortalTourStep
                {
                    Title       = "That's It — You're All Set!",
                    Description = "You now know how to create an IT Assisted Request on behalf of an employee.\n\nClick the  ❓  button at any time to reopen this guide.",
                    Side        = "over"
                }
            };
        }

        // ── Approver Homepage Tour (6 steps) ─────────────────────────────────

        private List<WpfPortalTourStep> BuildApproverHomepageSteps()
        {
            return new List<WpfPortalTourStep>
            {
                // 1 · Welcome
                new WpfPortalTourStep
                {
                    Title       = "Welcome to the Approver Portal",
                    Description = "This tour walks you through the Approver homepage. You will learn how to navigate, access your modules, and get help at any time.\n\nUse the buttons below to move through each step, or press Skip Tour at any time.",
                    Side        = "over"
                },

                // 2 · Notification bell
                new WpfPortalTourStep
                {
                    Title          = "Notification Bell",
                    Description    = "You will be alerted here whenever a new cartridge request from your department is waiting for your approval. A red badge shows how many unread notifications you have.\n\nClick the bell to open your notification panel.",
                    TargetWinForms = () => _notifBellHost,
                    Side           = "bottom",
                    Align          = "end"
                },

                // 3 · Authorize Cartridge Requests card
                new WpfPortalTourStep
                {
                    Title          = "Authorize Cartridge Requests",
                    Description    = "This is your primary module. When employees in your department submit cartridge requests, they appear here for you to review, sign, and approve — or reject.\n\nClick this card to open the Authorization Queue.",
                    TargetWinForms = () => _landingCardAuthorize,
                    Side           = "right",
                    Align          = "start"
                },

                // 4 · Submit a Request card
                new WpfPortalTourStep
                {
                    Title          = "Submit a Request",
                    Description    = "As an approver, you can also submit cartridge requests for yourself. Because you have approval authority, your own request will appear in your Authorization Queue — you can sign and authorize it immediately.\n\nThis card also gives you access to your Request History.",
                    TargetWinForms = () => _landingCardSubmit,
                    Side           = "bottom",
                    Align          = "start"
                },

                // 5 · Authorization History card
                new WpfPortalTourStep
                {
                    Title          = "Authorization History",
                    Description    = "View a complete record of all your past authorizations — both requests you submitted yourself and requests you signed for others in your department.\n\nStatuses include Pending, Approved, and Rejected.",
                    TargetWinForms = () => _landingCardHistory,
                    Side           = "left",
                    Align          = "start"
                },

                // 6 · Finish
                new WpfPortalTourStep
                {
                    Title       = "That's It — You're All Set!",
                    Description = "You now know how the Approver Portal is organized.\n\nClick the  ❓  button at any time to reopen this guide. Each module also has its own walkthrough — open a module and press  ❓  to start its guide.",
                    Side        = "over"
                }
            };
        }

        // ── Approval Queue Tour (15 steps) ───────────────────────────────────

        private List<WpfPortalTourStep> BuildApprovalQueueSteps()
        {
            return new List<WpfPortalTourStep>
            {
                // 1 · Welcome
                new WpfPortalTourStep
                {
                    Title       = "Authorization Queue",
                    Description = "This is where cartridge requests from your department arrive for your review.\n\nThis tour walks you through how to find, review, sign, and approve or reject requests. Two demo requests have been loaded so every step is visible — they will be removed when the guide closes.\n\nUse the buttons below to move through each step, or press Skip Tour at any time.",
                    Side        = "over"
                },

                // 2 · Notification bell
                new WpfPortalTourStep
                {
                    Title          = "Notification Bell",
                    Description    = "You will be alerted here whenever a new cartridge request from your department is waiting for your approval. A red badge shows how many unread notifications you have.\n\nClick the bell to open your notification panel.",
                    TargetWinForms = () => _notifBellHost,
                    Side           = "bottom",
                    Align          = "end"
                },

                // 3 · Queue panel (left column)
                new WpfPortalTourStep
                {
                    Title     = "Pending Requests",
                    Description = "This panel lists all cartridge requests currently waiting for your authorization.\n\nClick any request card to load its full details on the right side of the screen.",
                    TargetWpf = () => _authorizeRequestView?.TourTarget_QueuePanel,
                    Side      = "right",
                    Align     = "start"
                },

                // 4 · Request Card & Status Indicator
                new WpfPortalTourStep
                {
                    Title     = "Request Card & Status Indicator",
                    Description = "Each card shows the employee name, department, requested models, and submission date. The coloured dot on the left indicates the approval stage:",
                    ColoredBullets = new System.Collections.Generic.List<TourBulletItem>
                    {
                        TourBulletItem.Make("#F9A825", "Yellow — Awaiting your sign-off (Pending Supervisor)."),
                        TourBulletItem.Make("#1976D2", "Blue — Awaiting a manager (Pending Manager)."),
                        TourBulletItem.Make("#9E9E9E", "Grey — Awaiting coordinator review."),
                    },
                    TargetWpf = () => _authorizeRequestView?.TourTarget_QueueList,
                    Side      = "right",
                    Align     = "start"
                },

                // 5 · Search
                new WpfPortalTourStep
                {
                    Title     = "Quick Search",
                    Description = "Type any part of an employee name, department, branch, or company to instantly narrow the queue.\n\nThe list updates as you type — useful when there are many pending requests waiting.",
                    TargetWpf = () => _authorizeRequestView?.TourTarget_SearchArea,
                    Side      = "right",
                    Align     = "start"
                },

                // 6 · Detail panel overview
                new WpfPortalTourStep
                {
                    Title     = "Request Details Panel",
                    Description = "When you click a request, the full details appear here — the employee info, cartridge table, your authorization statement preview, and the signature area.\n\nScroll down within this panel to see and complete the form.",
                    TargetWpf = () => _authorizeRequestView?.TourTarget_RequestDetails,
                    Side      = "left",
                    Align     = "start"
                },

                // 7 · Requester information card
                new WpfPortalTourStep
                {
                    Title     = "Requester Information",
                    Description = "This card shows the employee who submitted the request — their name and position as recorded in the system at the time of submission.",
                    TargetWpf = () => _authorizeRequestView?.TourTarget_RequestDetails,
                    Side      = "left",
                    Align     = "start"
                },

                // 8 · Cartridge details
                new WpfPortalTourStep
                {
                    Title     = "Cartridge Details",
                    Description = "Review the cartridge models and quantities being requested, along with the count of returned empty cartridges (good and damaged).\n\nVerify these details carefully before signing.",
                    TargetWpf       = () => _authorizeRequestView?.TourTarget_CartridgeCard,
                    ScrollAnchorWpf = () => _authorizeRequestView?.TourTarget_CartridgeCard,
                    Side      = "left",
                    Align     = "start"
                },

                // 9 · Authorization statement preview
                new WpfPortalTourStep
                {
                    Title     = "Authorization Statement Preview",
                    Description = "This is a preview of the official authorization document that will be generated when you sign.\n\nIt includes your name, position, department, and all cartridge details. Review it carefully before adding your signature below.",
                    TargetWpf       = () => _authorizeRequestView?.TourTarget_PreviewCard,
                    ScrollAnchorWpf = () => _authorizeRequestView?.TourTarget_PreviewCard,
                    Side      = "left",
                    Align     = "start"
                },

                // 10 · Draw signature tab
                new WpfPortalTourStep
                {
                    Title     = "Draw Your Signature",
                    Description = "The Draw tab lets you sign directly with your mouse or finger on the canvas below. This is the default method.\n\nYour signature will appear live in the Authorization Statement Preview as you draw.",
                    TargetWpf       = () => _authorizeRequestView?.TourTarget_DrawTabBtn,
                    ScrollAnchorWpf = () => _authorizeRequestView?.TourTarget_DrawTabBtn,
                    Side      = "left",
                    Align     = "start"
                },

                // 11 · Upload signature tab
                new WpfPortalTourStep
                {
                    Title     = "Upload Your Signature",
                    Description = "Prefer a pre-prepared signature? Switch to the Upload tab to attach a PNG or JPG image of your signature.\n\nIt is embedded into the authorization document exactly like a drawn signature.",
                    TargetWpf       = () => _authorizeRequestView?.TourTarget_UploadTabBtn,
                    ScrollAnchorWpf = () => _authorizeRequestView?.TourTarget_DrawTabBtn,
                    Side      = "left",
                    Align     = "start"
                },

                // 12 · Clear button
                new WpfPortalTourStep
                {
                    Title     = "Clear and Redo",
                    Description = "Made a mistake? Click Clear to erase the canvas and start your signature over.\n\nThe Clear button resets both drawn and uploaded signatures so you can start fresh.",
                    TargetWpf       = () => _authorizeRequestView?.TourTarget_ClearBtnRow,
                    ScrollAnchorWpf = () => _authorizeRequestView?.TourTarget_DrawTabBtn,
                    Side      = "left",
                    Align     = "start"
                },

                // 13 · Sign & Approve
                new WpfPortalTourStep
                {
                    Title     = "Sign & Approve",
                    Description = "Once you have reviewed all the details and drawn or uploaded your signature, click Approve to finalize the authorization.\n\nThe signed document is generated immediately and the request moves forward for IT processing. The requester will be notified right away.",
                    TargetWpf       = () => _authorizeRequestView?.TourTarget_ApproveBtn,
                    ScrollAnchorWpf = () => _authorizeRequestView?.TourTarget_ActionCard,
                    Side      = "left",
                    Align     = "start"
                },

                // 14 · Reject
                new WpfPortalTourStep
                {
                    Title     = "Reject Request",
                    Description = "If the request cannot be approved, click Reject Request.\n\nYou can optionally enter a reason or remarks in the Notes field above before rejecting. The requester will be notified. This action cannot be undone, so be sure to review all details first.",
                    TargetWpf       = () => _authorizeRequestView?.TourTarget_RejectBtn,
                    ScrollAnchorWpf = () => _authorizeRequestView?.TourTarget_ActionCard,
                    Side      = "left",
                    Align     = "start"
                },

                // 15 · Finish
                new WpfPortalTourStep
                {
                    Title       = "That's It — You're All Set!",
                    Description = "You now know how the Authorization Queue works.\n\nSelect a pending request from the queue on the left, review the details, provide your signature, and click Approve or Reject.\n\nClick the  ❓  button at any time to reopen this guide and revisit any step.",
                    Side        = "over"
                }
            };
        }

        // ────────────────────────────────────────────────────────────────────
        // IDisposable
        // ────────────────────────────────────────────────────────────────────

        public void Dispose()
        {
            CloseTourWindow();
            // On application shutdown it is safe to destroy the overlay HWND.
            if (_window != null)
            {
                try { _window.Close(); } catch { /* best effort */ }
                _window = null;
            }
        }
    }

    // Extension method so we can safely check IsDisposed-equivalent on WPF Window
    internal static class WindowExtensions
    {
        public static bool IsClosed(this Window w)
        {
            try { return !w.IsLoaded; }
            catch { return true; }
        }
    }
}
