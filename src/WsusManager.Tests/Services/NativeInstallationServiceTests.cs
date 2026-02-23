using Moq;
using WsusManager.Core.Logging;
using WsusManager.Core.Models;
using WsusManager.Core.Services;

namespace WsusManager.Tests.Services;

public class NativeInstallationServiceTests
{
    private readonly Mock<ILogService> _mockLog = new();

    [Fact]
    public async Task InstallAsync_Returns_Failure_When_Native_Workflow_Is_Not_Implemented()
    {
        var service = new NativeInstallationService(_mockLog.Object);

        var result = await service.InstallAsync(new InstallOptions
        {
            InstallerPath = @"C:\WSUS\SQLDB",
            SaUsername = "sa",
            SaPassword = "ValidPassword1!@#"
        }).ConfigureAwait(false);

        Assert.False(result.Success);
        Assert.Contains("not yet implemented", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task InstallAsync_Reports_Native_Start_Message()
    {
        var service = new NativeInstallationService(_mockLog.Object);
        var progressMessages = new List<string>();
        var progress = new Progress<string>(m => progressMessages.Add(m));

        await service.InstallAsync(new InstallOptions { SaPassword = "ValidPassword1!@#" }, progress).ConfigureAwait(false);

        Assert.Contains(progressMessages, m => m.Contains("native", StringComparison.OrdinalIgnoreCase));
    }
}
