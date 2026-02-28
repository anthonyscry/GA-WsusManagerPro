using WsusManager.Core.Models;

namespace WsusManager.Core.Services.Interfaces;

/// <summary>
/// Runs non-destructive WSUS cleanup using WSUS built-in cleanup only.
/// This intentionally avoids direct SQL delete/update/shrink operations.
/// </summary>
public interface IDeepCleanupService
{
    /// <summary>
    /// Runs the built-in cleanup workflow.
    /// Progress reports a single step in format: [Step 1/1] ...
    /// Database size telemetry is best-effort and non-blocking.
    /// </summary>
    /// <param name="sqlInstance">SQL Server instance (e.g., "localhost\SQLEXPRESS").</param>
    /// <param name="progress">Progress reporter for real-time status lines.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>OperationResult indicating success or failure.</returns>
    Task<OperationResult> RunAsync(
        string sqlInstance,
        IProgress<string> progress,
        CancellationToken ct);
}
