# Diagnostics + Fleet WSUS Target Audit Design

Date: 2026-02-25
Status: Approved
Owner: WSUS Manager team

## Objective

Expand Diagnostics and Auto-Fix coverage to include GPO/WSUS policy visibility, and add a fleet-wide audit that shows which WSUS server each client is currently pointed to.

## User-Approved Direction

- Approach selected: split into two coordinated features.
- Client scope selected: audit all WSUS clients from WSUS inventory.
- Keep server diagnostics and fleet audit as separate operations.
- Keep fleet behavior audit-first in v1 (no bulk client auto-remediation).

## Scope

1. Extend server-side `Run Diagnostics` with GPO baseline checks and clear pass/fail/warn output.
2. Add new Client Tools operation: `Fleet WSUS Target Audit`.
3. Collect current client WSUS settings (`WUServer`, `WUStatusServer`, `UseWUServer`) for all WSUS clients.
4. Summarize fleet compliance/mismatch/unreachable/error counts and grouped WSUS target URLs.
5. Document how to interpret results and plan remediation safely.

## Non-Goals (v1)

- Automatic bulk registry/GPO rewrite across all clients.
- Replacing existing single-host client diagnostics.
- Merging server diagnostics and fleet audit into one long-running button.

## Existing Architecture Context

- Server diagnostics pipeline is in `IHealthService` and `HealthService` and currently runs 12 checks with optional auto-repair for safe local issues.
- Client remote operations are in `IClientService` and `ClientService` and already parse remote diagnostics output (`WSUS=...;STATUS=...;USE=...`).
- WSUS computer inventory is available via `IWsusServerService.GetComputersAsync(...)` (exposed through dashboard loading).
- UI has a dedicated Diagnostics panel and Client Tools panel in `MainWindow.xaml`, with operation orchestration in `MainViewModel.RunOperationAsync(...)`.

## Proposed Design

### A) Server Diagnostics Expansion (GPO Baseline)

Add one new diagnostics group in `HealthService` with checks that are local/safe on the WSUS server side:

1. **GPO Script Artifact Presence**
   - Validate expected GPO deployment assets exist under `C:\WSUS\WSUS GPO`.
   - Result examples:
     - Pass: required files/folders present.
     - Fail/Warning: required artifacts missing with exact missing list.

2. **Expected WSUS Policy Baseline (reference check)**
   - Validate expected baseline values used by generated client/GPO scripts are internally consistent:
     - URL format sanity.
     - HTTP/HTTPS port defaults (`8530`/`8531`) when values are not specified.
   - This check is informational/warning-oriented unless a hard-invalid configuration is detected.

Auto-fix boundaries for this group:

- Safe local file-system repairs only (for example, create missing folder scaffolding if feasible).
- No domain-level remote policy writes in server diagnostics.

### B) New Client Tools Operation: Fleet WSUS Target Audit

Add `Fleet WSUS Target Audit` command in Client Tools that:

1. Loads all WSUS client hostnames from WSUS inventory.
2. Runs remote diagnostics per host using existing WinRM execution/parsing patterns.
3. Captures per-host values:
   - `WUServer`
   - `WUStatusServer`
   - `UseWUServer`
4. Produces per-host compliance status:
   - `Compliant`
   - `Mismatch`
   - `Unreachable`
   - `Error`
5. Produces aggregate summary:
   - totals by status
   - grouped counts by configured WSUS server URL
   - mismatch reason distribution

### Expected Target Baseline for Comparison

Fleet audit computes expected URLs from user-supplied target host/ports (with defaults):

- Hostname required for expected target comparison.
- HTTP port default: `8530`.
- HTTPS port default: `8531`.

Comparison normalizes URLs (scheme/host/port) before mismatch classification.

## Data Model Additions

Add dedicated models for fleet reporting:

- `FleetWsusTargetAuditItem`
  - `Hostname`
  - `Reachable`
  - `WUServer`
  - `WUStatusServer`
  - `UseWUServer`
  - `ComplianceStatus`
  - `Details`

- `FleetWsusTargetAuditReport`
  - totals (`Total`, `Compliant`, `Mismatch`, `Unreachable`, `Error`)
  - grouped target counts (`Dictionary<string,int>`)
  - per-host results (`IReadOnlyList<FleetWsusTargetAuditItem>`)

## Execution and Performance

- Use bounded concurrency for host diagnostics (target: 10 concurrent hosts, configurable constant).
- Respect existing WinRM timeout/retry settings from `AppSettings`.
- Do not fail the whole fleet operation on individual host failures.
- Keep progress output concise (periodic host completion updates + final summary block).

## UI/UX Changes

1. Client Tools panel:
   - Add `Fleet WSUS Target Audit` button near existing remote/mass operations.
2. Baseline entry for expected target comparison:
   - capture expected WSUS hostname + HTTP/HTTPS ports before audit starts.
   - defaults: `8530`/`8531` if blank/invalid.
3. Output:
   - log final summary and top mismatch categories.
   - list sample mismatch hosts and unreachable hosts.

No destructive auto-remediation button is added in v1.

## Error Handling

- Invalid expected hostname/ports: fail fast with actionable validation message.
- WSUS inventory unavailable: fail operation with guidance to verify WSUS connection.
- Host-level WinRM failure: classify as `Unreachable` and continue.
- Parse failures on host output: classify as `Error` and continue.

## Testing Strategy

1. `HealthServiceTests`
   - verify new GPO baseline checks are included and reported.
   - verify safe auto-fix behavior remains bounded to local-safe actions.
2. `ClientServiceTests`
   - per-host classification logic (compliant/mismatch/unreachable/error).
   - grouped server target summary and totals.
   - URL normalization and default-port comparisons.
3. `MainViewModelTests`
   - command wiring for new fleet audit operation.
   - operation state, cancellation, and progress behavior.
4. `ScriptGeneratorServiceTests` (if script op is added in v1)
   - generated fleet audit script includes WSUS policy reads and summary output.

## Documentation Updates

- Update `wiki/User-Guide.md`:
  - Diagnostics section: include new GPO baseline checks.
  - Client Tools section: include Fleet WSUS Target Audit behavior and status interpretation.

## Acceptance Criteria

1. Diagnostics includes GPO baseline checks with clear output.
2. Client Tools can audit all WSUS clients and report each client's configured WSUS target.
3. Fleet summary includes grouped target URLs and mismatch/unreachable/error counts.
4. Operation remains non-destructive for client fleet in v1.
5. Unit tests cover classification/aggregation and diagnostics check additions.
6. User guide documents the new diagnostics and fleet-audit workflow.
