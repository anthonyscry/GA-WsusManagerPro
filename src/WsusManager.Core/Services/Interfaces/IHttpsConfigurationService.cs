using WsusManager.Core.Models;

namespace WsusManager.Core.Services.Interfaces;

/// <summary>
/// Configures WSUS HTTPS bindings and SSL settings.
/// </summary>
public interface IHttpsConfigurationService
{
    /// <summary>
    /// Configures HTTPS for the WSUS server using a native C# path first,
    /// with optional fallback behavior handled by the implementation.
    /// </summary>
    Task<OperationResult> ConfigureAsync(
        string wsusServer,
        string certThumbprint,
        IProgress<string>? progress,
        CancellationToken ct = default);
}
