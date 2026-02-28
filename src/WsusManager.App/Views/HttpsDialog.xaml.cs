using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Input;
using WsusManager.App.Services;

namespace WsusManager.App.Views;

/// <summary>
/// Collects WSUS server and certificate thumbprint for HTTPS configuration.
/// </summary>
public partial class HttpsDialog : Window
{
    private static readonly Regex HexThumbprintRegex = new("^[0-9A-Fa-f]{40}$", RegexOptions.Compiled);
    private KeyEventHandler? _escHandler;

    public HttpsDialogResult? Result { get; private set; }

    public HttpsDialog()
    {
        InitializeComponent();

        _escHandler = (s, e) =>
        {
            if (e.Key == Key.Escape)
                Close();
        };
        KeyDown += _escHandler;
        Closed += Dialog_Closed;

        ValidateInputs();
    }

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
        if (TxtServerName is null || TxtThumbprint is null || TxtValidation is null || BtnSetHttps is null)
            return;

        var serverName = TxtServerName.Text.Trim();
        var thumbprint = NormalizeThumbprint(TxtThumbprint.Text);

        if (string.IsNullOrWhiteSpace(serverName))
        {
            TxtValidation.Text = "WSUS server name is required.";
            BtnSetHttps.IsEnabled = false;
            return;
        }

        if (string.IsNullOrWhiteSpace(thumbprint))
        {
            TxtValidation.Text = "Certificate thumbprint is required.";
            BtnSetHttps.IsEnabled = false;
            return;
        }

        if (!HexThumbprintRegex.IsMatch(thumbprint))
        {
            TxtValidation.Text = "Thumbprint must be 40 hexadecimal characters.";
            BtnSetHttps.IsEnabled = false;
            return;
        }

        TxtValidation.Text = string.Empty;
        BtnSetHttps.IsEnabled = true;
    }

    private void Ok_Click(object sender, RoutedEventArgs e)
    {
        var serverName = TxtServerName.Text.Trim();
        var thumbprint = NormalizeThumbprint(TxtThumbprint.Text);

        if (string.IsNullOrWhiteSpace(serverName) || !HexThumbprintRegex.IsMatch(thumbprint))
            return;

        Result = new HttpsDialogResult(serverName, thumbprint.ToUpperInvariant());
        DialogResult = true;
        Close();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private static string NormalizeThumbprint(string value) =>
        string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : value.Replace(" ", string.Empty, StringComparison.Ordinal).Trim();
}
