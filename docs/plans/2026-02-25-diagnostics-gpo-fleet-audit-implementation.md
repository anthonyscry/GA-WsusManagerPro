# Diagnostics + Fleet WSUS Target Audit Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Add GPO baseline validation to server diagnostics and a new fleet-wide client audit that reports what WSUS target each client is currently configured to use.

**Architecture:** Extend existing `HealthService` and `ClientService` instead of creating parallel subsystems. Keep server diagnostics and fleet audit as separate operations under existing `RunOperationAsync` orchestration in `MainViewModel`. Implement fleet audit as audit-only in v1 (classification + summary), with no bulk remote auto-remediation.

**Tech Stack:** C# (.NET 8), WPF (MVVM), CommunityToolkit.Mvvm, xUnit, Moq.

---

### Task 1: Add fleet audit models and failing contract tests

**Files:**
- Create: `src/WsusManager.Core/Models/FleetWsusTargetAuditItem.cs`
- Create: `src/WsusManager.Core/Models/FleetWsusTargetAuditReport.cs`
- Modify: `src/WsusManager.Core/Services/Interfaces/IClientService.cs`
- Test: `src/WsusManager.Tests/Services/ClientServiceTests.cs`

**Step 1: Write the failing tests**

Add tests that expect:

```csharp
[Fact]
public async Task RunFleetWsusTargetAuditAsync_EmptyHostList_Returns_Fail()
{
    var service = CreateService();
    var result = await service.RunFleetWsusTargetAuditAsync(
        Array.Empty<string>(),
        expectedHostname: "wsus01",
        expectedHttpPort: 8530,
        expectedHttpsPort: 8531,
        progress: new Progress<string>());

    Assert.False(result.Success);
    Assert.Contains("host", result.Message, StringComparison.OrdinalIgnoreCase);
}
```

And a compile-level contract test that `IClientService` exposes this method.

**Step 2: Run test to verify it fails**

Run: `dotnet test src/WsusManager.Tests/WsusManager.Tests.csproj --filter "FullyQualifiedName~RunFleetWsusTargetAuditAsync_EmptyHostList_Returns_Fail"`

Expected: FAIL because method/models do not exist yet.

**Step 3: Write minimal implementation**

Add model types and interface method only (no algorithm yet):

```csharp
public enum FleetComplianceStatus { Compliant, Mismatch, Unreachable, Error }

public record FleetWsusTargetAuditItem(
    string Hostname,
    bool Reachable,
    string? WUServer,
    string? WUStatusServer,
    bool UseWUServer,
    FleetComplianceStatus ComplianceStatus,
    string Details);

public record FleetWsusTargetAuditReport(
    IReadOnlyList<FleetWsusTargetAuditItem> Items,
    int Total,
    int Compliant,
    int Mismatch,
    int Unreachable,
    int Error,
    IReadOnlyDictionary<string, int> GroupedTargets);
```

And in `IClientService`:

```csharp
Task<OperationResult<FleetWsusTargetAuditReport>> RunFleetWsusTargetAuditAsync(
    IReadOnlyList<string> hostnames,
    string expectedHostname,
    int expectedHttpPort,
    int expectedHttpsPort,
    IProgress<string> progress,
    CancellationToken ct = default);
```

**Step 4: Run test to verify it passes**

Run: same command as Step 2.

Expected: PASS (with temporary stub in service returning fail for empty list).

**Step 5: Commit**

```bash
git add src/WsusManager.Core/Models/FleetWsusTargetAuditItem.cs src/WsusManager.Core/Models/FleetWsusTargetAuditReport.cs src/WsusManager.Core/Services/Interfaces/IClientService.cs src/WsusManager.Tests/Services/ClientServiceTests.cs
git commit -m "feat(client): add fleet wsus target audit contracts"
```

### Task 2: Implement fleet audit classification and aggregation in ClientService

**Files:**
- Modify: `src/WsusManager.Core/Services/ClientService.cs`
- Test: `src/WsusManager.Tests/Services/ClientServiceTests.cs`

**Step 1: Write the failing tests**

Add tests for:

1. host marked `Compliant` when `UseWUServer=true` and both URLs match expected.
2. host marked `Mismatch` when URL or `UseWUServer` differs.
3. host marked `Unreachable` when WinRM test fails.
4. grouped target summary counts are correct.

Example test fixture output line:

```csharp
const string line = "WSUS=http://wsus01:8530;STATUS=http://wsus01:8530;USE=1;SVCS=wuauserv=Running;REBOOT=False;LASTCHECKIN=;AGENT=10.0";
```

**Step 2: Run test to verify it fails**

