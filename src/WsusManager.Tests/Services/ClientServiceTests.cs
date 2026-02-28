using Moq;
using WsusManager.Core.Infrastructure;
using WsusManager.Core.Logging;
using WsusManager.Core.Models;
using WsusManager.Core.Services;

namespace WsusManager.Tests.Services;

// ────────────────────────────────────────────────────────────────────────────────
// EDGE CASE AUDIT (Phase 18-02):
// ────────────────────────────────────────────────────────────────────────────────
// High Priority - External data handlers (WinRM inputs, computer names):
// [ ] Null hostname: CancelStuckJobsAsync(null, ...) - missing
// [ ] Null hostname: ForceCheckInAsync(null, ...) - missing
// [ ] Null hostname: TestConnectivityAsync(null, ...) - missing
// [ ] Null hostname: RunDiagnosticsAsync(null, ...) - missing
// [ ] Null errorCode: LookupErrorCode(null) - missing
// [ ] Null hostnames list: MassForceCheckInAsync(null, ...) - missing
// [x] Empty hostname: CancelStuckJobsAsync("", ...) - tested
// [ ] Empty hostname: ForceCheckInAsync("", ...) - missing
// [ ] Empty hostname: TestConnectivityAsync("", ...) - missing
// [ ] Empty hostname: RunDiagnosticsAsync("", ...) - missing
// [ ] Empty errorCode: LookupErrorCode("") - missing
// [x] Empty hostnames list: MassForceCheckInAsync([], ...) - tested
// [ ] Whitespace hostname: "   ", "\t", "\n" - partially tested (MassForceCheckIn)
// [ ] Invalid computer name: "INVALID\\NAME/FORMAT", "../path" - missing
// [ ] Boundary: Very long hostname (>255 chars) - missing
// [ ] Boundary: hostname with null characters - missing
// [ ] Null wsusServerUrl: TestConnectivityAsync(..., null, ...) - missing
// [ ] Empty wsusServerUrl: TestConnectivityAsync(..., "", ...) - missing
// [ ] Invalid URL format: "not-a-url", "http://" - partially tested (ExtractHostname)
// ────────────────────────────────────────────────────────────────────────────────

/// <summary>
/// Unit tests for <see cref="ClientService"/> using mocked IProcessRunner and ILogService.
/// WinRmExecutor is constructed from the mock runner so its real logic is exercised.
/// </summary>
public class ClientServiceTests
{
    private readonly Mock<IProcessRunner> _mockRunner = new();
    private readonly Mock<ILogService> _mockLog = new();

    private ClientService CreateService()
    {
        var executor = new WinRmExecutor(_mockRunner.Object, _mockLog.Object);
        return new ClientService(executor, _mockLog.Object);
    }

    /// <summary>
    /// Creates a ProcessResult representing a successful process exit (exit code 0).
    /// </summary>
    private static ProcessResult SuccessResult(string output = "OK") =>
        new(0, [output]);

    /// <summary>
    /// Creates a ProcessResult representing a failed process exit (exit code 1).
    /// </summary>
    private static ProcessResult FailResult(string output = "Error occurred") =>
        new(1, [output]);

    /// <summary>
    /// Creates a ProcessResult that simulates a WinRM connectivity failure.
    /// </summary>
    private static ProcessResult WinRmFailResult() =>
        new(1, ["WinRM cannot complete the operation. Verify WSManFault."]);

    /// <summary>
    /// Captures progress messages reported during an operation.
    /// Uses a thread-safe collection because Progress&lt;T&gt; can post callbacks on the thread pool
    /// when there is no SynchronizationContext (e.g., in xUnit tests).
    /// </summary>
    private static (System.Collections.Concurrent.ConcurrentBag<string> Messages, IProgress<string> Progress) CreateProgressCapture()
    {
        var messages = new System.Collections.Concurrent.ConcurrentBag<string>();
        var progress = new Progress<string>(m => messages.Add(m));
        return (messages, progress);
    }

    // -------------------------------------------------------------------------
    // CancelStuckJobsAsync
    // -------------------------------------------------------------------------

