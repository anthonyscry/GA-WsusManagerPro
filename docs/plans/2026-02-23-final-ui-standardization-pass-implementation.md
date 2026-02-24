# Final UI Standardization Pass Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Deliver a final UI uniformity pass with Script Generator row cleanup, robust error-code lookup UX, complete common error-code coverage, and reliable About-panel GA icon rendering.

**Architecture:** Apply targeted, low-risk changes in existing WPF MVVM layers: shared styles (`SharedStyles.xaml`), panel layout (`MainWindow.xaml`), lookup model (`WsusErrorCodes.cs`), and view-model dialog/theme glue (`MainViewModel.cs`). Use xUnit structural tests and service tests first (RED), then minimal code changes (GREEN), then focused verification. Keep current commands/bindings and avoid broad template refactors.

**Tech Stack:** .NET 8, C# WPF/XAML, CommunityToolkit.Mvvm, xUnit.

---

### Task 1: Script Generator single-row layout and compact width

**Files:**
- Modify: `src/WsusManager.App/Views/MainWindow.xaml`
- Test: `src/WsusManager.Tests/KeyboardNavigationTests.cs`

**Step 1: Write the failing test**

```csharp
[Fact]
public void MainWindow_ScriptGenerator_ShouldRemoveOperationLabel_AndUseSingleRow()
{
    var content = File.ReadAllText(GetXamlPath("MainWindow.xaml"));

    Assert.DoesNotContain("Text=\"Operation:\"", content);
    Assert.Contains("AutomationProperties.AutomationId=\"ScriptOperationComboBox\"", content);
    Assert.Contains("Width=\"260\"", content);
    Assert.Contains("Content=\"Generate Script\"", content);
}
```

**Step 2: Run test to verify it fails**

Run: `dotnet test src/WsusManager.Tests/WsusManager.Tests.csproj --filter "FullyQualifiedName~MainWindow_ScriptGenerator_ShouldRemoveOperationLabel_AndUseSingleRow"`
Expected: FAIL on current label/layout.

**Step 3: Write minimal implementation**

In `src/WsusManager.App/Views/MainWindow.xaml`:
- Remove Script Generator `Operation:` TextBlock.
- Keep dropdown at left (`Width="260"`).
- Place `Generate Script` button to the right in the same Grid row.

**Step 4: Run test to verify it passes**

Run: same command as Step 2.
Expected: PASS.

**Step 5: Commit**

```bash
git add src/WsusManager.App/Views/MainWindow.xaml src/WsusManager.Tests/KeyboardNavigationTests.cs
git commit -m "style: streamline script generator row layout"
```

### Task 2: Error Code lookup supports both select and typing

**Files:**
- Modify: `src/WsusManager.App/Views/MainWindow.xaml`
- Test: `src/WsusManager.Tests/KeyboardNavigationTests.cs`

**Step 1: Write the failing test**

```csharp
[Fact]
public void MainWindow_ErrorCodeLookup_ShouldBeEditableAndSearchable()
{
    var content = File.ReadAllText(GetXamlPath("MainWindow.xaml"));

    Assert.Contains("AutomationProperties.AutomationId=\"ErrorCodeInputTextBox\"", content);
    Assert.Contains("IsEditable=\"True\"", content);
    Assert.Contains("IsTextSearchEnabled=\"True\"", content);
    Assert.Contains("Text=\"{Binding ErrorCodeInput", content);
}
```

**Step 2: Run test to verify it fails**

Run: `dotnet test src/WsusManager.Tests/WsusManager.Tests.csproj --filter "FullyQualifiedName~MainWindow_ErrorCodeLookup_ShouldBeEditableAndSearchable"`
Expected: FAIL until ComboBox supports both modes and text binding.

**Step 3: Write minimal implementation**

In `src/WsusManager.App/Views/MainWindow.xaml` Error Code section:
- Use editable `ComboBox` with:
  - `IsEditable="True"`
  - `IsTextSearchEnabled="True"`
  - `Text="{Binding ErrorCodeInput, UpdateSourceTrigger=PropertyChanged}"`
- Keep `ItemsSource="{Binding CommonErrorCodes}"` and Lookup button command unchanged.

