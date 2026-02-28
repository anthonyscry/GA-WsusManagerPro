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

    internal ProcessStartInfo CreateStartInfo(string executable, string arguments, bool useVisibleTerminal = false)
    {
        return new ProcessStartInfo(executable, arguments)
        {
            RedirectStandardOutput = !useVisibleTerminal,
            RedirectStandardError = !useVisibleTerminal,
            UseShellExecute = useVisibleTerminal,
            CreateNoWindow = !useVisibleTerminal
        };
    }

    public async Task<ProcessResult> RunAsync(
        string executable,
        string arguments,
        IProgress<string>? progress = null,
        CancellationToken ct = default)
    {
        return await RunAsync(executable, arguments, allowVisibleTerminal: false, progress, ct).ConfigureAwait(false);
    }

    public async Task<ProcessResult> RunAsync(
        string executable,
        string arguments,
        bool allowVisibleTerminal,
        IProgress<string>? progress = null,
        CancellationToken ct = default)
    {
        var useVisibleTerminal = allowVisibleTerminal && _settingsService.Current.LiveTerminalMode;
        return await RunCoreAsync(executable, arguments, progress, useVisibleTerminal, ct).ConfigureAwait(false);
    }

    public async Task<ProcessResult> RunVisibleAsync(
        string executable,
        string arguments,
        CancellationToken ct = default)
    {
        _logService.Debug("Visible terminal execution requested (setting: {LiveTerminalMode})", _settingsService.Current.LiveTerminalMode);
        return await RunAsync(executable, arguments, allowVisibleTerminal: true, progress: null, ct).ConfigureAwait(false);
    }

    private async Task<ProcessResult> RunCoreAsync(
        string executable,
        string arguments,
        IProgress<string>? progress,
        bool useVisibleTerminal,
        CancellationToken ct)
    {
        _logService.Debug("Running: {Executable} [arguments hidden]", executable);

        using var proc = new Process
        {
            StartInfo = CreateStartInfo(executable, arguments, useVisibleTerminal)
        };

        var outputLines = new List<string>();
        var outputLock = new object();

        if (!useVisibleTerminal)
        {
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
        }

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
