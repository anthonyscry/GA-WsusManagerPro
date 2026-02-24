namespace WsusManager.App.Services;

/// <summary>
/// Presents the Set HTTPS dialog and returns user input when confirmed.
/// </summary>
public interface IHttpsDialogService
{
    /// <summary>
    /// Shows the dialog and returns values on success, otherwise null.
    /// </summary>
    HttpsDialogResult? ShowDialog();
}
