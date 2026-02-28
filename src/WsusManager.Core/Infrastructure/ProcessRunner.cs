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
    private readonly ISettingsService? _settingsService;

    internal ProcessStartInfo? LastStartInfoSnapshot { get; private set; }

    public ProcessRunner(ILogService logService, ISettingsService? settingsService = null)
    {
        _logService = logService;
        _settingsService = settingsService;
    }

    public Task<ProcessResult> RunAsync(
        string executable,
        string arguments,
        IProgress<string>? progress = null,
        CancellationToken ct = default,
        bool enableLiveTerminal = false)
    {
        return RunAsync(executable, arguments, progress, ct, enableLiveTerminal, null);
    }

    public async Task<ProcessResult> RunAsync(
        string executable,
        string arguments,
        IProgress<string>? progress,
        CancellationToken ct,
        bool enableLiveTerminal,
        IReadOnlyDictionary<string, string?>? environmentVariables)
    {
        _logService.Debug("Running: {Executable} [arguments hidden]", executable);

        var globalLiveTerminal = _settingsService?.Current.LiveTerminalMode ?? false;
        var requiresChildEnvironment = environmentVariables is { Count: > 0 };
        var liveTerminalMode = enableLiveTerminal && globalLiveTerminal;
        if (requiresChildEnvironment && liveTerminalMode)
        {
            liveTerminalMode = false;
            progress?.Report("[INFO] Live Terminal mode disabled for this operation because scoped environment variables are required.");
        }

        if (liveTerminalMode)
        {
            progress?.Report("[INFO] Live Terminal mode enabled for this operation.");
        }

        var startInfo = new ProcessStartInfo(executable, arguments)
        {
            RedirectStandardOutput = !liveTerminalMode,
            RedirectStandardError = !liveTerminalMode,
            UseShellExecute = liveTerminalMode,
            CreateNoWindow = !liveTerminalMode
        };

        if (requiresChildEnvironment)
        {
            foreach (var variable in environmentVariables!)
            {
                if (string.IsNullOrWhiteSpace(variable.Key))
                {
                    continue;
                }

                if (variable.Value is null)
                {
                    startInfo.Environment.Remove(variable.Key);
                }
                else
                {
                    startInfo.Environment[variable.Key] = variable.Value;
                }
            }
        }

        LastStartInfoSnapshot = new ProcessStartInfo(startInfo.FileName, startInfo.Arguments)
        {
            RedirectStandardOutput = startInfo.RedirectStandardOutput,
            RedirectStandardError = startInfo.RedirectStandardError,
            UseShellExecute = startInfo.UseShellExecute,
            CreateNoWindow = startInfo.CreateNoWindow
        };

        using var proc = new Process
        {
            StartInfo = startInfo
        };

        var outputLines = new List<string>();
        var outputLock = new object();

        if (!liveTerminalMode)
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
        if (!liveTerminalMode)
        {
            proc.BeginOutputReadLine();
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
