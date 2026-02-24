using System.Windows;
using WsusManager.App.Views;

namespace WsusManager.App.Services;

/// <summary>
/// Default UI-backed implementation for HTTPS dialog prompts.
/// </summary>
public class HttpsDialogService : IHttpsDialogService
{
    public HttpsDialogResult? ShowDialog()
    {
        var dialog = new HttpsDialog();
        if (Application.Current.MainWindow is not null)
        {
            dialog.Owner = Application.Current.MainWindow;
        }

        if (dialog.ShowDialog() != true)
        {
            return null;
        }

        return new HttpsDialogResult(dialog.ServerName, dialog.CertificateThumbprint);
    }
}
