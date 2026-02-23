using Serilog;
using Serilog.Events;
using WsusManager.Core.Models;
using AppLogLevel = WsusManager.Core.Models.LogLevel;

namespace WsusManager.Core.Logging;

/// <summary>
/// Centralized log configuration mapping from AppSettings to Serilog.
/// Keeps Program and LogService aligned on runtime logging behavior.
/// </summary>
public static class LogConfiguration
{
    private const int MinRetentionDays = 1;
    private const int MaxRetentionDays = 365;
    private const int MinFileSizeMb = 1;
    private const int MaxFileSizeMb = 1000;

    private const string OutputTemplate =
        "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] {Message:lj}{NewLine}{Exception}";

    public static LoggerConfiguration CreateFileLoggerConfiguration(string logPath, AppSettings? settings = null)
    {
        var options = ToOptions(settings);

        return new LoggerConfiguration()
            .MinimumLevel.Is(options.MinimumLevel)
            .WriteTo.File(
                logPath,
                rollingInterval: RollingInterval.Day,
                outputTemplate: OutputTemplate,
                retainedFileTimeLimit: options.Retention,
                retainedFileCountLimit: null,
                fileSizeLimitBytes: options.FileSizeLimitBytes,
                rollOnFileSizeLimit: true,
                shared: true);
    }

    public static LogOptions ToOptions(AppSettings? settings)
    {
        var configuredSettings = settings ?? new AppSettings();

        var retentionDays = Math.Clamp(configuredSettings.LogRetentionDays, MinRetentionDays, MaxRetentionDays);
        var fileSizeMb = Math.Clamp(configuredSettings.LogMaxFileSizeMb, MinFileSizeMb, MaxFileSizeMb);

        return new LogOptions(
            MinimumLevel: ToSerilogLevel(configuredSettings.LogLevel),
            Retention: TimeSpan.FromDays(retentionDays),
            FileSizeLimitBytes: fileSizeMb * 1024L * 1024L);
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
}

public sealed record LogOptions(
    LogEventLevel MinimumLevel,
    TimeSpan Retention,
    long FileSizeLimitBytes);
