# Tasks: Fallback Path Hardening

## Phase A: Prep / analysis

### Task A1 - Confirm current fallback behavior and shared gaps
- **Expected files touched:** none (analysis)
- **Verification command:** `dotnet test src/WsusManager.Tests/WsusManager.Tests.csproj --filter "FullyQualifiedName~InstallationServiceTests|FullyQualifiedName~ScheduledTaskServiceTests|FullyQualifiedName~HttpsConfigurationServiceTests" --verbosity minimal`
- **Success criteria:** Baseline tests pass; identified path-resolution gaps are documented and scoped.

## Phase B: Implementation

### Task B1 - Add shared script file search helper
- **Expected files touched:**
  - `src/WsusManager.Core/Infrastructure/ScriptPathLocator.cs` (new)
- **Verification command:** `dotnet build src/WsusManager.sln --configuration Debug --no-restore`
- **Success criteria:** Helper returns deterministic, de-duplicated search paths with bounded parent traversal.

### Task B2 - Wire helper into fallback services
- **Expected files touched:**
  - `src/WsusManager.Core/Services/InstallationService.cs`
  - `src/WsusManager.Core/Services/ScheduledTaskService.cs`
  - `src/WsusManager.Core/Services/LegacyHttpsConfigurationFallback.cs`
- **Verification command:** `dotnet build src/WsusManager.sln --configuration Debug --no-restore`
- **Success criteria:** All three services use shared resolver and include full searched path lists in failure output.

## Phase C: Tests + verification

### Task C1 - Add tests for path search behavior
- **Expected files touched:**
  - `src/WsusManager.Tests/Services/InstallationServiceTests.cs`
  - `src/WsusManager.Tests/Services/ScheduledTaskServiceTests.cs`
  - `src/WsusManager.Tests/Services/HttpsConfigurationServiceTests.cs`
- **Verification command:** `dotnet test src/WsusManager.Tests/WsusManager.Tests.csproj --filter "FullyQualifiedName~InstallationServiceTests|FullyQualifiedName~ScheduledTaskServiceTests|FullyQualifiedName~HttpsConfigurationServiceTests" --verbosity minimal`
- **Success criteria:** New tests assert bounded app-parent candidates are present and current-directory candidates are excluded.

### Task C2 - Add negative test for credential env restoration on install failure
- **Expected files touched:**
  - `src/WsusManager.Tests/Services/InstallationServiceTests.cs`
- **Verification command:** `dotnet test src/WsusManager.Tests/WsusManager.Tests.csproj --filter "FullyQualifiedName~InstallationServiceTests" --verbosity minimal`
- **Success criteria:** Test proves `WSUS_INSTALL_SA_PASSWORD` environment variable is restored when process runner throws.

## Phase D: Docs + cleanup

### Task D1 - Verify publish output still supports fallback assets
- **Expected files touched:** none (verification)
- **Verification command:** `dotnet publish src/WsusManager.App/WsusManager.App.csproj --configuration Debug --output /tmp/wsus-publish-parity --self-contained true --runtime win-x64 --no-restore -p:PublishSingleFile=true -p:IncludeAllContentForSelfExtract=true --verbosity minimal`
- **Success criteria:** Publish output contains `Scripts/` and `Modules/` next to app executable.

### Task D2 - Run targeted regression checks and summarize
- **Expected files touched:** none (verification)
- **Verification command:**
  - `dotnet test src/WsusManager.Tests/WsusManager.Tests.csproj --filter "FullyQualifiedName~AppProjectPackagingTests|FullyQualifiedName~DistributionPackageTests|FullyQualifiedName~InstallationServiceTests|FullyQualifiedName~ScheduledTaskServiceTests|FullyQualifiedName~HttpsConfigurationServiceTests" --verbosity minimal`
- **Success criteria:** All targeted tests pass and results are captured in delivery summary.
