using System;
using System.Drawing;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;

class Program
{
    [STAThread]
    static int Main()
    {
        var asm = Assembly.LoadFrom(@"Yakult.Inventory.App\\bin\\Debug-Codex2\\Yakult.Inventory.App.exe");
        var t = asm.GetType("Yakult.Inventory.App.Forms.CallMonitoring.NotificationDrawerControl", throwOnError: true);

        // Find nested TicketRowControl
        var nested = t.GetNestedType("TicketRowControl", BindingFlags.NonPublic);
        if (nested == null)
        {
            Console.WriteLine("TicketRowControl not found");
            return 2;
        }

        var row = (Control)Activator.CreateInstance(nested);

        // Host it in a panel to simulate docking/layout
        var host = new Panel { Size = new Size(800, 200), AutoScroll = true };
        host.Controls.Add(row);
        row.Dock = DockStyle.Top;

        // Force layout
        host.CreateControl();
        row.CreateControl();
        host.PerformLayout();
        row.PerformLayout();

        Console.WriteLine($"row: {row.Bounds}");
        foreach (Control c in row.Controls)
        {
            Console.WriteLine($" child: {c.GetType().Name} {c.Name} dock={c.Dock} bounds={c.Bounds} visible={c.Visible}");
            foreach (Control cc in c.Controls)
            {
                Console.WriteLine($"  grandchild: {cc.GetType().Name} {cc.Name} dock={cc.Dock} bounds={cc.Bounds} visible={cc.Visible} text='{cc.Text}' back={cc.BackColor}");
                foreach (Control ccc in cc.Controls)
                {
                    Console.WriteLine($"   ggchild: {ccc.GetType().Name} {ccc.Name} dock={ccc.Dock} bounds={ccc.Bounds} visible={ccc.Visible} text='{ccc.Text}' back={ccc.BackColor}");
                }
            }
        }

        // Try to locate pnlRight by heuristic: right-docked panel with width ~180
        var pnlRight = row.Controls.Cast<Control>().SelectMany(x => x.Controls.Cast<Control>()).FirstOrDefault(x => x.Dock == DockStyle.Right);
        Console.WriteLine($"pnlRight found: {pnlRight != null} bounds={pnlRight?.Bounds}");

        return 0;
    }
}
