using Serilog;
using Serilog.Events;
using WsusManager.Core.Models;
using AppLogLevel = WsusManager.Core.Models.LogLevel;

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
    private const int MinRetentionDays = 1;
    private const int MaxRetentionDays = 365;
    private const int MinFileSizeMb = 1;
    private const int MaxFileSizeMb = 1000;

    private readonly Serilog.Core.Logger _logger;
    private bool _disposed;

    public LogService()
        : this(DefaultLogDirectory, settings: null)
    {
    }

    public LogService(string logDirectory)
        : this(logDirectory, settings: null)
    {
    }

    public LogService(string logDirectory, AppSettings? settings)
    {
        var configuredSettings = settings ?? new AppSettings();

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
        var minimumLevel = ToSerilogLevel(configuredSettings.LogLevel);
        var retainedFileCountLimit = Math.Clamp(configuredSettings.LogRetentionDays, MinRetentionDays, MaxRetentionDays);
        var fileSizeLimitMb = Math.Clamp(configuredSettings.LogMaxFileSizeMb, MinFileSizeMb, MaxFileSizeMb);
        var fileSizeLimitBytes = fileSizeLimitMb * 1024L * 1024L;

        _logger = new LoggerConfiguration()
            .MinimumLevel.Is(minimumLevel)
            .WriteTo.File(
                logPath,
                rollingInterval: RollingInterval.Day,
                outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] {Message:lj}{NewLine}{Exception}",
                retainedFileCountLimit: retainedFileCountLimit,
                fileSizeLimitBytes: fileSizeLimitBytes,
                rollOnFileSizeLimit: true,
                shared: true)
            .CreateLogger();
    }

    private static LogEventLevel ToSerilogLevel(AppLogLevel level)
        => level switch
        {
            AppLogLevel.Debug => LogEventLevel.Debug,
            AppLogLevel.Info => LogEventLevel.Information,
            AppLogLevel.Warning => LogEventLevel.Warning,
            AppLogLevel.Error => LogEventLevel.Error,
            AppLogLevel.Fatal => LogEventLevel.Fatal,
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
