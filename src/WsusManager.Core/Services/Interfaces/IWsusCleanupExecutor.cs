using WsusManager.Core.Models;

namespace WsusManager.Core.Services.Interfaces;

/// <summary>
/// Executes WSUS built-in cleanup operation used in deep cleanup step 1.
/// </summary>
public interface IWsusCleanupExecutor
{
    Task<OperationResult> RunBuiltInCleanupAsync(IProgress<string> progress, CancellationToken ct);
}
