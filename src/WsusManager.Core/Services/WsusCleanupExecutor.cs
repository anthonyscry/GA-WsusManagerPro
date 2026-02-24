using WsusManager.Core.Infrastructure;
using WsusManager.Core.Logging;
using WsusManager.Core.Models;
using WsusManager.Core.Services.Interfaces;

namespace WsusManager.Core.Services;

/// <summary>
/// PowerShell-backed implementation of WSUS built-in cleanup.
/// </summary>
public class WsusCleanupExecutor : IWsusCleanupExecutor
{
    private readonly IProcessRunner _processRunner;
    private readonly ILogService _logService;
    private readonly ISettingsService? _settingsService;

    public WsusCleanupExecutor(
        IProcessRunner processRunner,
        ILogService logService,
        ISettingsService? settingsService = null)
    {
        _processRunner = processRunner;
        _logService = logService;
        _settingsService = settingsService;
    }

    public async Task<OperationResult> RunBuiltInCleanupAsync(IProgress<string> progress, CancellationToken ct)
    {
        var allowFallback = _settingsService?.Current.EnableLegacyFallbackForCleanup ?? true;
        if (!allowFallback)
        {
            _logService.Warning("Cleanup fallback execution is disabled by settings.");
            return OperationResult.Fail("Legacy cleanup fallback is disabled by settings.");
        }

        var psCommand =
            "Get-WsusServer -Name localhost -PortNumber 8530 | " +
            "Invoke-WsusServerCleanup " +
            "-CleanupObsoleteUpdates " +
            "-CleanupUnneededContentFiles " +
            "-CompressUpdates " +
            "-DeclineSupersededUpdates";

        var result = await _processRunner.RunAsync(
            "powershell.exe",
            $"-NonInteractive -NoProfile -Command \"{psCommand}\"",
            progress,
            ct).ConfigureAwait(false);

        if (result.Success)
        {
            return OperationResult.Ok("WSUS built-in cleanup succeeded.");
        }

        _logService.Warning("WSUS built-in cleanup returned exit code {ExitCode}", result.ExitCode);
        return OperationResult.Fail($"WSUS built-in cleanup failed with exit code {result.ExitCode}.");
    }
}
