using WsusManager.Core.Models;

namespace WsusManager.Core.Services.Interfaces;

/// <summary>
/// Native C# installation orchestrator for WSUS + SQL Express.
/// This service attempts the native path first before any legacy fallback.
/// </summary>
public interface INativeInstallationService
{
    /// <summary>
    /// Executes the native installation workflow.
    /// Returns a failed result when native execution cannot complete,
    /// allowing the caller to choose fallback behavior.
    /// </summary>
    Task<OperationResult> InstallAsync(InstallOptions options, IProgress<string>? progress = null, CancellationToken ct = default);
}
