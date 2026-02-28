using Moq;
using WsusManager.Core.Infrastructure;
using WsusManager.Core.Logging;
using WsusManager.Core.Models;
using WsusManager.Core.Services;
using WsusManager.Core.Services.Interfaces;

namespace WsusManager.Tests.Integration;

public class MigrationParityTests
{
    [Fact]
    public async Task NativePathSuccess_DoesNotEmitFallbackMarker()
    {
        var mockLog = new Mock<ILogService>();
        var mockRunner = new Mock<IProcessRunner>(MockBehavior.Strict);
        var mockNative = new Mock<INativeInstallationService>();

        mockNative
            .Setup(n => n.InstallAsync(It.IsAny<InstallOptions>(), It.IsAny<IProgress<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(NativeInstallationResult.Ok("Native install success."));

        var lines = new List<string>();
        var progress = new Progress<string>(line => lines.Add(line));

        var service = new InstallationService(
            mockRunner.Object,
            mockLog.Object,
            mockNative.Object,
            @"C:\WSUS\Scripts\Install-WsusWithSqlExpress.ps1");

        var result = await service.InstallAsync(new InstallOptions
        {
            InstallerPath = @"C:\WSUS\SQLDB",
            SaUsername = "sa",
            SaPassword = "ValidPassword1!@#"
        }, progress, CancellationToken.None).ConfigureAwait(false);

        Assert.True(result.Success);
        Assert.DoesNotContain(lines, l => l.Contains("[FALLBACK]", StringComparison.Ordinal));
        mockRunner.Verify(r => r.RunAsync(
            "powershell.exe",
            It.IsAny<string>(),
            It.IsAny<IProgress<string>?>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }
}
