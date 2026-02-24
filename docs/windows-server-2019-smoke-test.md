# Windows Server 2019 Smoke Test (Cutover Acceptance)

## Scope

Validate the C#-first cutover behavior for:
- Set HTTPS operation
- Live Terminal mode on/off behavior
- Operation telemetry and transcript output
- Fallback markers on happy paths

## Preconditions

- Target host: Windows Server 2019 with admin access
- WSUS + SQL Express installed
- Build under test: latest `WsusManager.App` publish output
- Known test certificate thumbprint (for HTTPS test)

## Runbook

1. Launch the app as Administrator.
2. Open **Settings** and confirm these defaults are enabled:
   - `Enable install fallback`
   - `Enable HTTPS fallback`
   - `Enable cleanup fallback`
3. In **Settings**, turn **Live Terminal mode** off.
4. Run `Set HTTPS` from sidebar with valid server + thumbprint.
5. Verify operation completes successfully and no `[FALLBACK]` marker appears in log output.
6. Open `%ProgramData%\WSUS\Logs` equivalent configured log directory (`C:\WSUS\Logs` by default):
   - verify transcript file exists under `Transcripts\Set_HTTPS_<guid>.log` (name may vary by sanitization)
   - verify operation id appears in app log around start/finish.
7. In **Settings**, turn **Live Terminal mode** on.
8. Execute a process-backed operation (for example, Online Sync or Schedule Task create/delete in test scope).
9. Verify terminal behavior is visible and operation still succeeds.
10. Re-run `Set HTTPS` with valid input and confirm happy path still has no `[FALLBACK]` marker.

## Expected Results

- No failures on happy-path operations.
- No `[FALLBACK]` marker for native-success paths.
- Transcript files are created and contain operation lines.
- Live Terminal on/off changes process execution behavior without breaking operations.

## Evidence Capture

- Screenshot of successful `Set HTTPS` completion.
- Screenshot of log panel showing no `[FALLBACK]` marker.
- Path + snippet from transcript file.
- Path + snippet from app log showing operation id start/finish.

## Pass/Fail

- Pass: all expected results met and evidence captured.
- Fail: any operation fails or fallback marker appears on native-success path.
