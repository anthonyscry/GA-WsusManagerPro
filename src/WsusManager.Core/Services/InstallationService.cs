using System.Text;
using System.Text.RegularExpressions;
using WsusManager.Core.Infrastructure;
using WsusManager.Core.Logging;
using WsusManager.Core.Models;
using WsusManager.Core.Services.Interfaces;

namespace WsusManager.Core.Services;

/// <summary>
/// Validates installation prerequisites and launches the legacy
/// Install-WsusWithSqlExpress.ps1 script via IProcessRunner in non-interactive mode.
/// This is the only operation that deliberately shells out to PowerShell --
/// the install script is complex and re-implementing in C# provides no benefit.
/// </summary>
public class InstallationService : IInstallationService
{
    private const string InstallPasswordEnvironmentVariable = "WSUS_INSTALL_SA_PASSWORD";

    private readonly IProcessRunner _processRunner;
    private readonly ILogService _logService;
    private readonly INativeInstallationService _nativeInstallationService;
    private readonly Func<string?> _locateScript;
    private readonly ISettingsService? _settingsService;

    /// <summary>Required SQL Express installer filename.</summary>
    public const string RequiredInstallerExe = "SQLEXPRADV_x64_ENU.exe";

    /// <summary>PowerShell script filename for installation.</summary>
    public const string InstallScriptName = "Install-WsusWithSqlExpress.ps1";

    /// <summary>Minimum SA password length.</summary>
    public const int MinPasswordLength = 15;

    public InstallationService(
        IProcessRunner processRunner,
        ILogService logService,
        INativeInstallationService nativeInstallationService,
        ISettingsService? settingsService = null,
        Func<string?>? locateScript = null)
    {
        _processRunner = processRunner;
        _logService = logService;
        _nativeInstallationService = nativeInstallationService;
        _settingsService = settingsService;
        _locateScript = locateScript ?? LocateScript;
    }

    /// <inheritdoc/>
    public Task<OperationResult> ValidatePrerequisitesAsync(InstallOptions options, CancellationToken ct = default)
    {
        // Check installer path exists
        if (string.IsNullOrWhiteSpace(options.InstallerPath) || !Directory.Exists(options.InstallerPath))
        {
            return Task.FromResult(OperationResult.Fail(
                $"Installer path does not exist: {options.InstallerPath}"));
        }

        // Check required EXE is present
        var exePath = Path.Combine(options.InstallerPath, RequiredInstallerExe);
        if (!File.Exists(exePath))
        {
            return Task.FromResult(OperationResult.Fail(
                $"Required installer not found: {exePath}. " +
                $"Place {RequiredInstallerExe} in the installer directory."));
        }

        // Check password not empty
        if (string.IsNullOrWhiteSpace(options.SaPassword))
        {
            return Task.FromResult(OperationResult.Fail(
                "SA password cannot be empty."));
        }

        // Check password complexity: 15+ chars, 1 digit, 1 special char
        if (options.SaPassword.Length < MinPasswordLength)
        {
            return Task.FromResult(OperationResult.Fail(
                $"SA password must be at least {MinPasswordLength} characters long."));
        }

        if (!Regex.IsMatch(options.SaPassword, @"\d"))
        {
            return Task.FromResult(OperationResult.Fail(
                "SA password must contain at least 1 digit."));
        }

        if (!Regex.IsMatch(options.SaPassword, @"[^a-zA-Z0-9]"))
        {
            return Task.FromResult(OperationResult.Fail(
                "SA password must contain at least 1 special character."));
        }

        return Task.FromResult(OperationResult.Ok("All prerequisites met."));
    }

