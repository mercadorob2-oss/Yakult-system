using System.Configuration;
using Microsoft.IdentityModel.Protocols;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Data.SqlClient;
using System.Windows.Forms;
using System.Windows.Interop;
using System.Windows.Media;
using Yakult.Inventory.App.Core;
using Yakult.Inventory.App.Data;
using Yakult.Inventory.App.Helpers;
using Yakult.Inventory.App.Pages.Admin.DBConn;
using Yakult.Inventory.App.Pages.User;


namespace Yakult.Inventory.App
{
    internal static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.AddMessageFilter(new DataGridViewSafetyMessageFilter());

            // Force WPF to render in software instead of hardware/GPU-accelerated mode, process-wide.
            // Repeatedly opening/closing MediaElement (video evidence playback) can destabilize the
            // GPU composition/render thread on some machines/drivers, which then fails on a later,
            // unrelated WPF operation (e.g. scrolling) with COMException 0x88980406
            // (UCEERR_RENDERTHREADFAILURE) — a fatal, unrecoverable render-thread crash that no
            // exception handler can catch because it originates outside the managed dispatcher loop.
            // Software rendering avoids that failure mode entirely; the CPU cost is negligible for
            // this app's fairly simple, mostly-static WPF surfaces (forms/lists, not animation-heavy UI).
            RenderOptions.ProcessRenderMode = RenderMode.SoftwareOnly;

            // Without these, an unhandled exception anywhere on the main UI thread's message pump
            // (including WinForms' own WM_ACTIVATE focus-restoration logic — see CLAUDE.md's "WPF
            // Overlay Crash on Close" — which can throw when it tries to restore focus into a WPF
            // window/control that's mid-teardown, e.g. after Alt+Tab away and back then closing a
            // Repair Portal window) falls through to AppDomain.UnhandledException with
            // IsTerminating=true, which the CLR treats as fatal and kills the whole process. A
            // per-window WPF Dispatcher.UnhandledException subscription (see
            // RepairPortalShellWindow.xaml.cs) only catches exceptions raised on WPF's own
            // dispatcher loop — it does not catch exceptions raised inside WinForms' message pump
            // itself. Catch them here, at the true top of the app, instead.
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
            Application.ThreadException += (s, e) =>
            {
                System.Diagnostics.Debug.WriteLine("Unhandled UI-thread exception (swallowed): " + e.Exception);
            };
            AppDomain.CurrentDomain.UnhandledException += (s, e) =>
            {
                System.Diagnostics.Debug.WriteLine("Unhandled non-UI-thread exception: " + e.ExceptionObject);
            };

            Icon appIcon = null;
            try
            {
                appIcon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
            }
            catch
            {
                appIcon = null;
            }

            if (appIcon != null)
            {
                // Icon application is handled in the global Application.Idle hook below.
            }

            var iconAppliedForms = new HashSet<Form>();
            Application.Idle += (sender, e) =>
            {
                foreach (var form in Application.OpenForms.Cast<Form>().ToArray())
                {
                    if (form == null || form.IsDisposed || !form.IsHandleCreated)
                        continue;

                    // Global guard: prevent DataGridView default error dialogs
                    // (checkbox value type mismatches, paste scenarios, etc.).
                    DataGridViewSafety.AttachAll(form);

                    if (appIcon == null)
                        continue;

                    if (iconAppliedForms.Contains(form))
                        continue;

                    form.Icon = appIcon;
                    iconAppliedForms.Add(form);
                }
            };

            // Load connection string from appsettings.json / appsettings.Development.json.
            // This mirrors the ASP.NET Core appsettings pattern so credentials don't need
            // to be entered manually on first launch.
            var jsonConn = AppSettingsLoader.LoadConnectionString();
            if (!string.IsNullOrWhiteSpace(jsonConn))
                DatabaseConfig.SetJsonDefault(jsonConn);

            // Only show the manual setup form if no config was found at all
            // (no JSON files and no previously saved user settings).
            if (!DatabaseConfig.IsConfigured)
            {
                using (var setupForm = new DatabaseSetupForm())
                {
                    if (setupForm.ShowDialog() != DialogResult.OK)
                        return;
                }
            }

            // Start with MainForm which will show login/register dialog
            Application.Run(new MainForm());
        }

        private sealed class DataGridViewSafetyMessageFilter : IMessageFilter
        {
            private int _lastScanTick;

            public bool PreFilterMessage(ref Message m)
            {
                // Scan at most 4x/second to keep overhead low.
                var now = Environment.TickCount;
                if (unchecked(now - _lastScanTick) < 250)
                    return false;
                _lastScanTick = now;

                try
                {
                    foreach (var form in Application.OpenForms.Cast<Form>().ToArray())
                    {
                        if (form == null || form.IsDisposed || !form.IsHandleCreated)
                            continue;

                        DataGridViewSafety.AttachAll(form);
                    }
                }
                catch
                {
                }

                return false;
            }
        }

    }
}
