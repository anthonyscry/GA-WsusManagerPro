# Client Tools and Updates UX Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Remove mock update rows, compact the Client Tools panel, add dropdown+typeahead Error Code lookup, and theme Updates column headers.

**Architecture:** Keep existing MVVM shape and card-based XAML layout, then make targeted updates in ViewModel/service/XAML styles only. Route Updates loading through real dashboard metadata API using current settings and preserve existing error handling. Use existing shared theme resources (`VirtualizedListView`, `TextSecondary`, `NavBackground`) to avoid introducing a parallel style system.

**Tech Stack:** C# (.NET 8), WPF/XAML, CommunityToolkit.Mvvm, xUnit.

---

### Task 1: Replace mock Updates data path

**Files:**
- Modify: `src/WsusManager.App/ViewModels/MainViewModel.cs`
- Modify: `src/WsusManager.Core/Services/DashboardService.cs`
- Test: `src/WsusManager.Tests/ViewModels/MainViewModelTests.cs` (or nearest existing ViewModel test file)

**Step 1: Write the failing test**

```csharp
[Fact]
public async Task LoadUpdatesAsync_ShouldCallSettingsBasedDashboardApi()
{
    // Arrange
    var dashboard = new Mock<IDashboardService>();
    dashboard
        .Setup(d => d.GetUpdatesAsync(It.IsAny<AppSettings>(), 1, 100, It.IsAny<CancellationToken>()))
        .ReturnsAsync(new List<UpdateInfo>());

    var vm = MainViewModelTestFactory.Create(dashboardService: dashboard.Object);

    // Act
    await vm.LoadUpdatesAsync();

    // Assert
    dashboard.Verify(d => d.GetUpdatesAsync(It.IsAny<AppSettings>(), 1, 100, It.IsAny<CancellationToken>()), Times.Once);
}
```

**Step 2: Run test to verify it fails**

Run: `dotnet test src/WsusManager.Tests/WsusManager.Tests.csproj --filter "FullyQualifiedName~LoadUpdatesAsync_ShouldCallSettingsBasedDashboardApi"`
Expected: FAIL because current code calls `GetUpdatesAsync(CancellationToken)`.

**Step 3: Write minimal implementation**

Update `LoadUpdatesAsync()` in `MainViewModel` to call:

```csharp
var updates = await _dashboardService
    .GetUpdatesAsync(_settings, pageNumber: 1, pageSize: 100, ct)
    .ConfigureAwait(true);
```

**Step 4: Run test to verify it passes**

Run: same command as Step 2.
Expected: PASS.

**Step 5: Commit**

```bash
git add src/WsusManager.App/ViewModels/MainViewModel.cs src/WsusManager.Tests/ViewModels/MainViewModelTests.cs
git commit -m "fix: load updates from real dashboard metadata API"
```

### Task 2: Remove mock fallback list in DashboardService

**Files:**
- Modify: `src/WsusManager.Core/Services/DashboardService.cs`
- Test: `src/WsusManager.Tests/Services/DashboardServiceTests.cs`

**Step 1: Write the failing test**

```csharp
[Fact]
public async Task GetUpdatesAsync_NoSettingsOverload_ShouldNotReturnHardcodedKbRows()
{
    var service = DashboardServiceTestFactory.Create();
    var result = await service.GetUpdatesAsync();

    Assert.DoesNotContain(result, x => string.Equals(x.KbArticle, "KB5034441", StringComparison.OrdinalIgnoreCase));
}
```

**Step 2: Run test to verify it fails**

Run: `dotnet test src/WsusManager.Tests/WsusManager.Tests.csproj --filter "FullyQualifiedName~GetUpdatesAsync_NoSettingsOverload_ShouldNotReturnHardcodedKbRows"`
Expected: FAIL due to hardcoded mock rows.

**Step 3: Write minimal implementation**

