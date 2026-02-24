using WsusManager.Core.Logging;
using WsusManager.Core.Models;
using WsusManager.Core.Services.Interfaces;

namespace WsusManager.Core.Services;

/// <summary>
/// Coordinates HTTPS setup with a native C# path first, then legacy fallback.
/// </summary>
public class HttpsConfigurationService : IHttpsConfigurationService
{
    private readonly LegacyHttpsConfigurationFallback _fallback;
    private readonly ILogService _logService;
    private readonly ISettingsService? _settingsService;
    private readonly Func<string, string, IProgress<string>?, CancellationToken, Task<OperationResult>> _configureNativeAsync;

    public HttpsConfigurationService(
        LegacyHttpsConfigurationFallback fallback,
        ILogService logService,
        ISettingsService? settingsService = null,
        Func<string, string, IProgress<string>?, CancellationToken, Task<OperationResult>>? configureNativeAsync = null)
    {
        _fallback = fallback;
        _logService = logService;
        _settingsService = settingsService;
        _configureNativeAsync = configureNativeAsync ?? ConfigureNativeAsync;
    }

    public async Task<OperationResult> ConfigureAsync(
        string wsusServer,
        string certThumbprint,
        IProgress<string>? progress,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(wsusServer))
        {
            return OperationResult.Fail("WSUS server name is required.");
        }

        if (string.IsNullOrWhiteSpace(certThumbprint))
        {
            return OperationResult.Fail("Certificate thumbprint is required.");
        }

        var native = await _configureNativeAsync(
            wsusServer,
            certThumbprint,
            progress,
            ct).ConfigureAwait(false);

        if (native.Success)
        {
            return native;
        }

        var allowFallback = _settingsService?.Current.EnableLegacyFallbackForHttps ?? true;
        if (!allowFallback)
        {
            _logService.Warning("Native HTTPS configuration failed and fallback is disabled. Reason: {Reason}", native.Message);
            return native;
        }

        if (native.Message.Contains("requires Windows", StringComparison.OrdinalIgnoreCase))
        {
            return native;
        }

        progress?.Report("[FALLBACK] Native HTTPS configuration failed; trying legacy adapter...");
        _logService.Warning(
            "Native HTTPS configuration failed for {Server}; using fallback. Reason: {Reason}",
            wsusServer,
            native.Message);

        return await _fallback.ConfigureAsync(
            wsusServer,
            certThumbprint,
            progress,
            ct).ConfigureAwait(false);
    }

    private Task<OperationResult> ConfigureNativeAsync(
        string wsusServer,
        string certThumbprint,
        IProgress<string>? progress,
        CancellationToken ct)
    {
        if (!OperatingSystem.IsWindows())
        {
            return Task.FromResult(OperationResult.Fail("Native HTTPS configuration requires Windows."));
        }

        progress?.Report("[NATIVE] Native HTTPS configuration is not yet implemented; using fallback.");
        return Task.FromResult(OperationResult.Fail("Native HTTPS configuration path is not yet implemented."));
    }
}
