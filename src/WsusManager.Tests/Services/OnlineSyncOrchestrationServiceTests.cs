using Moq;
using WsusManager.Core.Models;
using WsusManager.Core.Services;
using WsusManager.Core.Services.Interfaces;

namespace WsusManager.Tests.Services;

public class OnlineSyncOrchestrationServiceTests
{
    private readonly Mock<ISyncService> _mockSync = new();
    private readonly Mock<IExportService> _mockExport = new();

    [Fact]
    public async Task RunAsync_WhenSyncFails_DoesNotRunExport()
    {
        _mockSync
            .Setup(s => s.RunSyncAsync(
                It.IsAny<SyncProfile>(),
                It.IsAny<int>(),
                It.IsAny<IProgress<string>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(OperationResult.Fail("Sync failed"));

        var service = new OnlineSyncOrchestrationService(_mockSync.Object, _mockExport.Object);

        var result = await service.RunAsync(
            SyncProfile.FullSync,
            maxAutoApproveCount: 200,
            new ExportOptions { SourcePath = @"C:\WSUS", FullExportPath = @"D:\Exports" },
            progress: null,
            ct: CancellationToken.None).ConfigureAwait(false);

        Assert.False(result.Success);
        _mockExport.Verify(e => e.ExportAsync(
            It.IsAny<ExportOptions>(),
            It.IsAny<IProgress<string>>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RunAsync_WhenNoExportPaths_DoesNotRunExport()
    {
        _mockSync
            .Setup(s => s.RunSyncAsync(
                It.IsAny<SyncProfile>(),
                It.IsAny<int>(),
                It.IsAny<IProgress<string>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(OperationResult.Ok("Sync complete"));

        var service = new OnlineSyncOrchestrationService(_mockSync.Object, _mockExport.Object);

        var result = await service.RunAsync(
            SyncProfile.QuickSync,
            maxAutoApproveCount: 200,
            new ExportOptions { SourcePath = @"C:\WSUS" },
            progress: null,
            ct: CancellationToken.None).ConfigureAwait(false);

        Assert.True(result.Success);
        _mockExport.Verify(e => e.ExportAsync(
            It.IsAny<ExportOptions>(),
            It.IsAny<IProgress<string>>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RunAsync_WhenExportPathsPresent_RunsExportWithDefaults()
    {
        _mockSync
            .Setup(s => s.RunSyncAsync(
                It.IsAny<SyncProfile>(),
                It.IsAny<int>(),
                It.IsAny<IProgress<string>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(OperationResult.Ok("Sync complete"));

        _mockExport
            .Setup(e => e.ExportAsync(
                It.IsAny<ExportOptions>(),
                It.IsAny<IProgress<string>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(OperationResult.Ok("Export complete"));

        ExportOptions? capturedOptions = null;
        _mockExport
            .Setup(e => e.ExportAsync(
                It.IsAny<ExportOptions>(),
                It.IsAny<IProgress<string>>(),
                It.IsAny<CancellationToken>()))
            .Callback<ExportOptions, IProgress<string>, CancellationToken>((opts, _, _) => capturedOptions = opts)
            .ReturnsAsync(OperationResult.Ok("Export complete"));

        var service = new OnlineSyncOrchestrationService(_mockSync.Object, _mockExport.Object);

        var result = await service.RunAsync(
            SyncProfile.SyncOnly,
            maxAutoApproveCount: 200,
            new ExportOptions
            {
                SourcePath = @"C:\WSUS",
                FullExportPath = @"D:\Full",
                ExportDays = 0,
                IncludeDatabaseBackup = false
            },
            progress: null,
            ct: CancellationToken.None).ConfigureAwait(false);

        Assert.True(result.Success);
        _mockExport.Verify(e => e.ExportAsync(
            It.IsAny<ExportOptions>(),
            It.IsAny<IProgress<string>>(),
            It.IsAny<CancellationToken>()), Times.Once);
        Assert.NotNull(capturedOptions);
        Assert.Equal(30, capturedOptions!.ExportDays);
        Assert.False(capturedOptions.IncludeDatabaseBackup);
    }

    [Fact]
    public async Task RunAsync_WhenExportRequestsBackup_PreservesIncludeDatabaseBackupFlag()
    {
        _mockSync
            .Setup(s => s.RunSyncAsync(
                It.IsAny<SyncProfile>(),
                It.IsAny<int>(),
                It.IsAny<IProgress<string>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(OperationResult.Ok("Sync complete"));

        ExportOptions? capturedOptions = null;
        _mockExport
            .Setup(e => e.ExportAsync(
                It.IsAny<ExportOptions>(),
                It.IsAny<IProgress<string>>(),
                It.IsAny<CancellationToken>()))
            .Callback<ExportOptions, IProgress<string>, CancellationToken>((opts, _, _) => capturedOptions = opts)
            .ReturnsAsync(OperationResult.Ok("Export complete"));

        var service = new OnlineSyncOrchestrationService(_mockSync.Object, _mockExport.Object);

        var result = await service.RunAsync(
            SyncProfile.FullSync,
            maxAutoApproveCount: 200,
            new ExportOptions
            {
                SourcePath = @"C:\WSUS",
                FullExportPath = @"D:\Full",
                IncludeDatabaseBackup = true
            },
            progress: null,
            ct: CancellationToken.None).ConfigureAwait(false);

        Assert.True(result.Success);
        Assert.NotNull(capturedOptions);
        Assert.True(capturedOptions!.IncludeDatabaseBackup);
    }
}
