using WsusManager.Core.Logging;
using WsusManager.Core.Models;
using WsusManager.Core.Services.Interfaces;

namespace WsusManager.Core.Services;

/// <summary>
/// Copies GPO deployment files from the application's DomainController/ directory
/// to C:\WSUS\WSUS GPO\ and returns structured instruction text for the DC admin.
/// </summary>
public class GpoDeploymentService : IGpoDeploymentService
{
    private readonly ILogService _logService;
    private readonly string _destinationDirectory;

    /// <summary>Source directory name containing GPO files.</summary>
    public const string SourceDirectoryName = "DomainController";

    /// <summary>Default destination directory for GPO files.</summary>
    public const string DefaultDestination = @"C:\WSUS\WSUS GPO";

    public GpoDeploymentService(ILogService logService)
        : this(logService, DefaultDestination)
    {
    }

    internal GpoDeploymentService(ILogService logService, string destinationDirectory)
    {
        _logService = logService;
        _destinationDirectory = string.IsNullOrWhiteSpace(destinationDirectory)
            ? throw new ArgumentException("Destination directory must not be empty.", nameof(destinationDirectory))
            : destinationDirectory;
    }

    /// <inheritdoc/>
    public async Task<OperationResult<string>> DeployGpoFilesAsync(
        string wsusHostname,
        int httpPort = 8530,
        int httpsPort = 8531,
        IProgress<string>? progress = null,
        CancellationToken ct = default)
    {
        try
        {
            var normalizedHostname = ValidateHostname(wsusHostname);

            // Locate source directory
            var sourceDir = LocateSourceDirectory();
            if (sourceDir is null)
            {
                var paths = GetSearchPaths();
                var msg = $"DomainController directory not found. Searched:\n" +
                          $"  {paths[0]}\n  {paths[1]}";
                _logService.Warning(msg);
                progress?.Report($"[FAIL] {msg}");
                return OperationResult<string>.Fail(msg);
            }

            _logService.Info("Deploying GPO files from {Source} to {Dest}", sourceDir, _destinationDirectory);
            progress?.Report($"Source: {sourceDir}");
            progress?.Report($"Destination: {_destinationDirectory}");

            // Create destination directory
            Directory.CreateDirectory(_destinationDirectory);
            progress?.Report("Created destination directory.");

            // Copy all files recursively
            var fileCount = await Task.Run(() => CopyDirectory(sourceDir, _destinationDirectory, progress, ct), ct).ConfigureAwait(false);

            var wrapperScriptPath = Path.Combine(_destinationDirectory, "Run-WsusGpoSetup.ps1");
            var wrapperScriptText = BuildWrapperScriptText(normalizedHostname, httpPort, httpsPort);
            await File.WriteAllTextAsync(wrapperScriptPath, wrapperScriptText, ct).ConfigureAwait(false);
            progress?.Report($"Generated wrapper script: {wrapperScriptPath}");

            _logService.Info("GPO deployment complete: {Count} files copied", fileCount);
            progress?.Report($"[OK] Copied {fileCount} files to {_destinationDirectory}");

            var instructions = BuildInstructionText(normalizedHostname, httpPort, httpsPort);
            return OperationResult<string>.Ok(instructions, $"GPO files deployed ({fileCount} files) and wrapper generated.");
        }
        catch (OperationCanceledException)
        {
            _logService.Info("GPO deployment cancelled by user");
            throw;
        }
        catch (Exception ex)
        {
            _logService.Error(ex, "GPO deployment failed with unexpected error");
            return OperationResult<string>.Fail($"GPO deployment failed: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Recursively copies all files from source to destination, preserving directory structure.
    /// Returns the number of files copied.
    /// </summary>
    internal static int CopyDirectory(string sourceDir, string destDir, IProgress<string>? progress, CancellationToken ct)
    {
        var count = 0;
        var source = new DirectoryInfo(sourceDir);

        foreach (var file in source.GetFiles("*", SearchOption.AllDirectories))
        {
            ct.ThrowIfCancellationRequested();

            var relativePath = Path.GetRelativePath(sourceDir, file.FullName);
            var destPath = Path.Combine(destDir, relativePath);
            var destDirectory = Path.GetDirectoryName(destPath)!;

            Directory.CreateDirectory(destDirectory);
            file.CopyTo(destPath, overwrite: true);
            count++;

            progress?.Report($"Copied: {relativePath}");
        }

        return count;
    }

    /// <summary>
    /// Builds the step-by-step instruction text for the DC admin.
    /// </summary>
    internal static string BuildInstructionText(string wsusHostname, int httpPort = 8530, int httpsPort = 8531)
    {
        var normalizedHostname = ValidateHostname(wsusHostname);
        var normalizedHttpPort = NormalizePort(httpPort, 8530);
        var normalizedHttpsPort = NormalizePort(httpsPort, 8531);
        var httpServerUrl = BuildWsusServerUrl("http", normalizedHostname, normalizedHttpPort);
        var httpsServerUrl = BuildWsusServerUrl("https", normalizedHostname, normalizedHttpsPort);
        return $"""
            GPO files have been copied to {DefaultDestination}

            WSUS Server URLs:
              HTTP:  {httpServerUrl}
              HTTPS: {httpsServerUrl}

            Steps for the Domain Controller admin:

            1. Copy the "WSUS GPO" folder to the Domain Controller

            2. Open PowerShell as Administrator on the Domain Controller and run the wrapper:
               powershell -ExecutionPolicy Bypass -File .\Run-WsusGpoSetup.ps1

            3. To target HTTPS instead, add -UseHttps:
               powershell -ExecutionPolicy Bypass -File .\Run-WsusGpoSetup.ps1 -UseHttps

               Note: The wrapper calls Set-WsusGroupPolicy.ps1 with the selected URL.

            4. Force client check-in on target machines:
               gpupdate /force
               wuauclt /detectnow
            """;
    }

    internal static int NormalizePort(int candidate, int fallback)
    {
        return candidate is >= 1 and <= 65535 ? candidate : fallback;
    }

    internal static string BuildWrapperScriptText(string wsusHostname, int httpPort, int httpsPort)
    {
        var normalizedHostname = ValidateHostname(wsusHostname);
        var normalizedHttpPort = NormalizePort(httpPort, 8530);
        var normalizedHttpsPort = NormalizePort(httpsPort, 8531);
        var httpServerUrl = EscapePowerShellSingleQuotedString(BuildWsusServerUrl("http", normalizedHostname, normalizedHttpPort));
        var httpsServerUrl = EscapePowerShellSingleQuotedString(BuildWsusServerUrl("https", normalizedHostname, normalizedHttpsPort));

        return $$"""
            #requires -RunAsAdministrator
            [CmdletBinding()]
            param(
                [string]$BackupPath = (Join-Path -Path $PSScriptRoot -ChildPath "WSUS GPOs"),
                [switch]$UseHttps
            )

            $isAdmin = ([Security.Principal.WindowsPrincipal] [Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
            if (-not $isAdmin) {
                Write-Error "This script must be run as Administrator."
                exit 1
            }

            $computerSystem = Get-CimInstance -ClassName Win32_ComputerSystem
            if ($computerSystem.DomainRole -lt 4) {
                Write-Error "This script must be run on a Domain Controller."
                exit 1
            }

            $wsusServerUrl = if ($UseHttps) { '{{httpsServerUrl}}' } else { '{{httpServerUrl}}' }

            $setGpoScript = Join-Path -Path $PSScriptRoot -ChildPath "Set-WsusGroupPolicy.ps1"
            & $setGpoScript -WsusServerUrl $wsusServerUrl -BackupPath $BackupPath
            """;
    }

    internal static string ValidateHostname(string candidate)
    {
        if (string.IsNullOrWhiteSpace(candidate))
            throw new ArgumentException("WSUS hostname must not be empty.", nameof(candidate));

        var normalized = candidate.Trim();
        var hostType = Uri.CheckHostName(normalized);

        if (hostType is UriHostNameType.Unknown)
            throw new ArgumentException("WSUS hostname must be a valid host or IP address.", nameof(candidate));

        return normalized;
    }

    internal static string BuildWsusServerUrl(string scheme, string hostname, int port)
    {
        var wrappedHost = Uri.CheckHostName(hostname) == UriHostNameType.IPv6
            ? $"[{hostname}]"
            : hostname;

        return $"{scheme}://{wrappedHost}:{port}";
    }

    internal static string EscapePowerShellSingleQuotedString(string value)
    {
        return value.Replace("'", "''", StringComparison.Ordinal);
    }

    /// <summary>
    /// Locates the DomainController directory relative to the current executable directory.
    /// </summary>
    internal string? LocateSourceDirectory()
    {
        foreach (var path in GetSearchPaths())
        {
            if (Directory.Exists(path))
                return path;
        }
        return null;
    }

    internal string[] GetSearchPaths()
    {
        var paths = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var root in GetDirectorySearchRoots())
        {
            AddUnique(paths, seen, Path.Combine(root, SourceDirectoryName));
            AddUnique(paths, seen, Path.Combine(root, "..", SourceDirectoryName));
        }

        return [.. paths];
    }

    private static IEnumerable<string> GetDirectorySearchRoots()
    {
        yield return Path.GetFullPath(AppContext.BaseDirectory);

        var processPath = Environment.ProcessPath;
        if (string.IsNullOrWhiteSpace(processPath)) yield break;

        var processDir = Path.GetDirectoryName(processPath);
        if (!string.IsNullOrWhiteSpace(processDir))
            yield return Path.GetFullPath(processDir);
    }

    private static void AddUnique(List<string> paths, HashSet<string> seen, string path)
    {
        var full = Path.GetFullPath(path);
        if (seen.Add(full))
            paths.Add(full);
    }
}
