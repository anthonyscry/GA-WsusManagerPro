namespace WsusManager.Core.Services.Interfaces;

/// <summary>
/// Writes per-operation transcript output to disk.
/// </summary>
public interface IOperationTranscriptService
{
    /// <summary>
    /// Gets the current transcript file path, or null when no operation is active.
    /// </summary>
    string? CurrentTranscriptPath { get; }

    /// <summary>
    /// Starts a new operation transcript file and returns the full path.
    /// If a transcript is already active, it is closed first.
    /// </summary>
    string StartOperation(string operationName);

    /// <summary>
    /// Appends a line to the active operation transcript.
    /// No-op when no operation transcript is active.
    /// </summary>
    void WriteLine(string line);

    /// <summary>
    /// Ends the current operation transcript.
    /// </summary>
    void EndOperation();
}