**Step 4: Run test to verify it passes**

Run: same command as Step 2.
Expected: PASS.

**Step 5: Commit**

```bash
git add src/WsusManager.App/Views/MainWindow.xaml src/WsusManager.Tests/KeyboardNavigationTests.cs
git commit -m "fix: restore error lookup typed and dropdown entry"
```

### Task 3: Add missing trailing error code definitions

**Files:**
- Modify: `src/WsusManager.Core/Models/WsusErrorCodes.cs`
- Test: `src/WsusManager.Tests/Services/ClientServiceTests.cs`

**Step 1: Write the failing tests**

```csharp
[Theory]
[InlineData("0x80244022")]
[InlineData("0x80070643")]
public void LookupErrorCode_ShouldRecognize_TrailingCommonCodes(string code)
{
    var service = CreateService();

    var result = service.LookupErrorCode(code);

    Assert.True(result.Success);
    Assert.NotNull(result.Data);
    Assert.Equal(code, result.Data.Code);
}
```

**Step 2: Run test to verify it fails**

Run: `dotnet test src/WsusManager.Tests/WsusManager.Tests.csproj --filter "FullyQualifiedName~LookupErrorCode_ShouldRecognize_TrailingCommonCodes"`
Expected: FAIL for missing dictionary entries.

**Step 3: Write minimal implementation**

In `src/WsusManager.Core/Models/WsusErrorCodes.cs` add missing entries for:
- `80244022` (WSUS service unavailable / HTTP 503 path)
- `80070643` (installer/update installation failure)

Ensure `Code`, `HexCode`, description, and recommended fix follow existing format.

**Step 4: Run test to verify it passes**

Run: same command as Step 2.
Expected: PASS.

**Step 5: Commit**

```bash
git add src/WsusManager.Core/Models/WsusErrorCodes.cs src/WsusManager.Tests/Services/ClientServiceTests.cs
git commit -m "fix: add missing common WSUS error code definitions"
```

### Task 4: Uniform input heights and spacing in Client/Computers/Updates

**Files:**
- Modify: `src/WsusManager.App/Themes/SharedStyles.xaml`
- Modify: `src/WsusManager.App/Views/MainWindow.xaml`
- Test: `src/WsusManager.Tests/KeyboardNavigationTests.cs`

**Step 1: Write the failing test**

```csharp
[Fact]
public void MainWindow_PrimaryFiltersAndClientInputs_ShouldUseUniformStyles()
{
    var content = File.ReadAllText(GetXamlPath("MainWindow.xaml"));

    Assert.Contains("Style=\"{StaticResource UniformInputTextBox}\"", content);
    Assert.Contains("Style=\"{StaticResource UniformInputComboBox}\"", content);
}
```

**Step 2: Run test to verify it fails**

Run: `dotnet test src/WsusManager.Tests/WsusManager.Tests.csproj --filter "FullyQualifiedName~MainWindow_PrimaryFiltersAndClientInputs_ShouldUseUniformStyles"`
Expected: FAIL until style usage is complete.

**Step 3: Write minimal implementation**

In `src/WsusManager.App/Themes/SharedStyles.xaml`:
- Keep/add `UniformInputTextBox` and `UniformInputComboBox` with baseline height `34`.

In `src/WsusManager.App/Views/MainWindow.xaml`:
- Apply uniform styles to key rows in Client Tools, Computers filters, and Updates filters.
- Keep current bindings and automation IDs.

**Step 4: Run test to verify it passes**

Run: same command as Step 2.
Expected: PASS.

**Step 5: Commit**

```bash
git add src/WsusManager.App/Themes/SharedStyles.xaml src/WsusManager.App/Views/MainWindow.xaml src/WsusManager.Tests/KeyboardNavigationTests.cs
git commit -m "style: standardize input heights and spacing across panels"
```

### Task 5: Fix About panel GA icon reliability

**Files:**
- Modify: `src/WsusManager.App/Views/MainWindow.xaml`
- Optional Modify: `src/WsusManager.App/WsusManager.App.csproj`
- Test: `src/WsusManager.Tests/KeyboardNavigationTests.cs`

