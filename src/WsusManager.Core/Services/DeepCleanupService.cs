using System.Diagnostics;
using WsusManager.Core.Logging;
using WsusManager.Core.Models;
using WsusManager.Core.Services.Interfaces;

namespace WsusManager.Core.Services;

/// <summary>
/// Runs the non-destructive WSUS built-in cleanup only.
/// This service intentionally avoids direct SQL delete/update/shrink operations.
/// </summary>
public class DeepCleanupService : IDeepCleanupService
{
    private readonly ISqlService _sqlService;
    private readonly IWsusCleanupExecutor _wsusCleanupExecutor;
    private readonly ILogService _logService;

    private const string SusDb = "SUSDB";

    public DeepCleanupService(
        ISqlService sqlService,
        IWsusCleanupExecutor wsusCleanupExecutor,
        ILogService logService)
    {
        _sqlService = sqlService;
        _wsusCleanupExecutor = wsusCleanupExecutor;
        _logService = logService;
    }

    /// <inheritdoc/>
    public async Task<OperationResult> RunAsync(
        string sqlInstance,
        IProgress<string> progress,
        CancellationToken ct)
    {
        _logService.Info("Starting WSUS built-in cleanup on {SqlInstance}", sqlInstance);

        var dbSizeBefore = await TryGetDatabaseSizeAsync(sqlInstance, ct).ConfigureAwait(false);
        if (dbSizeBefore >= 0)
        {
            progress.Report($"Current database size: {dbSizeBefore:F2} GB");
        }

        try
        {
            var cleanupResult = await RunBuiltInCleanupStepAsync(progress, ct).ConfigureAwait(false);
            if (!cleanupResult.Success)
            {
                _logService.Warning("WSUS built-in cleanup failed: {Message}", cleanupResult.Message);
                return cleanupResult;
            }

            var dbSizeAfter = await TryGetDatabaseSizeAsync(sqlInstance, ct).ConfigureAwait(false);
            if (dbSizeBefore >= 0 && dbSizeAfter >= 0)
            {
                var delta = dbSizeBefore - dbSizeAfter;
                progress.Report(
                    $"Database size (allocated): {dbSizeBefore:F2} GB -> {dbSizeAfter:F2} GB (delta {delta:F2} GB). " +
                    "Note: safe cleanup does not run database shrink.");
            }

            _logService.Info("WSUS built-in cleanup completed successfully");
            return OperationResult.Ok("WSUS built-in cleanup completed successfully.");
        }
        catch (OperationCanceledException)
        {
            _logService.Info("WSUS cleanup cancelled by user");
            return OperationResult.Fail("WSUS cleanup was cancelled.");
        }
        catch (Exception ex)
        {
            _logService.Error(ex, "WSUS cleanup failed");
            return OperationResult.Fail($"WSUS cleanup failed: {ex.Message}", ex);
        }
    }

    private async Task<OperationResult> RunBuiltInCleanupStepAsync(IProgress<string> progress, CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();
        progress.Report("[Step 1/1] WSUS built-in cleanup...");

        var result = await _wsusCleanupExecutor.RunBuiltInCleanupAsync(progress, ct).ConfigureAwait(false);

        sw.Stop();
        if (result.Success)
        {
            progress.Report($"[Step 1/1] WSUS built-in cleanup... done ({sw.Elapsed.TotalSeconds:F0}s)");
            return OperationResult.Ok("WSUS built-in cleanup step completed.");
        }

        progress.Report($"[Step 1/1] WSUS built-in cleanup... failed ({result.Message}, {sw.Elapsed.TotalSeconds:F0}s)");
        return OperationResult.Fail(result.Message, result.Exception);
    }

    private async Task<double> TryGetDatabaseSizeAsync(string sqlInstance, CancellationToken ct)
    {
        try
        {
            return await _sqlService.GetDatabaseSizeAsync(sqlInstance, SusDb, ct).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logService.Warning("Could not query SUSDB size for cleanup telemetry: {Error}", ex.Message);
            return -1;
        }
    }
}
