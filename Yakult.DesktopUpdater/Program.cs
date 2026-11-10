using System;
using System.Windows.Forms;

namespace Yakult.DesktopUpdates
{
    internal static class Program
    {
        [STAThread]
        private static int Main(string[] args)
        {
            // Inno uses this headless path for manual upgrades as well as automatic updates.
            if (args.Length == 3 && args[0] == "--merge-config")
            {
                try { InstalledSettingsBackup.MergeConfiguration(args[1], args[2]); return 0; }
                catch (Exception ex) { System.Diagnostics.Trace.WriteLine(ex); return 1; }
            }
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            try
            {
                if (args.Length != 2 || args[0] != "--job") throw new ArgumentException("Start updates from the Yakult dashboard.");
                Application.Run(new UpdaterForm(UpdateProtocol.ValidateJobPath(args[1])));
            }
            catch (Exception ex) { MessageBox.Show(ex.Message, "Yakult update", MessageBoxButtons.OK, MessageBoxIcon.Error); return 1; }
            return 0;
        }
    }
}
