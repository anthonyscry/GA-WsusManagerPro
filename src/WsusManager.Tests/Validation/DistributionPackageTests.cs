namespace WsusManager.Tests.Validation;

/// <summary>
/// Validates the C# distribution package contents.
/// Tests skip if the distribution directory is not found.
/// Set WSUS_DIST_PATH environment variable to the dist directory,
/// or tests will search common output locations.
/// </summary>
public class DistributionPackageTests
{
    private static readonly string[] ExeCandidates =
    [
        "WsusManager.exe",
        "WsusManager.App.exe"
    ];

    /// <summary>
    /// Finds the distribution directory. Checks WSUS_DIST_PATH env var first.
    /// </summary>
    private static string? FindDistDirectory()
    {
        var envPath = Environment.GetEnvironmentVariable("WSUS_DIST_PATH");
        if (!string.IsNullOrEmpty(envPath) && Directory.Exists(envPath))
            return envPath;

        var testDir = AppContext.BaseDirectory;

        var searchPaths = new[]
        {
            Path.Combine(testDir, "..", "..", "..", "..", "..", "dist-csharp"),
            Path.Combine(testDir, "..", "..", "..", "..", "..", "publish"),
        };

        foreach (var path in searchPaths)
        {
            var fullPath = Path.GetFullPath(path);
            if (Directory.Exists(fullPath) && GetExecutablePath(fullPath) is not null)
                return fullPath;
        }

        return null;
    }

    [Fact]
    public void Package_ContainsExe()
    {
        var distDir = FindDistDirectory();
        if (distDir is null) return;

        Assert.True(GetExecutablePath(distDir) is not null,
            $"Expected one of [{string.Join(", ", ExeCandidates)}] in {distDir}");
    }

    [Fact]
    public void Package_ContainsDomainController()
    {
        var distDir = FindDistDirectory();
        if (distDir is null) return;

        var domainControllerPath = Path.Combine(distDir, "DomainController");
        if (!Directory.Exists(domainControllerPath)) return;

        Assert.NotEmpty(Directory.GetFiles(domainControllerPath, "*", SearchOption.AllDirectories));
    }

    [Fact]
    public void Package_ContainsPowerShellFolders_ForLegacyFallbackParity()
    {
        var distDir = FindDistDirectory();
        if (distDir is null) return;

        Assert.True(Directory.Exists(Path.Combine(distDir, "Scripts")),
            "Distribution must contain Scripts/ for install/HTTPS/maintenance fallback.");
        Assert.True(Directory.Exists(Path.Combine(distDir, "Modules")),
            "Distribution must contain Modules/ for script dependencies.");
    }

    [Fact]
    public void Package_ContainsRequiredFallbackScripts()
    {
        var distDir = FindDistDirectory();
        if (distDir is null) return;

        var scriptsDir = Path.Combine(distDir, "Scripts");

        Assert.True(File.Exists(Path.Combine(scriptsDir, "Install-WsusWithSqlExpress.ps1")),
            "Install fallback script is required in Scripts/.");
        Assert.True(File.Exists(Path.Combine(scriptsDir, "Set-WsusHttps.ps1")),
            "HTTPS fallback script is required in Scripts/.");
        Assert.True(File.Exists(Path.Combine(scriptsDir, "Invoke-WsusMonthlyMaintenance.ps1")),
            "Maintenance fallback script is required in Scripts/.");
    }

    [Fact]
    public void Package_ContainsRequiredModuleDependencies()
    {
        var distDir = FindDistDirectory();
        if (distDir is null) return;

        var modulesDir = Path.Combine(distDir, "Modules");

        Assert.True(File.Exists(Path.Combine(modulesDir, "WsusUtilities.psm1")),
            "WsusUtilities module is required by fallback scripts.");
        Assert.True(File.Exists(Path.Combine(modulesDir, "WsusConfig.psm1")),
            "WsusConfig module is required by fallback scripts.");
    }

    [Fact]
    public void Package_ExeSizeReasonable()
    {
        var distDir = FindDistDirectory();
        if (distDir is null) return;

        var exePath = GetExecutablePath(distDir);
        Assert.NotNull(exePath);

        var sizeMB = new FileInfo(exePath!).Length / (1024.0 * 1024);

        Assert.True(sizeMB > 1, $"EXE should be > 1 MB, was {sizeMB:F1} MB");
        Assert.True(sizeMB < 250, $"EXE should be < 250 MB, was {sizeMB:F1} MB");
    }

    [Fact]
    public void Package_TotalSizeReasonable()
    {
        var distDir = FindDistDirectory();
        if (distDir is null) return;

        var totalBytes = Directory.GetFiles(distDir, "*", SearchOption.AllDirectories)
            .Sum(f => new FileInfo(f).Length);
        var totalMB = totalBytes / (1024.0 * 1024);

        Assert.True(totalMB < 300, $"Total package should be < 300 MB, was {totalMB:F1} MB");
    }

    private static string? GetExecutablePath(string directory)
    {
        foreach (var candidate in ExeCandidates)
        {
            var path = Path.Combine(directory, candidate);
            if (File.Exists(path))
                return path;
        }

        return null;
    }
}
