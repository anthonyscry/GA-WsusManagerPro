using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using WsusManager.Core.Logging;
using WsusManager.Core.Models;
using AppLogLevel = WsusManager.Core.Models.LogLevel;

namespace WsusManager.Tests.Integration;

public sealed class ProgramLoggingWiringTests : IDisposable
{
    private readonly string _tempDir;

    public ProgramLoggingWiringTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"WsusManagerProgramLogTests_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_tempDir, recursive: true);
        }
        catch
        {
            // Best effort cleanup.
        }
    }

    [Fact]
    public void CreateHost_Uses_Settings_Driven_Host_Logger_Configuration()
    {
        var settings = new AppSettings
        {
            LogLevel = AppLogLevel.Warning,
            LogRetentionDays = 30,
            LogMaxFileSizeMb = 10
        };

        using var logService = new LogService(_tempDir, settings);
        using (var host = WsusManager.App.Program.CreateHost(Array.Empty<string>(), logService, settings, _tempDir))
        {
            var loggerFactory = host.Services.GetRequiredService<ILoggerFactory>();
            var logger = loggerFactory.CreateLogger("ProgramHostLogger");

            logger.LogDebug("Host debug should be suppressed");
            logger.LogWarning("Host warning should be present");
        }

        logService.Flush();

        var logFiles = Directory.GetFiles(_tempDir, "WsusManager-*.log", SearchOption.TopDirectoryOnly);
        Assert.NotEmpty(logFiles);

        var contents = string.Join(Environment.NewLine, logFiles.Select(File.ReadAllText));
        Assert.DoesNotContain("Host debug should be suppressed", contents);
        Assert.Contains("Host warning should be present", contents);
    }
}
