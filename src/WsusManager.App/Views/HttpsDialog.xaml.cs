using System.Windows;
using System.Windows.Input;

namespace WsusManager.App.Views;

/// <summary>
/// Collects WSUS server name and certificate thumbprint for HTTPS setup.
/// </summary>
public partial class HttpsDialog : Window
{
    private KeyEventHandler? _escHandler;

    public HttpsDialog()
    {
        InitializeComponent();

        TxtServerName.Text = Environment.MachineName;

        _escHandler = (s, e) =>
        {
            if (e.Key == Key.Escape)
            {
                Close();
            }
        };

        KeyDown += _escHandler;
        Closed += Dialog_Closed;

        ValidateInputs();
    }

    public string ServerName => TxtServerName.Text.Trim();

    public string CertificateThumbprint => TxtThumbprint.Text.Trim().Replace(" ", string.Empty, StringComparison.Ordinal);

    private void Dialog_Closed(object? sender, EventArgs e)
    {
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
        if (TxtServerName is null || TxtThumbprint is null || TxtValidation is null || BtnApply is null)
            return;

        if (string.IsNullOrWhiteSpace(ServerName))
        {
            TxtValidation.Text = "WSUS server name is required.";
            BtnApply.IsEnabled = false;
            return;
        }

        if (string.IsNullOrWhiteSpace(CertificateThumbprint))
        {
            TxtValidation.Text = "Certificate thumbprint is required.";
            BtnApply.IsEnabled = false;
            return;
        }

        TxtValidation.Text = string.Empty;
        BtnApply.IsEnabled = true;
    }

    private void Ok_Click(object sender, RoutedEventArgs e)
    {
        ValidateInputs();
        if (!BtnApply.IsEnabled)
            return;

        DialogResult = true;
        Close();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
