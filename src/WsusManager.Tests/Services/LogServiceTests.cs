using WsusManager.Core.Logging;
using WsusManager.Core.Models;

namespace WsusManager.Tests.Services;

/// <summary>
/// Tests for LogService: initialization, log directory creation,
/// Flush(), and Dispose() safety.
/// </summary>
public class LogServiceTests : IDisposable
{
    private readonly string _tempDir;

    public LogServiceTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"WsusManagerLogTests_{Guid.NewGuid():N}");
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, recursive: true); } catch { }
    }

    [Fact]
    public void Constructor_Creates_Log_Directory_If_Missing()
    {
        Assert.False(Directory.Exists(_tempDir));

        using var svc = new LogService(_tempDir);

        Assert.True(Directory.Exists(_tempDir));
    }

    [Fact]
    public void Info_Does_Not_Throw()
    {
        using var svc = new LogService(_tempDir);

        var ex = Record.Exception(() => svc.Info("Test message {Value}", 42));

        Assert.Null(ex);
    }

    [Fact]
    public void Warning_And_Error_Do_Not_Throw()
    {
        using var svc = new LogService(_tempDir);

        var ex = Record.Exception(() =>
        {
            svc.Warning("Warning {Code}", 100);
            svc.Error("Error {Code}", 500);
            svc.Error(new InvalidOperationException("test"), "Error with exception");
            svc.Debug("Debug message");
            svc.Fatal("Fatal message");
            svc.Fatal(new Exception("fatal"), "Fatal with exception");
        });

        Assert.Null(ex);
    }

    [Fact]
    public void LogStartup_Does_Not_Throw()
    {
        using var svc = new LogService(_tempDir);

        var ex = Record.Exception(() => svc.LogStartup("4.0.0", 250));

        Assert.Null(ex);
    }

    [Fact]
    public void Dispose_Can_Be_Called_Multiple_Times()
    {
        var svc = new LogService(_tempDir);
        svc.Dispose();

        var ex = Record.Exception(() => svc.Dispose());

        Assert.Null(ex);
    }

    [Fact]
    public void Flush_Does_Not_Throw()
    {
        using var svc = new LogService(_tempDir);

        var ex = Record.Exception(() => svc.Flush());

        Assert.Null(ex);
    }

    [Fact]
    public void Warning_Log_Level_Suppresses_Debug_Entries()
    {
        var settings = new AppSettings
        {
            LogLevel = LogLevel.Warning,
            LogRetentionDays = 30,
            LogMaxFileSizeMb = 10
        };

        using var svc = new LogService(_tempDir, settings);

        svc.Debug("Debug should be suppressed");
        svc.Warning("Warning should be present");
        svc.Flush();

        var logFiles = Directory.GetFiles(_tempDir, "WsusManager-*.log", SearchOption.TopDirectoryOnly);
        Assert.NotEmpty(logFiles);

        var logContents = string.Join(Environment.NewLine, logFiles.Select(File.ReadAllText));
        Assert.DoesNotContain("Debug should be suppressed", logContents);
        Assert.Contains("Warning should be present", logContents);
    }

    [Fact]
    public void Retention_And_File_Size_Use_AppSettings_Values()
    {
        var settings = new AppSettings
        {
            LogLevel = LogLevel.Debug,
            LogRetentionDays = 2,
            LogMaxFileSizeMb = 1
        };

        using var svc = new LogService(_tempDir, settings);

        var payload = new string('X', 300_000);
        for (var i = 0; i < 300; i++)
        {
            svc.Info("{Index} {Payload}", i, payload);
        }

        svc.Flush();

        var logFiles = Directory.GetFiles(_tempDir, "WsusManager-*.log", SearchOption.TopDirectoryOnly);
        Assert.NotEmpty(logFiles);
        Assert.True(
            logFiles.Length > settings.LogRetentionDays,
            $"Expected more than {settings.LogRetentionDays} files because retention is time-based (days), but found {logFiles.Length}.");

        var maxBytes = settings.LogMaxFileSizeMb * 1024 * 1024;
        var sizeAllowanceBytes = 256 * 1024;
        Assert.All(logFiles, file =>
        {
            var length = new FileInfo(file).Length;
            Assert.True(length <= maxBytes + sizeAllowanceBytes, $"File '{file}' length {length} exceeded configured max {maxBytes}");
        });
    }

    [Fact]
    public void ToOptions_Maps_LogLevel_RetentionDays_And_FileSize_From_Settings()
    {
        var settings = new AppSettings
        {
            LogLevel = LogLevel.Warning,
            LogRetentionDays = 45,
            LogMaxFileSizeMb = 7
        };

        var options = LogConfiguration.ToOptions(settings);

        Assert.Equal(Serilog.Events.LogEventLevel.Warning, options.MinimumLevel);
        Assert.Equal(TimeSpan.FromDays(45), options.Retention);
        Assert.Equal(7L * 1024L * 1024L, options.FileSizeLimitBytes);
    }
}
