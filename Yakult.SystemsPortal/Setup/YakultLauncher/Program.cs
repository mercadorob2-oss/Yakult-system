using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;

namespace YakultLauncher;

internal static class Program
{
    private const string DefaultYakultPath = @"C:\Program Files\Yakult\Yakult.Inventory.App.exe";
    private const int DefaultStartupDelaySeconds = 5;
    private const string LogPath = @"C:\YakultLauncher.log";
    private const string YakultAppPathEnvVar = "YAKULT_APP_PATH";
    private const string StartupDelayEnvVar = "YAKULT_STARTUP_DELAY_SECONDS";

    private static SplashForm? _splashForm;
    private static Thread? _uiThread;
    private static Process? _explorerProcess;
    private static Process? _yakultProcess;

    [STAThread]
    private static int Main()
    {
        Log("YakultLauncher starting");

        var yakultPath = ReadEnv(YakultAppPathEnvVar, DefaultYakultPath);
        var delaySeconds = ReadEnvInt(StartupDelayEnvVar, DefaultStartupDelaySeconds);

        Log($"Yakult app path  : {yakultPath}");
        Log($"Startup delay   : {delaySeconds}s");

        // Show splash screen on separate UI thread
        ShowSplash("Starting Yakult Inventory System...\nPlease wait.");

        try
        {
            // Start desktop
            _explorerProcess = StartProcess("explorer.exe");
            if (_explorerProcess == null)
            {
                CloseSplash();
                Log("FATAL: failed to start explorer.exe");
                ShowError("Failed to start desktop.\nThe RDP session will end.", "Critical Error");
                return 1;
            }
            Log($"Started explorer.exe (PID {_explorerProcess.Id})");
            UpdateSplash("Desktop started.\nLaunching Yakult Inventory System...");

            // Wait before launching app
            Thread.Sleep(TimeSpan.FromSeconds(delaySeconds));

            // Launch Yakult app
            if (!File.Exists(yakultPath))
            {
                CloseSplash();
                Log($"WARNING: Yakult app not found at {yakultPath}");
                ShowError(
                    $"Yakult app not found at:\n{yakultPath}\n\nPlease install the application or contact your administrator.",
                    "Application Not Found"
                );
            }
            else if (StartProcess(yakultPath) is { } yakult)
            {
                _yakultProcess = yakult;
                Log($"Started Yakult app (PID {_yakultProcess.Id})");
                UpdateSplash("Yakult Inventory System launched successfully!");

                // Register Yakult app to auto-start on reconnect
                RegisterStartup(yakultPath);

                // Keep splash visible for a moment so user sees success message
                Thread.Sleep(TimeSpan.FromSeconds(2));
                CloseSplash();
            }
            else
            {
                CloseSplash();
                Log($"WARNING: could not start {yakultPath}");
                ShowError(
                    $"Could not start:\n{yakultPath}\n\nYou can launch it manually from the desktop.",
                    "Launch Failed"
                );
            }
        }
        catch (Exception ex)
        {
            CloseSplash();
            Log($"FATAL ERROR: {ex}");
            ShowError($"An unexpected error occurred:\n{ex.Message}", "Error");
            return 1;
        }

        // Wait for explorer to exit (keeps session alive)
        Log("Waiting for explorer.exe to exit. Close the desktop or sign out to end the RDP session.");
        
        try
        {
            _explorerProcess?.WaitForExit();
        }
        catch (Exception ex)
        {
            Log($"Error waiting for explorer: {ex.Message}");
        }

        // Cleanup
        Cleanup();
        Log("explorer.exe exited. YakultLauncher ending.");
        return 0;
    }

    private static string ReadEnv(string name, string fallback)
    {
        var value = Environment.GetEnvironmentVariable(name);
        return string.IsNullOrWhiteSpace(value) ? fallback : value;
    }

    private static int ReadEnvInt(string name, int fallback)
    {
        var value = Environment.GetEnvironmentVariable(name);
        return int.TryParse(value, out var parsed) && parsed >= 0 ? parsed : fallback;
    }