**Step 1: Write the failing test**

```csharp
[Fact]
public void MainWindow_AboutPanel_ShouldUsePackUriForGaLogo()
{
    var content = File.ReadAllText(GetXamlPath("MainWindow.xaml"));
    Assert.Contains("general_atomics_logo_big.ico", content);
    Assert.Contains("pack://application:,,,", content);
}
```

**Step 2: Run test to verify it fails**

Run: `dotnet test src/WsusManager.Tests/WsusManager.Tests.csproj --filter "FullyQualifiedName~MainWindow_AboutPanel_ShouldUsePackUriForGaLogo"`
Expected: FAIL with current relative source.

**Step 3: Write minimal implementation**

In `src/WsusManager.App/Views/MainWindow.xaml`:
- Update About image source to resilient pack URI (e.g., `pack://application:,,,/general_atomics_logo_big.ico`).

If needed, in `src/WsusManager.App/WsusManager.App.csproj` ensure logo asset is packaged appropriately for pack URI resolution.

**Step 4: Run test to verify it passes**

Run: same command as Step 2.
Expected: PASS.

**Step 5: Commit**

```bash
git add src/WsusManager.App/Views/MainWindow.xaml src/WsusManager.App/WsusManager.App.csproj src/WsusManager.Tests/KeyboardNavigationTests.cs
git commit -m "fix: restore reliable GA icon rendering on about panel"
```

### Task 6: Reset Content explicit warning confirmation

**Files:**
- Modify: `src/WsusManager.App/ViewModels/MainViewModel.cs`
- Test: `src/WsusManager.Tests/ViewModels/MainViewModelTests.cs` (if available in current target build)

**Step 1: Write the failing test**

```csharp
[Fact]
public async Task ResetContent_ShouldRequireExplicitWarningConfirmation()
{
    // Add a test seam or dialog abstraction if needed, then assert operation cancels when warning not confirmed.
    Assert.True(true); // replace with real failing assertion using existing test pattern
}
```

**Step 2: Run test to verify it fails**

Run: `dotnet test src/WsusManager.Tests/WsusManager.Tests.csproj --filter "FullyQualifiedName~ResetContent_ShouldRequireExplicitWarningConfirmation"`
Expected: FAIL initially.

**Step 3: Write minimal implementation**

In `src/WsusManager.App/ViewModels/MainViewModel.cs` `ResetContent()`:
- Show warning/impact confirmation dialog with default No.
- Abort operation unless user confirms Yes.

**Step 4: Run test to verify it passes**

Run: same command as Step 2 (or nearest feasible test if dialog seam is not currently testable).
Expected: PASS.

**Step 5: Commit**

```bash
git add src/WsusManager.App/ViewModels/MainViewModel.cs src/WsusManager.Tests/ViewModels/MainViewModelTests.cs
git commit -m "feat: require explicit warning confirmation for reset content"
```

### Task 7: Final verification and smoke run

**Files:**
- Modify: none expected

**Step 1: Run focused regression suite**

Run:
- `dotnet test src/WsusManager.Tests/WsusManager.Tests.csproj --filter "FullyQualifiedName~KeyboardNavigationTests|FullyQualifiedName~ClientServiceTests.LookupErrorCode|FullyQualifiedName~DashboardServiceTests.GetUpdatesAsync_Does_Not_Return_Phase29_Mock_Kb_Rows"`

Expected: PASS.

**Step 2: Build app**

Run: `dotnet build src/WsusManager.App/WsusManager.App.csproj`
Expected: Build succeeds, 0 errors.

**Step 3: Manual smoke check**

In app UI verify:
1. Script Generator row is compact with dropdown left and button right.
2. Error lookup supports both selection and typed custom values.
3. Last two dropdown codes resolve successfully.
4. About GA icon is visible.
5. Client/Computers/Updates form rows feel visually uniform.

**Step 4: Commit docs update**

```bash
git add docs/plans/2026-02-23-final-ui-standardization-pass-design.md docs/plans/2026-02-23-final-ui-standardization-pass-implementation.md
git commit -m "docs: add final ui standardization design and implementation plan"
```
