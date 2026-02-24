# Pure C# Cutover and Verbose Logging Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Deliver C#-first HTTPS setup, Live Terminal execution, and high-value verbose diagnostics while safely migrating remaining script-backed operations behind fallback gates.

**Architecture:** Introduce operation strategy adapters that run native C# paths first and only use legacy process/script fallback when explicitly allowed. Centralize execution telemetry and per-operation transcripts in Core logging infrastructure. Migrate install, schedule, and deep-cleanup script dependencies incrementally with parity tests and explicit fallback logs.

**Tech Stack:** .NET 8, WPF (MVVM), Serilog, xUnit, Moq, Microsoft.Extensions.DependencyInjection, existing WSUS Core services.

---

Implementation rules for this plan:
- Follow @superpowers:test-driven-development for every behavior change.
- Use @superpowers:systematic-debugging if any test fails unexpectedly.
- Use @superpowers:verification-before-completion before each task-complete claim.
- Request review using @superpowers:requesting-code-review after each major task batch.

### Task 1: Wire Runtime Log Level and Retention From Settings

**Files:**
- Modify: `src/WsusManager.App/Program.cs`
- Modify: `src/WsusManager.Core/Logging/LogService.cs`
- Test: `src/WsusManager.Tests/Services/LogServiceTests.cs`

**Step 1: Write the failing test**

```csharp
[Fact]
public void CreateLogger_RespectsConfiguredLogLevel()
{
    var settings = new AppSettings { LogLevel = LogLevel.Warning, LogRetentionDays = 7, LogMaxFileSizeMb = 5 };
    using var svc = new LogService(Path.GetTempPath(), settings);

    svc.Debug("debug-message-should-not-appear");
    svc.Warning("warning-message-should-appear");
    svc.Flush();

    var content = File.ReadAllText(FindLatestTempLog());
    Assert.DoesNotContain("debug-message-should-not-appear", content, StringComparison.Ordinal);
    Assert.Contains("warning-message-should-appear", content, StringComparison.Ordinal);
}
```

**Step 2: Run test to verify it fails**

Run: `dotnet test src/WsusManager.Tests/WsusManager.Tests.csproj --filter "FullyQualifiedName~LogServiceTests.CreateLogger_RespectsConfiguredLogLevel" --nologo`
Expected: FAIL because constructor/config path does not honor settings.

**Step 3: Write minimal implementation**

```csharp
public LogService(string logDirectory, AppSettings? settings = null)
{
    var level = MapLevel(settings?.LogLevel ?? Models.LogLevel.Info);
    var retainedFiles = Math.Max(settings?.LogRetentionDays ?? 30, 1);
    var fileSizeBytes = (long)Math.Max(settings?.LogMaxFileSizeMb ?? 10, 1) * 1024 * 1024;

    _logger = new LoggerConfiguration()
        .MinimumLevel.Is(level)
        .WriteTo.File(logPath,
            rollingInterval: RollingInterval.Day,
            retainedFileCountLimit: retainedFiles,
            fileSizeLimitBytes: fileSizeBytes,
            shared: true,
            outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] {Message:lj}{NewLine}{Exception}")
        .CreateLogger();
}
```

**Step 4: Run test to verify it passes**

Run: same command as Step 2
Expected: PASS

**Step 5: Commit**

```bash
git add src/WsusManager.App/Program.cs src/WsusManager.Core/Logging/LogService.cs src/WsusManager.Tests/Services/LogServiceTests.cs
git commit -m "feat(logging): honor runtime log level and retention settings"
```

### Task 2: Add Operation-Scoped Telemetry Context

**Files:**
- Create: `src/WsusManager.Core/Models/OperationTelemetryContext.cs`
- Modify: `src/WsusManager.App/ViewModels/MainViewModel.cs`
- Test: `src/WsusManager.Tests/ViewModels/MainViewModelTests.cs`

**Step 1: Write the failing test**

```csharp
[Fact]
public async Task RunOperationAsync_LogsOperationId_OnStartAndFinish()
{
    var vm = CreateViewModel();
    await vm.RunOperationAsync("Diagnostics", async (_, _) => { await Task.CompletedTask; return true; });

    _mockLog.Verify(l => l.Info(It.Is<string>(m => m.Contains("OperationId")), It.IsAny<object[]>()), Times.AtLeast(2));
}
```

**Step 2: Run test to verify it fails**

