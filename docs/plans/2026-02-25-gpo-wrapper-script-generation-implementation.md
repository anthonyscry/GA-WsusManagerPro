# GPO Wrapper Script Generation Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Add a generated DC-local wrapper `.ps1` to the Create GPO workflow, with hostname + HTTP/HTTPS port support and default ports `8530/8531` when not selected.

**Architecture:** Keep the current Create GPO flow and service boundary, then extend `IGpoDeploymentService` and `GpoDeploymentService` to accept HTTPS port input, normalize invalid ports, generate a wrapper script in `C:\WSUS\WSUS GPO`, and return updated instructions text. Update `MainViewModel.RunCreateGpo` dialog to collect hostname + both ports and pass values into the service.

**Tech Stack:** C# (.NET 8), WPF, CommunityToolkit.Mvvm, xUnit, Moq.

---

### Task 1: Add failing tests for dual-mode GPO instructions

**Files:**
- Modify: `src/WsusManager.Tests/Services/GpoDeploymentServiceTests.cs`
- Modify: `src/WsusManager.Core/Services/GpoDeploymentService.cs`

**Step 1: Write the failing test**

```csharp
[Fact]
public void BuildInstructionText_Includes_Wrapper_Http_And_Https_Commands()
{
    var text = GpoDeploymentService.BuildInstructionText("WSUS01", 8530, 8531);

    Assert.Contains("Run-WsusGpoSetup.ps1", text);
    Assert.Contains("http://WSUS01:8530", text);
    Assert.Contains("https://WSUS01:8531", text);
    Assert.Contains("-UseHttps", text);
}
```

**Step 2: Run test to verify it fails**

Run: `dotnet test src/WsusManager.Tests/WsusManager.Tests.csproj --filter "FullyQualifiedName~BuildInstructionText_Includes_Wrapper_Http_And_Https_Commands"`
Expected: FAIL (current instruction text has only HTTP and no wrapper script commands).

**Step 3: Write minimal implementation**

In `GpoDeploymentService`:

- Update `BuildInstructionText` signature to accept both `httpPort` and `httpsPort`.
- Add wrapper usage lines for both HTTP and HTTPS (`-UseHttps`).
- Keep existing guidance text for DC admin next steps.

Example target snippet:

```csharp
internal static string BuildInstructionText(string wsusHostname, int httpPort = 8530, int httpsPort = 8531)
{
    var httpUrl = $"http://{wsusHostname}:{httpPort}";
    var httpsUrl = $"https://{wsusHostname}:{httpsPort}";
    // include Run-WsusGpoSetup.ps1 command examples for both modes
}
```

**Step 4: Run test to verify it passes**

Run: same command as Step 2.
Expected: PASS.

**Step 5: Commit**

```bash
git add src/WsusManager.Tests/Services/GpoDeploymentServiceTests.cs src/WsusManager.Core/Services/GpoDeploymentService.cs
git commit -m "test: cover dual-mode gpo instruction output"
```

### Task 2: Add failing tests for wrapper script template and port normalization

**Files:**
- Modify: `src/WsusManager.Tests/Services/GpoDeploymentServiceTests.cs`
- Modify: `src/WsusManager.Core/Services/GpoDeploymentService.cs`

**Step 1: Write the failing tests**

```csharp
[Fact]
public void BuildWrapperScriptText_Contains_DcCheck_UseHttps_And_SetWsusInvocation()
{
    var script = GpoDeploymentService.BuildWrapperScriptText("WSUS01", 8530, 8531);

    Assert.Contains("param(", script);
    Assert.Contains("[switch]$UseHttps", script);
    Assert.Contains("Win32_ComputerSystem", script);
    Assert.Contains("Set-WsusGroupPolicy.ps1", script);
    Assert.Contains("-WsusServerUrl", script);
    Assert.Contains("-BackupPath", script);
}

[Theory]
[InlineData(0, 8530, 8530)]
[InlineData(-1, 8531, 8531)]
[InlineData(70000, 8530, 8530)]
[InlineData(8535, 8530, 8535)]
public void NormalizePort_Uses_Default_When_Invalid(int candidate, int fallback, int expected)
{
    var actual = GpoDeploymentService.NormalizePort(candidate, fallback);
    Assert.Equal(expected, actual);
}
```

**Step 2: Run tests to verify they fail**

