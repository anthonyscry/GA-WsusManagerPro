using System.IO;
using Xunit;

namespace WsusManager.Tests;

/// <summary>
/// Structural regression tests for high-visibility UI consistency fixes.
/// These tests validate XAML/source contracts without requiring UI automation.
/// </summary>
public class UiConsistencyRegressionTests
{
    private static string GetProjectRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null && !File.Exists(Path.Combine(directory.FullName, "WsusManager.sln")))
        {
            directory = directory.Parent;
        }

        if (directory == null)
            throw new DirectoryNotFoundException("Could not find solution root directory.");

        return Path.Combine(directory.FullName, "WsusManager.App");
    }

    private static string GetViewsPath() => Path.Combine(GetProjectRoot(), "Views");
    private static string GetThemesPath() => Path.Combine(GetProjectRoot(), "Themes");
    private static string GetViewModelsPath() => Path.Combine(GetProjectRoot(), "ViewModels");

    [Fact]
    public void MainWindow_AboutPanel_ShouldUseSiteOfOriginPackUri_ForGaIcon()
    {
        var xaml = File.ReadAllText(Path.Combine(GetViewsPath(), "MainWindow.xaml"));

        Assert.Contains("pack://siteoforigin:,,,/general_atomics_logo_big.ico", xaml);
    }

    [Fact]
    public void MainWindow_UpdatesListView_ShouldUseStandardizedStyles()
    {
        var xaml = File.ReadAllText(Path.Combine(GetViewsPath(), "MainWindow.xaml"));

        Assert.Contains("x:Name=\"UpdatesListView\"", xaml);
        Assert.Contains("BasedOn=\"{StaticResource VirtualizedListView}\"", xaml);
        Assert.Contains("ItemContainerStyle=\"{StaticResource DarkListViewItem}\"", xaml);
    }

    [Fact]
    public void SharedStyles_ShouldDefineDarkListViewItem_WithSelectionTrigger()
    {
        var styles = File.ReadAllText(Path.Combine(GetThemesPath(), "SharedStyles.xaml"));

        Assert.Contains("x:Key=\"DarkListViewItem\"", styles);
        Assert.Contains("<Trigger Property=\"IsSelected\" Value=\"True\">", styles);
    }

    [Fact]
    public void SharedStyles_ShouldUseTableTokens_ForHeaderAndRowStates()
    {
        var styles = File.ReadAllText(Path.Combine(GetThemesPath(), "SharedStyles.xaml"));

        Assert.Contains("{DynamicResource TableHeaderBackground}", styles);
        Assert.Contains("{DynamicResource TableHeaderForeground}", styles);
        Assert.Contains("{DynamicResource TableRowHoverBackground}", styles);
        Assert.Contains("{DynamicResource TableRowSelectedBackground}", styles);
        Assert.Contains("{DynamicResource TableRowSelectedForeground}", styles);
    }

    [Theory]
    [InlineData("DefaultDark.xaml")]
    [InlineData("JustBlack.xaml")]
    [InlineData("Slate.xaml")]
    [InlineData("Serenity.xaml")]
    [InlineData("Rose.xaml")]
    [InlineData("ClassicBlue.xaml")]
    public void ThemeFiles_ShouldDefineTableColorTokens(string themeFile)
    {
        var theme = File.ReadAllText(Path.Combine(GetThemesPath(), themeFile));

        Assert.Contains("x:Key=\"TableHeaderBackground\"", theme);
        Assert.Contains("x:Key=\"TableHeaderForeground\"", theme);
        Assert.Contains("x:Key=\"TableRowHoverBackground\"", theme);
        Assert.Contains("x:Key=\"TableRowSelectedBackground\"", theme);
        Assert.Contains("x:Key=\"TableRowSelectedForeground\"", theme);
    }

    [Fact]
    public void MainViewModel_RunInstallWsus_ShouldApplyThemeToInstallDialog()
    {
        var viewModel = File.ReadAllText(Path.Combine(GetViewModelsPath(), "MainViewModel.cs"));

        Assert.Contains("_themeService.ApplyTitleBarColorsToWindow(dialog, _settingsService.Current.SelectedTheme);", viewModel);
    }
}
