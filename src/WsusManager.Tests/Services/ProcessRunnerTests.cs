using Moq;
using WsusManager.Core.Infrastructure;
using WsusManager.Core.Logging;
using WsusManager.Core.Models;
using WsusManager.Core.Services.Interfaces;

namespace WsusManager.Tests.Services;

public class ProcessRunnerTests
{
    [Fact]
    public void CreateStartInfo_WhenLiveTerminalModeEnabled_UsesVisibleConsoleSemantics()
    {
        var runner = CreateRunner(liveTerminalMode: true);

        var startInfo = runner.CreateStartInfo("pwsh", "-NoLogo");

        Assert.True(startInfo.UseShellExecute);
        Assert.False(startInfo.RedirectStandardOutput);
        Assert.False(startInfo.RedirectStandardError);
        Assert.False(startInfo.CreateNoWindow);
    }

    [Fact]
    public void CreateStartInfo_WhenLiveTerminalModeDisabled_PreservesHiddenRedirectedMode()
    {
        var runner = CreateRunner(liveTerminalMode: false);

        var startInfo = runner.CreateStartInfo("pwsh", "-NoLogo");

        Assert.False(startInfo.UseShellExecute);
        Assert.True(startInfo.RedirectStandardOutput);
        Assert.True(startInfo.RedirectStandardError);
        Assert.True(startInfo.CreateNoWindow);
    }

    [Fact]
    public async Task RunAsync_Does_Not_Log_Sensitive_Arguments()
    {
        var mockLog = new Mock<ILogService>();
        string? capturedTemplate = null;
        object[]? capturedValues = null;

        mockLog
            .Setup(l => l.Debug(It.IsAny<string>(), It.IsAny<object[]>()))
            .Callback<string, object[]>((template, values) =>
            {
                capturedTemplate = template;
                capturedValues = values;
            });

        var runner = CreateRunner(mockLog.Object, liveTerminalMode: false);

        _ = await Assert.ThrowsAnyAsync<Exception>(() =>
            runner.RunAsync("__not_a_real_executable__", "-SaPassword \"SuperSecret123!\" -Password=\"OtherSecret!\" /RP \"TaskSecret\"")).ConfigureAwait(false);

        Assert.Equal("Running: {Executable} [arguments hidden]", capturedTemplate);
        Assert.NotNull(capturedValues);
        Assert.Single(capturedValues!);
        Assert.Equal("__not_a_real_executable__", capturedValues![0]);
    }

    [Fact]
    public async Task RunAsync_Does_Not_Log_NonSensitive_Arguments_Either()
    {
        var mockLog = new Mock<ILogService>();
        string? capturedTemplate = null;
        object[]? capturedValues = null;

        mockLog
            .Setup(l => l.Debug(It.IsAny<string>(), It.IsAny<object[]>()))
            .Callback<string, object[]>((template, values) =>
            {
                capturedTemplate = template;
                capturedValues = values;
            });

        var runner = CreateRunner(mockLog.Object, liveTerminalMode: false);

        _ = await Assert.ThrowsAnyAsync<Exception>(() =>
            runner.RunAsync("__not_a_real_executable__", "-ExecutionPolicy Bypass -File script.ps1")).ConfigureAwait(false);

        Assert.Equal("Running: {Executable} [arguments hidden]", capturedTemplate);
        Assert.NotNull(capturedValues);
        Assert.Single(capturedValues!);
        Assert.Equal("__not_a_real_executable__", capturedValues![0]);
    }

    private static ProcessRunner CreateRunner(ILogService? logService = null, bool liveTerminalMode = false)
    {
        var mockSettings = new Mock<ISettingsService>();
        mockSettings.SetupGet(s => s.Current).Returns(new AppSettings { LiveTerminalMode = liveTerminalMode });

        return new ProcessRunner(logService ?? Mock.Of<ILogService>(), mockSettings.Object);
    }
}
