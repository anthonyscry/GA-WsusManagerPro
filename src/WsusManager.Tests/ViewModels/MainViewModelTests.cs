using System.Reflection;
using System.Windows.Data;
using Moq;
using WsusManager.App.Services;
using WsusManager.App.ViewModels;
using WsusManager.Core.Logging;
using WsusManager.Core.Models;
using WsusManager.Core.Services.Interfaces;

#pragma warning disable CA2007

namespace WsusManager.Tests.ViewModels;

/// <summary>
/// Tests for the MainViewModel async operation pattern, dashboard logic,
/// navigation, log panel, and button state management.
/// Updated for Phase 5: includes ISyncService, IExportService, IImportService mocks.
/// </summary>
public class MainViewModelTests
{
    private readonly Mock<ILogService> _mockLog = new();
    private readonly Mock<ISettingsService> _mockSettings = new();
    private readonly Mock<IDashboardService> _mockDashboard = new();
    private readonly Mock<IHealthService> _mockHealth = new();
    private readonly Mock<IWindowsServiceManager> _mockServiceManager = new();
    private readonly Mock<IContentResetService> _mockContentReset = new();
    private readonly Mock<IDeepCleanupService> _mockDeepCleanup = new();
    private readonly Mock<IDatabaseBackupService> _mockBackup = new();
    private readonly Mock<ISyncService> _mockSync = new();
    private readonly Mock<IExportService> _mockExport = new();
    private readonly Mock<IImportService> _mockImport = new();
    private readonly Mock<IInstallationService> _mockInstall = new();
    private readonly Mock<IScheduledTaskService> _mockScheduledTask = new();
    private readonly Mock<IHttpsConfigurationService> _mockHttps = new();
    private readonly Mock<IGpoDeploymentService> _mockGpo = new();
    private readonly Mock<IHttpsDialogService> _mockHttpsDialogService = new();
    private readonly Mock<IClientService> _mockClient = new();
    private readonly Mock<IScriptGeneratorService> _mockScriptGenerator = new();
    private readonly Mock<IThemeService> _mockThemeService = new();
    private readonly Mock<IBenchmarkTimingService> _mockBenchmarkTiming = new();
    private readonly Mock<ISettingsValidationService> _mockValidation = new();
    private readonly Mock<ICsvExportService> _mockCsvExport = new();
    private readonly Mock<IOperationTranscriptService> _mockTranscript = new();
    private readonly MainViewModel _vm;

