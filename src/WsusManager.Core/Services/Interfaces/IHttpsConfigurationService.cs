using WsusManager.Core.Models;

namespace WsusManager.Core.Services.Interfaces;

/// <summary>
/// Configures WSUS HTTPS (port 8531) using a C#-first strategy.
/// Implementations may fallback to the legacy PowerShell script when native steps fail.
/// </summary>
public interface IHttpsConfigurationService
{
    /// <summary>
    /// Configures HTTPS for WSUS using the provided certificate thumbprint.
    /// Native steps are attempted first; fallback may be used automatically.
    /// </summary>
    Task<OperationResult> ConfigureHttpsAsync(
        string? certificateThumbprint,
        IProgress<string>? progress = null,
        CancellationToken ct = default);
}
