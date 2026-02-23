using Moq;
using WsusManager.Core.Infrastructure;
using WsusManager.Core.Logging;
using WsusManager.Core.Models;
using WsusManager.Core.Services;
using WsusManager.Core.Services.Interfaces;

namespace WsusManager.Tests.Services;

/// <summary>
/// Tests for InstallationService: prerequisite validation, argument construction,
/// script path resolution, and progress reporting.
/// </summary>
public class InstallationServiceTests
{
    private readonly Mock<IProcessRunner> _mockRunner = new();
    private readonly Mock<ILogService> _mockLog = new();
    private readonly Mock<INativeInstallationService> _mockNative = new();

    private InstallationService CreateService() =>
        new(_mockRunner.Object, _mockLog.Object, _mockNative.Object);

    private InstallationService CreateService(string scriptPathOverride) =>
        new(_mockRunner.Object, _mockLog.Object, _mockNative.Object, scriptPathOverride);

    // ═══════════════════════════════════════════════════════════════
    // ValidatePrerequisitesAsync Tests
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public async Task Validate_Fails_When_InstallerPath_Does_Not_Exist()
    {
        var service = CreateService();
        var options = new InstallOptions { InstallerPath = @"C:\NonExistent\Path" };

        var result = await service.ValidatePrerequisitesAsync(options).ConfigureAwait(false);

        Assert.False(result.Success);
        Assert.Contains("does not exist", result.Message);
    }

    [Fact]
    public async Task Validate_Fails_When_Required_EXE_Is_Missing()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);

        try
        {
            var service = CreateService();
            var options = new InstallOptions
            {
                InstallerPath = tempDir,
                SaPassword = "ValidPassword123!@#"
            };

            var result = await service.ValidatePrerequisitesAsync(options).ConfigureAwait(false);

            Assert.False(result.Success);
            Assert.Contains("SQLEXPRADV_x64_ENU.exe", result.Message);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public async Task Validate_Fails_When_Password_Is_Empty()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        File.WriteAllText(Path.Combine(tempDir, "SQLEXPRADV_x64_ENU.exe"), "mock");

        try
        {
            var service = CreateService();
            var options = new InstallOptions
            {
                InstallerPath = tempDir,
                SaPassword = ""
            };

            var result = await service.ValidatePrerequisitesAsync(options).ConfigureAwait(false);

            Assert.False(result.Success);
            Assert.Contains("empty", result.Message, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public async Task Validate_Fails_When_Password_Too_Short()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        File.WriteAllText(Path.Combine(tempDir, "SQLEXPRADV_x64_ENU.exe"), "mock");

        try
        {
            var service = CreateService();
            var options = new InstallOptions
            {
                InstallerPath = tempDir,
                SaPassword = "Short1!"
            };

            var result = await service.ValidatePrerequisitesAsync(options).ConfigureAwait(false);

            Assert.False(result.Success);
            Assert.Contains("15", result.Message);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public async Task Validate_Fails_When_Password_Has_No_Digit()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        File.WriteAllText(Path.Combine(tempDir, "SQLEXPRADV_x64_ENU.exe"), "mock");

        try
        {
            var service = CreateService();
            var options = new InstallOptions
            {
                InstallerPath = tempDir,
                SaPassword = "NoDigitsHere!@#$%"
            };

            var result = await service.ValidatePrerequisitesAsync(options).ConfigureAwait(false);

            Assert.False(result.Success);
            Assert.Contains("digit", result.Message, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public async Task Validate_Fails_When_Password_Has_No_Special_Char()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        File.WriteAllText(Path.Combine(tempDir, "SQLEXPRADV_x64_ENU.exe"), "mock");

        try
        {
            var service = CreateService();
            var options = new InstallOptions
            {
                InstallerPath = tempDir,
                SaPassword = "NoSpecialChar12345"
            };

            var result = await service.ValidatePrerequisitesAsync(options).ConfigureAwait(false);

            Assert.False(result.Success);
            Assert.Contains("special", result.Message, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public async Task Validate_Succeeds_With_Valid_Options()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        File.WriteAllText(Path.Combine(tempDir, "SQLEXPRADV_x64_ENU.exe"), "mock");

        try
        {
            var service = CreateService();
            var options = new InstallOptions
            {
                InstallerPath = tempDir,
                SaPassword = "ValidPassword1!@#"
            };

            var result = await service.ValidatePrerequisitesAsync(options).ConfigureAwait(false);

            Assert.True(result.Success);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    // ═══════════════════════════════════════════════════════════════
    // InstallAsync Tests
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public async Task Install_When_Native_Succeeds_Does_Not_Invoke_PowerShell()
    {
        var service = CreateService();
        var options = new InstallOptions { SaPassword = "ValidPassword1!@#" };

        _mockNative
            .Setup(n => n.InstallAsync(options, It.IsAny<IProgress<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(OperationResult.Ok("Native install complete."));

        var result = await service.InstallAsync(options).ConfigureAwait(false);

        Assert.True(result.Success);
        Assert.Contains("Native", result.Message, StringComparison.OrdinalIgnoreCase);

        _mockRunner.Verify(r => r.RunAsync(
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<IProgress<string>>(),
            It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Install_When_Native_Fails_Emits_Fallback_And_Uses_PowerShell()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        var scriptPath = Path.Combine(tempDir, "Install-WsusWithSqlExpress.ps1");
        File.WriteAllText(scriptPath, "# mock script");

        try
        {
            var service = CreateService(scriptPath);
            var options = new InstallOptions
            {
                InstallerPath = @"C:\WSUS\SQLDB",
                SaUsername = "sa",
                SaPassword = "ValidPassword1!@#"
            };

            _mockNative
                .Setup(n => n.InstallAsync(options, It.IsAny<IProgress<string>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(OperationResult.Fail("Native orchestrator step failed."));

            _mockRunner
                .Setup(r => r.RunAsync(
                    "powershell.exe",
                    It.IsAny<string>(),
                    It.IsAny<IProgress<string>>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ProcessResult(0, ["Installation complete."]));

            var messages = new List<string>();
            var progress = new Progress<string>(msg => messages.Add(msg));

            var result = await service.InstallAsync(options, progress).ConfigureAwait(false);

            Assert.True(result.Success);
            Assert.Contains(messages, m => m.Contains("[FALLBACK]", StringComparison.Ordinal));

            _mockRunner.Verify(r => r.RunAsync(
                "powershell.exe",
                It.Is<string>(args =>
                    args.Contains("-NonInteractive", StringComparison.Ordinal) &&
                    args.Contains("-InstallerPath", StringComparison.Ordinal) &&
                    args.Contains("-SaUsername", StringComparison.Ordinal) &&
                    args.Contains("-SaPassword", StringComparison.Ordinal) &&
                    args.Contains("-ExecutionPolicy Bypass", StringComparison.Ordinal)),
                It.IsAny<IProgress<string>>(),
                It.IsAny<CancellationToken>()),
                Times.Once);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void RequiredInstallerExe_Is_Correct()
    {
        Assert.Equal("SQLEXPRADV_x64_ENU.exe", InstallationService.RequiredInstallerExe);
    }

    [Fact]
    public void MinPasswordLength_Is_15()
    {
        Assert.Equal(15, InstallationService.MinPasswordLength);
    }
}
