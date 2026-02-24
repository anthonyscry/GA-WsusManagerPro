using WsusManager.Core.Infrastructure;
using WsusManager.Core.Logging;
using WsusManager.Core.Models;

namespace WsusManager.Core.Services;

/// <summary>
/// Legacy adapter that configures HTTPS by running Set-WsusHttps.ps1.
/// </summary>
public class LegacyHttpsConfigurationFallback
{
    private const string ScriptName = "Set-WsusHttps.ps1";
    private readonly IProcessRunner _processRunner;
    private readonly ILogService _logService;
    private readonly Func<string?> _locateScript;

    public LegacyHttpsConfigurationFallback(
        IProcessRunner processRunner,
        ILogService logService,
        Func<string?>? locateScript = null)
    {
        _processRunner = processRunner;
        _logService = logService;
        _locateScript = locateScript ?? LocateScript;
    }

    public virtual async Task<OperationResult> ConfigureAsync(
        string wsusServer,
        string certThumbprint,
        IProgress<string>? progress,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(certThumbprint))
        {
            return OperationResult.Fail("Certificate thumbprint is required.");
        }

        var scriptPath = _locateScript();
        if (scriptPath is null)
        {
            var paths = GetSearchPaths();
            var message = $"HTTPS script not found. Searched for '{ScriptName}' in:\n  {paths[0]}\n  {paths[1]}";
            _logService.Warning(message);
            progress?.Report(message);
            return OperationResult.Fail(message);
        }

        progress?.Report("[FALLBACK] Running legacy HTTPS configuration script.");

        var arguments = $"-ExecutionPolicy Bypass -File \"{scriptPath}\" -CertificateThumbprint \"{certThumbprint}\"";

        var result = await _processRunner.RunAsync(
            "powershell.exe",
            arguments,
            progress,
            ct).ConfigureAwait(false);

        if (result.Success)
        {
            _logService.Info("Legacy HTTPS fallback completed for {Server}", wsusServer);
            return OperationResult.Ok("HTTPS configured via legacy script.");
        }

        var failMessage = $"Legacy HTTPS fallback failed with exit code {result.ExitCode}.";
        _logService.Warning(failMessage);
        return OperationResult.Fail(failMessage);
    }

    internal string? LocateScript()
    {
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
