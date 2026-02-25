using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media.Imaging;
using WsusManager.App.Services;
using WsusManager.App.ViewModels;
using WsusManager.Core.Infrastructure;
using WsusManager.Core.Logging;
using WsusManager.Core.Models;
using WsusManager.Core.Services.Interfaces;

namespace WsusManager.App.Views;

/// <summary>
/// Main application window. Constructor-only code-behind that sets DataContext
/// from the DI-injected ViewModel. Minimal code-behind for UI-only concerns.
/// </summary>
public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;
    private readonly ISettingsService _settingsService;
    private readonly IThemeService _themeService;
    private readonly ILogService _logService;
    private AppSettings _settings;

    public MainWindow(
        MainViewModel viewModel,
        ISettingsService settingsService,
        IThemeService themeService,
        ILogService logService)
    {
        InitializeComponent();
        _viewModel = viewModel;
        _settingsService = settingsService;
        _themeService = themeService;
        _logService = logService;
        _settings = _settingsService.Current;
        DataContext = viewModel;

        // Load GA logo as window icon
        LoadIcon();
        ValidateBrandingAssets();

        SourceInitialized += MainWindow_SourceInitialized;
        Loaded += MainWindow_Loaded;
        Activated += MainWindow_Activated;
        Deactivated += MainWindow_Deactivated;
        Closing += MainWindow_Closing;
        Closed += MainWindow_Closed;
    }

    private void LoadIcon()
    {
        var iconPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "general_atomics_logo_small.ico");

        try
        {
            if (System.IO.File.Exists(iconPath))
            {
                var uri = new Uri(iconPath);
                var bitmap = new BitmapImage(uri);
                bitmap.Freeze();
                Icon = bitmap;
            }
            else
            {
                _logService.Warning("Window icon file not found at {IconPath}. Using system default icon.", iconPath);
            }
        }
        catch (Exception ex)
        {
            _logService.Warning("Failed to load window icon from {IconPath}: {Error}", iconPath, ex.Message);
        }
    }

    private void ValidateBrandingAssets()
    {
        try
        {
            var aboutLogoUri = new Uri("pack://application:,,,/general_atomics_logo_big.ico", UriKind.Absolute);
            var logoStream = Application.GetResourceStream(aboutLogoUri);
            if (logoStream == null)
            {
                _logService.Warning("Branding asset missing at startup: {AssetUri}. About panel icon may be unavailable.", aboutLogoUri);
            }
        }
        catch (Exception ex)
        {
            _logService.Warning("Failed to validate branding asset resource: {Error}", ex.Message);
        }
    }

    private void MainWindow_SourceInitialized(object? sender, EventArgs e)
    {
        ApplyCurrentTitleBarTheme();
    }

    private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        // Unsubscribe immediately (before any await) to prevent memory leak and re-entry
        Loaded -= MainWindow_Loaded;

        // Re-apply current theme title bar colors now that HWND exists.
        // Do NOT reset to system defaults here; that causes white caption flash on startup.
        ApplyCurrentTitleBarTheme();

        // Restore window bounds if enabled and valid
        if (_settings.PersistWindowState && _settings.WindowBounds != null)
        {
            var bounds = _settings.WindowBounds;

            // Validate bounds are within screen
            if (bounds.IsValid() && IsWithinScreenBounds(bounds))
            {
                Width = bounds.Width;
                Height = bounds.Height;
                Left = bounds.Left;
                Top = bounds.Top;

                if (string.Equals(bounds.WindowState, "Maximized", StringComparison.Ordinal))
                {
                    WindowState = WindowState.Maximized;
                }
            }
            // If bounds invalid, use default XAML values
        }

        await _viewModel.InitializeAsync().ConfigureAwait(false);
    }

    private void MainWindow_Activated(object? sender, EventArgs e)
    {
        ApplyCurrentTitleBarTheme();
    }

    private void MainWindow_Deactivated(object? sender, EventArgs e)
    {
        ApplyCurrentTitleBarTheme();
    }

    private void ApplyCurrentTitleBarTheme()
    {
        var selectedTheme = _settingsService.Current.SelectedTheme;
        _themeService.ApplyTitleBarColorsToWindow(this, selectedTheme);
    }

    private void MainWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        // Save window bounds if enabled
        if (_settings.PersistWindowState)
        {
            _settings.WindowBounds = new WindowBounds
            {
                Width = WindowState == WindowState.Maximized ? RestoreBounds.Width : ActualWidth,
                Height = WindowState == WindowState.Maximized ? RestoreBounds.Height : ActualHeight,
                Left = WindowState == WindowState.Maximized ? RestoreBounds.Left : Left,
                Top = WindowState == WindowState.Maximized ? RestoreBounds.Top : Top,
                WindowState = WindowState == WindowState.Maximized ? "Maximized" : "Normal"
            };

            // Save settings to disk (fire and forget)
            _ = _settingsService.SaveAsync(_settings);
        }
        else if (!_settings.PersistWindowState)
        {
            // Clear saved bounds
            _settings.WindowBounds = null;
            _ = _settingsService.SaveAsync(_settings);
        }
    }

    private void MainWindow_Closed(object? sender, EventArgs e)
    {
        Activated -= MainWindow_Activated;
        Deactivated -= MainWindow_Deactivated;
        SourceInitialized -= MainWindow_SourceInitialized;

        // Cleanup ViewModel resources (timer, CTS, log builder)
        _viewModel.Dispose();
    }

    /// <summary>
    /// Checks if window bounds are within the primary screen's working area.
    /// </summary>
    private bool IsWithinScreenBounds(WindowBounds bounds)
    {
        try
        {
            var workingArea = SystemParameters.WorkArea;
            return bounds.Left >= workingArea.Left &&
                   bounds.Top >= workingArea.Top &&
                   bounds.Left + bounds.Width <= workingArea.Right &&
                   bounds.Top + bounds.Height <= workingArea.Bottom;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Auto-scroll log text to the bottom when new text is added.
    /// This is a UI-only concern, not business logic.
    /// </summary>
    private void LogTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (sender is TextBox textBox)
        {
            textBox.ScrollToEnd();
        }
    }
}
