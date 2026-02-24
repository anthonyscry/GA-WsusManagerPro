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

    public WsusCleanupExecutor(IProcessRunner processRunner, ILogService logService)
    {
        _processRunner = processRunner;
        _logService = logService;
    }

    public async Task<OperationResult> RunBuiltInCleanupAsync(IProgress<string> progress, CancellationToken ct)
    {
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
