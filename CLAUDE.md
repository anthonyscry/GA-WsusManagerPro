# CLAUDE.md - WSUS Manager

## Project Overview

WSUS Manager is a C# WPF automation suite for Windows Server Update Services (WSUS) with SQL Server Express 2022. Modern GUI for managing WSUS servers, including air-gapped networks. Legacy PowerShell version (v3.8.11) still available.

**Author:** Tony Tran, ISSO, GA-ASI
**Current Version:** 4.5.20 (C# WPF)
**Legacy Version:** 3.8.11 (PowerShell)

## Repository Structure

```
GA-WsusManager/
├── src/
│   ├── WsusManager.Core/           # Core library (.NET 8.0)
│   │   ├── Models/                  # Data models (ComputerInfo, UpdateInfo, AppSettings)
│   │   ├── Services/                # Service implementations + Interfaces/
│   │   ├── Infrastructure/          # Utilities (logging, admin checks)
│   │   └── Logging/                 # Serilog configuration
│   ├── WsusManager.App/            # WPF GUI application (MVVM)
│   │   ├── Views/                   # XAML views (MainWindow, Dialogs)
│   │   ├── ViewModels/              # MainViewModel.cs — all business logic
│   │   └── Services/                # App-specific services (ThemeService)
│   ├── WsusManager.Tests/          # xUnit tests (560+ tests)
│   └── WsusManager.Benchmarks/     # BenchmarkDotNet performance tests
├── Scripts/                         # Legacy PowerShell scripts
├── Modules/                         # Legacy PowerShell modules (11 modules)
├── Tests/                           # Pester unit tests (legacy)
├── DomainController/                # GPO deployment scripts
├── build.ps1                        # Legacy PS build script (PS2EXE)
└── WsusManager.sln
```

## Build & Test (C# — Primary)

```bash
# Build
dotnet build src/WsusManager.App/WsusManager.App.csproj

# Run
dotnet run --project src/WsusManager.App

# Test
dotnet test src/WsusManager.Tests --verbosity normal

# Publish single-file EXE
dotnet publish src/WsusManager.App/WsusManager.App.csproj \
  --configuration Release --output publish --self-contained true \
  --runtime win-x64 -p:PublishSingleFile=true
```

## Build & Test (PowerShell — Legacy)

```powershell
.\build.ps1              # Full build with tests + code review
.\build.ps1 -SkipTests   # Build without tests
.\build.ps1 -TestOnly    # Tests only
Invoke-Pester -Path .\Tests -Output Detailed   # Run Pester tests directly
```

**Version:** Update in `build.ps1` and `Scripts\WsusManagementGui.ps1` (`$script:AppVersion`)

**IMPORTANT:** The PS EXE requires Scripts/ and Modules/ folders co-located. Do not deploy alone.

## Architecture (C# v4.5)

**MVVM with CommunityToolkit.Mvvm:**
- `MainViewModel.cs` — All business logic, commands, observable properties
- `MainWindow.xaml` — UI layout and bindings (no code-behind except constructor)
- `[RelayCommand]` generates ICommand properties; `[ObservableProperty]` generates notify properties
- All operations flow through `RunOperationAsync("Name", async () => { ... })` which handles button disable, log expand, errors, and re-enable

**Key Services (Core):**
- `ICsvExportService` — CSV export with UTF-8 BOM, streaming writes, formula injection prevention
- `ISettingsService` — Settings persistence to `%APPDATA%\WsusManager\settings.json`
- `IThemeService` — Theme management with pre-loaded cache

**Standard Paths:**
- WSUS Content: `C:\WSUS\`
- SQL/SSMS Installers: `C:\WSUS\SQLDB\`
- Logs: `C:\WSUS\Logs\`
- SQL Instance: `localhost\SQLEXPRESS`
- WSUS Ports: 8530 (HTTP), 8531 (HTTPS)

## Code Style

**C#:**
- MVVM pattern — logic in ViewModels, not code-behind
- `async/await` everywhere — no `Dispatcher.Invoke` needed
- Test naming: `{Method}_Should_{Expected}`
- xUnit with `[Theory]`/`[InlineData]` for data-driven tests

**PowerShell (legacy):**
- Approved verbs (Get-, Set-, New-, Invoke-, etc.)
- Prefix WSUS functions with `Wsus` (e.g., `Get-WsusDatabaseSize`)
- Color output: `Write-Success`, `Write-Failure`, `Write-Info`, `Write-WsusWarning`
- Logging: `Write-Log`, `Start-WsusLogging`, `Stop-WsusLogging`
- Modules export functions explicitly via `Export-ModuleMember`

## Security

- **Path Validation:** `Test-ValidPath` and `Test-SafePath` prevent command injection (PS)
- **SQL Injection:** Input validation in database operations
- **CSV Injection:** Escape fields containing `=`, `+`, `-`, `@`
- **Service Status:** Re-query services instead of `Refresh()` (PSCustomObject compat)

## Important Constraints

- **Admin Required:** All scripts and the app require elevated privileges
- **SQL Express 10GB limit:** Dashboard monitors and alerts near limit
- **Air-Gap Support:** Export/import designed for offline networks
- **Service Dependencies:** SQL Server must run before WSUS operations

## Git Workflow

- Main branch: `main`
- Build artifacts go to `dist/` (gitignored)
- Conventional commit messages
- GitHub Actions builds EXE on push/PR and creates releases

## Known GUI Anti-Patterns (PowerShell Legacy)

These are documented issues from the PS GUI. When maintaining legacy code:

1. Show dialogs BEFORE switching panels (avoid blank windows)
2. Suppress function return values (`$null =` or `| Out-Null`)
3. Use `Dispatcher.Invoke()` for UI updates from background threads
4. Pass data via `-MessageData` in event handlers, not `$script:` vars
5. Use `.GetNewClosure()` for click handlers capturing variables
6. Always validate script paths before use
7. Disable all operation buttons during operations
8. Reset `$script:OperationRunning` in ALL exit paths (including error/cancel)
9. Add ESC key handler to all dialogs
10. Use `-Skip` on `Context` blocks (not `Describe`) in Pester 5
11. Use `BeforeDiscovery` for variables that `-Skip` depends on
12. Always start async reading: `BeginOutputReadLine()` / `BeginErrorReadLine()`
13. CLI scripts must support non-interactive mode when called from GUI
14. Update BOTH GUI and CLI when adding new parameters
