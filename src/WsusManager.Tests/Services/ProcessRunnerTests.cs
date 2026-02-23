using Moq;
using WsusManager.Core.Infrastructure;
using WsusManager.Core.Logging;

namespace WsusManager.Tests.Services;

public class ProcessRunnerTests
{
    [Fact]
    public async Task RunAsync_Redacts_SaPassword_In_Debug_Log()
    {
        var mockLog = new Mock<ILogService>();
        string? capturedArgs = null;

        mockLog
            .Setup(l => l.Debug(It.IsAny<string>(), It.IsAny<object[]>()))
            .Callback<string, object[]>((_, values) =>
            {
                if (values.Length >= 2 && values[1] is string args)
                {
                    capturedArgs = args;
                }
            });

        var runner = new ProcessRunner(mockLog.Object);

        _ = await Assert.ThrowsAnyAsync<Exception>(() =>
            runner.RunAsync("__not_a_real_executable__", "-SaPassword \"SuperSecret123!\"")).ConfigureAwait(false);

        Assert.NotNull(capturedArgs);
        Assert.DoesNotContain("SuperSecret123!", capturedArgs, StringComparison.Ordinal);
        Assert.Contains("-SaPassword ***", capturedArgs, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RunAsync_Redacts_Schtasks_RP_Password_In_Debug_Log()
    {
        var mockLog = new Mock<ILogService>();
        string? capturedArgs = null;

        mockLog
            .Setup(l => l.Debug(It.IsAny<string>(), It.IsAny<object[]>()))
            .Callback<string, object[]>((_, values) =>
            {
                if (values.Length >= 2 && values[1] is string args)
                {
                    capturedArgs = args;
                }
            });

        var runner = new ProcessRunner(mockLog.Object);

        _ = await Assert.ThrowsAnyAsync<Exception>(() =>
            runner.RunAsync("__not_a_real_executable__", "/Create /TN \"T\" /RP \"P@ssw0rd!\"")).ConfigureAwait(false);

        Assert.NotNull(capturedArgs);
        Assert.DoesNotContain("P@ssw0rd!", capturedArgs, StringComparison.Ordinal);
        Assert.Contains("/RP ***", capturedArgs, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RunAsync_Redacts_Password_Equals_Syntax_In_Debug_Log()
    {
        var mockLog = new Mock<ILogService>();
        string? capturedArgs = null;

        mockLog
            .Setup(l => l.Debug(It.IsAny<string>(), It.IsAny<object[]>()))
            .Callback<string, object[]>((_, values) =>
            {
                if (values.Length >= 2 && values[1] is string args)
                {
                    capturedArgs = args;
                }
            });

        var runner = new ProcessRunner(mockLog.Object);

        _ = await Assert.ThrowsAnyAsync<Exception>(() =>
            runner.RunAsync("__not_a_real_executable__", "-Password=\"SuperSecret!\"")).ConfigureAwait(false);

        Assert.NotNull(capturedArgs);
        Assert.DoesNotContain("SuperSecret!", capturedArgs, StringComparison.Ordinal);
        Assert.Contains("-Password=***", capturedArgs, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RunAsync_Redacts_Rp_Colon_Syntax_In_Debug_Log()
    {
        var mockLog = new Mock<ILogService>();
        string? capturedArgs = null;

        mockLog
            .Setup(l => l.Debug(It.IsAny<string>(), It.IsAny<object[]>()))
            .Callback<string, object[]>((_, values) =>
            {
                if (values.Length >= 2 && values[1] is string args)
                {
                    capturedArgs = args;
                }
            });

        var runner = new ProcessRunner(mockLog.Object);

        _ = await Assert.ThrowsAnyAsync<Exception>(() =>
            runner.RunAsync("__not_a_real_executable__", "/RP:'P@ssw0rd!' ")).ConfigureAwait(false);

        Assert.NotNull(capturedArgs);
        Assert.DoesNotContain("P@ssw0rd!", capturedArgs, StringComparison.Ordinal);
        Assert.Contains("/RP:***", capturedArgs, StringComparison.Ordinal);
    }
}