Run: `dotnet test src/WsusManager.Tests/WsusManager.Tests.csproj --filter "FullyQualifiedName~RunOperationAsync_LogsOperationId_OnStartAndFinish" --nologo`
Expected: FAIL because OperationId is not logged.

**Step 3: Write minimal implementation**

```csharp
var op = new OperationTelemetryContext(Guid.NewGuid(), operationName, DateTimeOffset.UtcNow);
_logService.Info("Operation started {OperationName} OperationId={OperationId}", op.OperationName, op.OperationId);
...
_logService.Info("Operation finished {OperationName} OperationId={OperationId} Success={Success}", operationName, op.OperationId, success);
```

**Step 4: Run test to verify it passes**

Run: same command as Step 2
Expected: PASS

**Step 5: Commit**

```bash
git add src/WsusManager.Core/Models/OperationTelemetryContext.cs src/WsusManager.App/ViewModels/MainViewModel.cs src/WsusManager.Tests/ViewModels/MainViewModelTests.cs
git commit -m "feat(logging): add operation-scoped telemetry context"
```

### Task 3: Add Per-Operation Transcript Writer

**Files:**
- Create: `src/WsusManager.Core/Services/Interfaces/IOperationTranscriptService.cs`
- Create: `src/WsusManager.Core/Services/OperationTranscriptService.cs`
- Modify: `src/WsusManager.App/Program.cs`
- Modify: `src/WsusManager.App/ViewModels/MainViewModel.cs`
- Test: `src/WsusManager.Tests/Services/OperationTranscriptServiceTests.cs`

**Step 1: Write the failing test**

```csharp
[Fact]
public async Task WriteLineAsync_CreatesOperationTranscriptFile()
{
    var service = new OperationTranscriptService(_tempDir);
    var opId = Guid.NewGuid();

    await service.WriteLineAsync(opId, "Diagnostics", "[Step 1/3] Start", CancellationToken.None);
    var file = service.GetTranscriptPath(opId, "Diagnostics");

    Assert.True(File.Exists(file));
    Assert.Contains("[Step 1/3] Start", File.ReadAllText(file), StringComparison.Ordinal);
}
```

**Step 2: Run test to verify it fails**

Run: `dotnet test src/WsusManager.Tests/WsusManager.Tests.csproj --filter "FullyQualifiedName~OperationTranscriptServiceTests" --nologo`
Expected: FAIL because service does not exist.

**Step 3: Write minimal implementation**

```csharp
public sealed class OperationTranscriptService : IOperationTranscriptService
{
    public async Task WriteLineAsync(Guid operationId, string operationName, string line, CancellationToken ct)
    {
        var path = GetTranscriptPath(operationId, operationName);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.AppendAllTextAsync(path, line + Environment.NewLine, ct).ConfigureAwait(false);
    }
}
```

**Step 4: Run test to verify it passes**

Run: same command as Step 2
Expected: PASS

**Step 5: Commit**

```bash
git add src/WsusManager.Core/Services/Interfaces/IOperationTranscriptService.cs src/WsusManager.Core/Services/OperationTranscriptService.cs src/WsusManager.App/Program.cs src/WsusManager.App/ViewModels/MainViewModel.cs src/WsusManager.Tests/Services/OperationTranscriptServiceTests.cs
git commit -m "feat(logging): add per-operation transcript files"
```

### Task 4: Implement Live Terminal Execution Mode in ProcessRunner

**Files:**
- Modify: `src/WsusManager.Core/Infrastructure/ProcessRunner.cs`
- Modify: `src/WsusManager.Core/Services/Interfaces/ISettingsService.cs` (if needed for runtime setting access)
- Modify: `src/WsusManager.App/Program.cs` (DI wiring)
- Test: `src/WsusManager.Tests/Services/ProcessRunnerTests.cs`

**Step 1: Write the failing test**

```csharp
[Fact]
public async Task RunAsync_LiveTerminalMode_UsesVisibleProcessStart()
{
    var settings = CreateSettings(liveTerminalMode: true);
    var runner = CreateRunner(settings);

    await runner.RunAsync("cmd.exe", "/c exit 0", null, CancellationToken.None);

    Assert.True(runner.LastStartInfoSnapshot.CreateNoWindow == false);
}
```

**Step 2: Run test to verify it fails**

Run: `dotnet test src/WsusManager.Tests/WsusManager.Tests.csproj --filter "FullyQualifiedName~ProcessRunnerTests.RunAsync_LiveTerminalMode_UsesVisibleProcessStart" --nologo`
Expected: FAIL because runner always uses hidden-window mode.

