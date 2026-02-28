namespace WsusManager.Core.Models;

/// <summary>
/// Holds telemetry data for a single operation execution.
/// </summary>
public sealed record OperationTelemetryContext
{
    private readonly long _startTimestamp;

    private OperationTelemetryContext(Guid operationId, string operationName, DateTime startedAtUtc, long startTimestamp)
    {
        OperationId = operationId;
        OperationName = operationName;
        StartedAtUtc = startedAtUtc;
        _startTimestamp = startTimestamp;
    }

    public Guid OperationId { get; }

    public string OperationName { get; }

    public DateTime StartedAtUtc { get; }

    public static OperationTelemetryContext Start(string operationName)
    {
        return new OperationTelemetryContext(
            Guid.NewGuid(),
            operationName,
            DateTime.UtcNow,
            System.Diagnostics.Stopwatch.GetTimestamp());
    }

    public TimeSpan GetElapsed()
    {
        return System.Diagnostics.Stopwatch.GetElapsedTime(_startTimestamp);
    }
}
