# PR Summary: C#-First Cutover and Verbose Logging

## What Changed

- Added operation-scoped telemetry and per-operation transcript logging.
- Added Live Terminal runtime execution mode in `ProcessRunner`.
- Added C#-first HTTPS configuration workflow with fallback adapter and UI command/dialog integration.
- Added C#-first installation orchestration with explicit legacy fallback behavior.
- Refactored scheduled task action construction into `IMaintenanceCommandBuilder`.
- Refactored Deep Cleanup step 1 into `IWsusCleanupExecutor` abstraction.
- Added safe-cutover fallback settings for install/https/cleanup and wired settings UI controls.
- Added parity and logging tests to validate native happy path and fallback marker behavior.

## Verification Evidence

- `dotnet test src/WsusManager.Tests/WsusManager.Tests.csproj --nologo`
  - Result: pass (`619` tests passed, `0` failed).
- `dotnet test src/WsusManager.sln --configuration Release --nologo`
  - Result: pass (`616` tests passed, `0` failed).
- `dotnet publish src/WsusManager.App/WsusManager.App.csproj --configuration Release --self-contained true --runtime win-x64 -p:PublishSingleFile=true --nologo`
  - Result: publish succeeded.

## Known Notes

- Linux test environment excludes `ViewModels/**` tests in `WsusManager.Tests.csproj` for non-Windows targets; full suite status still green for enabled tests.
- Existing non-blocking test analyzer warnings (`CA1846` in `ColorContrastHelper`) remain and should be handled separately.

## Manual Validation Pending

- Windows Server 2019 smoke test remains pending.
- Checklist and helper script are prepared:
  - `docs/windows-server-2019-smoke-test.md`
  - `Scripts/Invoke-WsusManagerSmokeTest.ps1`