    /// <inheritdoc/>
    public async Task<OperationResult> InstallAsync(
        InstallOptions options,
        IProgress<string>? progress = null,
        CancellationToken ct = default)
    {
        try
        {
            var native = await _nativeInstallationService
                .InstallAsync(options, progress, ct)
                .ConfigureAwait(false);

            if (native.Success)
            {
                _logService.Info("WSUS installation completed via native C# path");
                return native;
            }

            var allowFallback = _settingsService?.Current.EnableLegacyFallbackForInstall ?? true;
            var nativePathUnavailable = native.Message?.Contains("not yet implemented", StringComparison.OrdinalIgnoreCase) == true;
            var nativeReason = native.Message ?? "Unknown reason";

            if (!allowFallback)
            {
                var fallbackDisabledMessage = nativePathUnavailable
                    ? "Native installation path is unavailable and legacy fallback is disabled; installation cannot proceed."
                    : $"Native installation failed and legacy fallback is disabled. Reason: {nativeReason}";

                _logService.Warning(fallbackDisabledMessage);
                progress?.Report(fallbackDisabledMessage);
                return OperationResult.Fail(fallbackDisabledMessage);
            }

            progress?.Report("[FALLBACK] Native install failed; using legacy PowerShell install path.");
            _logService.Warning("Native install path failed; using legacy PowerShell fallback. Reason: {Reason}", nativeReason);

            // Locate the PowerShell install script
            var scriptPath = _locateScript();
            if (scriptPath is null)
            {
                var searchPaths = GetSearchPaths();
                var msg = $"Install script not found. Searched for '{InstallScriptName}' in:\n" +
                          $"  {string.Join("\n  ", searchPaths)}";
                _logService.Warning(msg);
                progress?.Report(msg);
                return OperationResult.Fail(msg);
            }

            _logService.Info("Starting WSUS installation via {Script}", scriptPath);
            progress?.Report($"Script: {scriptPath}");
            progress?.Report($"Installer path: {options.InstallerPath}");
            var saUsername = string.IsNullOrWhiteSpace(options.SaUsername) ? "sa" : options.SaUsername;
            progress?.Report($"SA Username: {saUsername}");
            progress?.Report("Starting installation (this may take 30+ minutes)...");
            progress?.Report("");

            // Build PowerShell arguments using environment variable password passing.
            // This mirrors legacy GUI behavior and avoids exposing plaintext in command-line arguments.
            var scriptPathSafe = EscapePowerShellSingleQuotedString(scriptPath);
            var installerPathSafe = EscapePowerShellSingleQuotedString(options.InstallerPath);
            var saUsernameSafe = EscapePowerShellSingleQuotedString(saUsername);

            var command =
                $"& '{scriptPathSafe}' -InstallerPath '{installerPathSafe}' -SaUsername '{saUsernameSafe}' " +
                $"-SaPassword $env:{InstallPasswordEnvironmentVariable} -NonInteractive; " +
                $"Remove-Item Env:\\{InstallPasswordEnvironmentVariable} -ErrorAction SilentlyContinue";

            var encodedCommand = EncodePowerShellCommand(command);
            var arguments = $"-NoProfile -ExecutionPolicy Bypass -EncodedCommand {encodedCommand}";

            var previousPassword = Environment.GetEnvironmentVariable(InstallPasswordEnvironmentVariable, EnvironmentVariableTarget.Process);
            Environment.SetEnvironmentVariable(InstallPasswordEnvironmentVariable, options.SaPassword, EnvironmentVariableTarget.Process);

            ProcessResult result;
            try
            {
                result = await _processRunner.RunAsync(
                    "powershell.exe",
                    arguments,
                    progress,
                    ct,
                    enableLiveTerminal: true).ConfigureAwait(false);
            }
            finally
            {
                Environment.SetEnvironmentVariable(
                    InstallPasswordEnvironmentVariable,
                    previousPassword,
                    EnvironmentVariableTarget.Process);
            }

            if (result.Success)
            {
                _logService.Info("WSUS installation completed successfully");
                return OperationResult.Ok("WSUS installation completed successfully.");
            }
            else
            {
                var msg = $"Installation failed with exit code {result.ExitCode}. Check output log for details.";

                _logService.Warning(msg);
                return OperationResult.Fail(msg);
            }
        }
        catch (OperationCanceledException)
        {
            _logService.Info("WSUS installation cancelled by user");
            throw;
        }
        catch (Exception ex)
        {
            _logService.Error(ex, "WSUS installation failed with unexpected error");
            return OperationResult.Fail($"Installation failed: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Locates the install script relative to the current executable directory.
    /// Checks: {AppDir}\Scripts\Install-WsusWithSqlExpress.ps1, then {AppDir}\Install-WsusWithSqlExpress.ps1.
    /// </summary>
    internal string? LocateScript()
    {
        return ScriptPathLocator.LocateScript(InstallScriptName);
    }

    private static string EncodePowerShellCommand(string command)
    {
        var bytes = Encoding.Unicode.GetBytes(command);
        return Convert.ToBase64String(bytes);
    }

    internal string[] GetSearchPaths()
    {
        return ScriptPathLocator.GetScriptSearchPaths(InstallScriptName);
    }

    private static string EscapePowerShellSingleQuotedString(string value)
    {
        return value.Replace("'", "''", StringComparison.Ordinal);
    }
}
