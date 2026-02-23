using System.Runtime.InteropServices;
using Moq;
using WsusManager.Core.Infrastructure;
using WsusManager.Core.Logging;
using WsusManager.Core.Models;
using WsusManager.Core.Services.Interfaces;

namespace WsusManager.Tests.Services;

public class ProcessRunnerTests
{
    [Fact]
    public async Task RunAsync_WhenLiveTerminalModeEnabledInSettings_StillCapturesOutput()
    {
        var runner = CreateRunner(liveTerminalMode: true);

        var command = GetEchoCommand("process-runner-capture");

        var result = await runner.RunAsync(command.Executable, command.Arguments).ConfigureAwait(false);

        Assert.True(result.Success);
        Assert.Contains(result.OutputLines, line => line.Contains("process-runner-capture", StringComparison.Ordinal));
    }

    [Fact]
    public async Task RunAsync_WhenOptInAndLiveTerminalEnabled_UsesVisibleModeWithoutCapture()
    {
        var runner = CreateRunner(liveTerminalMode: true);
        var command = GetEchoCommand("process-runner-visible");

        var result = await runner.RunAsync(
            command.Executable,
            command.Arguments,
            allowVisibleTerminal: true).ConfigureAwait(false);

        Assert.True(result.Success);
        Assert.Empty(result.OutputLines);
    }

    [Fact]
    public async Task RunAsync_WhenOptInAndLiveTerminalDisabled_RemainsHiddenAndCapturesOutput()
    {
        var runner = CreateRunner(liveTerminalMode: false);

        var command = GetEchoCommand("process-runner-hidden");

        var result = await runner.RunAsync(
            command.Executable,
            command.Arguments,
            allowVisibleTerminal: true).ConfigureAwait(false);

        Assert.True(result.Success);
        Assert.Contains(result.OutputLines, line => line.Contains("process-runner-hidden", StringComparison.Ordinal));
    }

    [Fact]
    public async Task RunAsync_WhenOptInAndLiveTerminalEnabledAndCancelled_ThrowsOperationCanceledException()
    {
        var runner = CreateRunner(liveTerminalMode: true);
        var command = GetSleepCommand(30);
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(200));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            runner.RunAsync(
                command.Executable,
                command.Arguments,
                allowVisibleTerminal: true,
                ct: cts.Token)).ConfigureAwait(false);
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

    private static (string Executable, string Arguments) GetEchoCommand(string message)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return ("cmd.exe", $"/c echo {message}");
        }

        return ("/bin/sh", $"-c \"printf '{message}\\n'\"");
    }

    private static (string Executable, string Arguments) GetSleepCommand(int seconds)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return ("cmd.exe", $"/c timeout /t {seconds} /nobreak >nul");
        }

        return ("/bin/sh", $"-c \"sleep {seconds}\"");
    }
}
