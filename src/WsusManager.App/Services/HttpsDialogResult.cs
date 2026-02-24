namespace WsusManager.App.Services;

/// <summary>
/// User-provided HTTPS dialog input values.
/// </summary>
public sealed record HttpsDialogResult(string ServerName, string CertificateThumbprint);
