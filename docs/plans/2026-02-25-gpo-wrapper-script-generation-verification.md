# GPO Wrapper Script Generation - Verification Artifact (2026-02-25)

## Environment

- Date: 2026-02-25
- Workspace under test: `/mnt/c/projects/GA-WsusManagerPro/.worktrees/gpo-wrapper-script-generation`
- Linux host: WSL (OpenCode execution environment)
- Windows host execution path: `powershell.exe` from WSL
- .NET SDK / VSTest observed:
  - Linux test runs: VSTest `17.14.1`
  - Windows host test runs: VSTest `17.11.1`

## MainViewModel Linux/Windows Behavior Notes

### Linux gating evidence (project conditions)

From `src/WsusManager.Tests/WsusManager.Tests.csproj`:

- `UseWPF` is enabled only on Windows (`'$(OS)' == 'Windows_NT'`).
- `WsusManager.App` project reference is included only on Windows.
- On non-Windows, ViewModel tests are excluded from compilation:
  - `<Compile Remove="ViewModels\**" />`

Relevant lines:

- `src/WsusManager.Tests/WsusManager.Tests.csproj:9`
- `src/WsusManager.Tests/WsusManager.Tests.csproj:27`
- `src/WsusManager.Tests/WsusManager.Tests.csproj:32`

### Linux runtime behavior

- Running `MainViewModel`-filtered tests on Linux returns no matching tests (expected from project gating).

## Commands and Results (with Exit Codes)

| # | Scope | Command | Result | Pass/Fail/Skip/Total | Exit Code |
|---|---|---|---|---|---|
| 1 | Windows host (RED, pre-implementation) | `powershell.exe -NoProfile -Command "Set-Location 'C:\projects\GA-WsusManagerPro\.worktrees\gpo-wrapper-script-generation'; dotnet test 'src/WsusManager.Tests/WsusManager.Tests.csproj' --filter 'FullyQualifiedName~CreateGpoDeploymentRequest_Forwards_Host_And_Both_Ports_To_Service'"` | Failed as expected (`Assert.NotNull() Failure: Value is null`, method missing) | `0/1/0/1` | `1` |
| 2 | Windows host (GREEN, post-implementation) | `powershell.exe -NoProfile -Command "Set-Location 'C:\projects\GA-WsusManagerPro\.worktrees\gpo-wrapper-script-generation'; dotnet test 'src/WsusManager.Tests/WsusManager.Tests.csproj' --filter 'FullyQualifiedName~CreateGpoDeploymentRequest_Forwards_Host_And_Both_Ports_To_Service'"` | Passed | `1/0/0/1` | `0` |
| 3 | Linux | `dotnet restore src/WsusManager.Tests/WsusManager.Tests.csproj && dotnet test src/WsusManager.Tests/WsusManager.Tests.csproj --filter "FullyQualifiedName~GpoDeploymentServiceTests"` | Passed | `25/0/0/25` | `0` |
| 4 | Linux | `dotnet test src/WsusManager.Tests/WsusManager.Tests.csproj --filter "FullyQualifiedName~CreateGpoDeploymentRequest_Forwards_Host_And_Both_Ports_To_Service"` | Completed; no matching tests for filter (expected on Linux) | `0/0/0/0` (no matches) | `0` |
| 5 | Windows host | `powershell.exe -NoProfile -Command "Set-Location 'C:\projects\GA-WsusManagerPro\.worktrees\gpo-wrapper-script-generation'; dotnet test 'src/WsusManager.Tests/WsusManager.Tests.csproj' --filter 'FullyQualifiedName~GpoDeploymentServiceTests'"` | Passed | `25/0/0/25` | `0` |
| 6 | Windows host (baseline main) | `powershell.exe -NoProfile -Command "Set-Location 'C:\projects\GA-WsusManagerPro'; dotnet test 'src/WsusManager.Tests/WsusManager.Tests.csproj' --filter 'FullyQualifiedName~MainViewModelTests'"` | Failed on baseline main (pre-existing failures) | `80/10/0/90` | `1` |
| 7 | Windows host | `powershell.exe -NoProfile -Command "Set-Location 'C:\projects\GA-WsusManagerPro\.worktrees\gpo-wrapper-script-generation'; dotnet build 'src/WsusManager.App/WsusManager.App.csproj'"` | Build succeeded, 0 warnings, 0 errors | N/A | `0` |

## Task 6 Blocker Closure Evidence

- **Forwarding evidence (automated):** `CreateGpoDeploymentRequest_Forwards_Host_And_Both_Ports_To_Service` is now green and verifies `IGpoDeploymentService.DeployGpoFilesAsync(...)` receives hostname + HTTP port + HTTPS port.
- **TDD red->green evidence:** Command #1 failed first (missing extraction), command #2 passed after minimal extraction in `MainViewModel`.
- **Baseline comparison evidence:** command #6 shows `MainViewModelTests` failures are present on `main`, supporting that these are pre-existing and not introduced by this feature work.
- **Non-interactive smoke evidence (completed):**
  - Targeted service suite passed on Linux and Windows host (commands #3 and #5).
  - App build passed on Windows host (command #7).
  - Linux `MainViewModel` filter behavior captured as expected compile-time gating (command #4).
- **Artifacts/logs captured in command output:** xUnit failure stack (RED), xUnit pass summaries (GREEN), and successful `dotnet build` output.
