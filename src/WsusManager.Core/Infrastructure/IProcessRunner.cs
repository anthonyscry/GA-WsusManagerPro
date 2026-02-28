using WsusManager.Core.Models;

namespace WsusManager.Core.Infrastructure;

/// <summary>
/// Interface for external process execution. All shell commands (wsusutil,
/// netsh, sc, schtasks, etc.) go through this interface for testability.
/// </summary>
public interface IProcessRunner
{
    /// <summary>
    /// Runs an external process with captured stdout/stderr.
    /// </summary>
    /// <param name="executable">Path to the executable.</param>
    /// <param name="arguments">Command-line arguments.</param>
    /// <param name="progress">Optional progress reporter for real-time output.</param>
    /// <param name="ct">Cancellation token — kills the process on cancellation.</param>
    /// <returns>Process result with exit code and captured output.</returns>
    Task<ProcessResult> RunAsync(
        string executable,
        string arguments,
        IProgress<string>? progress = null,
        CancellationToken ct = default);

    /// <summary>
    /// Runs an external process with default captured mode, but allows the caller to
    /// opt in to visible terminal execution when settings permit it.
    /// </summary>
    /// <param name="executable">Path to the executable.</param>
    /// <param name="arguments">Command-line arguments.</param>
    /// <param name="allowVisibleTerminal">
    /// When true, ProcessRunner may use visible terminal mode if AppSettings.LiveTerminalMode is enabled.
    /// When false, captured hidden mode is always used.
    /// </param>
    /// <param name="progress">Optional progress reporter for real-time output in captured mode.</param>
    /// <param name="ct">Cancellation token — kills the process on cancellation.</param>
    /// <returns>Process result with exit code and captured output when captured mode is used.</returns>
    Task<ProcessResult> RunAsync(
        string executable,
        string arguments,
        bool allowVisibleTerminal,
        IProgress<string>? progress = null,
        CancellationToken ct = default);

    /// <summary>
    /// Runs an external process in a visible terminal window. Output is not captured,
    /// so this is only appropriate for callers that do not parse stdout/stderr.
    /// </summary>
    /// <param name="executable">Path to the executable.</param>
    /// <param name="arguments">Command-line arguments.</param>
    /// <param name="ct">Cancellation token — kills the process on cancellation.</param>
    /// <returns>Process result with exit code and no captured output.</returns>
    Task<ProcessResult> RunVisibleAsync(
        string executable,
        string arguments,
        CancellationToken ct = default);
}
