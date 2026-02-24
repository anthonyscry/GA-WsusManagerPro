using System;
using System.Threading;
using System.Threading.Tasks;

namespace WsusManager.Core.Services.Interfaces;

/// <summary>
/// Writes per-operation transcript files that mirror output and can be used for
/// post-run troubleshooting and audit requirements.
/// </summary>
public interface IOperationTranscriptService
{
    /// <summary>
    /// Appends a single line to the transcript for the given operation.
    /// </summary>
    /// <param name="operationId">Operation correlation id.</param>
    /// <param name="operationName">Operation display name.</param>
    /// <param name="line">Line to append.</param>
    /// <param name="ct">Cancellation token.</param>
    Task WriteLineAsync(
        Guid operationId,
        string operationName,
        string line,
        CancellationToken ct);

    /// <summary>
    /// Returns the expected transcript file path for an operation.
    /// </summary>
    string GetTranscriptPath(Guid operationId, string operationName);
}