Run: `dotnet test src/WsusManager.Tests/WsusManager.Tests.csproj --filter "FullyQualifiedName~RunFleetWsusTargetAuditAsync"`

Expected: FAIL (method still stub/incomplete).

**Step 3: Write minimal implementation**

In `ClientService` implement `RunFleetWsusTargetAuditAsync` using existing helpers:

```csharp
const int MaxConcurrentHosts = 10;

var normalizedExpectedHttp = NormalizeWsusUrl(expectedHostname, expectedHttpPort, false);
var normalizedExpectedHttps = NormalizeWsusUrl(expectedHostname, expectedHttpsPort, true);

// bounded concurrency with SemaphoreSlim
// per host: EnsureWinRmAvailableAsync -> RunDiagnosticsAsync script path -> classify
// continue on per-host failures
```

Add internal helper methods for URL normalization and compliance evaluation, for example:

```csharp
internal static string NormalizeWsusUrl(string host, int port, bool https) => ...
internal static FleetComplianceStatus ClassifyHost(ClientDiagnosticResult diag, string expectedHttp, string expectedHttps, out string details) => ...
```

**Step 4: Run test to verify it passes**

Run:
- `dotnet test src/WsusManager.Tests/WsusManager.Tests.csproj --filter "FullyQualifiedName~RunFleetWsusTargetAuditAsync"`
- `dotnet test src/WsusManager.Tests/WsusManager.Tests.csproj --filter "FullyQualifiedName~ClientServiceTests"`

Expected: PASS.

**Step 5: Commit**

```bash
git add src/WsusManager.Core/Services/ClientService.cs src/WsusManager.Tests/Services/ClientServiceTests.cs
git commit -m "feat(client): implement fleet wsus target audit classification"
```

### Task 3: Add Fleet WSUS Target Audit command and UI wiring

**Files:**
- Modify: `src/WsusManager.App/ViewModels/MainViewModel.cs`
- Modify: `src/WsusManager.App/Views/MainWindow.xaml`
- Test: `src/WsusManager.Tests/ViewModels/MainViewModelTests.cs`

**Step 1: Write the failing test**

Add a ViewModel test (reflection pattern is acceptable in this test suite) that verifies host+port values are forwarded to `_clientService.RunFleetWsusTargetAuditAsync(...)`.

```csharp
[Fact]
public async Task RunFleetWsusTargetAudit_Forwards_Expected_Target_Values()
{
    // Arrange VM + mocks
    // Set ExpectedWsusHostname="wsus01", ExpectedHttpPort="8530", ExpectedHttpsPort="8531"
    // Act via command invocation
    // Assert mock received expected values
}
```

**Step 2: Run test to verify it fails**

Run (Windows host if needed for ViewModel test execution):

`powershell.exe -NoProfile -Command "Set-Location 'C:\projects\GA-WsusManagerPro'; dotnet test 'src/WsusManager.Tests/WsusManager.Tests.csproj' --filter 'FullyQualifiedName~RunFleetWsusTargetAudit_Forwards_Expected_Target_Values'"`

Expected: FAIL (command/properties not implemented).

**Step 3: Write minimal implementation**

In `MainViewModel`:

- Add observable properties:
  - `ExpectedWsusHostname`
  - `ExpectedWsusHttpPort`
  - `ExpectedWsusHttpsPort`
- Add relay command `RunFleetWsusTargetAudit`:
  - load hostnames from `_dashboardService.GetComputersAsync(ct)`
  - normalize ports with existing `NormalizeWsusPort(...)`
  - call `_clientService.RunFleetWsusTargetAuditAsync(...)`
  - log summary totals and grouped targets

In `MainWindow.xaml`:

- Add baseline input controls (hostname/http/https) and a new button bound to `RunFleetWsusTargetAuditCommand` in Client Tools.

**Step 4: Run test to verify it passes**

Run:
- Windows-host targeted ViewModel test command from Step 2
- `dotnet test src/WsusManager.Tests/WsusManager.Tests.csproj --filter "FullyQualifiedName~MainViewModelTests"` (where runnable)

Expected: new test PASS; any existing baseline failures documented separately.

**Step 5: Commit**

```bash
git add src/WsusManager.App/ViewModels/MainViewModel.cs src/WsusManager.App/Views/MainWindow.xaml src/WsusManager.Tests/ViewModels/MainViewModelTests.cs
git commit -m "feat(ui): add fleet wsus target audit command and controls"
```

### Task 4: Extend HealthService with GPO baseline checks

**Files:**
- Modify: `src/WsusManager.Core/Services/HealthService.cs`
- Test: `src/WsusManager.Tests/Services/HealthServiceTests.cs`

