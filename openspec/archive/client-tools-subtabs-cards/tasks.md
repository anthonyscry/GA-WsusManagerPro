# Tasks: Client Tools Subtabs and Card Iteration

## Phase A: Prep / analysis

1. Confirm current Client Tools cards and bindings in `MainWindow.xaml` and `MainViewModel.cs`.
   - Verify: `dotnet build src/WsusManager.sln --configuration Debug --no-restore`
   - Success: baseline compiles before refactor.

## Phase B: Implementation

2. Add ViewModel card-group collections for tabbed iteration.
   - Files: `src/WsusManager.App/ViewModels/MainViewModel.cs`
   - Verify: `dotnet test src/WsusManager.Tests/WsusManager.Tests.csproj --filter "FullyQualifiedName~MainViewModelTests" --verbosity minimal`
   - Success: tests for group definitions pass.

3. Refactor Client Tools XAML into subtabs and `ItemsControl` card iteration.
   - Files: `src/WsusManager.App/Views/MainWindow.xaml`
   - Verify: `dotnet build src/WsusManager.sln --configuration Debug --no-restore`
   - Success: XAML compiles and panel layout uses subtabs with card foreach rendering.

## Phase C: Tests + verification

4. Add/adjust ViewModel tests for card-group structure.
   - Files: `src/WsusManager.Tests/ViewModels/MainViewModelTests.cs`
   - Verify: `dotnet test src/WsusManager.Tests/WsusManager.Tests.csproj --filter "FullyQualifiedName~MainViewModelTests" --verbosity minimal`
   - Success: new tests pass and no regressions in MainViewModel tests.

## Phase D: Docs + cleanup

5. Run targeted regression checks and summarize results.
   - Verify:
     - `dotnet test src/WsusManager.Tests/WsusManager.Tests.csproj --filter "FullyQualifiedName~MainViewModelTests|FullyQualifiedName~AppProjectPackagingTests|FullyQualifiedName~DistributionPackageTests" --verbosity minimal`
     - `dotnet build src/WsusManager.sln --configuration Debug --no-restore`
   - Success: all targeted checks pass and changes are ready for review.
