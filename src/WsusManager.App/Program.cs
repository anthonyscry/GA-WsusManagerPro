using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Events;
using WsusManager.App.Services;
using WsusManager.App.ViewModels;
using WsusManager.App.Views;
using WsusManager.Core.Infrastructure;
using WsusManager.Core.Logging;
using WsusManager.Core.Models;
using WsusManager.Core.Services;
using WsusManager.Core.Services.Interfaces;
using AppLogLevel = WsusManager.Core.Models.LogLevel;

namespace WsusManager.App;

/// <summary>
/// Application entry point. Sets up the DI host, resolves the main window,
/// and runs the WPF application. All service dependencies are registered here.
/// </summary>
public static class Program
{
    public const string AppVersion = "4.5.9";
    private const string LogDirectory = @"C:\WSUS\Logs";
    private const int MinRetentionDays = 1;
    private const int MaxRetentionDays = 365;
    private const int MinFileSizeMb = 1;
    private const int MaxFileSizeMb = 1000;

    [STAThread]
    public static void Main(string[] args)
    {
        var startupTimer = Stopwatch.StartNew();
        var loggingSettings = LoadLoggingSettings();

        // Create structured logger early so all startup activity is captured
        var logService = new LogService(LogDirectory, loggingSettings);

        var host = CreateHost(args, logService, loggingSettings);

        var app = new App();
        app.InitializeComponent();
        app.ConfigureServices(host.Services);

        var window = host.Services.GetRequiredService<MainWindow>();

        // Apply theme after window creation for faster initial render
        var themeService = host.Services.GetRequiredService<IThemeService>();
        var settingsService = host.Services.GetRequiredService<ISettingsService>();
        var settings = settingsService.Current;

        // Set main window for title bar theming
        themeService.SetMainWindow(window);

        // Pre-load all theme resources to enable instant switching (<100ms)
        themeService.PreloadThemes();

        themeService.ApplyTheme(settings.SelectedTheme);

        startupTimer.Stop();
        logService.LogStartup(AppVersion, startupTimer.ElapsedMilliseconds);

        app.MainWindow = window;

        try
        {
            app.Run(window);
        }
        finally
        {
            logService.Flush();
        }
    }

    /// <summary>
    /// Creates the DI host with all services registered.
    /// Exposed as internal for integration testing.
    /// </summary>
    internal static IHost CreateHost(string[] args, LogService logService, AppSettings loggingSettings)
    {
        var builder = Host.CreateApplicationBuilder(args);
        var retainedFileCountLimit = Math.Clamp(loggingSettings.LogRetentionDays, MinRetentionDays, MaxRetentionDays);
        var fileSizeLimitMb = Math.Clamp(loggingSettings.LogMaxFileSizeMb, MinFileSizeMb, MaxFileSizeMb);
        var fileSizeLimitBytes = fileSizeLimitMb * 1024L * 1024L;
        var minimumLevel = ToSerilogLevel(loggingSettings.LogLevel);

        // Configure Serilog as the logging provider for Microsoft.Extensions.Logging
        var serilogLogger = new LoggerConfiguration()
            .MinimumLevel.Is(minimumLevel)
            .WriteTo.File(
                Path.Combine(LogDirectory, "WsusManager-.log"),
                rollingInterval: Serilog.RollingInterval.Day,
                outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] {Message:lj}{NewLine}{Exception}",
                retainedFileCountLimit: retainedFileCountLimit,
                fileSizeLimitBytes: fileSizeLimitBytes,
                rollOnFileSizeLimit: true,
                shared: true)
            .CreateLogger();

        builder.Logging.ClearProviders();
        builder.Logging.AddSerilog(serilogLogger);

        // Core services
        builder.Services.AddSingleton<ILogService>(logService);
        builder.Services.AddSingleton<ISettingsService, SettingsService>();
        builder.Services.AddSingleton<IProcessRunner, ProcessRunner>();
        builder.Services.AddSingleton<IDashboardService, DashboardService>();

        // Phase 3: Diagnostics and Service Management
        builder.Services.AddSingleton<IWindowsServiceManager, WindowsServiceManager>();
        builder.Services.AddSingleton<IFirewallService, FirewallService>();
        builder.Services.AddSingleton<IPermissionsService, PermissionsService>();
        builder.Services.AddSingleton<IHealthService, HealthService>();
        builder.Services.AddSingleton<IContentResetService, ContentResetService>();

        // Phase 4: Database Operations
        builder.Services.AddSingleton<ISqlService, SqlService>();
        builder.Services.AddSingleton<IDeepCleanupService, DeepCleanupService>();
        builder.Services.AddSingleton<IDatabaseBackupService, DatabaseBackupService>();

        // Phase 5: WSUS Operations
        builder.Services.AddSingleton<IWsusServerService, WsusServerService>();
        builder.Services.AddSingleton<ISyncService, SyncService>();
        builder.Services.AddSingleton<IRobocopyService, RobocopyService>();
        builder.Services.AddSingleton<IExportService, ExportService>();
        builder.Services.AddSingleton<IImportService, ImportService>();

        // Phase 6: Installation and Scheduling
        builder.Services.AddSingleton<IInstallationService, InstallationService>();
        builder.Services.AddSingleton<IScheduledTaskService, ScheduledTaskService>();
        builder.Services.AddSingleton<IGpoDeploymentService, GpoDeploymentService>();

        // Phase 14: Client Management
        builder.Services.AddSingleton<WinRmExecutor>();
        builder.Services.AddSingleton<IClientService, ClientService>();

        // Phase 15: Script Generator
        builder.Services.AddSingleton<IScriptGeneratorService, ScriptGeneratorService>();

        // Phase 16: Theme Infrastructure
        builder.Services.AddSingleton<IThemeService, ThemeService>();

        // Phase 27: Visual Feedback Polish
        builder.Services.AddSingleton<IBenchmarkTimingService, BenchmarkTimingService>();

        // Phase 28: Settings Expansion
        builder.Services.AddSingleton<ISettingsValidationService, SettingsValidationService>();

        // Phase 30: Data Export
        builder.Services.AddSingleton<ICsvExportService, CsvExportService>();

        // ViewModels
        builder.Services.AddSingleton<MainViewModel>();

        // Views
        builder.Services.AddSingleton<MainWindow>();

        return builder.Build();
    }

    private static AppSettings LoadLoggingSettings()
    {
        try
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var settingsPath = Path.Combine(appData, "WsusManager", "settings.json");

            if (!File.Exists(settingsPath))
            {
                return new AppSettings();
            }

            var json = File.ReadAllText(settingsPath);
            return JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
        }
        catch
        {
            return new AppSettings();
        }
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
