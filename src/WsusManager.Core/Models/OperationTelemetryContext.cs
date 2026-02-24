using System;

namespace WsusManager.Core.Models;

/// <summary>
/// Encapsulates per-operation telemetry metadata used for structured logging.
/// </summary>
public sealed record OperationTelemetryContext(
    Guid OperationId,
    string OperationName,
    DateTimeOffset StartedAtUtc)
{
    /// <summary>Initializes a new instance of the <see cref="OperationTelemetryContext"/> class.</summary>
    public OperationTelemetryContext(string operationName)
        : this(Guid.NewGuid(), operationName, DateTimeOffset.UtcNow)
    {
    }

    /// <summary>
    /// Duration since this operation started.
    /// </summary>
    public TimeSpan Elapsed => DateTimeOffset.UtcNow - StartedAtUtc;
}
