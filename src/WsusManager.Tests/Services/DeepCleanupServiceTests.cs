using System.Collections.Concurrent;
using Moq;
using WsusManager.Core.Logging;
using WsusManager.Core.Models;
using WsusManager.Core.Services;
using WsusManager.Core.Services.Interfaces;

namespace WsusManager.Tests.Services;

public class DeepCleanupServiceTests
{
    private readonly Mock<ISqlService> _mockSql = new();
    private readonly Mock<IWsusCleanupExecutor> _mockCleanupExecutor = new();
    private readonly Mock<ILogService> _mockLog = new();

    private DeepCleanupService CreateService() =>
        new(_mockSql.Object, _mockCleanupExecutor.Object, _mockLog.Object);

    [Fact]
    public async Task RunAsync_UsesWsusCleanupExecutor_ForBuiltInCleanup()
    {
        _mockSql
            .SetupSequence(s => s.GetDatabaseSizeAsync(It.IsAny<string>(), "SUSDB", It.IsAny<CancellationToken>()))
            .ReturnsAsync(-1)
            .ReturnsAsync(-1);

        _mockCleanupExecutor
            .Setup(e => e.RunBuiltInCleanupAsync(It.IsAny<IProgress<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(OperationResult.Ok("Cleanup complete."));

        var service = CreateService();
        var result = await service.RunAsync("localhost\\SQLEXPRESS", new Progress<string>(_ => { }), CancellationToken.None).ConfigureAwait(false);

        Assert.True(result.Success);
        _mockCleanupExecutor.Verify(e => e.RunBuiltInCleanupAsync(It.IsAny<IProgress<string>>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RunAsync_UsesBuiltInCleanupOnly_AndAvoidsSqlMutationSteps()
    {
        _mockSql
            .SetupSequence(s => s.GetDatabaseSizeAsync(It.IsAny<string>(), "SUSDB", It.IsAny<CancellationToken>()))
            .ReturnsAsync(2.8)
            .ReturnsAsync(2.7);

        _mockSql
            .Setup(s => s.ExecuteNonQueryAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("SQL mutation should not run in built-in cleanup mode."));

        _mockCleanupExecutor
            .Setup(e => e.RunBuiltInCleanupAsync(It.IsAny<IProgress<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(OperationResult.Ok("Cleanup complete."));

        var service = CreateService();
        var result = await service.RunAsync("localhost\\SQLEXPRESS", new Progress<string>(_ => { }), CancellationToken.None).ConfigureAwait(false);

        Assert.True(result.Success);
        _mockSql.Verify(s => s.ExecuteNonQueryAsync(
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<int>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RunAsync_ReportsBuiltInCleanupAsSingleStep()
    {
        _mockSql
            .SetupSequence(s => s.GetDatabaseSizeAsync(It.IsAny<string>(), "SUSDB", It.IsAny<CancellationToken>()))
            .ReturnsAsync(2.8)
            .ReturnsAsync(2.7);

        _mockCleanupExecutor
            .Setup(e => e.RunBuiltInCleanupAsync(It.IsAny<IProgress<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(OperationResult.Ok("Cleanup complete."));

        var lines = new ConcurrentQueue<string>();
        var progress = new Progress<string>(line => lines.Enqueue(line));
        var service = CreateService();

        var result = await service.RunAsync("localhost\\SQLEXPRESS", progress, CancellationToken.None).ConfigureAwait(false);

        var snapshot = lines.ToArray();

        Assert.True(result.Success);
        Assert.Contains(snapshot, line => line.Contains("[Step 1/1]", StringComparison.Ordinal));
        Assert.DoesNotContain(snapshot, line => line.Contains("/6", StringComparison.Ordinal));
    }

    [Fact]
    public async Task RunAsync_ReturnsFailure_WhenBuiltInCleanupFails()
    {
        _mockSql
            .Setup(s => s.GetDatabaseSizeAsync(It.IsAny<string>(), "SUSDB", It.IsAny<CancellationToken>()))
            .ReturnsAsync(-1);

        _mockCleanupExecutor
            .Setup(e => e.RunBuiltInCleanupAsync(It.IsAny<IProgress<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(OperationResult.Fail("WSUS cleanup failed."));

        var service = CreateService();
        var result = await service.RunAsync("localhost\\SQLEXPRESS", new Progress<string>(_ => { }), CancellationToken.None).ConfigureAwait(false);

        Assert.False(result.Success);
        Assert.Contains("WSUS cleanup failed", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RunAsync_ReportsDatabaseSizeDelta_WhenAvailable()
    {
        _mockSql
            .SetupSequence(s => s.GetDatabaseSizeAsync(It.IsAny<string>(), "SUSDB", It.IsAny<CancellationToken>()))
            .ReturnsAsync(3.0)
            .ReturnsAsync(2.5);

        _mockCleanupExecutor
            .Setup(e => e.RunBuiltInCleanupAsync(It.IsAny<IProgress<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(OperationResult.Ok("Cleanup complete."));

        var lines = new List<string>();
        var progress = new Progress<string>(line => lines.Add(line));
        var service = CreateService();

        var result = await service.RunAsync("localhost\\SQLEXPRESS", progress, CancellationToken.None).ConfigureAwait(false);

        Assert.True(result.Success);
        Assert.Contains(lines, line => line.Contains("Database size (allocated): 3.00 GB -> 2.50 GB", StringComparison.Ordinal));
    }

    [Fact]
    public async Task RunAsync_Continues_WhenDatabaseSizeTelemetryFails()
    {
        _mockSql
            .Setup(s => s.GetDatabaseSizeAsync(It.IsAny<string>(), "SUSDB", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("SQL unavailable"));

        _mockCleanupExecutor
            .Setup(e => e.RunBuiltInCleanupAsync(It.IsAny<IProgress<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(OperationResult.Ok("Cleanup complete."));

        var service = CreateService();
        var result = await service.RunAsync("localhost\\SQLEXPRESS", new Progress<string>(_ => { }), CancellationToken.None).ConfigureAwait(false);

        Assert.True(result.Success);
    }

    [Fact]
    public async Task RunAsync_ReturnsCancelled_WhenCancelled()
    {
        _mockSql
            .Setup(s => s.GetDatabaseSizeAsync(It.IsAny<string>(), "SUSDB", It.IsAny<CancellationToken>()))
            .ReturnsAsync(-1);

        _mockCleanupExecutor
            .Setup(e => e.RunBuiltInCleanupAsync(It.IsAny<IProgress<string>>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException());

        var service = CreateService();
        var result = await service.RunAsync("localhost\\SQLEXPRESS", new Progress<string>(_ => { }), CancellationToken.None).ConfigureAwait(false);

        Assert.False(result.Success);
        Assert.Contains("cancelled", result.Message, StringComparison.OrdinalIgnoreCase);
    }
}
