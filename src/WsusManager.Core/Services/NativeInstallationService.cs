using WsusManager.Core.Logging;
using WsusManager.Core.Models;
using WsusManager.Core.Services.Interfaces;

namespace WsusManager.Core.Services;

/// <summary>
/// Native C# installation path. Current implementation validates input and
/// reports unavailability so callers can decide whether to fallback.
/// </summary>
public class NativeInstallationService : INativeInstallationService
{
    private readonly ILogService _logService;

    public NativeInstallationService(ILogService logService)
    {
        _logService = logService;
    }

    public Task<OperationResult> InstallAsync(
        InstallOptions options,
        IProgress<string>? progress = null,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(options.InstallerPath))
        {
            return Task.FromResult(OperationResult.Fail("Installer path is required for native installation."));
        }

        if (string.IsNullOrWhiteSpace(options.SaPassword))
        {
            return Task.FromResult(OperationResult.Fail("SA password is required for native installation."));
        }

        if (!OperatingSystem.IsWindows())
        {
            return Task.FromResult(OperationResult.Fail("Native installation requires Windows."));
        }

        progress?.Report("[NATIVE] Native installation path is not yet implemented; using fallback.");
        _logService.Warning("Native installation requested but not implemented; caller should fallback.");
        return Task.FromResult(OperationResult.Fail(
            "Native installation path is not yet implemented.",
            new NotSupportedException("Native installation path is not yet implemented.")));
    }
}