In `DashboardService.GetUpdatesAsync(CancellationToken)`:
- Remove hardcoded sample list.
- Delegate to settings-based overload using safe defaults from current app settings context (or return empty list if settings are unavailable in this overload).
- Keep cancellation/error behavior stable.

**Step 4: Run test to verify it passes**

Run: same command as Step 2.
Expected: PASS.

**Step 5: Commit**

```bash
git add src/WsusManager.Core/Services/DashboardService.cs src/WsusManager.Tests/Services/DashboardServiceTests.cs
git commit -m "refactor: remove hardcoded updates mock list"
```

### Task 3: Add common error code dropdown + typeahead

**Files:**
- Modify: `src/WsusManager.App/ViewModels/MainViewModel.cs`
- Modify: `src/WsusManager.App/Views/MainWindow.xaml`
- Test: `src/WsusManager.Tests/ViewModels/MainViewModelTests.cs`
- Test: `src/WsusManager.Tests/KeyboardNavigationTests.cs`

**Step 1: Write the failing ViewModel test**

```csharp
[Fact]
public void CommonErrorCodes_ShouldContainKnownWsusCodes()
{
    var vm = MainViewModelTestFactory.Create();
    Assert.Contains("0x80072EE2", vm.CommonErrorCodes);
    Assert.Contains("0x80244022", vm.CommonErrorCodes);
}
```

**Step 2: Run test to verify it fails**

Run: `dotnet test src/WsusManager.Tests/WsusManager.Tests.csproj --filter "FullyQualifiedName~CommonErrorCodes_ShouldContainKnownWsusCodes"`
Expected: FAIL because property does not exist yet.

**Step 3: Write minimal implementation**

In `MainViewModel`:

```csharp
public IReadOnlyList<string> CommonErrorCodes =>
[
    "0x80072EE2", "0x80244022", "0x80244010", "0x80070005", "0x80240022"
];
```

In `MainWindow.xaml` (Error Code section):
- Replace `ErrorCodeInputTextBox` with editable `ComboBox`.
- Bind `ItemsSource="{Binding CommonErrorCodes}"`.
- Bind `Text="{Binding ErrorCodeInput, UpdateSourceTrigger=PropertyChanged}"`.
- Set `IsEditable="True"` and keep existing AutomationId (or update test accordingly).

**Step 4: Run tests to verify pass**

Run:
- `dotnet test src/WsusManager.Tests/WsusManager.Tests.csproj --filter "FullyQualifiedName~CommonErrorCodes_ShouldContainKnownWsusCodes"`
- `dotnet test src/WsusManager.Tests/WsusManager.Tests.csproj --filter "FullyQualifiedName~KeyboardNavigationTests"`

Expected: PASS.

**Step 5: Commit**

```bash
git add src/WsusManager.App/ViewModels/MainViewModel.cs src/WsusManager.App/Views/MainWindow.xaml src/WsusManager.Tests/ViewModels/MainViewModelTests.cs src/WsusManager.Tests/KeyboardNavigationTests.cs
git commit -m "feat: add common error code dropdown with typeahead"
```

### Task 4: Compact Client Tools panel layout

**Files:**
- Modify: `src/WsusManager.App/Views/MainWindow.xaml`
- Test: `src/WsusManager.Tests/KeyboardNavigationTests.cs`

**Step 1: Write the failing structural test**

```csharp
[Fact]
public void MainWindow_ClientHostnameInput_ShouldUseCompactWidth()
{
    var content = File.ReadAllText(GetXamlPath("MainWindow.xaml"));
    Assert.Contains("AutomationProperties.AutomationId=\"ClientHostnameTextBox\"", content);
    Assert.Contains("Width=\"320\"", content);
}
```

**Step 2: Run test to verify it fails**

Run: `dotnet test src/WsusManager.Tests/WsusManager.Tests.csproj --filter "FullyQualifiedName~ClientHostnameInput_ShouldUseCompactWidth"`
Expected: FAIL.

