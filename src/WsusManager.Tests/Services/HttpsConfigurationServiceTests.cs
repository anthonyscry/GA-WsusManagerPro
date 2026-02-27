using Moq;
using WsusManager.Core.Infrastructure;
using WsusManager.Core.Logging;
using WsusManager.Core.Models;
using WsusManager.Core.Services;

namespace WsusManager.Tests.Services;

public class HttpsConfigurationServiceTests
{
    [Fact]
    public async Task ConfigureAsync_WhenNativePathSucceeds_DoesNotUseFallback()
    {
        var mockLog = new Mock<ILogService>();
        var mockRunner = new Mock<IProcessRunner>(MockBehavior.Strict);
        var fallback = new LegacyHttpsConfigurationFallback(
            mockRunner.Object,
            mockLog.Object,
            () => @"C:\WSUS\Scripts\Set-WsusHttps.ps1");

        var progressLines = new List<string>();
        var progress = new Progress<string>(line => progressLines.Add(line));

        var service = new HttpsConfigurationService(
            fallback,
            mockLog.Object,
            null,
            (_, _, _, _) => Task.FromResult(OperationResult.Ok("Native HTTPS configured.")));

        var result = await service.ConfigureAsync(
            "wsus.contoso.local",
            "THUMBPRINT",
            progress,
            CancellationToken.None);

        Assert.True(result.Success);
        Assert.DoesNotContain(progressLines, l => l.Contains("[FALLBACK]", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ConfigureAsync_WhenNativePathFails_UsesFallback()
    {
        var mockLog = new Mock<ILogService>();
        var mockRunner = new Mock<IProcessRunner>();
        mockRunner
            .Setup(r => r.RunAsync(
                "powershell.exe",
                It.Is<string>(a => a.Contains("Set-WsusHttps.ps1", StringComparison.Ordinal) && a.Contains("THUMBPRINT", StringComparison.Ordinal)),
                It.IsAny<IProgress<string>>(),
                It.IsAny<CancellationToken>(), It.IsAny<bool>()))
            .ReturnsAsync(new ProcessResult(0, []));

        var fallback = new LegacyHttpsConfigurationFallback(
            mockRunner.Object,
            mockLog.Object,
            () => @"C:\WSUS\Scripts\Set-WsusHttps.ps1");

        var progressLines = new List<string>();
        var progress = new Progress<string>(line => progressLines.Add(line));

        var service = new HttpsConfigurationService(
            fallback,
            mockLog.Object,
            null,
            (_, _, _, _) => Task.FromResult(OperationResult.Fail("Native failed.")));

        var result = await service.ConfigureAsync(
            "wsus.contoso.local",
            "THUMBPRINT",
            progress,
            CancellationToken.None);

        Assert.True(result.Success);
        Assert.Contains(progressLines, l => l.Contains("[FALLBACK]", StringComparison.Ordinal));
        mockRunner.Verify(r => r.RunAsync(
            "powershell.exe",
            It.IsAny<string>(),
            It.IsAny<IProgress<string>>(),
            It.IsAny<CancellationToken>(), It.IsAny<bool>()), Times.Once);
    }

    [Fact]
    public async Task ConfigureAsync_WhenInputInvalid_DoesNotInvokeFallback()
    {
        var mockLog = new Mock<ILogService>();
        var mockRunner = new Mock<IProcessRunner>(MockBehavior.Strict);
        var fallback = new LegacyHttpsConfigurationFallback(
            mockRunner.Object,
            mockLog.Object,
            () => @"C:\WSUS\Scripts\Set-WsusHttps.ps1");

        var nativeCalled = false;
        var service = new HttpsConfigurationService(
            fallback,
            mockLog.Object,
            null,
            (_, _, _, _) =>
            {
                nativeCalled = true;
                return Task.FromResult(OperationResult.Ok("Should not run native path."));
            });

        var result = await service.ConfigureAsync(
            string.Empty,
            "THUMBPRINT",
            null,
            CancellationToken.None);

        Assert.False(result.Success);
        Assert.False(nativeCalled);
        mockRunner.Verify(r => r.RunAsync(
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<IProgress<string>>(),
            It.IsAny<CancellationToken>(), It.IsAny<bool>()), Times.Never);
    }

    [Fact]
    public void LegacyFallback_GetSearchPaths_DoesNotInclude_CurrentDirectory_Candidates()
    {
        var fallback = new LegacyHttpsConfigurationFallback(
            new Mock<IProcessRunner>().Object,
            new Mock<ILogService>().Object,
            () => null);

        var originalCurrentDirectory = Directory.GetCurrentDirectory();
        var tempCurrentDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempCurrentDirectory);

        try
        {
            Directory.SetCurrentDirectory(tempCurrentDirectory);
            var paths = fallback.GetSearchPaths();

            Assert.DoesNotContain(Path.Combine(tempCurrentDirectory, "Scripts", "Set-WsusHttps.ps1"), paths);
            Assert.DoesNotContain(Path.Combine(tempCurrentDirectory, "Set-WsusHttps.ps1"), paths);
        }
        finally
        {
            Directory.SetCurrentDirectory(originalCurrentDirectory);
            Directory.Delete(tempCurrentDirectory, true);
        }
    }

    [Fact]
    public void LegacyFallback_GetSearchPaths_Includes_AppDirectory_Parent_Candidates()
    {
        var fallback = new LegacyHttpsConfigurationFallback(
            new Mock<IProcessRunner>().Object,
            new Mock<ILogService>().Object,
            () => null);

        var paths = fallback.GetSearchPaths();
        var appDir = AppContext.BaseDirectory;
        var parent = new DirectoryInfo(appDir).Parent;

        Assert.NotNull(parent);
        Assert.Contains(Path.Combine(parent!.FullName, "Scripts", "Set-WsusHttps.ps1"), paths);
        Assert.Contains(Path.Combine(parent.FullName, "Set-WsusHttps.ps1"), paths);
    }
}
