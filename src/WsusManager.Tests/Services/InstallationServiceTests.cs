using System.Text;
using System.Text.RegularExpressions;
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
    private readonly Mock<INativeInstallationService> _mockNativeInstall = new();

    private InstallationService CreateService() =>
        new(_mockRunner.Object, _mockLog.Object, _mockNativeInstall.Object);

    private InstallationService CreateServiceWithScript(string scriptPath) =>
        new(_mockRunner.Object, _mockLog.Object, _mockNativeInstall.Object, null, () => scriptPath);

    private InstallationService CreateServiceWithScriptAndSettings(string scriptPath, AppSettings settings)
    {
        var mockSettings = new Mock<ISettingsService>();
        mockSettings.Setup(s => s.Current).Returns(settings);
        return new InstallationService(
            _mockRunner.Object,
            _mockLog.Object,
            _mockNativeInstall.Object,
            mockSettings.Object,
            () => scriptPath);
    }

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
    public async Task InstallAsync_WhenNativePathSucceeds_DoesNotInvokePowershellScript()
    {
        var options = new InstallOptions
        {
            InstallerPath = @"C:\WSUS\SQLDB",
            SaUsername = "sa",
            SaPassword = "ValidPassword1!@#"
        };

        _mockNativeInstall
            .Setup(n => n.InstallAsync(options, It.IsAny<IProgress<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(OperationResult.Ok("Native install succeeded."));

        var service = CreateService();
        var result = await service.InstallAsync(options, null, CancellationToken.None).ConfigureAwait(false);

        Assert.True(result.Success);
        _mockRunner.Verify(r => r.RunAsync(
            "powershell.exe",
            It.IsAny<string>(),
            It.IsAny<IProgress<string>>(),
            It.IsAny<CancellationToken>(), It.IsAny<bool>()), Times.Never);
    }

    [Fact]
    public async Task Install_Returns_Failure_When_Script_Not_Found()
    {
        _mockNativeInstall
            .Setup(n => n.InstallAsync(It.IsAny<InstallOptions>(), It.IsAny<IProgress<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(OperationResult.Fail("Native installation path is not yet implemented."));

        var service = new InstallationService(
            _mockRunner.Object,
            _mockLog.Object,
            _mockNativeInstall.Object,
            null,
            () => null);
        var options = new InstallOptions
        {
            InstallerPath = @"C:\WSUS\SQLDB",
            SaPassword = "ValidPassword1!@#"
        };

        // Script won't be found in test environment
        var result = await service.InstallAsync(options).ConfigureAwait(false);

        Assert.False(result.Success);
        Assert.Contains("Install script not found", result.Message);
    }

    [Fact]
    public async Task Install_Constructs_Correct_PowerShell_Arguments()
    {
        var scriptPath = @"C:\WSUS\Scripts\Install-WsusWithSqlExpress.ps1";

        _mockNativeInstall
            .Setup(n => n.InstallAsync(It.IsAny<InstallOptions>(), It.IsAny<IProgress<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(OperationResult.Fail("Native installation path is not yet implemented."));

        var service = CreateServiceWithScript(scriptPath);

        var options = new InstallOptions
        {
            InstallerPath = @"C:\WSUS\SQLDB",
            SaUsername = "sa",
            SaPassword = "MyPassword123!@#"
        };

        string? capturedArgs = null;
        _mockRunner
            .Setup(r => r.RunAsync(
                "powershell.exe",
                It.IsAny<string>(),
                It.IsAny<IProgress<string>>(),
                It.IsAny<CancellationToken>(), It.IsAny<bool>()))
            .Callback<string, string, IProgress<string>?, CancellationToken, bool>((_, args, _, _, _) => capturedArgs = args)
            .ReturnsAsync(new ProcessResult(0, ["Installation complete."]));

        var result = await service.InstallAsync(options).ConfigureAwait(false);

        Assert.True(result.Success);
        Assert.NotNull(capturedArgs);
        Assert.Contains("-EncodedCommand", capturedArgs, StringComparison.Ordinal);
        Assert.DoesNotContain("-Command ", capturedArgs, StringComparison.Ordinal);

        var encodedMatch = Regex.Match(capturedArgs, "-EncodedCommand\\s+([A-Za-z0-9+/=]+)");
        Assert.True(encodedMatch.Success, "Encoded command argument missing");

        var decodedCommand = Encoding.Unicode.GetString(Convert.FromBase64String(encodedMatch.Groups[1].Value));
        Assert.Contains("-NonInteractive", decodedCommand, StringComparison.Ordinal);
        Assert.Contains("-InstallerPath", decodedCommand, StringComparison.Ordinal);
        Assert.Contains("-SaUsername", decodedCommand, StringComparison.Ordinal);
        Assert.Contains("-SaPassword", decodedCommand, StringComparison.Ordinal);
        Assert.Contains("Install-WsusWithSqlExpress.ps1", decodedCommand, StringComparison.Ordinal);

        _mockRunner.Verify(r => r.RunAsync(
            "powershell.exe",
            It.IsAny<string>(),
            It.IsAny<IProgress<string>>(),
            It.IsAny<CancellationToken>(), It.IsAny<bool>()),
            Times.Once);
    }

    [Fact]
    public async Task InstallOptsInLiveTerminalMode_WhenUsingLegacyScript()
    {
        var scriptPath = @"C:\WSUS\Scripts\Install-WsusWithSqlExpress.ps1";

        _mockNativeInstall
            .Setup(n => n.InstallAsync(It.IsAny<InstallOptions>(), It.IsAny<IProgress<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(OperationResult.Fail("Native installation path is not yet implemented."));

        _mockRunner
            .Setup(r => r.RunAsync(
                "powershell.exe",
                It.IsAny<string>(),
                It.IsAny<IProgress<string>>(),
                It.IsAny<CancellationToken>(), true))
            .ReturnsAsync(new ProcessResult(0, ["Installation complete."]));

        var service = CreateServiceWithScript(scriptPath);

        var result = await service.InstallAsync(new InstallOptions
        {
            InstallerPath = @"C:\WSUS\SQLDB",
            SaPassword = "ValidPassword123!@#"
        }).ConfigureAwait(false);

        Assert.True(result.Success);
        _mockRunner.Verify(r => r.RunAsync(
            "powershell.exe",
            It.IsAny<string>(),
            It.IsAny<IProgress<string>>(),
            It.IsAny<CancellationToken>(), true),
            Times.Once);
    }

    [Fact]
    public async Task Install_DoesNot_Expose_PlaintextPassword_In_PowerShell_Arguments()
    {
        var scriptPath = @"C:\WSUS\Scripts\Install-WsusWithSqlExpress.ps1";

        _mockNativeInstall
            .Setup(n => n.InstallAsync(It.IsAny<InstallOptions>(), It.IsAny<IProgress<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(OperationResult.Fail("Native installation path is not yet implemented."));

        var service = CreateServiceWithScript(scriptPath);

        var options = new InstallOptions
        {
            InstallerPath = @"C:\WSUS\SQLDB",
            SaUsername = "sa",
            SaPassword = "MyPassword123!@#"
        };

        string? capturedArgs = null;
        _mockRunner
            .Setup(r => r.RunAsync(
                "powershell.exe",
                It.IsAny<string>(),
                It.IsAny<IProgress<string>>(),
                It.IsAny<CancellationToken>(), It.IsAny<bool>()))
            .Callback<string, string, IProgress<string>?, CancellationToken, bool>((_, args, _, _, _) => capturedArgs = args)
            .ReturnsAsync(new ProcessResult(0, ["Installation complete."]));

        var result = await service.InstallAsync(options).ConfigureAwait(false);

        Assert.True(result.Success);
        Assert.NotNull(capturedArgs);

        var encodedMatch = Regex.Match(capturedArgs, "-EncodedCommand\\s+([A-Za-z0-9+/=]+)");
        Assert.True(encodedMatch.Success, "Encoded command argument missing in password test");

        var decodedCommand = Encoding.Unicode.GetString(Convert.FromBase64String(encodedMatch.Groups[1].Value));
        Assert.DoesNotContain(options.SaPassword, decodedCommand, StringComparison.Ordinal);
        Assert.Contains("WSUS_INSTALL_SA_PASSWORD", decodedCommand, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Install_Fails_When_Fallback_Disabled()
    {
        _mockNativeInstall
            .Setup(n => n.InstallAsync(It.IsAny<InstallOptions>(), It.IsAny<IProgress<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(OperationResult.Fail("Something went wrong."));

        var service = CreateServiceWithScriptAndSettings("script", new AppSettings
        {
            EnableLegacyFallbackForInstall = false
        });

        var result = await service.InstallAsync(new InstallOptions
        {
            InstallerPath = @"C:\WSUS\SQLDB",
            SaUsername = "sa",
            SaPassword = "ValidPassword123!@#"
        }).ConfigureAwait(false);

        Assert.False(result.Success);
        Assert.Contains("legacy fallback is disabled", result.Message, StringComparison.OrdinalIgnoreCase);
        _mockRunner.Verify(r => r.RunAsync(
                "powershell.exe",
                It.IsAny<string>(),
                It.IsAny<IProgress<string>>(),
                It.IsAny<CancellationToken>(), It.IsAny<bool>()),
            Times.Never);
    }

    [Fact]
    public async Task Install_Fails_When_Fallback_Disabled_And_Native_Path_Unavailable()
    {
        _mockNativeInstall
            .Setup(n => n.InstallAsync(It.IsAny<InstallOptions>(), It.IsAny<IProgress<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(OperationResult.Fail("Native installation path is not yet implemented."));

        var service = CreateServiceWithScriptAndSettings("script", new AppSettings
        {
            EnableLegacyFallbackForInstall = false
        });

        var result = await service.InstallAsync(new InstallOptions
        {
            InstallerPath = @"C:\WSUS\SQLDB",
            SaUsername = "sa",
            SaPassword = "ValidPassword123!@#"
        }).ConfigureAwait(false);

        Assert.False(result.Success);
        Assert.Contains("native installation path is unavailable", result.Message, StringComparison.OrdinalIgnoreCase);
        _mockRunner.Verify(r => r.RunAsync(
                "powershell.exe",
                It.IsAny<string>(),
                It.IsAny<IProgress<string>>(),
                It.IsAny<CancellationToken>(), It.IsAny<bool>()),
            Times.Never);
    }

    [Fact]
    public async Task Install_Reports_Progress()
    {
        _mockNativeInstall
            .Setup(n => n.InstallAsync(It.IsAny<InstallOptions>(), It.IsAny<IProgress<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(OperationResult.Fail("Native installation path is not yet implemented."));

        var service = CreateServiceWithScript(@"C:\WSUS\Scripts\Install-WsusWithSqlExpress.ps1");

        _mockRunner
            .Setup(r => r.RunAsync(
                "powershell.exe",
                It.IsAny<string>(),
                It.IsAny<IProgress<string>>(),
                It.IsAny<CancellationToken>(), It.IsAny<bool>()))
            .ReturnsAsync(new ProcessResult(0, ["Done."]));

        var messages = new List<string>();
        var progress = new Progress<string>(msg => messages.Add(msg));

        await service.InstallAsync(new InstallOptions { SaPassword = "Test123!@#$%^&*(" }, progress).ConfigureAwait(false);

        Assert.True(messages.Count >= 4, $"Expected at least 4 progress messages, got {messages.Count}");
        Assert.Contains(messages, m => m.Contains("[FALLBACK]", StringComparison.Ordinal));
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