**Step 1: Write the failing tests**

Add tests for:

1. diagnostics now include GPO baseline checks.
2. missing GPO artifacts reported as fail/warn with actionable message.
3. check count updated from 12 to new expected total.

Example assertion:

```csharp
Assert.Contains(report.Checks, c => c.CheckName == "GPO Deployment Artifacts");
```

**Step 2: Run test to verify it fails**

Run: `dotnet test src/WsusManager.Tests/WsusManager.Tests.csproj --filter "FullyQualifiedName~HealthServiceTests"`

Expected: FAIL due old 12-check assumptions and missing new check.

**Step 3: Write minimal implementation**

In `HealthService.RunDiagnosticsAsync(...)` add two checks near the end:

```csharp
await RunCheckAsync(checks, progress, ct, () => CheckGpoArtifactsAsync(ct));
await RunCheckAsync(checks, progress, ct, () => CheckGpoWrapperBaselineAsync(ct));
```

Implement helpers that validate presence/consistency for:
- `C:\WSUS\WSUS GPO\Set-WsusGroupPolicy.ps1`
- `C:\WSUS\WSUS GPO\Run-WsusGpoSetup.ps1`
- `C:\WSUS\WSUS GPO\WSUS GPOs`

Return clear `DiagnosticCheckResult` values; only perform safe local fixes (for example create missing `WSUS GPOs` folder).

**Step 4: Run test to verify it passes**

Run: same command as Step 2.

Expected: PASS with updated count and check names.

**Step 5: Commit**

```bash
git add src/WsusManager.Core/Services/HealthService.cs src/WsusManager.Tests/Services/HealthServiceTests.cs
git commit -m "feat(diagnostics): add gpo baseline checks to health pipeline"
```

### Task 5: Update docs for diagnostics and fleet audit

**Files:**
- Modify: `wiki/User-Guide.md`

**Step 1: Write doc expectation test (optional) or checklist**

Use a small manual checklist in PR notes if no docs tests exist:
- Diagnostics lists GPO baseline checks.
- Client Tools lists Fleet WSUS Target Audit and status meanings.

**Step 2: Verify baseline doc search**

Run: `dotnet test src/WsusManager.Tests/WsusManager.Tests.csproj --filter "FullyQualifiedName~Documentation"`

Expected: PASS or no matching tests (acceptable).

**Step 3: Write minimal documentation updates**

Add:

- Diagnostics section: new GPO baseline checks and safe auto-fix boundary.
- Client Tools section: Fleet audit purpose, required inputs, and output interpretation:
  - `Compliant`
  - `Mismatch`
  - `Unreachable`
  - `Error`

**Step 4: Verify docs readability**

Run: manual markdown preview and spot-check command/examples.

Expected: clear operator guidance.

**Step 5: Commit**

```bash
git add wiki/User-Guide.md
git commit -m "docs: describe gpo diagnostics checks and fleet wsus target audit"
```

### Task 6: Full verification and release-readiness check

**Files:**
- Modify: none expected

**Step 1: Run focused service tests**

Run:
- `dotnet test src/WsusManager.Tests/WsusManager.Tests.csproj --filter "FullyQualifiedName~ClientServiceTests"`
- `dotnet test src/WsusManager.Tests/WsusManager.Tests.csproj --filter "FullyQualifiedName~HealthServiceTests"`

Expected: PASS.

**Step 2: Run ViewModel/UI tests where available**

Run:
- `dotnet test src/WsusManager.Tests/WsusManager.Tests.csproj --filter "FullyQualifiedName~MainViewModelTests"`

If Linux-gated, run Windows-host equivalent and capture evidence.

**Step 3: Build app**

Run:
- `dotnet build src/WsusManager.App/WsusManager.App.csproj -p:EnableWindowsTargeting=true`

Expected: build succeeds with 0 errors.

**Step 4: Manual smoke on Windows desktop session**

1. Run Diagnostics and verify new GPO baseline check lines appear.
2. Open Client Tools and run Fleet WSUS Target Audit with expected host/ports.
3. Confirm summary totals and grouped WSUS targets are emitted.
4. Cancel mid-run and verify graceful stop behavior.

**Step 5: Final commit (verification artifact if created)**

```bash
git add docs/plans/2026-02-25-diagnostics-gpo-fleet-audit-implementation.md
git commit -m "docs: add implementation plan for diagnostics gpo fleet audit"
```

---

## Execution Notes

- Required implementation discipline: `@superpowers/test-driven-development`
- Required final evidence gate: `@superpowers/verification-before-completion`
- If a regression appears during execution: `@superpowers/systematic-debugging`
