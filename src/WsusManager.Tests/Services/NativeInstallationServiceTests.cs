using Moq;
using WsusManager.Core.Logging;
using WsusManager.Core.Models;
using WsusManager.Core.Services;

namespace WsusManager.Tests.Services;

public class NativeInstallationServiceTests
{
    private readonly Mock<ILogService> _mockLog = new();

    [Fact]
    public async Task InstallAsync_Fails_WhenInstallerPathMissing()
    {
        var service = new NativeInstallationService(_mockLog.Object);
        var result = await service.InstallAsync(new InstallOptions
        {
            InstallerPath = string.Empty,
            SaPassword = "ValidPassword1!@#"
        }).ConfigureAwait(false);

        Assert.False(result.Success);
        Assert.Contains("Installer path", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task InstallAsync_Fails_WhenNativePathUnavailable()
    {
        var service = new NativeInstallationService(_mockLog.Object);
        var messages = new List<string>();
        var progress = new Progress<string>(m => messages.Add(m));

        var result = await service.InstallAsync(new InstallOptions
        {
            InstallerPath = @"C:\WSUS\SQLDB",
            SaPassword = "ValidPassword1!@#"
        }, progress).ConfigureAwait(false);

        Assert.False(result.Success);

        if (OperatingSystem.IsWindows())
        {
            Assert.Contains(messages, m => m.Contains("[NATIVE]", StringComparison.Ordinal));
            Assert.Contains("not yet implemented", result.Message, StringComparison.OrdinalIgnoreCase);
            Assert.IsType<NotSupportedException>(result.Exception);
        }
        else
        {
            Assert.Contains("requires Windows", result.Message, StringComparison.OrdinalIgnoreCase);
        }
    }
}
