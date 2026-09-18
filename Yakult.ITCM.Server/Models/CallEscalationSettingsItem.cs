namespace Yakult.ITCM.Server.Models;

public sealed class CallEscalationSettingsItem
{
    public int DaysToSupervisor { get; set; }
    public int DaysToManager { get; set; }
    public string SupervisorPosition { get; set; } = "IT Supervisor";
    public string ManagerPosition { get; set; } = "IT Manager";
}