**Step 3: Write minimal implementation**

```csharp
var liveTerminal = _settingsService.Current.LiveTerminalMode;
proc.StartInfo.CreateNoWindow = !liveTerminal;
proc.StartInfo.RedirectStandardOutput = !liveTerminal;
proc.StartInfo.RedirectStandardError = !liveTerminal;
if (liveTerminal)
{
    progress?.Report("[INFO] Live Terminal mode enabled for this operation.");
}
```

**Step 4: Run test to verify it passes**

Run: same command as Step 2
Expected: PASS

**Step 5: Commit**

```bash
git add src/WsusManager.Core/Infrastructure/ProcessRunner.cs src/WsusManager.App/Program.cs src/WsusManager.Tests/Services/ProcessRunnerTests.cs
git commit -m "feat(runtime): wire live terminal execution mode"
```

### Task 5: Add Native HTTPS Configuration Service (C#-First + Fallback)

**Files:**
- Create: `src/WsusManager.Core/Services/Interfaces/IHttpsConfigurationService.cs`
- Create: `src/WsusManager.Core/Services/HttpsConfigurationService.cs`
- Create: `src/WsusManager.Core/Services/LegacyHttpsConfigurationFallback.cs`
- Modify: `src/WsusManager.App/Program.cs`
- Test: `src/WsusManager.Tests/Services/HttpsConfigurationServiceTests.cs`

**Step 1: Write the failing test**

```csharp
[Fact]
public async Task ConfigureAsync_WhenNativePathSucceeds_DoesNotUseFallback()
{
    var svc = CreateHttpsService(nativeSucceeds: true);
    var result = await svc.ConfigureAsync("wsus.contoso.local", "THUMBPRINT", _progress, CancellationToken.None);

    Assert.True(result.Success);
    Assert.DoesNotContain(_progressLines, l => l.Contains("[FALLBACK]", StringComparison.Ordinal));
}
```

**Step 2: Run test to verify it fails**

Run: `dotnet test src/WsusManager.Tests/WsusManager.Tests.csproj --filter "FullyQualifiedName~HttpsConfigurationServiceTests" --nologo`
Expected: FAIL because service is missing.

**Step 3: Write minimal implementation**

```csharp
public async Task<OperationResult> ConfigureAsync(string wsusServer, string certThumbprint, IProgress<string>? progress, CancellationToken ct)
{
    var native = await ConfigureNativeAsync(wsusServer, certThumbprint, progress, ct).ConfigureAwait(false);
    if (native.Success) return native;

    progress?.Report("[FALLBACK] Native HTTPS configuration failed; trying legacy adapter...");
    return await _fallback.ConfigureAsync(wsusServer, certThumbprint, progress, ct).ConfigureAwait(false);
}
```

**Step 4: Run test to verify it passes**

Run: same command as Step 2
Expected: PASS

**Step 5: Commit**

```bash
git add src/WsusManager.Core/Services/Interfaces/IHttpsConfigurationService.cs src/WsusManager.Core/Services/HttpsConfigurationService.cs src/WsusManager.Core/Services/LegacyHttpsConfigurationFallback.cs src/WsusManager.App/Program.cs src/WsusManager.Tests/Services/HttpsConfigurationServiceTests.cs
git commit -m "feat(https): add csharp-first HTTPS configuration with fallback"
```

### Task 6: Add Set HTTPS Command to UI

**Files:**
- Modify: `src/WsusManager.App/ViewModels/MainViewModel.cs`
- Modify: `src/WsusManager.App/Views/MainWindow.xaml`
- Create: `src/WsusManager.App/Views/HttpsDialog.xaml`
- Create: `src/WsusManager.App/Views/HttpsDialog.xaml.cs`
- Test: `src/WsusManager.Tests/ViewModels/MainViewModelTests.cs`
- Test: `src/WsusManager.Tests/KeyboardNavigationTests.cs`

**Step 1: Write the failing test**

```csharp
[Fact]
public async Task RunSetHttpsCommand_InvokesHttpsConfigurationService()
{
    var vm = CreateViewModel();
    await vm.RunSetHttpsCommand.ExecuteAsync(null);

    _mockHttps.Verify(s => s.ConfigureAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<IProgress<string>>(), It.IsAny<CancellationToken>()), Times.Once);
}
```

**Step 2: Run test to verify it fails**

Run: `dotnet test src/WsusManager.Tests/WsusManager.Tests.csproj --filter "FullyQualifiedName~RunSetHttpsCommand_InvokesHttpsConfigurationService" --nologo`
Expected: FAIL because command and service dependency are missing.

