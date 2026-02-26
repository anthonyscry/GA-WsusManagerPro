# GPO Wrapper Script Generation Design

Date: 2026-02-25
Status: Approved
Owner: WSUS Manager team

## Objective

Enhance the existing Create GPO workflow to generate a ready-to-run PowerShell wrapper script for Domain Controller admins, so they can run one command directly on the DC without manually building the WSUS URL command line.

## Scope

1. Extend Create GPO input UX to capture WSUS hostname, HTTP port, and HTTPS port.
2. Apply defaults when ports are not selected/entered: 8530 (HTTP), 8531 (HTTPS).
3. Keep current GPO file copy behavior to `C:\WSUS\WSUS GPO`.
4. Generate a wrapper script in the output folder that runs `Set-WsusGroupPolicy.ps1` with prefilled server values.
5. Update instructions text/dialog to show how to run the wrapper script in HTTP or HTTPS mode.

## Non-Goals

- Remote execution from WSUS server to Domain Controller.
- Credential-prompt flow for Domain Admin from WSUS server.
- Rewriting the core DomainController script logic beyond parameterized invocation.

## User-Validated Requirements

- Wrapper script format is selected (not full standalone regeneration).
- Workflow remains DC-local execution (recommended by user).
- No credential prompt from WSUS server for DC execution path.
- If run on WSUS server/non-DC, wrapper should fail fast with clear guidance.
- Ports default to `8530` and `8531` when not explicitly selected.

## Approaches Considered

### A) Generated Wrapper Script Next to Copied GPO Files (Selected)

- Generate a small script (for example `Run-WsusGpoSetup.ps1`) into `C:\WSUS\WSUS GPO`.
- Wrapper references local `Set-WsusGroupPolicy.ps1` and injects selected hostname/ports.
- Wrapper supports optional `-UseHttps` to switch URL scheme/port.

Pros: Best operator UX, low duplication, keeps source-of-truth logic in one place.
Cons: Adds one generated artifact to manage.

### B) Directly Extend Set-WsusGroupPolicy Entry UX Only

- No generated wrapper; only update instructions and parameters in existing script.

Pros: Fewer files.
Cons: More manual operator steps and copy/paste risk.

### C) Instructions-Only Command Text

- Keep everything as text in dialog; no output script file.

Pros: Minimal engineering effort.
Cons: Does not satisfy run-directly convenience requirement.

## Selected Design

### 1) UI Input Model

Create GPO input dialog collects:

- `WSUS Hostname`
- `HTTP Port`
- `HTTPS Port`

Validation/default behavior:

- Hostname: required (trimmed; cancel/no value aborts operation).
- HTTP Port: if empty/invalid/out of range, use `8530`.
- HTTPS Port: if empty/invalid/out of range, use `8531`.

### 2) Service Contract and Data Flow

`MainViewModel.RunCreateGpo` passes hostname + both ports to `IGpoDeploymentService`.

`GpoDeploymentService` sequence:

1. Locate `DomainController` source.
2. Copy files recursively to `C:\WSUS\WSUS GPO`.
3. Generate wrapper script in destination folder with selected values.
4. Build instructions text including both HTTP and HTTPS run examples.
5. Return operation success only if copy + script generation complete.

### 3) Wrapper Script Runtime Behavior

Wrapper characteristics:

- `#Requires -RunAsAdministrator`
- Parameters: `[switch]$UseHttps`
- Embedded values:
  - `$WsusHostname = "<hostname>"`
  - `$HttpPort = <httpPort>`
  - `$HttpsPort = <httpsPort>`
- URL selection:
  - default: `http://<hostname>:<httpPort>`
  - with `-UseHttps`: `https://<hostname>:<httpsPort>`
- Safety checks:
  - verify script is running on a Domain Controller (`Get-ADDomainController`)
  - verify `Set-WsusGroupPolicy.ps1` exists in same directory
  - fail fast with actionable error when checks fail
- Execution:
  - run `Set-WsusGroupPolicy.ps1 -WsusServerUrl <computedUrl> -BackupPath ".\WSUS GPOs"`
  - propagate non-zero exit/failure status

### 4) Instructions Dialog Content

Instruction text must include:

- Destination folder path.
- Generated wrapper path.
- HTTP usage example:
  - `powershell -ExecutionPolicy Bypass -File .\Run-WsusGpoSetup.ps1`
- HTTPS usage example:
  - `powershell -ExecutionPolicy Bypass -File .\Run-WsusGpoSetup.ps1 -UseHttps`
- Reminder that script is intended to run on the DC as administrator.

### 5) Error Handling

- Source directory missing: existing failure behavior retained.
- Copy succeeds but wrapper generation fails: operation returns failure; message explains rerun needed.
- Invalid/blank ports: silently normalized to defaults rather than hard failure.
- Running generated wrapper on non-DC: wrapper exits with clear guidance.

## Testing Strategy

1. Unit tests in `src/WsusManager.Tests/Services/GpoDeploymentServiceTests.cs`:
   - instruction text includes wrapper and both run modes.
   - wrapper generation contains hostname, HTTP port, HTTPS port, and `-UseHttps` branch.
   - default fallback behavior for invalid/blank ports (`8530`/`8531`).
2. ViewModel tests for Create GPO dialog value mapping/defaulting (if covered in existing test patterns).
3. Manual smoke:
   - run Create GPO with blank ports; confirm generated wrapper uses defaults.
   - run wrapper on DC in HTTP and HTTPS mode.
   - run wrapper on non-DC host and verify fast-fail messaging.

## Acceptance Criteria

1. Create GPO captures hostname, HTTP port, and HTTPS port.
2. Unselected/invalid ports default to `8530`/`8531`.
3. Create GPO outputs a runnable wrapper script in `C:\WSUS\WSUS GPO`.
4. Wrapper supports HTTP default and `-UseHttps` mode.
5. Wrapper clearly rejects non-DC execution.
6. Instructions dialog provides copyable HTTP/HTTPS commands for DC admins.