Run:
- `dotnet test src/WsusManager.Tests/WsusManager.Tests.csproj --filter "FullyQualifiedName~BuildWrapperScriptText_Contains_DcCheck_UseHttps_And_SetWsusInvocation"`
- `dotnet test src/WsusManager.Tests/WsusManager.Tests.csproj --filter "FullyQualifiedName~NormalizePort_Uses_Default_When_Invalid"`

Expected: FAIL (methods not implemented yet).

**Step 3: Write minimal implementation**

In `GpoDeploymentService` add internal helpers:

```csharp
internal static int NormalizePort(int candidate, int fallback) =>
    candidate is >= 1 and <= 65535 ? candidate : fallback;

internal static string BuildWrapperScriptText(string wsusHostname, int httpPort, int httpsPort)
{
    // returns script text with:
    // - #Requires -RunAsAdministrator
    // - [switch]$UseHttps
    // - DC role check via Win32_ComputerSystem.DomainRole >= 4
    // - invocation of Set-WsusGroupPolicy.ps1 with computed URL
}
```

**Step 4: Run tests to verify they pass**

Run: same commands as Step 2.
Expected: PASS.

**Step 5: Commit**

```bash
git add src/WsusManager.Tests/Services/GpoDeploymentServiceTests.cs src/WsusManager.Core/Services/GpoDeploymentService.cs
git commit -m "feat: add gpo wrapper template and port normalization"
```

### Task 3: Add failing test for wrapper file creation and service signature

**Files:**
- Modify: `src/WsusManager.Tests/Services/GpoDeploymentServiceTests.cs`
- Modify: `src/WsusManager.Core/Services/Interfaces/IGpoDeploymentService.cs`
- Modify: `src/WsusManager.Core/Services/GpoDeploymentService.cs`

**Step 1: Write the failing tests**

```csharp
[Fact]
public void WriteWrapperScriptFile_Creates_File_In_Destination()
{
    var dest = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
    try
    {
        Directory.CreateDirectory(dest);
        var path = GpoDeploymentService.WriteWrapperScriptFile(dest, "# test");
        Assert.True(File.Exists(path));
    }
    finally
    {
        if (Directory.Exists(dest)) Directory.Delete(dest, true);
    }
}

[Fact]
public async Task DeployGpoFilesAsync_Accepts_HttpsPort_Parameter()
{
    var service = CreateService();
    var result = await service.DeployGpoFilesAsync("WSUS01", 8530, 8531);
    Assert.False(result.Success); // source missing in test env is expected
}
```

**Step 2: Run tests to verify they fail**

Run:
- `dotnet test src/WsusManager.Tests/WsusManager.Tests.csproj --filter "FullyQualifiedName~WriteWrapperScriptFile_Creates_File_In_Destination"`
- `dotnet test src/WsusManager.Tests/WsusManager.Tests.csproj --filter "FullyQualifiedName~DeployGpoFilesAsync_Accepts_HttpsPort_Parameter"`

Expected: FAIL (missing helper and method signature mismatch).

**Step 3: Write minimal implementation**

1. Update interface signature:

```csharp
Task<OperationResult<string>> DeployGpoFilesAsync(
    string wsusHostname,
    int httpPort = 8530,
    int httpsPort = 8531,
    IProgress<string>? progress = null,
    CancellationToken ct = default);
```

2. In `GpoDeploymentService.DeployGpoFilesAsync(...)`:
- Normalize ports.
- Build wrapper script content.
- Write wrapper file to destination and report path.
- Build instructions with both ports.

3. Add helper:

```csharp
internal static string WriteWrapperScriptFile(string destinationDir, string scriptText)
{
    var path = Path.Combine(destinationDir, "Run-WsusGpoSetup.ps1");
    File.WriteAllText(path, scriptText);
    return path;
}
```

**Step 4: Run tests to verify they pass**

Run: same commands as Step 2.
Expected: PASS.

**Step 5: Commit**

```bash
git add src/WsusManager.Core/Services/Interfaces/IGpoDeploymentService.cs src/WsusManager.Core/Services/GpoDeploymentService.cs src/WsusManager.Tests/Services/GpoDeploymentServiceTests.cs
git commit -m "feat: generate gpo wrapper file during create gpo deployment"
```

### Task 4: Update Create GPO dialog to collect HTTPS port and pass through

**Files:**
- Modify: `src/WsusManager.App/ViewModels/MainViewModel.cs`

