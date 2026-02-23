using Moq;
using WsusManager.Core.Infrastructure;
using WsusManager.Core.Logging;
using WsusManager.Core.Models;
using WsusManager.Core.Services;

namespace WsusManager.Tests.Services;

public sealed class HttpsConfigurationServiceTests : IDisposable
{
    private readonly Mock<IProcessRunner> _processRunner = new();
    private readonly Mock<ILogService> _logService = new();
    private readonly string _tempDirectory;

    public HttpsConfigurationServiceTests()
    {
        _tempDirectory = Path.Combine(Path.GetTempPath(), $"WsusHttpsServiceTests_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDirectory);
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_tempDirectory, recursive: true);
        }
        catch
        {
            // Ignore cleanup failures.
        }
    }

    [Fact]
    public async Task ConfigureHttpsAsync_WhenNativeSucceeds_ShouldNotUseFallback()
    {
        _processRunner
            .Setup(r => r.RunAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<IProgress<string>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProcessResult(0, ["ok"]));

        var fallback = new LegacyHttpsConfigurationFallback(_processRunner.Object, _logService.Object);
        var sut = new HttpsConfigurationService(_processRunner.Object, fallback, _logService.Object);

        var progressMessages = new List<string>();
        var progress = new Progress<string>(m => progressMessages.Add(m));

        var result = await sut.ConfigureHttpsAsync("ABC123", progress).ConfigureAwait(false);

        Assert.True(result.Success);
        Assert.DoesNotContain(progressMessages, m => m.Contains("[FALLBACK]", StringComparison.Ordinal));

        _processRunner.Verify(r => r.RunAsync(
            "powershell.exe",
            It.IsAny<string>(),
            It.IsAny<IProgress<string>>(),
            It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ConfigureHttpsAsync_WhenNativeFails_ShouldUseFallbackAndReportFallbackLine()
    {
        var fallbackScriptPath = Path.Combine(_tempDirectory, "Set-WsusHttps.ps1");
        File.WriteAllText(fallbackScriptPath, "# mock fallback script");

        _processRunner
            .Setup(r => r.RunAsync(
                It.Is<string>(e => !string.Equals(e, "powershell.exe", StringComparison.OrdinalIgnoreCase)),
                It.IsAny<string>(),
                It.IsAny<IProgress<string>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProcessResult(0, ["ok"]));

        _processRunner
            .Setup(r => r.RunAsync(
                "netsh",
                It.Is<string>(a => a.Contains("add sslcert", StringComparison.OrdinalIgnoreCase)),
                It.IsAny<IProgress<string>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProcessResult(5, ["Access denied"]));

        _processRunner
            .Setup(r => r.RunAsync(
                "powershell.exe",
                It.IsAny<string>(),
                It.IsAny<IProgress<string>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProcessResult(0, ["fallback ok"]));

        var fallback = new LegacyHttpsConfigurationFallback(
            _processRunner.Object,
            _logService.Object,
            fallbackScriptPath);
        var sut = new HttpsConfigurationService(_processRunner.Object, fallback, _logService.Object);

        var progressMessages = new List<string>();
        var progress = new Progress<string>(m => progressMessages.Add(m));

        var result = await sut.ConfigureHttpsAsync("ABC123", progress).ConfigureAwait(false);

        Assert.True(result.Success);
        Assert.Contains(progressMessages, m => m.Contains("[FALLBACK]", StringComparison.Ordinal));

        _processRunner.Verify(r => r.RunAsync(
            "powershell.exe",
            It.IsAny<string>(),
            It.IsAny<IProgress<string>>(),
            It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