    [Fact]
    public async Task CancelStuckJobs_Returns_Success_When_Remote_Succeeds()
    {
        // Arrange: Test-WSMan succeeds, then the remote command succeeds
        _mockRunner
            .Setup(r => r.RunAsync("powershell.exe",
                It.Is<string>(a => a.Contains("Test-WSMan")),
                It.IsAny<IProgress<string>?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(SuccessResult("WSMan responding"));

        _mockRunner
            .Setup(r => r.RunAsync("powershell.exe",
                It.Is<string>(a => a.Contains("Invoke-Command")),
                It.IsAny<IProgress<string>?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(SuccessResult("STEP=StopServices\nSTEP=ClearCache\nSTEP=RestartServices\nSTEP=Done"));

        var (_, progress) = CreateProgressCapture();
        var service = CreateService();

        // Act
        var result = await service.CancelStuckJobsAsync("testhost01", progress);

        // Assert
        Assert.True(result.Success);
        Assert.Contains("testhost01", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CancelStuckJobs_Returns_Failure_When_WinRm_Unavailable()
    {
        // Arrange: Test-WSMan fails
        _mockRunner
            .Setup(r => r.RunAsync("powershell.exe",
                It.Is<string>(a => a.Contains("Test-WSMan")),
                It.IsAny<IProgress<string>?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(WinRmFailResult());

        var (messages, progress) = CreateProgressCapture();
        var service = CreateService();

        // Act
        var result = await service.CancelStuckJobsAsync("unreachable-host", progress);

        // Assert
        Assert.False(result.Success);
        Assert.Contains("WinRM", result.Message, StringComparison.OrdinalIgnoreCase);

        // Should not have made a remote command call
        _mockRunner.Verify(r => r.RunAsync("powershell.exe",
            It.Is<string>(a => a.Contains("Invoke-Command")),
            It.IsAny<IProgress<string>?>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task CancelStuckJobs_Validates_Hostname_Returns_Failure_For_Empty()
    {
        // Arrange: no mock setup needed — validation should fail before any process call
        var service = CreateService();
        var (_, progress) = CreateProgressCapture();

        // Act
        var result = await service.CancelStuckJobsAsync("", progress);

        // Assert
        Assert.False(result.Success);
        Assert.Contains("empty", result.Message, StringComparison.OrdinalIgnoreCase);

        // No process should have been launched
        _mockRunner.Verify(r => r.RunAsync(
            It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<IProgress<string>?>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    // -------------------------------------------------------------------------
    // ForceCheckInAsync
    // -------------------------------------------------------------------------

    [Fact]
    public async Task ForceCheckIn_Returns_Success_When_Remote_Succeeds()
    {
        // Arrange: WinRM available, remote command succeeds
        _mockRunner
            .Setup(r => r.RunAsync("powershell.exe",
                It.Is<string>(a => a.Contains("Test-WSMan")),
                It.IsAny<IProgress<string>?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(SuccessResult());

        _mockRunner
            .Setup(r => r.RunAsync("powershell.exe",
                It.Is<string>(a => a.Contains("Invoke-Command")),
                It.IsAny<IProgress<string>?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(SuccessResult("STEP=GpUpdate\nSTEP=ResetAuth\nSTEP=DetectNow\nSTEP=ReportNow\nSTEP=Done"));

        var service = CreateService();
        var (_, progress) = CreateProgressCapture();

        // Act
        var result = await service.ForceCheckInAsync("testclient", progress);

        // Assert
        Assert.True(result.Success);
        Assert.Contains("testclient", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ForceCheckIn_Reports_Step_Progress()
    {
        // Arrange: WinRM available, remote command returns step markers
        _mockRunner
            .Setup(r => r.RunAsync("powershell.exe",
                It.Is<string>(a => a.Contains("Test-WSMan")),
                It.IsAny<IProgress<string>?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(SuccessResult());

        _mockRunner
            .Setup(r => r.RunAsync("powershell.exe",
                It.Is<string>(a => a.Contains("Invoke-Command")),
                It.IsAny<IProgress<string>?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProcessResult(0, [
                "STEP=GpUpdate",
                "STEP=ResetAuth",
                "STEP=DetectNow",
                "STEP=ReportNow",
                "STEP=Done"
            ]));

        var (messages, progress) = CreateProgressCapture();
        var service = CreateService();

        // Act
        await service.ForceCheckInAsync("testclient", progress);

        // Assert: progress messages should include [Step] prefixes
        Assert.True(messages.Any(m => m.Contains("[Step")),
            $"Expected [Step] progress message. Messages: {string.Join("; ", messages)}");
    }

    // -------------------------------------------------------------------------
    // TestConnectivityAsync
    // -------------------------------------------------------------------------

    [Fact]
    public async Task TestConnectivity_Parses_Port_Results_Correctly()
    {
        // Arrange: WinRM available, remote command returns parsed connectivity data
        _mockRunner
            .Setup(r => r.RunAsync("powershell.exe",
                It.Is<string>(a => a.Contains("Test-WSMan")),
                It.IsAny<IProgress<string>?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(SuccessResult());

        _mockRunner
            .Setup(r => r.RunAsync("powershell.exe",
                It.Is<string>(a => a.Contains("Invoke-Command")),
                It.IsAny<IProgress<string>?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProcessResult(0, ["PORT8530=True;PORT8531=False;LATENCY=5"]));

        var (_, progress) = CreateProgressCapture();
        var service = CreateService();

        // Act
        var result = await service.TestConnectivityAsync(
            "testclient", "http://wsus-server:8530", progress);

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.True(result.Data.Port8530Reachable);
        Assert.False(result.Data.Port8531Reachable);
        Assert.Equal(5, result.Data.LatencyMs);
    }

    [Fact]
    public async Task TestConnectivity_Returns_Failure_When_WinRm_Unavailable()
    {
        // Arrange: WinRM test fails
        _mockRunner
            .Setup(r => r.RunAsync("powershell.exe",
                It.Is<string>(a => a.Contains("Test-WSMan")),
                It.IsAny<IProgress<string>?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(WinRmFailResult());

        var (_, progress) = CreateProgressCapture();
        var service = CreateService();

        // Act
        var result = await service.TestConnectivityAsync(
            "disconnected-host", "http://wsus01:8530", progress);

        // Assert
        Assert.False(result.Success);
        Assert.Contains("WinRM", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void TestConnectivity_Extracts_Server_From_Url()
    {
        // Verify the URL parsing extracts "wsus01" from "http://wsus01:8530"
        var extracted = ClientService.ExtractHostname("http://wsus01:8530");
        Assert.Equal("wsus01", extracted);

        // Also test https, no port
        Assert.Equal("wsus-server", ClientService.ExtractHostname("https://wsus-server"));
        Assert.Equal("192.168.1.100", ClientService.ExtractHostname("http://192.168.1.100:8531/path"));
        Assert.Equal("plainname", ClientService.ExtractHostname("plainname"));
    }

    // -------------------------------------------------------------------------
    // RunDiagnosticsAsync
    // -------------------------------------------------------------------------

    [Fact]
    public async Task RunDiagnostics_Parses_Registry_Values_Correctly()
    {
        // Arrange: WinRM available, remote returns populated diagnostic line
        _mockRunner
            .Setup(r => r.RunAsync("powershell.exe",
                It.Is<string>(a => a.Contains("Test-WSMan")),
                It.IsAny<IProgress<string>?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(SuccessResult());

        // Simulate output with known values
        const string diagnosticOutput =
            "WSUS=http://wsus01:8530;STATUS=http://wsus01:8530;USE=1;SVCS=wuauserv=Running,bits=Running,cryptsvc=Stopped;REBOOT=False;LASTCHECKIN=;AGENT=10.0.19041.1949";

        _mockRunner
            .Setup(r => r.RunAsync("powershell.exe",
                It.Is<string>(a => a.Contains("Invoke-Command")),
                It.IsAny<IProgress<string>?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProcessResult(0, [diagnosticOutput]));

        var (_, progress) = CreateProgressCapture();
        var service = CreateService();

        // Act
        var result = await service.RunDiagnosticsAsync("testclient", progress);

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Equal("http://wsus01:8530", result.Data.WsusServerUrl);
        Assert.Equal("http://wsus01:8530", result.Data.WsusStatusServerUrl);
        Assert.True(result.Data.UseWUServer);
        Assert.False(result.Data.PendingRebootRequired);
        Assert.Equal("10.0.19041.1949", result.Data.WindowsUpdateAgentVersion);
        Assert.Equal("Running", result.Data.ServiceStatuses["wuauserv"]);
        Assert.Equal("Running", result.Data.ServiceStatuses["bits"]);
        Assert.Equal("Stopped", result.Data.ServiceStatuses["cryptsvc"]);
    }

    [Fact]
    public async Task RunDiagnostics_Handles_Missing_Registry_Keys()
    {
        // Arrange: WinRM available, remote returns minimal/empty values
        _mockRunner
            .Setup(r => r.RunAsync("powershell.exe",
                It.Is<string>(a => a.Contains("Test-WSMan")),
                It.IsAny<IProgress<string>?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(SuccessResult());

        // Simulate PowerShell output where registry keys are not present (empty/null values)
        const string minimalOutput =
            "WSUS=;STATUS=;USE=;SVCS=;REBOOT=False;LASTCHECKIN=;AGENT=";

        _mockRunner
            .Setup(r => r.RunAsync("powershell.exe",
                It.Is<string>(a => a.Contains("Invoke-Command")),
                It.IsAny<IProgress<string>?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProcessResult(0, [minimalOutput]));

        var (_, progress) = CreateProgressCapture();
        var service = CreateService();

        // Act — should not throw
        var result = await service.RunDiagnosticsAsync("freshclient", progress);

        // Assert: completes without exception, data has sensible defaults
        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Null(result.Data.WsusServerUrl);
        Assert.Null(result.Data.WsusStatusServerUrl);
        Assert.False(result.Data.UseWUServer);
        Assert.False(result.Data.PendingRebootRequired);
        Assert.Null(result.Data.WindowsUpdateAgentVersion);
        Assert.Empty(result.Data.ServiceStatuses);
    }

    // -------------------------------------------------------------------------
    // LookupErrorCode
    // -------------------------------------------------------------------------

    [Fact]
    public void LookupErrorCode_Returns_Known_Code()
    {
        var service = CreateService();

        // 0x80072EE2 = WININET_E_TIMEOUT
        var result = service.LookupErrorCode("0x80072EE2");

        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Equal("0x80072EE2", result.Data.Code);

        // Description should mention timeout or connection
        var description = result.Data.Description.ToLowerInvariant();
        Assert.True(
            description.Contains("timeout") || description.Contains("connect"),
            $"Expected 'timeout' or 'connect' in description: '{result.Data.Description}'");
    }

    [Fact]
    public void LookupErrorCode_Returns_Failure_For_Unknown_Code()
    {
        var service = CreateService();

        var result = service.LookupErrorCode("0xDEADBEEF");

        Assert.False(result.Success);
        Assert.Null(result.Data);
        Assert.Contains("not found", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void LookupErrorCode_Works_Without_0x_Prefix()
    {
        var service = CreateService();

        // Same code, without 0x prefix
        var result = service.LookupErrorCode("80072EE2");

        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Equal("0x80072EE2", result.Data.Code);
    }

    [Fact]
    public void LookupErrorCode_Works_With_Decimal_Input()
    {
        var service = CreateService();

        // -2147012894 is the signed decimal of 0x80072EE2
        var result = service.LookupErrorCode("-2147012894");

        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Equal("0x80072EE2", result.Data.Code);
    }

    [Fact]
    public void LookupErrorCode_Recognizes_0x80244022()
    {
        var service = CreateService();

        var result = service.LookupErrorCode("0x80244022");

        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Equal("0x80244022", result.Data.Code);
    }

    [Theory]
    [InlineData("0x80070643")]
    [InlineData("0x80242016")]
    public void LookupErrorCode_Recognizes_Trailing_Common_Codes(string code)
    {
        var service = CreateService();

        var result = service.LookupErrorCode(code);

        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Equal(code, result.Data.Code);
    }

    // -------------------------------------------------------------------------
    // MassForceCheckInAsync
    // -------------------------------------------------------------------------

    [Fact]
    public async Task MassForceCheckIn_AllSucceed_Returns_True_And_Mentions_All_Hosts()
    {
        // Arrange: WinRM available for all 3 hosts, all remote commands succeed
        _mockRunner
            .Setup(r => r.RunAsync("powershell.exe",
                It.Is<string>(a => a.Contains("Test-WSMan")),
                It.IsAny<IProgress<string>?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(SuccessResult());

        _mockRunner
            .Setup(r => r.RunAsync("powershell.exe",
                It.Is<string>(a => a.Contains("Invoke-Command")),
                It.IsAny<IProgress<string>?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(SuccessResult("STEP=GpUpdate\nSTEP=ResetAuth\nSTEP=DetectNow\nSTEP=ReportNow\nSTEP=Done"));

        var hostnames = new List<string> { "host01", "host02", "host03" };
        var (messages, progress) = CreateProgressCapture();
        var service = CreateService();

        // Act
        var result = await service.MassForceCheckInAsync(hostnames, progress);

        // Assert
        Assert.True(result.Success);
        // Summary should mention 3/3 succeeded
        Assert.Contains("3/3", result.Message, StringComparison.OrdinalIgnoreCase);
        // Progress messages should include per-host markers
        Assert.True(messages.Any(m => m.Contains("host01")),
            "Expected progress message mentioning host01");
        Assert.True(messages.Any(m => m.Contains("host02")),
            "Expected progress message mentioning host02");
        Assert.True(messages.Any(m => m.Contains("host03")),
            "Expected progress message mentioning host03");
    }

    [Fact]
    public async Task MassForceCheckIn_OneHostFails_Returns_False_Shows_PassFail_Count()
    {
        // Arrange: WinRM available on host1 (success), host2 WinRM unavailable (fail)
        _mockRunner
            .Setup(r => r.RunAsync("powershell.exe",
                It.Is<string>(a => a.Contains("Test-WSMan") && a.Contains("host-ok")),
                It.IsAny<IProgress<string>?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(SuccessResult());

        _mockRunner
            .Setup(r => r.RunAsync("powershell.exe",
                It.Is<string>(a => a.Contains("Test-WSMan") && a.Contains("host-fail")),
                It.IsAny<IProgress<string>?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(WinRmFailResult());

        _mockRunner
            .Setup(r => r.RunAsync("powershell.exe",
                It.Is<string>(a => a.Contains("Invoke-Command")),
                It.IsAny<IProgress<string>?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(SuccessResult("STEP=Done"));

        var hostnames = new List<string> { "host-ok", "host-fail" };
        var (messages, progress) = CreateProgressCapture();
        var service = CreateService();

        // Act
        var result = await service.MassForceCheckInAsync(hostnames, progress);

        // Assert
        Assert.False(result.Success);
        // Summary should show 1 passed, 1 failed
        Assert.Contains("1/2", result.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("1 failed", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task MassForceCheckIn_EmptyList_Returns_Failure_With_Descriptive_Message()
    {
        // Arrange: empty list — no mock setup needed, validation fires first
        var service = CreateService();
        var (_, progress) = CreateProgressCapture();

        // Act
        var result = await service.MassForceCheckInAsync([], progress);

        // Assert
        Assert.False(result.Success);
        Assert.Contains("hostname", result.Message, StringComparison.OrdinalIgnoreCase);

        // No process should have been launched
        _mockRunner.Verify(r => r.RunAsync(
            It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<IProgress<string>?>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task MassForceCheckIn_WhitespaceOnlyEntries_TreatedAsEmpty()
    {
        // Arrange: list with only whitespace/empty entries — treated as empty
        var service = CreateService();
        var (_, progress) = CreateProgressCapture();

        // Act: whitespace-only entries should be filtered out, leaving an empty valid list
        var result = await service.MassForceCheckInAsync(["   ", "", "\t"], progress);

        // Assert: same behaviour as empty list
        Assert.False(result.Success);
        Assert.Contains("hostname", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task MassForceCheckIn_Cancellation_StopsProcessingRemainingHosts()
    {
        // Arrange: use CancellationTokenSource cancelled after first host
        using var cts = new CancellationTokenSource();

        int callCount = 0;
        _mockRunner
            .Setup(r => r.RunAsync("powershell.exe",
                It.Is<string>(a => a.Contains("Test-WSMan")),
                It.IsAny<IProgress<string>?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                callCount++;
                // Cancel after the first Test-WSMan call
                if (callCount >= 1) cts.Cancel();
                return SuccessResult();
            });

        _mockRunner
            .Setup(r => r.RunAsync("powershell.exe",
                It.Is<string>(a => a.Contains("Invoke-Command")),
                It.IsAny<IProgress<string>?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(SuccessResult("STEP=Done"));

        var hostnames = new List<string> { "host01", "host02", "host03" };
        var (messages, progress) = CreateProgressCapture();
        var service = CreateService();

        // Act: should throw OperationCanceledException from the ct.ThrowIfCancellationRequested()
        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            service.MassForceCheckInAsync(hostnames, progress, cts.Token));
    }

    [Fact]
    public async Task RunFleetWsusTargetAuditAsync_EmptyHostList_Returns_Failure()
    {
        var service = CreateService();
        var (_, progress) = CreateProgressCapture();

        var result = await service.RunFleetWsusTargetAuditAsync(
            [],
            "wsus01",
            8530,
            8531,
            progress,
            CancellationToken.None);

        Assert.False(result.Success);
        Assert.Contains("host", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RunFleetWsusTargetAuditAsync_Compliant_When_UseWUServer_And_Urls_Match_Baseline()
    {
        _mockRunner
            .Setup(r => r.RunAsync("powershell.exe",
                It.Is<string>(a => a.Contains("Test-WSMan")),
                It.IsAny<IProgress<string>?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(SuccessResult("WSMan responding"));

        _mockRunner
            .Setup(r => r.RunAsync("powershell.exe",
                It.Is<string>(a => a.Contains("Invoke-Command") && a.Contains("host-a")),
                It.IsAny<IProgress<string>?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProcessResult(0, [
                "WSUS=http://wsus01:8530;STATUS=https://wsus01:8531;USE=1"
            ]));

        var service = CreateService();
        var (_, progress) = CreateProgressCapture();

        var result = await service.RunFleetWsusTargetAuditAsync(
            ["host-a"],
            "wsus01",
            8530,
            8531,
            progress,
            CancellationToken.None);

        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Equal(1, result.Data.TotalHosts);
        Assert.Equal(1, result.Data.CompliantHosts);
        Assert.Equal(0, result.Data.MismatchHosts);
        Assert.Equal(0, result.Data.UnreachableHosts);
        Assert.Equal(0, result.Data.ErrorHosts);

        var item = Assert.Single(result.Data.Items);
        Assert.Equal("host-a", item.Hostname);
        Assert.Equal(FleetComplianceStatus.Compliant, item.ComplianceStatus);
        Assert.Equal("wsus01", item.ReportedWsusHostname);
        Assert.Equal(8530, item.ReportedHttpPort);
        Assert.Equal(8531, item.ReportedHttpsPort);
    }

    [Fact]
    public async Task RunFleetWsusTargetAuditAsync_Mismatch_When_UseWUServer_False_Or_Urls_Differ()
    {
        _mockRunner
            .Setup(r => r.RunAsync("powershell.exe",
                It.Is<string>(a => a.Contains("Test-WSMan")),
                It.IsAny<IProgress<string>?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(SuccessResult("WSMan responding"));

        _mockRunner
            .Setup(r => r.RunAsync("powershell.exe",
                It.Is<string>(a => a.Contains("Invoke-Command") && a.Contains("host-usefalse")),
                It.IsAny<IProgress<string>?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProcessResult(0, [
                "WSUS=http://wsus01:8530;STATUS=https://wsus01:8531;USE=0"
            ]));

        _mockRunner
            .Setup(r => r.RunAsync("powershell.exe",
                It.Is<string>(a => a.Contains("Invoke-Command") && a.Contains("host-wrongurl")),
                It.IsAny<IProgress<string>?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProcessResult(0, [
                "WSUS=http://wrong-wsus:8530;STATUS=https://wrong-wsus:8531;USE=1"
            ]));

        var service = CreateService();
        var (_, progress) = CreateProgressCapture();

        var result = await service.RunFleetWsusTargetAuditAsync(
            ["host-usefalse", "host-wrongurl"],
            "wsus01",
            8530,
            8531,
            progress,
            CancellationToken.None);

        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Equal(2, result.Data.TotalHosts);
        Assert.Equal(0, result.Data.CompliantHosts);
        Assert.Equal(2, result.Data.MismatchHosts);
        Assert.Equal(0, result.Data.UnreachableHosts);
        Assert.Equal(0, result.Data.ErrorHosts);
        Assert.All(result.Data.Items, item => Assert.Equal(FleetComplianceStatus.Mismatch, item.ComplianceStatus));
    }

    [Fact]
    public async Task RunFleetWsusTargetAuditAsync_Unreachable_When_WinRm_Unavailable()
    {
        _mockRunner
            .Setup(r => r.RunAsync("powershell.exe",
                It.Is<string>(a => a.Contains("Test-WSMan") && a.Contains("host-down")),
                It.IsAny<IProgress<string>?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(WinRmFailResult());

        var service = CreateService();
        var (_, progress) = CreateProgressCapture();

        var result = await service.RunFleetWsusTargetAuditAsync(
            ["host-down"],
            "wsus01",
            8530,
            8531,
            progress,
            CancellationToken.None);

        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Equal(1, result.Data.TotalHosts);
        Assert.Equal(0, result.Data.CompliantHosts);
        Assert.Equal(0, result.Data.MismatchHosts);
        Assert.Equal(1, result.Data.UnreachableHosts);
        Assert.Equal(0, result.Data.ErrorHosts);
        Assert.Equal(FleetComplianceStatus.Unreachable, Assert.Single(result.Data.Items).ComplianceStatus);

        _mockRunner.Verify(r => r.RunAsync("powershell.exe",
            It.Is<string>(a => a.Contains("Invoke-Command") && a.Contains("host-down")),
            It.IsAny<IProgress<string>?>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task RunFleetWsusTargetAuditAsync_GroupedTargets_Returns_Correct_Counts()
    {
        _mockRunner
            .Setup(r => r.RunAsync("powershell.exe",
                It.Is<string>(a => a.Contains("Test-WSMan")),
                It.IsAny<IProgress<string>?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(SuccessResult("WSMan responding"));

        _mockRunner
            .Setup(r => r.RunAsync("powershell.exe",
                It.Is<string>(a => a.Contains("Invoke-Command") && a.Contains("host-01")),
                It.IsAny<IProgress<string>?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProcessResult(0, [
                "WSUS=http://wsus01:8530;STATUS=https://wsus01:8531;USE=1"
            ]));

        _mockRunner
            .Setup(r => r.RunAsync("powershell.exe",
                It.Is<string>(a => a.Contains("Invoke-Command") && a.Contains("host-02")),
                It.IsAny<IProgress<string>?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProcessResult(0, [
                "WSUS=http://wsus01:8530;STATUS=https://wsus01:8531;USE=1"
            ]));

        _mockRunner
            .Setup(r => r.RunAsync("powershell.exe",
                It.Is<string>(a => a.Contains("Invoke-Command") && a.Contains("host-03")),
                It.IsAny<IProgress<string>?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProcessResult(0, [
                "WSUS=http://wsus02:8530;STATUS=https://wsus02:8531;USE=1"
            ]));

        var service = CreateService();
        var (_, progress) = CreateProgressCapture();

        var result = await service.RunFleetWsusTargetAuditAsync(
            ["host-01", "host-02", "host-03"],
            "wsus01",
            8530,
            8531,
            progress,
            CancellationToken.None);

        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Equal(2, result.Data.GroupedTargets["http://wsus01:8530|https://wsus01:8531"]);
        Assert.Equal(1, result.Data.GroupedTargets["http://wsus02:8530|https://wsus02:8531"]);
    }

    [Fact]
    public async Task RunFleetWsusTargetAuditAsync_Mismatch_When_Wsus_And_Status_Urls_Are_Swapped()
    {
        _mockRunner
            .Setup(r => r.RunAsync("powershell.exe",
                It.Is<string>(a => a.Contains("Test-WSMan")),
                It.IsAny<IProgress<string>?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(SuccessResult("WSMan responding"));

        _mockRunner
            .Setup(r => r.RunAsync("powershell.exe",
                It.Is<string>(a => a.Contains("Invoke-Command") && a.Contains("host-swapped")),
                It.IsAny<IProgress<string>?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProcessResult(0, [
                "WSUS=https://wsus01:8531;STATUS=http://wsus01:8530;USE=1"
            ]));

        var service = CreateService();
        var (_, progress) = CreateProgressCapture();

        var result = await service.RunFleetWsusTargetAuditAsync(
            ["host-swapped"],
            "wsus01",
            8530,
            8531,
            progress,
            CancellationToken.None);

        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Equal(1, result.Data.TotalHosts);
        Assert.Equal(0, result.Data.CompliantHosts);
        Assert.Equal(1, result.Data.MismatchHosts);
        Assert.Equal(FleetComplianceStatus.Mismatch, Assert.Single(result.Data.Items).ComplianceStatus);
    }

    // ─── Edge Case Tests (Phase 18-02) ────────────────────────────────────────

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public async Task CancelStuckJobsAsync_Handles_Null_Or_Empty_Hostname(string hostname)
    {
        var service = CreateService();
        var (_, progress) = CreateProgressCapture();

        // Already tested for empty string - this extends to null
        if (hostname == null)
        {
            var result = await service.CancelStuckJobsAsync(null!, progress);
            Assert.False(result.Success);
        }
        else
        {
            var result = await service.CancelStuckJobsAsync(hostname, progress);
            Assert.False(result.Success);
        }
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task ForceCheckInAsync_Handles_Null_Empty_Whitespace_Hostname(string hostname)
    {
        var service = CreateService();
        var (_, progress) = CreateProgressCapture();

        if (hostname == null)
        {
            var result = await service.ForceCheckInAsync(null!, progress);
            Assert.False(result.Success);
        }
        else
        {
            var result = await service.ForceCheckInAsync(hostname, progress);
            Assert.False(result.Success);
            Assert.Contains("empty", result.Message, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("INVALID\\NAME/FORMAT")]
    [InlineData("../path")]
    public async Task TestConnectivityAsync_Handles_Invalid_Hostnames(string hostname)
    {
        var (_, progress) = CreateProgressCapture();
        var service = CreateService();

        var result = await service.TestConnectivityAsync(hostname, "http://wsus01:8530", progress);

        Assert.False(result.Success);
        Assert.Null(result.Data);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task TestConnectivityAsync_Handles_Null_Empty_Whitespace_WsusServerUrls(string wsusServerUrl)
    {
        var (_, progress) = CreateProgressCapture();
        var service = CreateService();

        var result = await service.TestConnectivityAsync("testhost", wsusServerUrl, progress);

        Assert.False(result.Success);
        Assert.Null(result.Data);
        Assert.Contains("WSUS", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task TestConnectivityAsync_Handles_Invalid_Url_Without_Colon()
    {
        var (_, progress) = CreateProgressCapture();
        var service = CreateService();

        var result = await service.TestConnectivityAsync("testhost", "invalid-url", progress);

        Assert.False(result.Success);
        Assert.Null(result.Data);
        Assert.Contains("WSUS", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task TestConnectivityAsync_Handles_Invalid_Url_With_Double_Slash()
    {
        _mockRunner
            .Setup(r => r.RunAsync("powershell.exe",
                It.Is<string>(a => a.Contains("Test-WSMan")),
                It.IsAny<IProgress<string>?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(SuccessResult());

        var (_, progress) = CreateProgressCapture();
        var service = CreateService();

        // "http://" returns empty string from ExtractHostname
        var result = await service.TestConnectivityAsync("testhost", "http://", progress);

        // Empty hostname causes WinRM failure
        Assert.False(result.Success);
    }


    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task RunDiagnosticsAsync_Handles_Null_Empty_Whitespace_Hostname(string hostname)
    {
        _mockRunner
            .Setup(r => r.RunAsync("powershell.exe",
                It.Is<string>(a => a.Contains("Test-WSMan")),
                It.IsAny<IProgress<string>?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(SuccessResult());

        var (_, progress) = CreateProgressCapture();
        var service = CreateService();

        if (hostname == null)
        {
            var result = await service.RunDiagnosticsAsync(null!, progress);
            Assert.False(result.Success);
        }
        else
        {
            var result = await service.RunDiagnosticsAsync(hostname, progress);
            Assert.False(result.Success);
            Assert.Contains("empty", result.Message, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void LookupErrorCode_Handles_Null_Empty_Whitespace_ErrorCode(string errorCode)
    {
        var service = CreateService();

        var result = service.LookupErrorCode(errorCode);

        Assert.False(result.Success);
        Assert.Contains("not found", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task MassForceCheckInAsync_Handles_Null_Hostnames()
    {
        var service = CreateService();
        var (_, progress) = CreateProgressCapture();

        // null list throws ArgumentNullException from LINQ Select
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => service.MassForceCheckInAsync(null!, progress));
    }

    [Fact]
    public async Task MassForceCheckInAsync_Handles_Single_Hostname_List()
    {
        _mockRunner
            .Setup(r => r.RunAsync("powershell.exe",
                It.Is<string>(a => a.Contains("Test-WSMan")),
                It.IsAny<IProgress<string>?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(SuccessResult());

        _mockRunner
            .Setup(r => r.RunAsync("powershell.exe",
                It.Is<string>(a => a.Contains("Invoke-Command")),
                It.IsAny<IProgress<string>?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(SuccessResult("STEP=Done"));

        var service = CreateService();
        var (_, progress) = CreateProgressCapture();

        // Single element list - boundary case
        var result = await service.MassForceCheckInAsync(new List<string> { "host01" }, progress);

        Assert.True(result.Success);
        Assert.Contains("1/1", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task MassForceCheckInAsync_Handles_Large_Hostname_List()
    {
        _mockRunner
            .Setup(r => r.RunAsync("powershell.exe",
                It.Is<string>(a => a.Contains("Test-WSMan")),
                It.IsAny<IProgress<string>?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(SuccessResult());

        _mockRunner
            .Setup(r => r.RunAsync("powershell.exe",
                It.Is<string>(a => a.Contains("Invoke-Command")),
                It.IsAny<IProgress<string>?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(SuccessResult("STEP=Done"));

        var service = CreateService();
        var (_, progress) = CreateProgressCapture();

        // Large list (100 hosts) - boundary case
        var hostnames = Enumerable.Range(1, 100).Select(i => $"host{i:D3}").ToList();
        var result = await service.MassForceCheckInAsync(hostnames, progress);

        Assert.True(result.Success);
        Assert.Contains("100/100", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task MassForceCheckInAsync_Handles_Very_Long_Hostname()
    {
        _mockRunner
            .Setup(r => r.RunAsync("powershell.exe",
                It.Is<string>(a => a.Contains("Test-WSMan")),
                It.IsAny<IProgress<string>?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(SuccessResult());

        _mockRunner
            .Setup(r => r.RunAsync("powershell.exe",
                It.Is<string>(a => a.Contains("Invoke-Command")),
                It.IsAny<IProgress<string>?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(SuccessResult("STEP=Done"));

        var service = CreateService();
        var (_, progress) = CreateProgressCapture();

        // Very long hostname (255 chars - max NetBIOS name length)
        var longHostname = new string('a', 255);
        var result = await service.MassForceCheckInAsync(new List<string> { longHostname }, progress);

        Assert.False(result.Success);
        Assert.Contains("0/1", result.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("1 failed", result.Message, StringComparison.OrdinalIgnoreCase);
    }
}
