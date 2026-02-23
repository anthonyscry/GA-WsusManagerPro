using WsusManager.Core.Infrastructure;
using WsusManager.Core.Logging;
using WsusManager.Core.Models;

namespace WsusManager.Core.Services;

/// <summary>
/// Fallback adapter that delegates HTTPS configuration to the legacy Set-WsusHttps.ps1 script.
/// </summary>
public class LegacyHttpsConfigurationFallback
{
    private readonly IProcessRunner _processRunner;
    private readonly ILogService _logService;
    private readonly string? _scriptPathOverride;

    public const string ScriptName = "Set-WsusHttps.ps1";

    internal LegacyHttpsConfigurationFallback(
        IProcessRunner processRunner,
        ILogService logService,
        string? scriptPathOverride)
    {
        _processRunner = processRunner;
        _logService = logService;
        _scriptPathOverride = scriptPathOverride;
    }

    public LegacyHttpsConfigurationFallback(IProcessRunner processRunner, ILogService logService)
        : this(processRunner, logService, scriptPathOverride: null)
    {
    }

    public async Task<OperationResult> ConfigureAsync(
        string? certificateThumbprint,
        IProgress<string>? progress = null,
        CancellationToken ct = default)
    {
        try
        {
            var scriptPath = LocateScript();
            if (scriptPath is null)
            {
                var msg = $"Legacy HTTPS script not found. Expected '{ScriptName}'.";
                _logService.Warning(msg);
                return OperationResult.Fail(msg);
            }

            var arguments = $"-ExecutionPolicy Bypass -File \"{scriptPath}\"";
            if (!string.IsNullOrWhiteSpace(certificateThumbprint))
            {
                arguments += $" -CertificateThumbprint \"{certificateThumbprint.Trim()}\"";
            }

            var result = await _processRunner.RunAsync(
                "powershell.exe",
                arguments,
                progress,
                ct).ConfigureAwait(false);

            return result.Success
                ? OperationResult.Ok("HTTPS configuration completed via legacy fallback.")
                : OperationResult.Fail($"Legacy HTTPS fallback failed with exit code {result.ExitCode}.");
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logService.Error(ex, "Legacy HTTPS fallback failed with unexpected error");
            return OperationResult.Fail($"Legacy HTTPS fallback failed: {ex.Message}", ex);
        }
    }

    internal string? LocateScript()
    {
        if (!string.IsNullOrWhiteSpace(_scriptPathOverride) && File.Exists(_scriptPathOverride))
        {
            return _scriptPathOverride;
        }

        foreach (var path in GetSearchPaths())
        {
            if (File.Exists(path))
            {
                return path;
            }
        }

        return null;
    }

    internal string[] GetSearchPaths()
    {
        var appDir = AppContext.BaseDirectory;
        return
        [
            Path.Combine(appDir, "Scripts", ScriptName),
            Path.Combine(appDir, ScriptName)
        ];
    }
}
