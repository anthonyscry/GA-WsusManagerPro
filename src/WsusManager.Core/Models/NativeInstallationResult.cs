namespace WsusManager.Core.Models;

/// <summary>
/// Result of native WSUS installation orchestration.
/// Includes explicit fallback permission to control whether legacy
/// PowerShell install may be attempted.
/// </summary>
public record NativeInstallationResult(
    bool Success,
    string Message,
    bool AllowLegacyFallback,
    Exception? Exception = null)
{
    /// <summary>Creates a successful native install result.</summary>
    public static NativeInstallationResult Ok(string message = "Success") =>
        new(true, message, AllowLegacyFallback: false);

    /// <summary>Creates a failed native install result with explicit fallback policy.</summary>
    public static NativeInstallationResult Fail(string message, bool allowLegacyFallback, Exception? exception = null) =>
        new(false, message, allowLegacyFallback, exception);
}
