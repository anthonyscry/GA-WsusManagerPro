using System.IO;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Input;
using WsusManager.Core.Models;
using WsusManager.Core.Services;

namespace WsusManager.App.Views;

/// <summary>
/// Install WSUS dialog. Collects installer path, SA username, and SA password
/// before installation begins. Returns InstallOptions via public property.
/// </summary>
public partial class InstallDialog : Window
{
    private KeyEventHandler? _escHandler;

    /// <summary>
    /// The install options collected from the dialog. Only valid when DialogResult is true.
    /// </summary>
    public InstallOptions? Options { get; private set; }

    public InstallDialog()
    {
        InitializeComponent();

        // ESC key closes dialog (GUI-04)
        // Store handler reference for cleanup to prevent memory leak
        _escHandler = (s, e) =>
        {
            if (e.Key == Key.Escape)
                Close();
        };
        KeyDown += _escHandler;
        Closed += Dialog_Closed;

        // Set initial validation state
        ValidateInputs();
    }

    private void Dialog_Closed(object? sender, EventArgs e)
    {
        // Cleanup event handlers to prevent memory leaks
        if (_escHandler != null)
        {
            KeyDown -= _escHandler;
            _escHandler = null;
        }
        Closed -= Dialog_Closed;
    }

    private void Input_Changed(object sender, RoutedEventArgs e) => ValidateInputs();

    private void ValidateInputs()
    {
        if (TxtInstallerPath is null || PwdSaPassword is null || PwdSaPasswordConfirm is null || BtnInstall is null || TxtValidation is null)
            return;

        var path = TxtInstallerPath.Text.Trim();
        var password = PwdSaPassword.Password;
        var confirmPassword = PwdSaPasswordConfirm.Password;

        // Check installer path
        if (string.IsNullOrWhiteSpace(path))
        {
            TxtValidation.Text = "Installer path is required.";
            BtnInstall.IsEnabled = false;
            return;
        }

        if (!Directory.Exists(path))
        {
            TxtValidation.Text = "Installer directory not found.";
            BtnInstall.IsEnabled = false;
            return;
        }

        var installerExePath = Path.Combine(path, InstallationService.RequiredInstallerExe);
        if (!File.Exists(installerExePath))
        {
            TxtValidation.Text = $"Required installer not found: {InstallationService.RequiredInstallerExe}";
            BtnInstall.IsEnabled = false;
            return;
        }

        // Check SA password
        if (string.IsNullOrEmpty(password))
        {
            TxtValidation.Text = "SA password is required.";
            BtnInstall.IsEnabled = false;
            return;
        }

        if (password.Length < InstallationService.MinPasswordLength)
        {
            TxtValidation.Text = $"SA password must be at least {InstallationService.MinPasswordLength} characters.";
            BtnInstall.IsEnabled = false;
            return;
        }

        if (!Regex.IsMatch(password, @"\d"))
        {
            TxtValidation.Text = "SA password must contain at least 1 digit.";
            BtnInstall.IsEnabled = false;
            return;
        }

        if (!Regex.IsMatch(password, @"[^a-zA-Z0-9]"))
        {
            TxtValidation.Text = "SA password must contain at least 1 special character.";
            BtnInstall.IsEnabled = false;
            return;
        }

        // Check password confirmation
        if (password != confirmPassword)
        {
            TxtValidation.Text = "Passwords do not match.";
            BtnInstall.IsEnabled = false;
            return;
        }

        // All valid
        TxtValidation.Text = string.Empty;
        BtnInstall.IsEnabled = true;
    }

    private void BrowseInstallerPath_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFolderDialog
        {
            Title = "Select Installer Directory"
        };

        if (dialog.ShowDialog() == true)
        {
            TxtInstallerPath.Text = dialog.FolderName;
            ValidateInputs();
        }
    }

    private void Ok_Click(object sender, RoutedEventArgs e)
    {
        ValidateInputs();

        if (BtnInstall is null || !BtnInstall.IsEnabled)
            return;

        var password = PwdSaPassword.Password;

        Options = new InstallOptions
        {
            InstallerPath = TxtInstallerPath.Text.Trim(),
            SaUsername = string.IsNullOrWhiteSpace(TxtSaUsername.Text) ? "sa" : TxtSaUsername.Text.Trim(),
            SaPassword = password
        };

        DialogResult = true;
        Close();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
