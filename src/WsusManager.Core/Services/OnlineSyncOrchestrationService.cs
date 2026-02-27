using WsusManager.Core.Models;
using WsusManager.Core.Services.Interfaces;

namespace WsusManager.Core.Services;

/// <summary>
/// Coordinates Online Sync execution with optional post-sync export.
/// Keeps orchestration logic outside the ViewModel for testability.
/// </summary>
public class OnlineSyncOrchestrationService
{
    private readonly ISyncService _syncService;
    private readonly IExportService _exportService;

    public OnlineSyncOrchestrationService(ISyncService syncService, IExportService exportService)
    {
        _syncService = syncService;
        _exportService = exportService;
    }

    public async Task<OperationResult> RunAsync(
        SyncProfile profile,
        int maxAutoApproveCount,
        ExportOptions? exportOptions,
        IProgress<string>? progress,
        CancellationToken ct)
    {
        var syncProgress = progress ?? new Progress<string>(_ => { });

        var syncResult = await _syncService.RunSyncAsync(
            profile,
            maxAutoApproveCount,
            syncProgress,
            ct).ConfigureAwait(false);

        if (!syncResult.Success)
        {
            return syncResult;
        }

        if (exportOptions is null)
        {
            return syncResult;
        }

        var hasExportDestination =
            !string.IsNullOrWhiteSpace(exportOptions.FullExportPath) ||
            !string.IsNullOrWhiteSpace(exportOptions.DifferentialExportPath);

        if (!hasExportDestination)
        {
            return syncResult;
        }

        var effectiveExportOptions = exportOptions with
        {
            SourcePath = string.IsNullOrWhiteSpace(exportOptions.SourcePath) ? @"C:\WSUS" : exportOptions.SourcePath,
            ExportDays = exportOptions.ExportDays > 0 ? exportOptions.ExportDays : 30,
            IncludeDatabaseBackup = true
        };

        var exportProgress = progress ?? new Progress<string>(_ => { });
        var exportResult = await _exportService.ExportAsync(effectiveExportOptions, exportProgress, ct).ConfigureAwait(false);

        if (!exportResult.Success)
        {
            return OperationResult.Fail($"Online Sync completed, but export failed: {exportResult.Message}");
        }

        return OperationResult.Ok("Online Sync and export completed successfully.");
    }
}
