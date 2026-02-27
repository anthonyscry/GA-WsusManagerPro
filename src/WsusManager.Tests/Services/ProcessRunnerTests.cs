using Moq;
using WsusManager.Core.Infrastructure;
using WsusManager.Core.Logging;
using WsusManager.Core.Models;
using WsusManager.Core.Services.Interfaces;

namespace WsusManager.Tests.Services;

public class ProcessRunnerTests
{
    [Fact]
    public async Task RunAsync_WithLiveTerminalOptIn_UsesVisibleProcessStart()
    {
        var mockLog = new Mock<ILogService>();
        var mockSettings = new Mock<ISettingsService>();
        mockSettings.Setup(s => s.Current).Returns(new AppSettings { LiveTerminalMode = true });

        var runner = new ProcessRunner(mockLog.Object, mockSettings.Object);
        var executable = OperatingSystem.IsWindows() ? "cmd.exe" : "/bin/sh";
        var arguments = OperatingSystem.IsWindows() ? "/c exit 0" : "-c \"exit 0\"";

        _ = await runner.RunAsync(executable, arguments, enableLiveTerminal: true).ConfigureAwait(false);

        Assert.NotNull(runner.LastStartInfoSnapshot);
        Assert.False(runner.LastStartInfoSnapshot!.CreateNoWindow);
        Assert.True(runner.LastStartInfoSnapshot.UseShellExecute);
        Assert.False(runner.LastStartInfoSnapshot.RedirectStandardOutput);
        Assert.False(runner.LastStartInfoSnapshot.RedirectStandardError);
    }

    [Fact]
    public async Task RunAsync_LiveTerminalSetting_WithoutOptIn_RemainsCaptured()
    {
        var mockLog = new Mock<ILogService>();
        var mockSettings = new Mock<ISettingsService>();
        mockSettings.Setup(s => s.Current).Returns(new AppSettings { LiveTerminalMode = true });

        var runner = new ProcessRunner(mockLog.Object, mockSettings.Object);
        var executable = OperatingSystem.IsWindows() ? "cmd.exe" : "/bin/sh";
        var arguments = OperatingSystem.IsWindows() ? "/c exit 0" : "-c \"exit 0\"";

        _ = await runner.RunAsync(executable, arguments).ConfigureAwait(false);

        Assert.NotNull(runner.LastStartInfoSnapshot);
        Assert.True(runner.LastStartInfoSnapshot!.CreateNoWindow);
        Assert.False(runner.LastStartInfoSnapshot.UseShellExecute);
        Assert.True(runner.LastStartInfoSnapshot.RedirectStandardOutput);
        Assert.True(runner.LastStartInfoSnapshot.RedirectStandardError);
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

        var runner = new ProcessRunner(mockLog.Object);

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

        var runner = new ProcessRunner(mockLog.Object);

        _ = await Assert.ThrowsAnyAsync<Exception>(() =>
            runner.RunAsync("__not_a_real_executable__", "-ExecutionPolicy Bypass -File script.ps1")).ConfigureAwait(false);

        Assert.Equal("Running: {Executable} [arguments hidden]", capturedTemplate);
        Assert.NotNull(capturedValues);
        Assert.Single(capturedValues!);
        Assert.Equal("__not_a_real_executable__", capturedValues![0]);
    }

    [Fact]
    public async Task RunAsync_DefaultMode_DoesNotEmitLiveTerminalMarker()
    {
        var mockLog = new Mock<ILogService>();
        var mockSettings = new Mock<ISettingsService>();
        mockSettings.Setup(s => s.Current).Returns(new AppSettings { LiveTerminalMode = false });

        var lines = new List<string>();
        var progress = new Progress<string>(line => lines.Add(line));

        var runner = new ProcessRunner(mockLog.Object, mockSettings.Object);
        var executable = OperatingSystem.IsWindows() ? "cmd.exe" : "/bin/sh";
        var arguments = OperatingSystem.IsWindows() ? "/c exit 0" : "-c \"exit 0\"";

        _ = await runner.RunAsync(executable, arguments, progress).ConfigureAwait(false);

        Assert.DoesNotContain(lines, l => l.Contains("Live Terminal mode enabled", StringComparison.Ordinal));
    }

    [Fact]
    public async Task RunAsync_LiveTerminalSettingWithoutOptIn_DoesNotEmitLiveTerminalMarker()
    {
        var mockLog = new Mock<ILogService>();
        var mockSettings = new Mock<ISettingsService>();
        mockSettings.Setup(s => s.Current).Returns(new AppSettings { LiveTerminalMode = true });

        var lines = new List<string>();
        var progress = new Progress<string>(line => lines.Add(line));

        var runner = new ProcessRunner(mockLog.Object, mockSettings.Object);
        var executable = OperatingSystem.IsWindows() ? "cmd.exe" : "/bin/sh";
        var arguments = OperatingSystem.IsWindows() ? "/c exit 0" : "-c \"exit 0\"";

        _ = await runner.RunAsync(executable, arguments, progress).ConfigureAwait(false);

        Assert.DoesNotContain(lines, l => l.Contains("Live Terminal mode enabled", StringComparison.Ordinal));
    }
}
