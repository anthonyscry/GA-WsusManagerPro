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
    /// Returns an explicit native result with fallback policy.
    /// InstallationService may only run legacy fallback when
    /// <see cref="NativeInstallationResult.AllowLegacyFallback"/> is true.
    /// </summary>
    Task<NativeInstallationResult> InstallAsync(InstallOptions options, IProgress<string>? progress = null, CancellationToken ct = default);
}
