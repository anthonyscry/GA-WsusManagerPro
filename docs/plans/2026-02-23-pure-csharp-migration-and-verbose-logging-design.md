# Pure C# Migration and Verbose Logging Design

Date: 2026-02-23
Status: Approved
Owner: WSUS Manager team

## Objective

Close remaining parity gaps between the C# app and legacy GA-WsusManager while improving debugging quality. The target is C#-first execution for all user-facing operations, with a safety fallback path during migration to avoid regressions.

## Scope

1. Add native C# HTTPS configuration workflow in the GUI.
2. Wire Live Terminal mode so it changes execution behavior (not just persisted setting).
3. Implement verbose, operation-scoped logging suitable for root-cause debugging.
4. Migrate remaining script-backed operations toward pure C# using safe cutover + fallback.

## Non-Goals

- Build or expand CLI features.
- Remove all fallback code immediately.
- Change existing UX patterns unless required for parity or diagnostics.

## Current Gaps

- No dedicated HTTPS setup flow exposed in C# UI.
- LiveTerminalMode setting is persisted but not fully wired into operation execution.
- Some operations still shell to PowerShell/scripts (install, scheduled maintenance action, deep cleanup step 1).
- Logging settings exist but are not consistently reflected in runtime log detail and trace usefulness.

## Approaches Considered

### A) C#-First with Fallback (Selected)

- Native C# path is attempted first.
- Fallback adapter (existing script/process path) is available during rollout.
- Fallback usage is explicitly logged.

Pros: Lowest risk, preserves operability, supports gradual parity verification.
Cons: Temporary dual-path complexity.

### B) Hard Cutover

- Replace script-backed flows and remove fallbacks immediately.

Pros: Fastest to pure C# end-state.
Cons: Highest regression risk.

### C) Narrow Scope Only (HTTPS + Live Terminal)

- Implement two features now, defer deeper migration.

Pros: Minimal short-term risk.
Cons: Delays pure C# objective.

## Selected Architecture

### 1) Execution Strategy Layer

Introduce a strategy abstraction per migratable operation:

- Primary: native C# implementation
- Secondary: fallback adapter (legacy script/process path)

Execution policy: C# first; fallback only on recoverable failure or explicit override.

### 2) Native HTTPS Configuration

Add `IHttpsConfigurationService` and implementation that:

1. Validates prerequisites (admin rights, WSUS/IIS presence, certificate availability).
2. Applies IIS binding for WSUS site on 8531.
3. Applies WSUS SSL mode/configuration.
4. Verifies endpoint/binding and returns actionable diagnostics.

Design requirement: idempotent re-run.

### 3) Live Terminal Execution Wiring

Add execution mode options in process pipeline:

- Integrated mode: current in-app log behavior.
- Live terminal mode: open visible external terminal and stream operation output there.

`RunOperationAsync` remains central orchestrator for cancellation, status, and outcome handling.

### 4) Verbose Logging Upgrade

Implement operation-scoped structured logs with fields:

- `OperationId`, `OperationName`, `Step`, `DurationMs`, `Outcome`, `FallbackUsed`, `ExceptionType`

Enhance outputs:

- Main rolling log (existing location)
- Per-operation transcript files under `C:\WSUS\Logs\ops\YYYY-MM-DD\`

Log levels:

- `Info`: operator-focused concise trail
- `Debug`: verbose diagnostics (timing checkpoints, detailed step context)
- `Warning` and above: minimal critical-only output

Security: preserve and extend credential redaction in all log channels.

## Migration Order

1. HTTPS workflow in C# (with fallback).
2. Live Terminal mode wiring.
3. Install workflow C#-first (fallback retained).
4. Scheduled task maintenance path C#-first (fallback retained).
5. Deep cleanup step 1 replacement (eliminate PowerShell dependency there).

## Rollout and Safety Gates

For each migrated operation:

1. Unit and integration parity tests pass.
2. Happy path completes without fallback in test environment.
3. Forced failure triggers fallback and logs `[FALLBACK]` trail.
4. Required logging fields are present and redaction checks pass.

Only after all gates pass for an operation:

- make native path default,
- keep fallback behind advanced setting,
- remove fallback in a later hardening release.

## Error Handling Standards

Every operation failure must include:

- summary error,
- root cause and inner exception chain,
- failing step and last successful step,
- remediation hints.

## Acceptance Criteria

1. HTTPS is fully configurable from C# UI without manual script invocation.
2. Live Terminal mode changes runtime behavior and remains cancel-safe.
3. Verbose logging materially improves triage quality and correlation.
4. Targeted script-backed operations execute via C# path by default without regressions.
5. Existing functional workflows remain stable.

## Out-of-Scope Note

CLI parity is intentionally excluded per product direction.
