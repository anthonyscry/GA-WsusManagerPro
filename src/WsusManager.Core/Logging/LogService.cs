using Serilog;
using Serilog.Events;
using WsusManager.Core.Models;

namespace WsusManager.Core.Logging;

/// <summary>
/// Serilog-based structured logging service. Writes daily rolling log files
/// to C:\WSUS\Logs\WsusManager-{Date}.log with detailed structured entries
/// suitable for remote troubleshooting on air-gapped servers.
/// </summary>
public class LogService : ILogService, IDisposable
{
    private const string DefaultLogDirectory = @"C:\WSUS\Logs";
    private const string LogFileTemplate = "WsusManager-.log";
    private const int MinimumRetentionDays = 1;
    private const int MaximumRetentionDays = 365;
    private const int MinimumLogFileSizeMb = 1;
    private const int MaximumLogFileSizeMb = 1000;

    private readonly Serilog.Core.Logger _logger;
    private bool _disposed;

    public LogService()
        : this(DefaultLogDirectory, new AppSettings())
    {
    }

    public LogService(string logDirectory)
        : this(logDirectory, new AppSettings())
    {
    }

    public LogService(string logDirectory, AppSettings settings)
    {
        // Ensure log directory exists
        try
        {
            if (!Directory.Exists(logDirectory))
            {
                Directory.CreateDirectory(logDirectory);
            }
        }
        catch
        {
            // If we can't create the directory, Serilog will handle the error gracefully
        }

        var logPath = Path.Combine(logDirectory, LogFileTemplate);
        var level = MapLogLevel(settings?.LogLevel ?? LogLevel.Info);
        var retentionDays = Math.Clamp(settings?.LogRetentionDays ?? 30, MinimumRetentionDays, MaximumRetentionDays);
        var fileSizeBytes = (long)Math.Clamp(settings?.LogMaxFileSizeMb ?? 10, MinimumLogFileSizeMb, MaximumLogFileSizeMb) * 1024 * 1024;

        _logger = new LoggerConfiguration()
            .MinimumLevel.Is(level)
            .WriteTo.File(
                logPath,
                rollingInterval: RollingInterval.Day,
                outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] {Message:lj}{NewLine}{Exception}",
                retainedFileCountLimit: retentionDays,
                fileSizeLimitBytes: fileSizeBytes,
                shared: true)
            .CreateLogger();
    }

    private static LogEventLevel MapLogLevel(LogLevel level) =>
        level switch
        {
            LogLevel.Debug => LogEventLevel.Debug,
            LogLevel.Info => LogEventLevel.Information,
            LogLevel.Warning => LogEventLevel.Warning,
            LogLevel.Error => LogEventLevel.Error,
            LogLevel.Fatal => LogEventLevel.Fatal,
            _ => LogEventLevel.Information
        };

    public void Info(string messageTemplate, params object[] propertyValues)
        => _logger.Information(messageTemplate, propertyValues);

    public void Warning(string messageTemplate, params object[] propertyValues)
        => _logger.Warning(messageTemplate, propertyValues);

    public void Error(string messageTemplate, params object[] propertyValues)
        => _logger.Error(messageTemplate, propertyValues);

    public void Error(Exception exception, string messageTemplate, params object[] propertyValues)
        => _logger.Error(exception, messageTemplate, propertyValues);

    public void Debug(string messageTemplate, params object[] propertyValues)
        => _logger.Debug(messageTemplate, propertyValues);

    public void Fatal(string messageTemplate, params object[] propertyValues)
        => _logger.Fatal(messageTemplate, propertyValues);

    public void Fatal(Exception exception, string messageTemplate, params object[] propertyValues)
        => _logger.Fatal(exception, messageTemplate, propertyValues);

    public void LogStartup(string version, long startupMs)
    {
        _logger.Information(
            "========== WSUS Manager v{Version} Starting ==========",
            version);
        _logger.Information(
            "Startup completed in {ElapsedMs}ms | OS: {OS} | CLR: {CLR} | Machine: {Machine} | User: {User}",
            startupMs,
            Environment.OSVersion,
            Environment.Version,
            Environment.MachineName,
            Environment.UserName);
    }

    public void Flush()
    {
        _logger.Information("========== WSUS Manager Shutting Down ==========");
        (_logger as IDisposable)?.Dispose();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Flush();
        GC.SuppressFinalize(this);
    }
}
