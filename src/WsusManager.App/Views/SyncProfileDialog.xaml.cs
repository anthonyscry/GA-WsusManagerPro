using System.Windows;
using System.Windows.Input;
using WsusManager.Core.Models;

namespace WsusManager.App.Views;

/// <summary>
/// Sync profile selection dialog. Returns the selected SyncProfile via the SelectedProfile property.
/// </summary>
public partial class SyncProfileDialog : Window
{
    private KeyEventHandler? _escHandler;

    /// <summary>
    /// The profile selected by the user. Only valid when DialogResult is true.
    /// </summary>
    public SyncProfile SelectedProfile { get; private set; } = SyncProfile.FullSync;

    /// <summary>
    /// Export parameters collected from the dialog. Paths may be null when the field was left empty.
    /// Only valid when DialogResult is true.
    /// </summary>
    public ExportOptions ExportOptions { get; private set; } = new();

    public SyncProfileDialog(DefaultSyncProfile defaultProfile = DefaultSyncProfile.Full)
    {
        InitializeComponent();

        switch (defaultProfile)
        {
            case DefaultSyncProfile.Quick:
                RbQuickSync.IsChecked = true;
                break;
            case DefaultSyncProfile.SyncOnly:
                RbSyncOnly.IsChecked = true;
                break;
            default:
                RbFullSync.IsChecked = true;
                break;
        }

        // ESC key closes dialog (GUI-04)
        // Store handler reference for cleanup to prevent memory leak
        _escHandler = (s, e) =>
        {
            if (e.Key == Key.Escape)
                Close();
        };
        KeyDown += _escHandler;
        Closed += Dialog_Closed;
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

    private void Ok_Click(object sender, RoutedEventArgs e)
    {
        SelectedProfile = RbFullSync.IsChecked == true ? SyncProfile.FullSync
            : RbQuickSync.IsChecked == true ? SyncProfile.QuickSync
            : SyncProfile.SyncOnly;

        var fullExportPath = string.IsNullOrWhiteSpace(TxtFullExportPath?.Text)
            ? null
            : TxtFullExportPath.Text.Trim();

        var diffExportPath = string.IsNullOrWhiteSpace(TxtDiffExportPath?.Text)
            ? null
            : TxtDiffExportPath.Text.Trim();

        var exportDays = 30;
        if (!string.IsNullOrWhiteSpace(TxtExportDays?.Text) && int.TryParse(TxtExportDays.Text, out var parsed) && parsed > 0)
        {
            exportDays = parsed;
        }

        ExportOptions = new ExportOptions
        {
            SourcePath = @"C:\WSUS",
            FullExportPath = fullExportPath,
            DifferentialExportPath = diffExportPath,
            ExportDays = exportDays
        };

        DialogResult = true;
        Close();
    }

    private void BrowseFullExport_Click(object sender, RoutedEventArgs e)
    {
        var path = BrowseFolder("Select Full Export Path");
        if (path is not null)
            TxtFullExportPath.Text = path;
    }

    private void BrowseDiffExport_Click(object sender, RoutedEventArgs e)
    {
        var path = BrowseFolder("Select Differential Export Path");
        if (path is not null)
            TxtDiffExportPath.Text = path;
    }

    private static string? BrowseFolder(string description)
    {
        var dialog = new Microsoft.Win32.OpenFolderDialog
        {
            Title = description
        };

        return dialog.ShowDialog() == true ? dialog.FolderName : null;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
