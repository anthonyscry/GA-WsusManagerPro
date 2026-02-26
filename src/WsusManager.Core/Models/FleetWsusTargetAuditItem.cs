namespace WsusManager.Core.Models;

public enum FleetComplianceStatus
{
    Compliant,
    Mismatch,
    Unreachable,
    Error
}

public record FleetWsusTargetAuditItem
{
    public string Hostname { get; init; } = string.Empty;

    public string? ReportedWsusHostname { get; init; }

    public int? ReportedHttpPort { get; init; }

    public int? ReportedHttpsPort { get; init; }

    public FleetComplianceStatus ComplianceStatus { get; init; }

    public string Message { get; init; } = string.Empty;
}
