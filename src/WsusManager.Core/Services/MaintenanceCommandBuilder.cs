using WsusManager.Core.Models;
using WsusManager.Core.Services.Interfaces;

namespace WsusManager.Core.Services;

/// <summary>
/// Default maintenance command builder for scheduled task actions.
/// </summary>
public class MaintenanceCommandBuilder : IMaintenanceCommandBuilder
{
    public string Build(ScheduledTaskOptions options, string scriptPath)
    {
        return "powershell.exe -ExecutionPolicy Bypass " +
               $"-File \\\"{scriptPath}\\\" " +
               $"-Unattended -MaintenanceProfile {options.MaintenanceProfile}";
    }
}
