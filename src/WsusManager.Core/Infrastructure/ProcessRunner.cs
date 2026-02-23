using System.Diagnostics;
using WsusManager.Core.Logging;
using WsusManager.Core.Models;
using WsusManager.Core.Services.Interfaces;

namespace WsusManager.Core.Infrastructure;

/// <summary>
/// Centralized external process execution. All shell commands flow through here
/// for consistent logging, cancellation, and output capture. On cancellation,
/// the entire process tree is killed to prevent orphan child processes.
/// </summary>
public class ProcessRunner : IProcessRunner
{
    private readonly ILogService _logService;
    private readonly ISettingsService _settingsService;

    public ProcessRunner(ILogService logService, ISettingsService settingsService)
    {
        _logService = logService;
        _settingsService = settingsService;
    }

    internal ProcessStartInfo CreateStartInfo(string executable, string arguments)
    {
        var liveTerminalMode = _settingsService.Current.LiveTerminalMode;

        return new ProcessStartInfo(executable, arguments)
        {
            RedirectStandardOutput = !liveTerminalMode,
            RedirectStandardError = !liveTerminalMode,
            UseShellExecute = liveTerminalMode,
            CreateNoWindow = !liveTerminalMode
        };
    }

    public async Task<ProcessResult> RunAsync(
        string executable,
        string arguments,
        IProgress<string>? progress = null,
        CancellationToken ct = default)
    {
        _logService.Debug("Running: {Executable} [arguments hidden]", executable);

        using var proc = new Process
        {
            StartInfo = CreateStartInfo(executable, arguments)
        };

        var outputLines = new List<string>();
        var outputLock = new object();

        proc.OutputDataReceived += (_, e) =>
        {
            if (e.Data is null) return;
            lock (outputLock)
            {
                outputLines.Add(e.Data);
            }
            progress?.Report(e.Data);
        };

        proc.ErrorDataReceived += (_, e) =>
        {
            if (e.Data is null) return;
            var line = $"[ERR] {e.Data}";
            lock (outputLock)
            {
                outputLines.Add(line);
            }
            progress?.Report(line);
        };

        proc.Start();
        if (proc.StartInfo.RedirectStandardOutput)
        {
            proc.BeginOutputReadLine();
        }

        if (proc.StartInfo.RedirectStandardError)
        {
            proc.BeginErrorReadLine();
        }

        try
        {
            await proc.WaitForExitAsync(ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            _logService.Info("Killing process tree: {Executable} (cancelled)", executable);
            try
            {
                proc.Kill(entireProcessTree: true);
            }
            catch (Exception ex)
            {
                _logService.Warning("Failed to kill process: {Error}", ex.Message);
            }
            throw;
        }

        var result = new ProcessResult(proc.ExitCode, outputLines);

        if (result.Success)
        {
            _logService.Debug("Process completed: {Executable} (exit code {ExitCode})", executable, proc.ExitCode);
        }
        else
        {
            _logService.Warning("Process failed: {Executable} (exit code {ExitCode})", executable, proc.ExitCode);
        }

        return result;
    }
}