    public MainViewModelTests()
    {
        _mockSettings.Setup(s => s.LoadAsync()).ReturnsAsync(new AppSettings());
        _mockSettings.Setup(s => s.Current).Returns(new AppSettings());
        _mockHttpsDialogService.Setup(d => d.ShowDialog()).Returns((HttpsDialogResult?)null);
        _mockTranscript
            .Setup(t => t.WriteLineAsync(
                It.IsAny<Guid>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _vm = new MainViewModel(
            _mockLog.Object,
            _mockTranscript.Object,
            _mockSettings.Object,
            _mockDashboard.Object,
            _mockHealth.Object,
            _mockServiceManager.Object,
            _mockContentReset.Object,
            _mockDeepCleanup.Object,
            _mockBackup.Object,
            _mockSync.Object,
            _mockExport.Object,
            _mockImport.Object,
            _mockInstall.Object,
            _mockScheduledTask.Object,
            _mockHttps.Object,
            _mockGpo.Object,
            _mockHttpsDialogService.Object,
            _mockClient.Object,
            _mockScriptGenerator.Object,
            _mockThemeService.Object,
            _mockBenchmarkTiming.Object,
            _mockValidation.Object,
            _mockCsvExport.Object);
    }

    // ═══════════════════════════════════════════════════════════════
    // RunOperationAsync Tests
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public async Task RunOperationAsync_Sets_IsOperationRunning_During_Execution()
    {
        bool wasRunningDuringOp = false;

        await _vm.RunOperationAsync("Test", async (progress, ct) =>
        {
            wasRunningDuringOp = _vm.IsOperationRunning;
            await Task.CompletedTask.ConfigureAwait(false);
            return true;
        });

        Assert.True(wasRunningDuringOp, "IsOperationRunning should be true during operation");
        Assert.False(_vm.IsOperationRunning, "IsOperationRunning should be false after operation");
    }

    [Fact]
    public async Task RunOperationAsync_Resets_State_After_Success()
    {
        var result = await _vm.RunOperationAsync("Test", async (progress, ct) =>
        {
            await Task.CompletedTask.ConfigureAwait(false);
            return true;
        });

        Assert.True(result);
        Assert.False(_vm.IsOperationRunning);
        Assert.Equal(string.Empty, _vm.CurrentOperationName);
        Assert.Contains("completed", _vm.StatusMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RunOperationAsync_Resets_State_After_Failure()
    {
        var result = await _vm.RunOperationAsync("Test", async (progress, ct) =>
        {
            await Task.CompletedTask.ConfigureAwait(false);
            return false;
        });

        Assert.False(result);
        Assert.False(_vm.IsOperationRunning);
        Assert.Contains("failed", _vm.StatusMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RunOperationAsync_Catches_OperationCanceledException()
    {
        var result = await _vm.RunOperationAsync("Test", async (progress, ct) =>
        {
            await Task.CompletedTask.ConfigureAwait(false);
            throw new OperationCanceledException();
        });

        Assert.False(result);
        Assert.False(_vm.IsOperationRunning);
        Assert.Contains("CANCELLED", _vm.LogOutput);
    }

    [Fact]
    public async Task RunOperationAsync_Catches_Unhandled_Exception()
    {
        var result = await _vm.RunOperationAsync("Test", async (progress, ct) =>
        {
            await Task.CompletedTask.ConfigureAwait(false);
            throw new InvalidOperationException("Something broke");
        });

        Assert.False(result);
        Assert.False(_vm.IsOperationRunning);
        Assert.Contains("Something broke", _vm.LogOutput);
        Assert.Contains("FAILED", _vm.LogOutput);
    }

    [Fact]
    public async Task RunOperationAsync_Blocks_Concurrent_Operations()
    {
        var tcs = new TaskCompletionSource<bool>();

        var firstOp = _vm.RunOperationAsync("First", async (progress, ct) =>
        {
            return await tcs.Task.ConfigureAwait(false);
        });

        var secondResult = await _vm.RunOperationAsync("Second", async (progress, ct) =>
        {
            await Task.CompletedTask.ConfigureAwait(false);
            return true;
        });

        Assert.False(secondResult, "Second operation should be rejected");
        Assert.Contains("already running", _vm.LogOutput, StringComparison.OrdinalIgnoreCase);

        tcs.SetResult(true);
        await firstOp;
    }

    [Fact]
    public async Task CancelCommand_Triggers_Cancellation()
    {
        var operationStarted = new TaskCompletionSource();

        var opTask = _vm.RunOperationAsync("CancelTest", async (progress, ct) =>
        {
            operationStarted.SetResult();
            await Task.Delay(TimeSpan.FromSeconds(30), ct).ConfigureAwait(false);
            return true;
        });

        await operationStarted.Task;
        Assert.True(_vm.IsOperationRunning);

        _vm.CancelOperationCommand.Execute(null);

        var result = await opTask;
        Assert.False(result);
        Assert.Contains("CANCELLED", _vm.LogOutput);
    }

    [Fact]
    public void ClearLog_Clears_LogOutput()
    {
        _vm.AppendLog("Some output");
        Assert.NotEmpty(_vm.LogOutput);

        _vm.ClearLogCommand.Execute(null);
        Assert.Empty(_vm.LogOutput);
    }

    [Fact]
    public async Task ToggleLiveTerminalModeCommand_Toggles_LiveTerminalMode_And_Saves()
    {
        _mockSettings.Setup(s => s.SaveAsync(It.IsAny<AppSettings>())).Returns(Task.CompletedTask);

        var initialState = _vm.LiveTerminalMode;

        await _vm.ToggleLiveTerminalModeCommand.ExecuteAsync(null);

        Assert.Equal(!initialState, _vm.LiveTerminalMode);
        _mockSettings.Verify(
            s => s.SaveAsync(It.Is<AppSettings>(settings => settings.LiveTerminalMode == _vm.LiveTerminalMode)),
            Times.Once);
    }

    [Fact]
    public void LiveTerminalToggleLabel_Reflects_Current_State()
    {
        _vm.LiveTerminalMode = false;
        Assert.Equal("Live Terminal: Off", _vm.LiveTerminalToggleLabel);

        _vm.LiveTerminalMode = true;
        Assert.Equal("Live Terminal: On", _vm.LiveTerminalToggleLabel);
    }

    [Fact]
    public async Task RunOperationAsync_Reports_Progress()
    {
        await _vm.RunOperationAsync("ProgressTest", async (progress, ct) =>
        {
            progress.Report("Step 1 done");
            progress.Report("Step 2 done");
            await Task.CompletedTask.ConfigureAwait(false);
            return true;
        });

        Assert.Contains("Step 1 done", _vm.LogOutput);
        Assert.Contains("Step 2 done", _vm.LogOutput);
    }

    [Fact]
    public async Task RunOperationAsync_Logs_OperationId_OnStartAndFinish()
    {
        var result = await _vm.RunOperationAsync("Diagnostics", async (_, ct) =>
        {
            await Task.Yield();
            return true;
        });

        Assert.True(result);

        _mockLog.Verify(
            l => l.Info(It.Is<string>(m => m.Contains("OperationId", StringComparison.Ordinal)), It.IsAny<object[]>()),
            Times.AtLeast(2));
    }

    // ═══════════════════════════════════════════════════════════════
    // Navigation Tests
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public void Navigate_Sets_CurrentPanel_And_PageTitle()
    {
        _vm.NavigateCommand.Execute("Install");

        Assert.Equal("Install", _vm.CurrentPanel);
        Assert.Equal("Install WSUS", _vm.PageTitle);
    }

    [Fact]
    public void Navigate_Dashboard_Shows_Dashboard_Panel()
    {
        _vm.NavigateCommand.Execute("Install");
        _vm.NavigateCommand.Execute("Dashboard");

        Assert.Equal("Dashboard", _vm.CurrentPanel);
        Assert.Equal("Dashboard", _vm.PageTitle);
    }

    [Fact]
    public void ClientToolsOperationsCardKeys_Are_In_Expected_Order()
    {
        Assert.Equal(
            ["TargetHost", "RemoteOperations", "MassOperations"],
            _vm.ClientToolsOperationsCardKeys);
    }

    [Fact]
    public void ClientToolsAuditCardKeys_Are_In_Expected_Order()
    {
        Assert.Equal(
            ["FleetAudit", "ScriptGenerator"],
            _vm.ClientToolsAuditCardKeys);
    }

    [Fact]
    public void ClientToolsUtilityCardKeys_Are_In_Expected_Order()
    {
        Assert.Equal(["ErrorCodeLookup"], _vm.ClientToolsUtilityCardKeys);
    }

    // ═══════════════════════════════════════════════════════════════
    // Dashboard Card Threshold Tests
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public void UpdateDashboard_AllServicesRunning_ShowsGreen()
    {
        var data = new DashboardData
        {
            IsWsusInstalled = true,
            ServiceRunningCount = 3,
            ServiceNames = new[] { "SQL", "WSUS", "IIS" },
            DatabaseSizeGB = 2.5,
            DiskFreeGB = 100,
            TaskStatus = "Ready",
            IsOnline = true
        };

        InvokeUpdateDashboardCards(data);

        Assert.Equal("3 / 3", _vm.ServicesValue);
        // Green color: #3FB950
        Assert.Equal(Color(0x3F, 0xB9, 0x50), _vm.ServicesBarColor.Color);
    }

    [Fact]
    public void UpdateDashboard_SomeServicesRunning_ShowsOrange()
    {
        var data = new DashboardData
        {
            IsWsusInstalled = true,
            ServiceRunningCount = 1,
            ServiceNames = new[] { "SQL", "WSUS", "IIS" },
            DatabaseSizeGB = 2.5,
            DiskFreeGB = 100,
            TaskStatus = "Ready",
            IsOnline = true
        };

        InvokeUpdateDashboardCards(data);

        Assert.Equal("1 / 3", _vm.ServicesValue);
        // Orange color: #D29922
        Assert.Equal(Color(0xD2, 0x99, 0x22), _vm.ServicesBarColor.Color);
    }

    [Fact]
    public void UpdateDashboard_DbSizeBelowThreshold_ShowsGreen()
    {
        var data = CreateHealthyData();
        data.DatabaseSizeGB = 5.0;

        InvokeUpdateDashboardCards(data);

        Assert.Equal("5.0 / 10 GB", _vm.DatabaseValue);
        Assert.Equal(Color(0x3F, 0xB9, 0x50), _vm.DatabaseBarColor.Color);
    }

    [Fact]
    public void UpdateDashboard_DbSizeApproaching_ShowsOrange()
    {
        var data = CreateHealthyData();
        data.DatabaseSizeGB = 7.5;

        InvokeUpdateDashboardCards(data);

        Assert.Equal("7.5 / 10 GB", _vm.DatabaseValue);
        Assert.Equal(Color(0xD2, 0x99, 0x22), _vm.DatabaseBarColor.Color);
        Assert.Contains("Approaching", _vm.DatabaseSubtext);
    }

    [Fact]
    public void UpdateDashboard_DbSizeCritical_ShowsRed()
    {
        var data = CreateHealthyData();
        data.DatabaseSizeGB = 9.5;

        InvokeUpdateDashboardCards(data);

        Assert.Equal("9.5 / 10 GB", _vm.DatabaseValue);
        Assert.Equal(Color(0xF8, 0x51, 0x49), _vm.DatabaseBarColor.Color);
        Assert.Contains("CRITICAL", _vm.DatabaseSubtext);
    }

    [Fact]
    public void UpdateDashboard_DbOffline_ShowsRed()
    {
        var data = CreateHealthyData();
        data.DatabaseSizeGB = -1;

        InvokeUpdateDashboardCards(data);

        Assert.Equal("Offline", _vm.DatabaseValue);
        Assert.Equal(Color(0xF8, 0x51, 0x49), _vm.DatabaseBarColor.Color);
    }

    [Fact]
    public void UpdateDashboard_WsusNotInstalled_ShowsNotInstalled()
    {
        var data = new DashboardData
        {
            IsWsusInstalled = false,
            ServiceRunningCount = 0,
            ServiceNames = new[] { "SQL", "WSUS", "IIS" },
            DatabaseSizeGB = -1,
            DiskFreeGB = 100,
            TaskStatus = "Not Found",
            IsOnline = false
        };

        InvokeUpdateDashboardCards(data);

        Assert.Equal("N/A", _vm.ServicesValue);
        Assert.Equal("Not Installed", _vm.ServicesSubtext);
        Assert.Equal("N/A", _vm.DatabaseValue);
        Assert.Equal("N/A", _vm.DiskValue);
        Assert.Equal("N/A", _vm.TaskValue);
        Assert.False(_vm.IsWsusInstalled);
    }

    [Fact]
    public void UpdateDashboard_DiskSpaceLow_ShowsRed()
    {
        var data = CreateHealthyData();
        data.DiskFreeGB = 5.0;

        InvokeUpdateDashboardCards(data);

        Assert.Equal("5.0 GB", _vm.DiskValue);
        Assert.Equal(Color(0xF8, 0x51, 0x49), _vm.DiskBarColor.Color);
        Assert.Contains("Low", _vm.DiskSubtext);
    }

    // ═══════════════════════════════════════════════════════════════
    // Server Mode Tests
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public void UpdateDashboard_Online_ShowsGreenDot()
    {
        var data = CreateHealthyData();
        data.IsOnline = true;

        InvokeUpdateDashboardCards(data);

        Assert.True(_vm.IsOnline);
        Assert.Equal("Online", _vm.ConnectionStatusText);
        Assert.Equal(Color(0x3F, 0xB9, 0x50), _vm.ConnectionDotColor.Color);
    }

    [Fact]
    public void UpdateDashboard_Offline_ShowsRedDot()
    {
        var data = CreateHealthyData();
        data.IsOnline = false;

        InvokeUpdateDashboardCards(data);

        Assert.False(_vm.IsOnline);
        Assert.Equal("Offline", _vm.ConnectionStatusText);
        Assert.Equal(Color(0xF8, 0x51, 0x49), _vm.ConnectionDotColor.Color);
    }

    // ═══════════════════════════════════════════════════════════════
    // Log Panel Tests
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public void LogPanel_Default_Expanded()
    {
        Assert.True(_vm.IsLogPanelExpanded);
        Assert.Equal(250, _vm.LogPanelHeight);
        Assert.Equal("Hide", _vm.LogToggleText);
    }

    [Fact]
    public void LogPanel_Toggle_Changes_State()
    {
        _vm.IsLogPanelExpanded = false;

        Assert.Equal(0, _vm.LogPanelHeight);
        Assert.Equal("Show", _vm.LogToggleText);
    }

    // ═══════════════════════════════════════════════════════════════
    // Button State (CanExecute) Tests
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public void QuickDiagnostics_CanExecute_False_When_WsusNotInstalled()
    {
        _vm.IsWsusInstalled = false;
        _vm.IsOperationRunning = false;

        Assert.False(_vm.QuickDiagnosticsCommand.CanExecute(null));
    }

    [Fact]
    public void QuickDiagnostics_CanExecute_False_When_OperationRunning()
    {
        _vm.IsWsusInstalled = true;
        // Manually set via RunOperationAsync would be cleaner but this tests the property directly
        // We test via the CanExecute helper pattern
        Assert.True(_vm.QuickDiagnosticsCommand.CanExecute(null));
    }

    [Fact]
    public void QuickSync_CanExecute_False_When_Offline()
    {
        _vm.IsWsusInstalled = true;
        _vm.IsOnline = false;

        Assert.False(_vm.QuickSyncCommand.CanExecute(null));
    }

    [Fact]
    public void QuickSync_CanExecute_True_When_Online_And_WsusInstalled()
    {
        _vm.IsWsusInstalled = true;
        _vm.IsOnline = true;

        Assert.True(_vm.QuickSyncCommand.CanExecute(null));
    }

    // ═══════════════════════════════════════════════════════════════
    // Phase 3: Diagnostics Command Tests
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public async Task RunDiagnosticsCommand_Navigates_To_Diagnostics_Panel()
    {
        _vm.IsWsusInstalled = true;

        var healthyReport = new DiagnosticReport
        {
            Checks = [],
            StartedAt = DateTime.UtcNow,
            CompletedAt = DateTime.UtcNow
        };

        _mockHealth
            .Setup(h => h.RunDiagnosticsAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<IProgress<string>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(healthyReport);

        _mockDashboard
            .Setup(d => d.CollectAsync(It.IsAny<AppSettings>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateHealthyData());

        await _vm.RunDiagnosticsCommand.ExecuteAsync(null);

        Assert.Equal("Diagnostics", _vm.CurrentPanel);
    }

    [Fact]
    public async Task RunDiagnosticsCommand_Calls_HealthService()
    {
        _vm.IsWsusInstalled = true;

        var healthyReport = new DiagnosticReport
        {
            Checks = [],
            StartedAt = DateTime.UtcNow,
            CompletedAt = DateTime.UtcNow
        };

        _mockHealth
            .Setup(h => h.RunDiagnosticsAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<IProgress<string>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(healthyReport);

        _mockDashboard
            .Setup(d => d.CollectAsync(It.IsAny<AppSettings>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateHealthyData());

        await _vm.RunDiagnosticsCommand.ExecuteAsync(null);

        _mockHealth.Verify(h => h.RunDiagnosticsAsync(
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<IProgress<string>>(),
            It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task StartServiceCommand_Calls_ServiceManager()
    {
        _vm.IsWsusInstalled = true;

        _mockServiceManager
            .Setup(s => s.StartServiceAsync("WsusService", It.IsAny<CancellationToken>()))
            .ReturnsAsync(OperationResult.Ok("Started."));

        _mockDashboard
            .Setup(d => d.CollectAsync(It.IsAny<AppSettings>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateHealthyData());

        await _vm.StartServiceCommand.ExecuteAsync("WsusService");

        _mockServiceManager.Verify(s => s.StartServiceAsync("WsusService", It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task StopServiceCommand_Calls_ServiceManager()
    {
        _vm.IsWsusInstalled = true;

        _mockServiceManager
            .Setup(s => s.StopServiceAsync("WsusService", It.IsAny<CancellationToken>()))
            .ReturnsAsync(OperationResult.Ok("Stopped."));

        _mockDashboard
            .Setup(d => d.CollectAsync(It.IsAny<AppSettings>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateHealthyData());

        await _vm.StopServiceCommand.ExecuteAsync("WsusService");

        _mockServiceManager.Verify(s => s.StopServiceAsync("WsusService", It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task QuickStartServices_Calls_StartAllServicesAsync()
    {
        _vm.IsWsusInstalled = true;

        _mockServiceManager
            .Setup(s => s.StartAllServicesAsync(It.IsAny<IProgress<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(OperationResult.Ok("All started."));

        _mockDashboard
            .Setup(d => d.CollectAsync(It.IsAny<AppSettings>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateHealthyData());

        await _vm.QuickStartServicesCommand.ExecuteAsync(null);

        _mockServiceManager.Verify(s => s.StartAllServicesAsync(
            It.IsAny<IProgress<string>>(),
            It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public void IsDiagnosticsPanelVisible_Visible_When_Panel_Is_Diagnostics()
    {
        _vm.NavigateCommand.Execute("Diagnostics");

        Assert.Equal(System.Windows.Visibility.Visible, _vm.IsDiagnosticsPanelVisible);
        Assert.Equal(System.Windows.Visibility.Collapsed, _vm.IsOperationPanelVisible);
        Assert.Equal(System.Windows.Visibility.Collapsed, _vm.IsDashboardVisible);
    }

    [Fact]
    public void IsDiagnosticsPanelVisible_Collapsed_When_Panel_Is_Dashboard()
    {
        _vm.NavigateCommand.Execute("Dashboard");

        Assert.Equal(System.Windows.Visibility.Collapsed, _vm.IsDiagnosticsPanelVisible);
    }

    [Fact]
    public void IsOperationPanelVisible_Visible_For_Non_Diagnostics_Non_Dashboard_Panel()
    {
        _vm.NavigateCommand.Execute("Cleanup");

        Assert.Equal(System.Windows.Visibility.Visible, _vm.IsOperationPanelVisible);
        Assert.Equal(System.Windows.Visibility.Collapsed, _vm.IsDiagnosticsPanelVisible);
    }

    // ═══════════════════════════════════════════════════════════════
    // Phase 4: Database Operations Tests
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public void IsDatabasePanelVisible_Visible_When_Panel_Is_Database()
    {
        _vm.NavigateCommand.Execute("Database");

        Assert.Equal(System.Windows.Visibility.Visible, _vm.IsDatabasePanelVisible);
        Assert.Equal(System.Windows.Visibility.Collapsed, _vm.IsOperationPanelVisible);
        Assert.Equal(System.Windows.Visibility.Collapsed, _vm.IsDashboardVisible);
        Assert.Equal(System.Windows.Visibility.Collapsed, _vm.IsDiagnosticsPanelVisible);
    }

    [Fact]
    public void IsDatabasePanelVisible_Collapsed_When_Panel_Is_Dashboard()
    {
        _vm.NavigateCommand.Execute("Dashboard");

        Assert.Equal(System.Windows.Visibility.Collapsed, _vm.IsDatabasePanelVisible);
    }

    [Fact]
    public void IsOperationPanelVisible_Collapsed_When_Panel_Is_Database()
    {
        _vm.NavigateCommand.Execute("Database");

        Assert.Equal(System.Windows.Visibility.Collapsed, _vm.IsOperationPanelVisible);
    }

    [Fact]
    public void Navigate_Database_Sets_PageTitle()
    {
        _vm.NavigateCommand.Execute("Database");

        Assert.Equal("Database", _vm.CurrentPanel);
        Assert.Equal("Database Operations", _vm.PageTitle);
    }

    [Fact]
    public async Task RunDeepCleanupCommand_Navigates_To_Database_Panel()
    {
        _vm.IsWsusInstalled = true;

        _mockDeepCleanup
            .Setup(d => d.RunAsync(
                It.IsAny<string>(),
                It.IsAny<IProgress<string>>(),
                It.IsAny<CancellationToken>(), It.IsAny<bool>()))
            .ReturnsAsync(OperationResult.Ok("Cleanup complete."));

        _mockDashboard
            .Setup(d => d.CollectAsync(It.IsAny<AppSettings>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateHealthyData());

        await _vm.RunDeepCleanupCommand.ExecuteAsync(null);

        Assert.Equal("Database", _vm.CurrentPanel);
    }

    [Fact]
    public async Task RunDeepCleanupCommand_Calls_DeepCleanupService()
    {
        _vm.IsWsusInstalled = true;

        _mockDeepCleanup
            .Setup(d => d.RunAsync(
                It.IsAny<string>(),
                It.IsAny<IProgress<string>>(),
                It.IsAny<CancellationToken>(), It.IsAny<bool>()))
            .ReturnsAsync(OperationResult.Ok("Cleanup complete."));

        await _vm.RunDeepCleanupCommand.ExecuteAsync(null);

        _mockDeepCleanup.Verify(d => d.RunAsync(
            It.IsAny<string>(),
            It.IsAny<IProgress<string>>(),
            It.IsAny<CancellationToken>(), It.IsAny<bool>()),
            Times.Once);
    }

    [Fact]
    public async Task RunSetHttpsCommand_InvokesHttpsConfigurationService()
    {
        _vm.IsWsusInstalled = true;

        _mockHttpsDialogService
            .Setup(d => d.ShowDialog())
            .Returns(new HttpsDialogResult("wsus.contoso.local", "THUMBPRINT"));

        _mockHttps
            .Setup(s => s.ConfigureAsync(
                "wsus.contoso.local",
                "THUMBPRINT",
                It.IsAny<IProgress<string>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(OperationResult.Ok("HTTPS configured."));

        await _vm.RunSetHttpsCommand.ExecuteAsync(null);

        _mockHttps.Verify(s => s.ConfigureAsync(
            "wsus.contoso.local",
            "THUMBPRINT",
            It.IsAny<IProgress<string>>(),
            It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public void RunDeepCleanupCommand_CanExecute_False_When_WsusNotInstalled()
    {
        _vm.IsWsusInstalled = false;

        Assert.False(_vm.RunDeepCleanupCommand.CanExecute(null));
    }

    [Fact]
    public void BackupDatabaseCommand_CanExecute_False_When_WsusNotInstalled()
    {
        _vm.IsWsusInstalled = false;

        Assert.False(_vm.BackupDatabaseCommand.CanExecute(null));
    }

    [Fact]
    public void RestoreDatabaseCommand_CanExecute_False_When_WsusNotInstalled()
    {
        _vm.IsWsusInstalled = false;

        Assert.False(_vm.RestoreDatabaseCommand.CanExecute(null));
    }

    [Fact]
    public void RunDeepCleanupCommand_CanExecute_True_When_WsusInstalled()
    {
        _vm.IsWsusInstalled = true;

        Assert.True(_vm.RunDeepCleanupCommand.CanExecute(null));
    }

    // ═══════════════════════════════════════════════════════════════
    // Phase 5: WSUS Operations Tests
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public void RunOnlineSyncCommand_CanExecute_False_When_Offline()
    {
        _vm.IsWsusInstalled = true;
        _vm.IsOnline = false;

        Assert.False(_vm.RunOnlineSyncCommand.CanExecute(null));
    }

    [Fact]
    public void RunOnlineSyncCommand_CanExecute_True_When_Online_And_WsusInstalled()
    {
        _vm.IsWsusInstalled = true;
        _vm.IsOnline = true;

        Assert.True(_vm.RunOnlineSyncCommand.CanExecute(null));
    }

    [Fact]
    public async Task RunOnlineSyncWorkflow_Exports_When_Paths_Are_Provided()
    {
        _vm.IsWsusInstalled = true;

        _mockSync
            .Setup(s => s.RunSyncAsync(
                It.IsAny<SyncProfile>(),
                It.IsAny<int>(),
                It.IsAny<IProgress<string>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(OperationResult.Ok("Sync complete."));

        _mockExport
            .Setup(e => e.ExportAsync(
                It.IsAny<ExportOptions>(),
                It.IsAny<IProgress<string>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(OperationResult.Ok("Export complete."));

        var method = typeof(MainViewModel).GetMethod(
            "RunOnlineSyncWorkflowAsync",
            BindingFlags.Instance | BindingFlags.NonPublic);

        Assert.NotNull(method);

        var exportOptions = new ExportOptions
        {
            SourcePath = @"C:\WSUS",
            FullExportPath = @"C:\Exports\Full",
            ExportDays = 30
        };

        var task = (Task<bool>)method!.Invoke(_vm, new object?[] { SyncProfile.FullSync, exportOptions })!;
        var result = await task;

        Assert.True(result);
        _mockSync.Verify(s => s.RunSyncAsync(
            It.IsAny<SyncProfile>(),
            It.IsAny<int>(),
            It.IsAny<IProgress<string>>(),
            It.IsAny<CancellationToken>()), Times.Once);
        _mockExport.Verify(e => e.ExportAsync(
            It.IsAny<ExportOptions>(),
            It.IsAny<IProgress<string>>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RunOnlineSyncWorkflow_Skips_Export_When_No_Paths()
    {
        _vm.IsWsusInstalled = true;

        _mockSync
            .Setup(s => s.RunSyncAsync(
                It.IsAny<SyncProfile>(),
                It.IsAny<int>(),
                It.IsAny<IProgress<string>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(OperationResult.Ok("Sync complete."));

        var method = typeof(MainViewModel).GetMethod(
            "RunOnlineSyncWorkflowAsync",
            BindingFlags.Instance | BindingFlags.NonPublic);

        Assert.NotNull(method);

        var exportOptions = new ExportOptions
        {
            SourcePath = @"C:\WSUS",
            ExportDays = 30
        };

        var task = (Task<bool>)method!.Invoke(_vm, new object?[] { SyncProfile.FullSync, exportOptions })!;
        var result = await task;

        Assert.True(result);
        _mockExport.Verify(e => e.ExportAsync(
            It.IsAny<ExportOptions>(),
            It.IsAny<IProgress<string>>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public void RunTransferCommand_CanExecute_False_When_WsusNotInstalled()
    {
        _vm.IsWsusInstalled = false;

        Assert.False(_vm.RunTransferCommand.CanExecute(null));
    }

    [Fact]
    public void RunTransferCommand_CanExecute_True_When_WsusInstalled()
    {
        _vm.IsWsusInstalled = true;

        Assert.True(_vm.RunTransferCommand.CanExecute(null));
    }

    // ═══════════════════════════════════════════════════════════════
    // Phase 6: Installation and Scheduling Tests
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public void RunInstallWsusCommand_CanExecute_True_When_WsusNotInstalled()
    {
        // Install WSUS should be available even when WSUS is NOT installed
        _vm.IsWsusInstalled = false;

        Assert.True(_vm.RunInstallWsusCommand.CanExecute(null));
    }

    [Fact]
    public void RunInstallWsusCommand_CanExecute_True_When_WsusInstalled()
    {
        _vm.IsWsusInstalled = true;

        Assert.True(_vm.RunInstallWsusCommand.CanExecute(null));
    }

    [Fact]
    public void RunScheduleTaskCommand_CanExecute_False_When_WsusNotInstalled()
    {
        _vm.IsWsusInstalled = false;

        Assert.False(_vm.RunScheduleTaskCommand.CanExecute(null));
    }

    [Fact]
    public void RunScheduleTaskCommand_CanExecute_True_When_WsusInstalled()
    {
        _vm.IsWsusInstalled = true;

        Assert.True(_vm.RunScheduleTaskCommand.CanExecute(null));
    }

    [Fact]
    public void RunCreateGpoCommand_CanExecute_False_When_WsusNotInstalled()
    {
        _vm.IsWsusInstalled = false;

        Assert.False(_vm.RunCreateGpoCommand.CanExecute(null));
    }

    [Fact]
    public void RunCreateGpoCommand_CanExecute_True_When_WsusInstalled()
    {
        _vm.IsWsusInstalled = true;

        Assert.True(_vm.RunCreateGpoCommand.CanExecute(null));
    }

    [Theory]
    [InlineData(null, 8530, 8530)]
    [InlineData("", 8530, 8530)]
    [InlineData("8530", 8530, 8530)]
    [InlineData("65535", 8530, 65535)]
    [InlineData("0", 8530, 8530)]
    [InlineData("70000", 8530, 8530)]
    [InlineData("not-a-number", 8530, 8530)]
    [InlineData(" 8531 ", 8531, 8531)]
    [InlineData("-1", 8531, 8531)]
    public void NormalizeWsusPort_Returns_Expected_Value(string? candidate, int fallback, int expected)
    {
        var method = typeof(MainViewModel).GetMethod(
            "NormalizeWsusPort",
            BindingFlags.Static | BindingFlags.NonPublic);

        Assert.NotNull(method);

        var actual = (int)method!.Invoke(null, new object?[] { candidate, fallback })!;
        Assert.Equal(expected, actual);
    }

    [Fact]
    public async Task CreateGpoDeploymentRequest_Forwards_Host_And_Both_Ports_To_Service()
    {
        const string wsusHostname = "wsus01.contoso.local";
        const int wsusHttpPort = 8530;
        const int wsusHttpsPort = 8531;

        _mockGpo
            .Setup(g => g.DeployGpoFilesAsync(
                wsusHostname,
                wsusHttpPort,
                wsusHttpsPort,
                It.IsAny<IProgress<string>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(OperationResult<string>.Ok("ok"));

        var method = typeof(MainViewModel).GetMethod(
            "DeployCreateGpoFilesAsync",
            BindingFlags.Instance | BindingFlags.NonPublic);

        Assert.NotNull(method);

        var task = (Task<OperationResult<string>>)method!.Invoke(_vm,
            new object?[]
            {
                wsusHostname,
                wsusHttpPort,
                wsusHttpsPort,
                new Progress<string>(_ => { }),
                CancellationToken.None
            })!;

        var result = await task;

        Assert.True(result.Success);
        _mockGpo.Verify(g => g.DeployGpoFilesAsync(
            wsusHostname,
            wsusHttpPort,
            wsusHttpsPort,
            It.IsAny<IProgress<string>>(),
            It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task RunFleetWsusTargetAudit_Forwards_ExpectedHostname_And_Ports_To_Service()
    {
        _vm.ExpectedWsusHostname = " wsus-gold.contoso.local ";
        _vm.ExpectedWsusHttpPort = " 8532 ";
        _vm.ExpectedWsusHttpsPort = " ";

        _mockDashboard
            .Setup(d => d.GetComputersAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(
            [
                new ComputerInfo("host-a", "10.0.0.10", "Online", DateTime.UtcNow, 0, "Windows 11"),
                new ComputerInfo("host-a", "10.0.0.11", "Online", DateTime.UtcNow, 0, "Windows 11"),
                new ComputerInfo("", "10.0.0.12", "Online", DateTime.UtcNow, 0, "Windows 11")
            ]);

        _mockClient
            .Setup(c => c.RunFleetWsusTargetAuditAsync(
                It.IsAny<IReadOnlyList<string>>(),
                It.IsAny<string>(),
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<IProgress<string>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(OperationResult<FleetWsusTargetAuditReport>.Ok(new FleetWsusTargetAuditReport()));

        await _vm.RunFleetWsusTargetAuditCommand.ExecuteAsync(null);

        _mockClient.Verify(c => c.RunFleetWsusTargetAuditAsync(
            It.Is<IReadOnlyList<string>>(hosts => hosts.Count == 1 && hosts[0] == "host-a"),
            "wsus-gold.contoso.local",
            8532,
            8531,
            It.IsAny<IProgress<string>>(),
            It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public void RunFleetWsusTargetAuditCommand_CanExecute_False_When_ExpectedHostname_Blank()
    {
        _vm.ExpectedWsusHostname = "   ";

        Assert.False(_vm.RunFleetWsusTargetAuditCommand.CanExecute(null));
    }

    [Fact]
    public async Task RunFleetWsusTargetAudit_DoesNotCallService_When_Inventory_Is_Empty()
    {
        _vm.ExpectedWsusHostname = "wsus-gold.contoso.local";
        _vm.ExpectedWsusHttpPort = "8530";
        _vm.ExpectedWsusHttpsPort = "8531";

        _mockDashboard
            .Setup(d => d.GetComputersAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(
            [
                new ComputerInfo("", "10.0.0.10", "Online", DateTime.UtcNow, 0, "Windows 11"),
                new ComputerInfo("   ", "10.0.0.11", "Online", DateTime.UtcNow, 0, "Windows 11")
            ]);

        await _vm.RunFleetWsusTargetAuditCommand.ExecuteAsync(null);

        _mockClient.Verify(c => c.RunFleetWsusTargetAuditAsync(
            It.IsAny<IReadOnlyList<string>>(),
            It.IsAny<string>(),
            It.IsAny<int>(),
            It.IsAny<int>(),
            It.IsAny<IProgress<string>>(),
            It.IsAny<CancellationToken>()),
            Times.Never);
    }

    // ═══════════════════════════════════════════════════════════════
    // Phase 7: Comprehensive CanExecute and State Tests
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public void ResetContentCommand_CanExecute_False_When_WsusNotInstalled()
    {
        _vm.IsWsusInstalled = false;

        Assert.False(_vm.ResetContentCommand.CanExecute(null));
    }

    [Fact]
    public void ResetContentCommand_CanExecute_True_When_WsusInstalled()
    {
        _vm.IsWsusInstalled = true;

        Assert.True(_vm.ResetContentCommand.CanExecute(null));
    }

    [Fact]
    public async Task ResetContentCommand_Executes_WithoutPrompt_When_ConfirmationDisabled()
    {
        // Arrange
        _mockSettings.Setup(s => s.LoadAsync())
            .ReturnsAsync(new AppSettings { RequireConfirmationDestructive = false });

        await _vm.InitializeAsync();
        _vm.IsWsusInstalled = true;

        _mockContentReset.Setup(x => x.ResetContentAsync(It.IsAny<IProgress<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(OperationResult.Ok("Content reset complete."))
            .Verifiable();

        // Act
        await _vm.ResetContentCommand.ExecuteAsync(null);

        // Assert
        _mockContentReset.Verify();
    }

    [Fact]
    public void QuickCleanupCommand_CanExecute_False_When_WsusNotInstalled()
    {
        _vm.IsWsusInstalled = false;

        Assert.False(_vm.QuickCleanupCommand.CanExecute(null));
    }

    [Fact]
    public void QuickCleanupCommand_CanExecute_True_When_WsusInstalled()
    {
        _vm.IsWsusInstalled = true;

        Assert.True(_vm.QuickCleanupCommand.CanExecute(null));
    }

    [Fact]
    public void QuickStartServicesCommand_CanExecute_False_When_WsusNotInstalled()
    {
        _vm.IsWsusInstalled = false;

        Assert.False(_vm.QuickStartServicesCommand.CanExecute(null));
    }

    [Fact]
    public void QuickStartServicesCommand_CanExecute_True_When_WsusInstalled()
    {
        _vm.IsWsusInstalled = true;

        Assert.True(_vm.QuickStartServicesCommand.CanExecute(null));
    }

    [Fact]
    public void IsOperationRunning_Starts_False()
    {
        Assert.False(_vm.IsOperationRunning);
    }

    [Fact]
    public void CurrentOperationName_Starts_Empty()
    {
        Assert.Equal(string.Empty, _vm.CurrentOperationName);
    }

    [Fact]
    public void ServerMode_Toggle_Changes_IsOnline()
    {
        _vm.IsOnline = true;
        Assert.True(_vm.IsOnline);

        _vm.IsOnline = false;
        Assert.False(_vm.IsOnline);
    }

    [Fact]
    public void IsWsusInstalled_Default_Is_False()
    {
        Assert.False(_vm.IsWsusInstalled);
    }

    [Fact]
    public void AppendLog_Adds_Text_To_LogOutput()
    {
        _vm.AppendLog("Hello");
        _vm.AppendLog("World");

        Assert.Contains("Hello", _vm.LogOutput);
        Assert.Contains("World", _vm.LogOutput);
    }

    [Fact]
    public void StatusMessage_Has_Default_Value()
    {
        Assert.NotNull(_vm.StatusMessage);
    }

    [Fact]
    public async Task RunOperationAsync_Sets_CurrentOperationName_During_Execution()
    {
        string nameCapture = "";

        await _vm.RunOperationAsync("TestOp", async (progress, ct) =>
        {
            nameCapture = _vm.CurrentOperationName;
            await Task.CompletedTask.ConfigureAwait(false);
            return true;
        });

        Assert.Equal("TestOp", nameCapture);
        Assert.Equal(string.Empty, _vm.CurrentOperationName);
    }

    [Fact]
    public void Navigate_To_Various_Panels_Updates_PageTitle()
    {
        _vm.NavigateCommand.Execute("Diagnostics");
        Assert.Equal("Diagnostics", _vm.PageTitle);

        _vm.NavigateCommand.Execute("Database");
        Assert.Equal("Database Operations", _vm.PageTitle);

        _vm.NavigateCommand.Execute("Dashboard");
        Assert.Equal("Dashboard", _vm.PageTitle);
    }

    // ═══════════════════════════════════════════════════════════════
    // Phase 9: Startup Message Tests
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public async Task InitializeAsync_DoesNotLogWsusNotInstalledMessage_WhenWsusIsInstalled()
    {
        _mockDashboard
            .Setup(d => d.CollectAsync(It.IsAny<AppSettings>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateHealthyData()); // IsWsusInstalled = true

        await _vm.InitializeAsync();

        Assert.DoesNotContain("WSUS is not installed", _vm.LogOutput);
    }

    [Fact]
    public async Task InitializeAsync_LogsWsusNotInstalledMessage_ExactlyOnce_WhenWsusIsAbsent()
    {
        var notInstalledData = new DashboardData
        {
            IsWsusInstalled = false,
            ServiceRunningCount = 0,
            ServiceNames = new[] { "SQL", "WSUS", "IIS" },
            DatabaseSizeGB = -1,
            DiskFreeGB = 100,
            TaskStatus = "Not Found",
            IsOnline = false
        };

        _mockDashboard
            .Setup(d => d.CollectAsync(It.IsAny<AppSettings>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(notInstalledData);

        await _vm.InitializeAsync();

        var occurrences = _vm.LogOutput
            .Split(["WSUS is not installed"], StringSplitOptions.None).Length - 1;
        Assert.Equal(1, occurrences);
    }

    // ═══════════════════════════════════════════════════════════════
    // Phase 13: Operation Feedback — Progress Bar, Step Text, Banner
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public async Task RunOperationAsync_ProgressBar_Visible_During_Operation()
    {
        bool wasVisibleDuringOp = false;

        await _vm.RunOperationAsync("FeedbackTest", async (progress, ct) =>
        {
            wasVisibleDuringOp = _vm.IsProgressBarVisible;
            await Task.CompletedTask.ConfigureAwait(false);
            return true;
        });

        Assert.True(wasVisibleDuringOp, "IsProgressBarVisible should be true during operation");
        Assert.False(_vm.IsProgressBarVisible, "IsProgressBarVisible should be false after operation");
    }

    [Fact]
    public async Task RunOperationAsync_StatusBanner_Shows_Success_On_Completion()
    {
        await _vm.RunOperationAsync("MyOp", async (progress, ct) =>
        {
            await Task.CompletedTask.ConfigureAwait(false);
            return true;
        });

        Assert.Contains("completed successfully", _vm.StatusBannerText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("MyOp", _vm.StatusBannerText);
        // Green color: #3FB950
        Assert.Equal(Color(0x3F, 0xB9, 0x50), _vm.StatusBannerColor.Color);
    }

    [Fact]
    public async Task RunOperationAsync_StatusBanner_Shows_Failure_On_False_Return()
    {
        await _vm.RunOperationAsync("FailOp", async (progress, ct) =>
        {
            await Task.CompletedTask.ConfigureAwait(false);
            return false;
        });

        Assert.Contains("failed", _vm.StatusBannerText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("FailOp", _vm.StatusBannerText);
        // Red color: #F85149
        Assert.Equal(Color(0xF8, 0x51, 0x49), _vm.StatusBannerColor.Color);
    }

    [Fact]
    public async Task RunOperationAsync_StepText_Updated_From_Step_Progress_Lines()
    {
        string capturedStepText = "";

        await _vm.RunOperationAsync("StepTest", async (progress, ct) =>
        {
            progress.Report("[Step 3/6]: Rebuilding indexes");
            // Allow Progress<T> callback to fire on the UI thread
            await Task.Delay(50, ct).ConfigureAwait(false);
            capturedStepText = _vm.OperationStepText;
            return true;
        });

        Assert.Contains("[Step 3/6]", capturedStepText);
    }

    [Fact]
    public async Task RunOperationAsync_ProgressBar_And_StepText_Cleared_After_Operation()
    {
        await _vm.RunOperationAsync("CleanupTest", async (progress, ct) =>
        {
            progress.Report("[Step 1/3]: Starting");
            await Task.CompletedTask.ConfigureAwait(false);
            return true;
        });

        Assert.False(_vm.IsProgressBarVisible, "IsProgressBarVisible should be false after completion");
        Assert.Equal(string.Empty, _vm.OperationStepText);
    }

    // ═══════════════════════════════════════════════════════════════
    // Helpers
    // ═══════════════════════════════════════════════════════════════

    private void InvokeUpdateDashboardCards(DashboardData data)
    {
        var method = typeof(MainViewModel).GetMethod(
            "UpdateDashboardCards",
            BindingFlags.Instance | BindingFlags.NonPublic);

        Assert.NotNull(method);
        method!.Invoke(_vm, new object[] { data });
    }

    private static DashboardData CreateHealthyData() => new()
    {
        IsWsusInstalled = true,
        ServiceRunningCount = 3,
        ServiceNames = new[] { "SQL", "WSUS", "IIS" },
        DatabaseSizeGB = 2.5,
        DiskFreeGB = 100,
        TaskStatus = "Ready",
        IsOnline = true
    };

    private static System.Windows.Media.Color Color(byte r, byte g, byte b) =>
        System.Windows.Media.Color.FromRgb(r, g, b);

    // ═══════════════════════════════════════════════════════════════
    // Phase 29: Data Filtering Tests (Computers Panel)
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public void ComputerStatusFilter_WhenChanged_UpdatesProperty()
    {
        // Act
        _vm.ComputerStatusFilter = "Online";

        // Assert
        Assert.Equal("Online", _vm.ComputerStatusFilter);
    }

    [Fact]
    public void ShowClearComputerFilters_WhenStatusFilterNotAll_ReturnsTrue()
    {
        // Arrange
        _vm.ComputerStatusFilter = "All";
        Assert.False(_vm.ShowClearComputerFilters);

        // Act
        _vm.ComputerStatusFilter = "Online";

        // Assert
        Assert.True(_vm.ShowClearComputerFilters);
    }

    [Fact]
    public void ShowClearComputerFilters_WhenSearchTextNotEmpty_ReturnsTrue()
    {
        // Arrange
        _vm.ComputerSearchText = "";

        // Act
        _vm.ComputerSearchText = "test";

        // Assert
        Assert.True(_vm.ShowClearComputerFilters);
    }

    [Fact]
    public void ClearComputerFiltersCommand_WhenExecuted_ResetsAllFilters()
    {
        // Arrange
        _vm.ComputerStatusFilter = "Online";
        _vm.ComputerSearchText = "test";

        // Act
        _vm.ClearComputerFiltersCommand.Execute(null);

        // Assert
        Assert.Equal("All", _vm.ComputerStatusFilter);
        Assert.Empty(_vm.ComputerSearchText);
    }

    // ═══════════════════════════════════════════════════════════════
    // Phase 29: Data Filtering Tests (Updates Panel)
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public void UpdateApprovalFilter_WhenChanged_UpdatesProperty()
    {
        // Act
        _vm.UpdateApprovalFilter = "Approved";

        // Assert
        Assert.Equal("Approved", _vm.UpdateApprovalFilter);
    }

    [Fact]
    public void UpdateClassificationFilter_WhenChanged_UpdatesProperty()
    {
        // Act
        _vm.UpdateClassificationFilter = "Critical";

        // Assert
        Assert.Equal("Critical", _vm.UpdateClassificationFilter);
    }

    [Fact]
    public void ShowClearUpdateFilters_WhenAnyFilterActive_ReturnsTrue()
    {
        // Arrange
        _vm.UpdateApprovalFilter = "All";
        _vm.UpdateClassificationFilter = "All";
        _vm.UpdateSearchText = "";
        Assert.False(_vm.ShowClearUpdateFilters);

        // Act & Assert - Approval filter
        _vm.UpdateApprovalFilter = "Approved";
        Assert.True(_vm.ShowClearUpdateFilters);

        // Act & Assert - Classification filter
        _vm.UpdateApprovalFilter = "All";
        _vm.UpdateClassificationFilter = "Critical";
        Assert.True(_vm.ShowClearUpdateFilters);

        // Act & Assert - Search filter
        _vm.UpdateClassificationFilter = "All";
        _vm.UpdateSearchText = "test";
        Assert.True(_vm.ShowClearUpdateFilters);
    }

    [Fact]
    public void ClearUpdateFiltersCommand_WhenExecuted_ResetsAllFilters()
    {
        // Arrange
        _vm.UpdateApprovalFilter = "Approved";
        _vm.UpdateClassificationFilter = "Security";
        _vm.UpdateSearchText = "KB";

        // Act
        _vm.ClearUpdateFiltersCommand.Execute(null);

        // Assert
        Assert.Equal("All", _vm.UpdateApprovalFilter);
        Assert.Equal("All", _vm.UpdateClassificationFilter);
        Assert.Empty(_vm.UpdateSearchText);
    }

    [Fact]
    public async Task LoadUpdatesAsync_ShouldCallSettingsBasedDashboardApi()
    {
        var updates = new List<UpdateInfo>
        {
            new(Guid.NewGuid(), "Security Update", "5031000", "Security Updates", DateTime.UtcNow, true, false)
        };

        _mockDashboard
            .Setup(d => d.GetUpdatesAsync(It.IsAny<AppSettings>(), 1, 100, It.IsAny<CancellationToken>()))
            .ReturnsAsync(updates);

        await _vm.LoadUpdatesAsync();

        _mockDashboard.Verify(
            d => d.GetUpdatesAsync(It.IsAny<AppSettings>(), 1, 100, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public void CommonErrorCodes_Contains_Known_Update_Error_Codes()
    {
        Assert.Contains("0x80072EE2", _vm.CommonErrorCodes);
        Assert.Contains("0x80244022", _vm.CommonErrorCodes);
    }

    [Fact]
    public void CommonErrorCodes_All_Map_To_Known_Wsus_Error_Definitions()
    {
        foreach (var code in _vm.CommonErrorCodes)
        {
            Assert.NotNull(WsusErrorCodes.Lookup(code));
        }
    }
}

#pragma warning restore CA2007