**Step 3: Write minimal implementation**

In `MainWindow.xaml` Client Tools section:
- Add AutomationId on hostname input if missing.
- Set compact width (e.g., `Width="320"`, `MaxWidth="360"`).
- Reduce card paddings/margins and button spacing to tighten vertical density.
- Keep bindings and commands unchanged.

**Step 4: Run test to verify it passes**

Run: same command as Step 2.
Expected: PASS.

**Step 5: Commit**

```bash
git add src/WsusManager.App/Views/MainWindow.xaml src/WsusManager.Tests/KeyboardNavigationTests.cs
git commit -m "style: compact client tools target host layout"
```

### Task 5: Apply themed Updates column header style

**Files:**
- Modify: `src/WsusManager.App/Views/MainWindow.xaml`
- Optional Modify: `src/WsusManager.App/Themes/SharedStyles.xaml`
- Test: `src/WsusManager.Tests/KeyboardNavigationTests.cs`

**Step 1: Write the failing structural test**

```csharp
[Fact]
public void MainWindow_UpdatesList_ShouldUseVirtualizedListViewStyle()
{
    var content = File.ReadAllText(GetXamlPath("MainWindow.xaml"));
    Assert.Contains("x:Name=\"UpdatesListView\"", content);
    Assert.Contains("Style=\"{StaticResource VirtualizedListView}\"", content);
}
```

**Step 2: Run test to verify it fails**

Run: `dotnet test src/WsusManager.Tests/WsusManager.Tests.csproj --filter "FullyQualifiedName~UpdatesList_ShouldUseVirtualizedListViewStyle"`
Expected: FAIL.

**Step 3: Write minimal implementation**

In Updates `ListView`:

```xml
<ListView x:Name="UpdatesListView"
          Style="{StaticResource VirtualizedListView}"
          ... />
```

If needed, refine `GridViewColumnHeader` colors in `SharedStyles.xaml` under `VirtualizedListView` only.

**Step 4: Run tests to verify pass**

Run:
- `dotnet test src/WsusManager.Tests/WsusManager.Tests.csproj --filter "FullyQualifiedName~UpdatesList_ShouldUseVirtualizedListViewStyle"`
- `dotnet test src/WsusManager.Tests/WsusManager.Tests.csproj --filter "FullyQualifiedName~KeyboardNavigationTests"`

Expected: PASS.

**Step 5: Commit**

```bash
git add src/WsusManager.App/Views/MainWindow.xaml src/WsusManager.App/Themes/SharedStyles.xaml src/WsusManager.Tests/KeyboardNavigationTests.cs
git commit -m "style: theme updates list headers to reduce glare"
```

### Task 6: Final verification and smoke checks

**Files:**
- Modify: none expected (verification only)

**Step 1: Run focused test suite**

Run:
- `dotnet test src/WsusManager.Tests/WsusManager.Tests.csproj --filter "FullyQualifiedName~KeyboardNavigationTests"`
- `dotnet test src/WsusManager.Tests/WsusManager.Tests.csproj --filter "FullyQualifiedName~MainViewModel"`
- `dotnet test src/WsusManager.Tests/WsusManager.Tests.csproj --filter "FullyQualifiedName~DashboardService"`

Expected: PASS.

**Step 2: Run app build**

Run: `dotnet build src/WsusManager.App/WsusManager.App.csproj`
Expected: Build succeeds with 0 errors.

**Step 3: Manual UX smoke verification**

Run app and verify:

1. Updates panel shows real data or empty state only (no KB50344xx sample list).
2. Updates headers match theme and are not bright white.
3. Client Tools hostname input is compact and panel is denser.
4. Error Code dropdown supports selection and typing custom code.

**Step 4: Commit verification note**

```bash
git add docs/plans/2026-02-23-client-tools-updates-ux-implementation.md
git commit -m "docs: add implementation plan for client tools and updates UX improvements"
```