**Step 3: Write minimal implementation**

```csharp
[RelayCommand(CanExecute = nameof(CanExecuteWsusOperation))]
private async Task RunSetHttps()
{
    await RunOperationAsync("Set HTTPS", async (progress, ct) =>
    {
        var dialog = new HttpsDialog();
        if (dialog.ShowDialog() != true) return false;
        var result = await _httpsConfigurationService.ConfigureAsync(dialog.ServerName, dialog.CertificateThumbprint, progress, ct).ConfigureAwait(false);
        return result.Success;
    }).ConfigureAwait(false);
}
```

**Step 4: Run test to verify it passes**

Run: same command as Step 2
Expected: PASS

**Step 5: Commit**

```bash
git add src/WsusManager.App/ViewModels/MainViewModel.cs src/WsusManager.App/Views/MainWindow.xaml src/WsusManager.App/Views/HttpsDialog.xaml src/WsusManager.App/Views/HttpsDialog.xaml.cs src/WsusManager.Tests/ViewModels/MainViewModelTests.cs src/WsusManager.Tests/KeyboardNavigationTests.cs
git commit -m "feat(ui): add Set HTTPS operation workflow"
```

### Task 7: Migrate Installation to Native C# Orchestrator (With Fallback)

**Files:**
- Create: `src/WsusManager.Core/Services/Interfaces/INativeInstallationService.cs`
- Create: `src/WsusManager.Core/Services/NativeInstallationService.cs`
- Modify: `src/WsusManager.Core/Services/InstallationService.cs`
- Test: `src/WsusManager.Tests/Services/InstallationServiceTests.cs`
- Test: `src/WsusManager.Tests/Services/NativeInstallationServiceTests.cs`

**Step 1: Write the failing test**

```csharp
[Fact]
public async Task InstallAsync_WhenNativePathSucceeds_DoesNotInvokePowershellScript()
{
    var svc = CreateInstallationService(nativeSuccess: true);
    var result = await svc.InstallAsync(CreateValidInstallOptions(), _progress, CancellationToken.None);

    Assert.True(result.Success);
    _mockRunner.Verify(r => r.RunAsync("powershell.exe", It.IsAny<string>(), It.IsAny<IProgress<string>>(), It.IsAny<CancellationToken>()), Times.Never);
}
```

**Step 2: Run test to verify it fails**

Run: `dotnet test src/WsusManager.Tests/WsusManager.Tests.csproj --filter "FullyQualifiedName~InstallAsync_WhenNativePathSucceeds_DoesNotInvokePowershellScript" --nologo`
Expected: FAIL because current install path is script-only.

**Step 3: Write minimal implementation**

```csharp
var native = await _nativeInstallationService.InstallAsync(options, progress, ct).ConfigureAwait(false);
if (native.Success) return native;

progress?.Report("[FALLBACK] Native install failed; using legacy PowerShell install path.");
return await RunLegacyInstallScriptAsync(options, progress, ct).ConfigureAwait(false);
```

**Step 4: Run test to verify it passes**

Run: same command as Step 2
Expected: PASS

**Step 5: Commit**

```bash
git add src/WsusManager.Core/Services/Interfaces/INativeInstallationService.cs src/WsusManager.Core/Services/NativeInstallationService.cs src/WsusManager.Core/Services/InstallationService.cs src/WsusManager.Tests/Services/InstallationServiceTests.cs src/WsusManager.Tests/Services/NativeInstallationServiceTests.cs
git commit -m "feat(install): add csharp-first installation orchestration with fallback"
```

### Task 8: Migrate Scheduled Task Action Builder to Native C# Command Path

**Files:**
- Modify: `src/WsusManager.Core/Services/ScheduledTaskService.cs`
- Create: `src/WsusManager.Core/Services/Interfaces/IMaintenanceCommandBuilder.cs`
- Create: `src/WsusManager.Core/Services/MaintenanceCommandBuilder.cs`
- Test: `src/WsusManager.Tests/Services/ScheduledTaskServiceTests.cs`

**Step 1: Write the failing test**

```csharp
[Fact]
public async Task CreateTaskAsync_UsesNativeMaintenanceCommandBuilder()
{
    var svc = CreateScheduledTaskService();
    await svc.CreateTaskAsync(CreateOptions(), _progress, CancellationToken.None);

    _mockCommandBuilder.Verify(b => b.Build(It.IsAny<ScheduledTaskOptions>()), Times.Once);
}
```

