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
    public async Task ConfigureHttpsAsync_WhenServerNameBlank_ShouldFailWithoutFallback()
    {
        var fallbackScriptPath = Path.Combine(_tempDirectory, "Set-WsusHttps.ps1");
        File.WriteAllText(fallbackScriptPath, "# mock fallback script");

        var fallback = new LegacyHttpsConfigurationFallback(
            _processRunner.Object,
            _logService.Object,
            fallbackScriptPath);
        var sut = new HttpsConfigurationService(_processRunner.Object, fallback, _logService.Object);

        var result = await sut.ConfigureHttpsAsync("   ", "00112233445566778899AABBCCDDEEFF00112233").ConfigureAwait(false);

        Assert.False(result.Success);
        Assert.Contains("server name", result.Message, StringComparison.OrdinalIgnoreCase);

        _processRunner.Verify(r => r.RunAsync(
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<IProgress<string>>(),
            It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Theory]
    [InlineData("wsus\"; whoami")]
    [InlineData("wsus && whoami")]
    [InlineData("wsus|powershell")]
    [InlineData("wsus$(calc)")]
    public async Task ConfigureHttpsAsync_WhenServerNameContainsUnsafeCharacters_ShouldFailWithoutProcessExecution(string serverName)
    {
        var fallbackScriptPath = Path.Combine(_tempDirectory, "Set-WsusHttps.ps1");
        File.WriteAllText(fallbackScriptPath, "# mock fallback script");

        var fallback = new LegacyHttpsConfigurationFallback(
            _processRunner.Object,
            _logService.Object,
            fallbackScriptPath);
        var sut = new HttpsConfigurationService(_processRunner.Object, fallback, _logService.Object);

        var result = await sut.ConfigureHttpsAsync(serverName, "00112233445566778899AABBCCDDEEFF00112233").ConfigureAwait(false);

        Assert.False(result.Success);
        Assert.Contains("server name", result.Message, StringComparison.OrdinalIgnoreCase);

        _processRunner.Verify(r => r.RunAsync(
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<IProgress<string>>(),
            It.IsAny<CancellationToken>()),
            Times.Never);
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

        var result = await sut.ConfigureHttpsAsync("wsus-server01", "00112233445566778899AABBCCDDEEFF00112233", progress).ConfigureAwait(false);

        Assert.True(result.Success);
        Assert.DoesNotContain(progressMessages, m => m.Contains("[FALLBACK]", StringComparison.Ordinal));

        _processRunner.Verify(r => r.RunAsync(
            "powershell.exe",
            It.IsAny<string>(),
            It.IsAny<IProgress<string>>(),
            It.IsAny<CancellationToken>()),
            Times.Never);

        _processRunner.Verify(r => r.RunAsync(
            It.IsAny<string>(),
            It.Is<string>(a => a.Contains("configuressl wsus-server01", StringComparison.OrdinalIgnoreCase)),
            It.IsAny<IProgress<string>>(),
            It.IsAny<CancellationToken>()),
            Times.Once);
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

        var result = await sut.ConfigureHttpsAsync("wsus-server01", "00112233445566778899AABBCCDDEEFF00112233", progress).ConfigureAwait(false);

        Assert.True(result.Success);
        Assert.Contains(progressMessages, m => m.Contains("[FALLBACK]", StringComparison.Ordinal));

        _processRunner.Verify(r => r.RunAsync(
            "powershell.exe",
            It.IsAny<string>(),
            It.IsAny<IProgress<string>>(),
            It.IsAny<CancellationToken>()),
            Times.Once);

        _processRunner.Verify(r => r.RunAsync(
            "powershell.exe",
            It.Is<string>(a => a.Contains("-ServerName \"wsus-server01\"", StringComparison.Ordinal)),
            It.IsAny<IProgress<string>>(),
            It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ConfigureHttpsAsync_WhenThumbprintBlank_ShouldFailWithoutFallback()
    {
        var fallbackScriptPath = Path.Combine(_tempDirectory, "Set-WsusHttps.ps1");
        File.WriteAllText(fallbackScriptPath, "# mock fallback script");

        var fallback = new LegacyHttpsConfigurationFallback(
            _processRunner.Object,
            _logService.Object,
            fallbackScriptPath);
        var sut = new HttpsConfigurationService(_processRunner.Object, fallback, _logService.Object);

        var progressMessages = new List<string>();
        var progress = new Progress<string>(m => progressMessages.Add(m));

        var result = await sut.ConfigureHttpsAsync("wsus-server01", "   ", progress).ConfigureAwait(false);

        Assert.False(result.Success);
        Assert.Contains("thumbprint", result.Message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(progressMessages, m => m.Contains("[FALLBACK]", StringComparison.Ordinal));

        _processRunner.Verify(r => r.RunAsync(
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<IProgress<string>>(),
            It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ConfigureHttpsAsync_WhenThumbprintMalformed_ShouldFailWithoutFallback()
    {
        var fallbackScriptPath = Path.Combine(_tempDirectory, "Set-WsusHttps.ps1");
        File.WriteAllText(fallbackScriptPath, "# mock fallback script");

        var fallback = new LegacyHttpsConfigurationFallback(
            _processRunner.Object,
            _logService.Object,
            fallbackScriptPath);
        var sut = new HttpsConfigurationService(_processRunner.Object, fallback, _logService.Object);

        var result = await sut.ConfigureHttpsAsync("wsus-server01", "ZZ11").ConfigureAwait(false);

        Assert.False(result.Success);
        Assert.Contains("hexadecimal", result.Message, StringComparison.OrdinalIgnoreCase);

        _processRunner.Verify(r => r.RunAsync(
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<IProgress<string>>(),
            It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ConfigureHttpsAsync_WhenFallbackUsed_ShouldForwardNormalizedThumbprint()
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

        var rawThumbprint = "00 11 22 33 44 55 66 77 88 99 aa bb cc dd ee ff 00 11 22 33";
        var result = await sut.ConfigureHttpsAsync("wsus-server01", rawThumbprint).ConfigureAwait(false);

        Assert.True(result.Success);

        _processRunner.Verify(r => r.RunAsync(
            "powershell.exe",
            It.Is<string>(a =>
                a.Contains("00112233445566778899AABBCCDDEEFF00112233", StringComparison.Ordinal) &&
                a.Contains("-ServerName \"wsus-server01\"", StringComparison.Ordinal)),
            It.IsAny<IProgress<string>>(),
            It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ConfigureHttpsAsync_WhenFallbackFails_ShouldIncludeOutputSummaryInFailureMessage()
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
            .ReturnsAsync(new ProcessResult(5, ["Native failed"]));

        _processRunner
            .Setup(r => r.RunAsync(
                "powershell.exe",
                It.IsAny<string>(),
                It.IsAny<IProgress<string>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProcessResult(1, ["Legacy failed at binding step"]));

        var fallback = new LegacyHttpsConfigurationFallback(
            _processRunner.Object,
            _logService.Object,
            fallbackScriptPath);
        var sut = new HttpsConfigurationService(_processRunner.Object, fallback, _logService.Object);

        var result = await sut.ConfigureHttpsAsync("wsus-server01", "00112233445566778899AABBCCDDEEFF00112233").ConfigureAwait(false);

        Assert.False(result.Success);
        Assert.Contains("Output:", result.Message, StringComparison.Ordinal);
        Assert.Contains("Legacy failed at binding step", result.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ConfigureHttpsAsync_WhenNativeFailsAndFallbackScriptMissing_ShouldReturnScriptNotFound()
    {
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
            .ReturnsAsync(new ProcessResult(5, ["Native failed"]));

        var fallback = new LegacyHttpsConfigurationFallback(_processRunner.Object, _logService.Object);
        var sut = new HttpsConfigurationService(_processRunner.Object, fallback, _logService.Object);

        var result = await sut.ConfigureHttpsAsync("wsus-server01", "00112233445566778899AABBCCDDEEFF00112233").ConfigureAwait(false);

        Assert.False(result.Success);
        Assert.Contains("script not found", result.Message, StringComparison.OrdinalIgnoreCase);

        _processRunner.Verify(r => r.RunAsync(
            "powershell.exe",
            It.IsAny<string>(),
            It.IsAny<IProgress<string>>(),
            It.IsAny<CancellationToken>()),
            Times.Never);
    }
}
