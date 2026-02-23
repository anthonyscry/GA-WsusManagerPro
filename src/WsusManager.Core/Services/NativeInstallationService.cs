using WsusManager.Core.Logging;
using WsusManager.Core.Models;
using WsusManager.Core.Services.Interfaces;

namespace WsusManager.Core.Services;

/// <summary>
/// Native C# install orchestrator entry point.
/// Current behavior is intentionally conservative: emit native progress and
/// return a controlled failure so InstallationService can use legacy fallback.
/// </summary>
public class NativeInstallationService : INativeInstallationService
{
    private readonly ILogService _logService;

    public NativeInstallationService(ILogService logService)
    {
        _logService = logService;
    }

    /// <inheritdoc />
    public Task<OperationResult> InstallAsync(
        InstallOptions options,
        IProgress<string>? progress = null,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        progress?.Report("[NATIVE] Starting native WSUS installation orchestration...");

        const string message = "Native installation orchestrator is not yet implemented for full install execution.";
        _logService.Warning(message);

        return Task.FromResult(OperationResult.Fail(message));
    }
}