**Step 2: Run test to verify it fails**

Run: `dotnet test src/WsusManager.Tests/WsusManager.Tests.csproj --filter "FullyQualifiedName~ScheduledTaskServiceTests.CreateTaskAsync_UsesNativeMaintenanceCommandBuilder" --nologo`
Expected: FAIL because command string is hardcoded.

**Step 3: Write minimal implementation**

```csharp
var taskAction = _maintenanceCommandBuilder.Build(options);
var args = BuildCreateArguments(options, taskAction);
```

**Step 4: Run test to verify it passes**

Run: same command as Step 2
Expected: PASS

**Step 5: Commit**

```bash
git add src/WsusManager.Core/Services/ScheduledTaskService.cs src/WsusManager.Core/Services/Interfaces/IMaintenanceCommandBuilder.cs src/WsusManager.Core/Services/MaintenanceCommandBuilder.cs src/WsusManager.Tests/Services/ScheduledTaskServiceTests.cs
git commit -m "refactor(schedule): isolate native maintenance command generation"
```

### Task 9: Replace Deep Cleanup Step 1 PowerShell Dependency

**Files:**
- Modify: `src/WsusManager.Core/Services/DeepCleanupService.cs`
- Create: `src/WsusManager.Core/Services/Interfaces/IWsusCleanupExecutor.cs`
- Create: `src/WsusManager.Core/Services/WsusCleanupExecutor.cs`
- Test: `src/WsusManager.Tests/Services/DeepCleanupServiceTests.cs`

**Step 1: Write the failing test**

```csharp
[Fact]
public async Task RunAsync_UsesWsusCleanupExecutor_ForStep1()
{
    var svc = CreateDeepCleanupService();
    await svc.RunAsync(@"localhost\SQLEXPRESS", _progress, CancellationToken.None);

    _mockCleanupExecutor.Verify(e => e.RunBuiltInCleanupAsync(It.IsAny<IProgress<string>>(), It.IsAny<CancellationToken>()), Times.Once);
}
```

**Step 2: Run test to verify it fails**

Run: `dotnet test src/WsusManager.Tests/WsusManager.Tests.csproj --filter "FullyQualifiedName~DeepCleanupServiceTests.RunAsync_UsesWsusCleanupExecutor_ForStep1" --nologo`
Expected: FAIL because Step 1 directly invokes powershell.exe.

**Step 3: Write minimal implementation**

```csharp
var cleanupResult = await _wsusCleanupExecutor.RunBuiltInCleanupAsync(progress, ct).ConfigureAwait(false);
if (!cleanupResult.Success)
{
    progress.Report($"[Step 1/6] WSUS built-in cleanup... warning ({cleanupResult.Message})");
}
```

**Step 4: Run test to verify it passes**

Run: same command as Step 2
Expected: PASS

**Step 5: Commit**

```bash
git add src/WsusManager.Core/Services/DeepCleanupService.cs src/WsusManager.Core/Services/Interfaces/IWsusCleanupExecutor.cs src/WsusManager.Core/Services/WsusCleanupExecutor.cs src/WsusManager.Tests/Services/DeepCleanupServiceTests.cs
git commit -m "feat(cleanup): abstract wsus built-in cleanup executor away from powershell"
```

### Task 10: Add Fallback Controls and Safe-Cutover Defaults

**Files:**
- Modify: `src/WsusManager.Core/Models/AppSettings.cs`
- Modify: `src/WsusManager.Core/Services/SettingsValidationService.cs`
- Modify: `src/WsusManager.App/Views/SettingsDialog.xaml`
- Modify: `src/WsusManager.App/Views/SettingsDialog.xaml.cs`
- Test: `src/WsusManager.Tests/SettingsTests.cs`
- Test: `src/WsusManager.Tests/Foundation/SettingsServiceTests.cs`

**Step 1: Write the failing test**

```csharp
[Fact]
public void AppSettings_Defaults_EnableSafeFallbackFlags()
{
    var settings = new AppSettings();
    Assert.True(settings.EnableLegacyFallbackForInstall);
    Assert.True(settings.EnableLegacyFallbackForHttps);
    Assert.True(settings.EnableLegacyFallbackForCleanup);
}
```

**Step 2: Run test to verify it fails**

Run: `dotnet test src/WsusManager.Tests/WsusManager.Tests.csproj --filter "FullyQualifiedName~AppSettings_Defaults_EnableSafeFallbackFlags" --nologo`
Expected: FAIL because flags do not exist.

