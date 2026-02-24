using WsusManager.Core.Models;

namespace WsusManager.Core.Services.Interfaces;

/// <summary>
/// Native C# installation orchestration for WSUS/SQL setup.
/// </summary>
public interface INativeInstallationService
{
    /// <summary>
    /// Attempts native installation path. Returns failed result when native path cannot complete.
    /// </summary>
    Task<OperationResult> InstallAsync(
        InstallOptions options,
        IProgress<string>? progress = null,
        CancellationToken ct = default);
}
