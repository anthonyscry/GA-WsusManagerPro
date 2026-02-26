namespace WsusManager.Core.Models;

public record FleetWsusTargetAuditReport
{
    public IReadOnlyList<FleetWsusTargetAuditItem> Items { get; init; } = [];

    public int TotalHosts { get; init; }

    public int CompliantHosts { get; init; }

    public int MismatchHosts { get; init; }

    public int UnreachableHosts { get; init; }

    public int ErrorHosts { get; init; }

    public IReadOnlyDictionary<string, int> GroupedTargets { get; init; } =
        new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
}