**Step 3: Write minimal implementation**

```csharp
[JsonPropertyName("enableLegacyFallbackForInstall")]
public bool EnableLegacyFallbackForInstall { get; set; } = true;

[JsonPropertyName("enableLegacyFallbackForHttps")]
public bool EnableLegacyFallbackForHttps { get; set; } = true;

[JsonPropertyName("enableLegacyFallbackForCleanup")]
public bool EnableLegacyFallbackForCleanup { get; set; } = true;
```

**Step 4: Run test to verify it passes**

Run: same command as Step 2
Expected: PASS

**Step 5: Commit**

```bash
git add src/WsusManager.Core/Models/AppSettings.cs src/WsusManager.Core/Services/SettingsValidationService.cs src/WsusManager.App/Views/SettingsDialog.xaml src/WsusManager.App/Views/SettingsDialog.xaml.cs src/WsusManager.Tests/SettingsTests.cs src/WsusManager.Tests/Foundation/SettingsServiceTests.cs
git commit -m "feat(settings): add safe-cutover fallback controls"
```

### Task 11: Add Parity and Logging Verification Tests

**Files:**
- Create: `src/WsusManager.Tests/Integration/MigrationParityTests.cs`
- Modify: `src/WsusManager.Tests/Services/ProcessRunnerTests.cs`
- Modify: `src/WsusManager.Tests/Services/LogServiceTests.cs`

**Step 1: Write the failing test**

```csharp
[Fact]
public async Task NativePathSuccess_DoesNotEmitFallbackMarker()
{
    var result = await ExecuteNativeHappyPathAsync();
    Assert.True(result.Success);
    Assert.DoesNotContain(result.LogLines, l => l.Contains("[FALLBACK]", StringComparison.Ordinal));
}
```

**Step 2: Run test to verify it fails**

Run: `dotnet test src/WsusManager.Tests/WsusManager.Tests.csproj --filter "FullyQualifiedName~MigrationParityTests" --nologo`
Expected: FAIL because parity test harness is missing.

**Step 3: Write minimal implementation**

```csharp
// Add deterministic test harness using mocked dependencies and captured log sink.
// Validate required fields: OperationId, Step, Outcome, FallbackUsed.
```

**Step 4: Run test to verify it passes**

Run: same command as Step 2
Expected: PASS

**Step 5: Commit**

```bash
git add src/WsusManager.Tests/Integration/MigrationParityTests.cs src/WsusManager.Tests/Services/ProcessRunnerTests.cs src/WsusManager.Tests/Services/LogServiceTests.cs
git commit -m "test: add parity and logging verification coverage for csharp cutover"
```

### Task 12: Final Verification, Docs, and Release Prep

**Files:**
- Modify: `README.md`
- Modify: `CHANGELOG.md`
- Modify: `docs/architecture.md`

**Step 1: Write the failing documentation test/checklist**

```text
Checklist:
- README mentions Set HTTPS and Live Terminal behavior.
- Architecture doc explains C#-first + fallback strategy.
- Changelog includes verbose logging and migration notes.
```

**Step 2: Run verification to show gaps**

Run: `dotnet test src/WsusManager.sln --configuration Release --nologo`
Expected: PASS on code; docs checklist still incomplete until edits are made.

**Step 3: Write minimal documentation updates**

```markdown
## Diagnostics Logging
- Operation IDs
- Transcript files
- Fallback markers
```

**Step 4: Run full verification**

Run: `dotnet test src/WsusManager.sln --configuration Release --nologo && dotnet publish src/WsusManager.App/WsusManager.App.csproj --configuration Release --self-contained true --runtime win-x64 -p:PublishSingleFile=true --nologo`
Expected: all tests pass and publish succeeds.

**Step 5: Commit**

```bash
git add README.md CHANGELOG.md docs/architecture.md
git commit -m "docs: update parity, logging, and csharp cutover guidance"
```

## Final Acceptance Gate

Before shipping:

1. No Critical or Important findings in code review.
2. `dotnet test src/WsusManager.sln --configuration Release --nologo` passes.
3. `dotnet publish src/WsusManager.App/WsusManager.App.csproj --configuration Release --self-contained true --runtime win-x64 -p:PublishSingleFile=true --nologo` passes.
4. At least one manual smoke test on Windows Server 2019 validates:
   - Set HTTPS operation
   - Live Terminal mode on/off behavior
   - Verbose log output usefulness
   - No fallback markers on happy path