    private static Process? StartProcess(string path)
    {
        try
        {
            return Process.Start(new ProcessStartInfo
            {
                FileName = path,
                UseShellExecute = true,
            });
        }
        catch (Exception ex)
        {
            Log($"Failed to start {path}: {ex.Message}");
            return null;
        }
    }

    private static void ShowSplash(string message)
    {
        _uiThread = new Thread(() =>
        {
            _splashForm = new SplashForm(message);
            Application.Run(_splashForm);
        });
        _uiThread.IsBackground = true;
        _uiThread.SetApartmentState(ApartmentState.STA);
        _uiThread.Start();
    }

    private static void UpdateSplash(string message)
    {
        _splashForm?.UpdateMessage(message);
    }

    private static void CloseSplash()
    {
        if (_splashForm != null && !_splashForm.IsDisposed)
        {
            try
            {
                if (_splashForm.InvokeRequired)
                {
                    _splashForm.BeginInvoke(new Action(() =>
                    {
                        if (!_splashForm.IsDisposed)
                            _splashForm.Close();
                    }));
                }
                else
                {
                    if (!_splashForm.IsDisposed)
                        _splashForm.Close();
                }
            }
            catch (Exception ex)
            {
                Log($"Error closing splash: {ex.Message}");
            }
        }
    }

    private static void ShowError(string message, string title)
    {
        try
        {
            if (_splashForm != null && !_splashForm.IsDisposed)
            {
                if (_splashForm.InvokeRequired)
                {
                    _splashForm.BeginInvoke(new Action(() =>
                    {
                        if (!_splashForm.IsDisposed)
                        {
                            MessageBox.Show(
                                _splashForm,
                                message,
                                title,
                                MessageBoxButtons.OK,
                                MessageBoxIcon.Error
                            );
                        }
                    }));
                }
                else
                {
                    MessageBox.Show(
                        _splashForm,
                        message,
                        title,
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Error
                    );
                }
            }
            else
            {
                MessageBox.Show(message, title, MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
        catch (Exception ex)
        {
            Log($"Error showing message box: {ex.Message}");
            Log($"Original error: {message}");
        }
    }

    private static void Cleanup()
    {
        Log("Cleaning up processes...");

        // Remove startup registration
        CleanupStartup();

        // Close splash if still open
        CloseSplash();

        // Kill Yakult app if running
        if (_yakultProcess != null && !_yakultProcess.HasExited)
        {
            try
            {
                Log($"Terminating Yakult app (PID {_yakultProcess.Id})");
                _yakultProcess.Kill();
                _yakultProcess.WaitForExit(5000);
            }
            catch (Exception ex)
            {
                Log($"Error terminating Yakult app: {ex.Message}");
            }
        }

        Log("Cleanup complete.");
    }

    private const string StartupValueName = "YakultInventoryApp";

    private static void RegisterStartup(string yakultPath)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(
                @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", writable: true);
            if (key != null)
            {
                key.SetValue(StartupValueName, yakultPath);
                Log("Registered Yakult app in user startup");
            }
        }
        catch (Exception ex)
        {
            Log($"Failed to register startup: {ex.Message}");
        }
    }

    private static void CleanupStartup()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(
                @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", writable: true);
            if (key?.GetValue(StartupValueName) != null)
            {
                key.DeleteValue(StartupValueName);
                Log("Removed Yakult app from user startup");
            }
        }
        catch (Exception ex)
        {
            Log($"Failed to clean up startup: {ex.Message}");
        }
    }

    private static void Log(string message)
    {
        var line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} [YakultLauncher] {message}";
        try { Console.WriteLine(line); } catch { }
        try
        {
            File.AppendAllText(LogPath, line + Environment.NewLine);
        }
        catch
        {
            // Fallback: try writing to the user's temp directory
            try
            {
                var fallbackPath = Path.Combine(Path.GetTempPath(), "YakultLauncher.log");
                File.AppendAllText(fallbackPath, line + Environment.NewLine);
            }
            catch { }
        }
    }
}
