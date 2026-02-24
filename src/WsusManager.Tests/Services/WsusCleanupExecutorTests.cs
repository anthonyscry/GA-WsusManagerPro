using Moq;
using WsusManager.Core.Infrastructure;
using WsusManager.Core.Logging;
using WsusManager.Core.Models;
using WsusManager.Core.Services;
using WsusManager.Core.Services.Interfaces;

namespace WsusManager.Tests.Services;

public class WsusCleanupExecutorTests
{
    [Fact]
    public async Task RunBuiltInCleanupAsync_WhenFallbackDisabled_DoesNotInvokeProcessRunner()
    {
        var mockRunner = new Mock<IProcessRunner>(MockBehavior.Strict);
        var mockLog = new Mock<ILogService>();
        var mockSettings = new Mock<ISettingsService>();
        mockSettings.Setup(s => s.Current).Returns(new AppSettings
        {
            EnableLegacyFallbackForCleanup = false
        });

        var executor = new WsusCleanupExecutor(mockRunner.Object, mockLog.Object, mockSettings.Object);
        var result = await executor.RunBuiltInCleanupAsync(new Progress<string>(_ => { }), CancellationToken.None).ConfigureAwait(false);

        Assert.False(result.Success);
        Assert.Contains("disabled", result.Message, StringComparison.OrdinalIgnoreCase);
        mockRunner.Verify(r => r.RunAsync(
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<IProgress<string>>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RunBuiltInCleanupAsync_WhenFallbackEnabled_InvokesProcessRunner()
    {
        var mockRunner = new Mock<IProcessRunner>();
        var mockLog = new Mock<ILogService>();
        var mockSettings = new Mock<ISettingsService>();
        mockSettings.Setup(s => s.Current).Returns(new AppSettings
        {
            EnableLegacyFallbackForCleanup = true
        });

        mockRunner
            .Setup(r => r.RunAsync(
                "powershell.exe",
                It.IsAny<string>(),
                It.IsAny<IProgress<string>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProcessResult(0, []));

        var executor = new WsusCleanupExecutor(mockRunner.Object, mockLog.Object, mockSettings.Object);
        var result = await executor.RunBuiltInCleanupAsync(new Progress<string>(_ => { }), CancellationToken.None).ConfigureAwait(false);

        Assert.True(result.Success);
        mockRunner.Verify(r => r.RunAsync(
            "powershell.exe",
            It.IsAny<string>(),
            It.IsAny<IProgress<string>>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }
}