**Step 1: Write a failing compile-level expectation in tests**

Update existing call sites in test/mocks to match new interface signature where necessary (compile should currently break after Task 3 if not updated).

**Step 2: Run targeted tests to confirm current failure surface**

Run: `dotnet test src/WsusManager.Tests/WsusManager.Tests.csproj --filter "FullyQualifiedName~MainViewModelTests"`
Expected: FAIL until all `DeployGpoFilesAsync` setups/calls align with new signature.

**Step 3: Write minimal implementation**

In `RunCreateGpo`:

- Add `wsusHttpsPort` local default `8531`.
- Add UI label + text box for HTTPS Port.
- Keep hostname + HTTP fields.
- Parse HTTP/HTTPS values with range check (`1..65535`), fallback to defaults when invalid/blank.
- Update service call:

```csharp
var result = await _gpoDeploymentService
    .DeployGpoFilesAsync(wsusHostname, wsusPort, wsusHttpsPort, progress, ct)
    .ConfigureAwait(false);
```

**Step 4: Run tests to verify pass**

Run: `dotnet test src/WsusManager.Tests/WsusManager.Tests.csproj --filter "FullyQualifiedName~MainViewModelTests"`
Expected: PASS.

**Step 5: Commit**

```bash
git add src/WsusManager.App/ViewModels/MainViewModel.cs src/WsusManager.Tests/ViewModels/MainViewModelTests.cs
git commit -m "feat: add https port input to create gpo flow"
```

### Task 5: Update user documentation for generated wrapper usage

**Files:**
- Modify: `wiki/User-Guide.md`
- Optional Modify: `README-CONFLUENCE.md`

**Step 1: Write failing doc expectation test (optional lightweight guard)**

If project has doc assertions, add/extend them; otherwise proceed with direct doc update and manual verification.

**Step 2: Run existing doc-related checks (if any)**

Run: `dotnet test src/WsusManager.Tests/WsusManager.Tests.csproj --filter "FullyQualifiedName~Documentation"`
Expected: either PASS or "No test matches" (acceptable).

**Step 3: Write minimal implementation**

In `wiki/User-Guide.md` Create GPO section:

- Add hostname/http/https field behavior and defaults.
- Document generated wrapper file in `C:\WSUS\WSUS GPO`.
- Add both commands:
  - `powershell -ExecutionPolicy Bypass -File .\Run-WsusGpoSetup.ps1`
  - `powershell -ExecutionPolicy Bypass -File .\Run-WsusGpoSetup.ps1 -UseHttps`
- Clarify script is intended to run on DC as admin (no WSUS-server credential prompt mode).

**Step 4: Verify documentation renders and reads cleanly**

Run: manual review in editor and quick markdown preview.
Expected: clear, copy/paste-ready commands.

**Step 5: Commit**

```bash
git add wiki/User-Guide.md README-CONFLUENCE.md
git commit -m "docs: document generated dc wrapper script for create gpo"
```

### Task 6: Full verification and smoke run

**Files:**
- Modify: none expected

**Step 1: Run focused service tests**

Run: `dotnet test src/WsusManager.Tests/WsusManager.Tests.csproj --filter "FullyQualifiedName~GpoDeploymentServiceTests"`
Expected: PASS.

**Step 2: Run main ViewModel test suite**

Run: `dotnet test src/WsusManager.Tests/WsusManager.Tests.csproj --filter "FullyQualifiedName~MainViewModelTests"`
Expected: PASS.

**Step 3: Build application**

Run: `dotnet build src/WsusManager.App/WsusManager.App.csproj`
Expected: Build succeeds with 0 errors.

**Step 4: Manual smoke verification**

1. In app, click Create GPO and confirm Hostname/HTTP/HTTPS fields are shown.
2. Leave ports blank or invalid; run and confirm generated wrapper uses `8530/8531`.
3. Confirm `C:\WSUS\WSUS GPO\Run-WsusGpoSetup.ps1` is created.
4. On DC, run wrapper default and `-UseHttps`; verify URL passed to `Set-WsusGroupPolicy.ps1`.
5. On non-DC host, verify wrapper exits with clear guidance.

**Step 5: Commit verification note**

```bash
git add docs/plans/2026-02-25-gpo-wrapper-script-generation-implementation.md
git commit -m "docs: add implementation plan for gpo wrapper script generation"
```
