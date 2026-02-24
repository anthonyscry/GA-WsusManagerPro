using WsusManager.Core.Models;

namespace WsusManager.Core.Services.Interfaces;

/// <summary>
/// Builds the command line used by scheduled tasks to run WSUS maintenance.
/// </summary>
public interface IMaintenanceCommandBuilder
{
    /// <summary>
    /// Build a command string appropriate for schtasks /TR.
    /// </summary>
    string Build(ScheduledTaskOptions options, string scriptPath);
}
