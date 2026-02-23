using WsusManager.Core.Infrastructure;
using WsusManager.Core.Logging;
using WsusManager.Core.Models;
using WsusManager.Core.Services.Interfaces;

namespace WsusManager.Core.Services;

/// <summary>
/// C#-first HTTPS configuration workflow with automatic legacy fallback on failure.
/// </summary>
public class HttpsConfigurationService : IHttpsConfigurationService
{
    private readonly IProcessRunner _processRunner;
    private readonly LegacyHttpsConfigurationFallback _fallback;
    private readonly ILogService _logService;

    private const string WsusUtilPath = @"C:\Program Files\Update Services\Tools\wsusutil.exe";
    private const string SslIpPort = "0.0.0.0:8531";
    private const string AppId = "{9f55f098-16f9-4f85-b6f9-7241f8b9e26a}";
    private const int ThumbprintLength = 40;

    public HttpsConfigurationService(
        IProcessRunner processRunner,
        LegacyHttpsConfigurationFallback fallback,
        ILogService logService)
    {
        _processRunner = processRunner;
        _fallback = fallback;
        _logService = logService;
    }

    /// <inheritdoc />
    public async Task<OperationResult> ConfigureHttpsAsync(
        string? serverName,
        string? certificateThumbprint,
        IProgress<string>? progress = null,
        CancellationToken ct = default)
    {
        var normalizedServerName = NormalizeServerName(serverName);
        var serverValidation = ValidateServerName(normalizedServerName);
        if (!serverValidation.Success)
        {
            progress?.Report($"[FAIL] {serverValidation.Message}");
            return serverValidation;
        }

        var normalizedThumbprint = NormalizeThumbprint(certificateThumbprint);
        var validation = ValidateThumbprint(normalizedThumbprint);
        if (!validation.Success)
        {
            progress?.Report($"[FAIL] {validation.Message}");
            return validation;
        }

        try
        {
            progress?.Report("Starting native HTTPS configuration...");
            progress?.Report("[Step 1/3] Applying SSL certificate binding on port 8531...");

            await _processRunner.RunAsync(
                "netsh",
                $"http delete sslcert ipport={SslIpPort}",
                progress,
                ct).ConfigureAwait(false);

            var addBindingResult = await _processRunner.RunAsync(
                "netsh",
                $"http add sslcert ipport={SslIpPort} certhash={normalizedThumbprint} appid={AppId} certstorename=MY",
                progress,
                ct).ConfigureAwait(false);

            if (!addBindingResult.Success)
            {
                throw new InvalidOperationException($"Native SSL binding failed: {addBindingResult.Output}");
            }

            progress?.Report("[Step 2/3] Configuring WSUS SSL mode via wsusutil...");

            var wsusResult = await _processRunner.RunAsync(
                WsusUtilPath,
                $"configuressl {normalizedServerName}",
                progress,
                ct).ConfigureAwait(false);

            if (!wsusResult.Success)
            {
                throw new InvalidOperationException($"Native wsusutil configuressl failed: {wsusResult.Output}");
            }

            progress?.Report("[Step 3/3] Verifying HTTPS binding...");

            var verifyBindingResult = await _processRunner.RunAsync(
                "netsh",
                $"http show sslcert ipport={SslIpPort}",
                progress,
                ct).ConfigureAwait(false);

            if (!verifyBindingResult.Success)
            {
                throw new InvalidOperationException($"Native HTTPS binding verification failed: {verifyBindingResult.Output}");
            }

            progress?.Report("[OK] HTTPS configuration completed using native C# workflow.");
            return OperationResult.Ok("HTTPS configuration completed successfully.");
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            var fallbackLine = $"[FALLBACK] Native HTTPS configuration failed: {ex.Message}. Switching to legacy Set-WsusHttps.ps1 path.";
            progress?.Report(fallbackLine);
            _logService.Warning(fallbackLine);

            return await _fallback.ConfigureAsync(normalizedServerName, normalizedThumbprint, progress, ct).ConfigureAwait(false);
        }
    }

    private static string? NormalizeServerName(string? serverName)
    {
        return string.IsNullOrWhiteSpace(serverName)
            ? null
            : serverName.Trim();
    }

    private static OperationResult ValidateServerName(string? normalizedServerName)
    {
        return string.IsNullOrWhiteSpace(normalizedServerName)
            ? OperationResult.Fail("WSUS server name is required.")
            : OperationResult.Ok();
    }

    private static string? NormalizeThumbprint(string? thumbprint)
    {
        if (string.IsNullOrWhiteSpace(thumbprint))
        {
            return null;
        }

        return thumbprint.Replace(" ", string.Empty, StringComparison.Ordinal).Trim().ToUpperInvariant();
    }

    private static OperationResult ValidateThumbprint(string? normalizedThumbprint)
    {
        if (string.IsNullOrWhiteSpace(normalizedThumbprint))
        {
            return OperationResult.Fail("Certificate thumbprint is required.");
        }

        if (normalizedThumbprint.Length != ThumbprintLength)
        {
            return OperationResult.Fail($"Certificate thumbprint must be {ThumbprintLength} hexadecimal characters.");
        }

        if (!normalizedThumbprint.All(IsHexCharacter))
        {
            return OperationResult.Fail("Certificate thumbprint format is invalid. Only hexadecimal characters are allowed.");
        }

        return OperationResult.Ok();
    }

    private static bool IsHexCharacter(char c)
    {
        return (c >= '0' && c <= '9')
            || (c >= 'A' && c <= 'F')
            || (c >= 'a' && c <= 'f');
    }
}
