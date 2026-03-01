using System.IO;
using System.Xml.Linq;
using Xunit;

namespace WsusManager.Tests;

/// <summary>
/// Structural tests for keyboard navigation support in XAML files.
/// Full keyboard navigation testing requires UI automation (manual testing).
/// </summary>
public class KeyboardNavigationTests
{
    // Resolve from test assembly location to avoid dependence on process current directory.
    private static string GetViewsPath() =>
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "WsusManager.App", "Views"));

    private static string GetXamlPath(string fileName) => Path.Combine(GetViewsPath(), fileName);

    [Fact]
    public void MainWindow_ShouldHaveKeyboardNavigationAttributes()
    {
        var content = File.ReadAllText(GetXamlPath("MainWindow.xaml"));

        // Verify KeyboardNavigation attributes are present
        Assert.Contains("KeyboardNavigation.", content);
    }

    [Theory]
    [InlineData("SettingsDialog.xaml")]
    [InlineData("SyncProfileDialog.xaml")]
    [InlineData("TransferDialog.xaml")]
    [InlineData("ScheduleTaskDialog.xaml")]
    [InlineData("InstallDialog.xaml")]
    [InlineData("HttpsDialog.xaml")]
    public void Dialogs_ShouldSupportTabNavigation(string dialogFile)
    {
        var dialogPath = GetXamlPath(dialogFile);
        Assert.True(File.Exists(dialogPath), $"{dialogFile} should exist");

        var xaml = XDocument.Load(dialogPath);

        // Count interactive elements (Button, TextBox, ComboBox, CheckBox, RadioButton)
        var interactiveElements = xaml.Descendants()
            .Where(e => e.Name.LocalName is "Button" or "TextBox" or "ComboBox" or "CheckBox" or "RadioButton" or "ListBox")
            .ToList();

        // Verify dialog has interactive elements (sanity check)
        Assert.True(interactiveElements.Count > 0, $"{dialogFile} should have interactive elements");
    }

    [Fact]
    public void MainWindow_ShouldNotContainIsTabStopFalse()
    {
        // Verify no elements explicitly disable tab navigation
        var content = File.ReadAllText(GetXamlPath("MainWindow.xaml"));

        Assert.DoesNotContain("IsTabStop=\"False\"", content);
        Assert.DoesNotContain("IsTabStop=\"false\"", content);
    }

    [Fact]
    public void AllDialogs_ShouldHaveTabNavigationAttribute()
    {
        var dialogFiles = new[]
        {
            "SettingsDialog.xaml",
            "SyncProfileDialog.xaml",
            "TransferDialog.xaml",
            "ScheduleTaskDialog.xaml",
            "InstallDialog.xaml",
            "HttpsDialog.xaml"
        };

        foreach (var dialogFile in dialogFiles)
        {
            var content = File.ReadAllText(GetXamlPath(dialogFile));

            // Verify root Grid has KeyboardNavigation.TabNavigation
            Assert.True(content.Contains("KeyboardNavigation.TabNavigation"),
                $"{dialogFile} should have KeyboardNavigation.TabNavigation attribute");
        }
    }

    [Theory]
    [InlineData("MainWindow.xaml", "KeyboardNavigation.TabNavigation=\"Once\"")]
    [InlineData("SettingsDialog.xaml", "KeyboardNavigation.TabNavigation=\"Continue\"")]
    [InlineData("SyncProfileDialog.xaml", "KeyboardNavigation.TabNavigation=\"Continue\"")]
    public void SpecificFiles_ShouldHaveExpectedNavigationMode(string fileName, string expectedAttribute)
    {
        var content = File.ReadAllText(GetXamlPath(fileName));

        Assert.True(content.Contains(expectedAttribute),
            $"{fileName} should have {expectedAttribute}");
    }

    [Fact]
    public void MainWindow_ShouldHaveInputBindingsForShortcuts()
    {
        var content = File.ReadAllText(GetXamlPath("MainWindow.xaml"));

        // Verify global keyboard shortcuts are defined
        Assert.Contains("<Window.InputBindings>", content);
        Assert.Contains("Key=\"F1\"", content);  // Help
        Assert.Contains("Key=\"F5\"", content);  // Refresh
        Assert.Contains("Key=\"S\" Modifiers=\"Control\"", content);  // Settings
        Assert.Contains("Key=\"Q\" Modifiers=\"Control\"", content);  // Quit
        Assert.Contains("Key=\"Escape\"", content);  // Cancel
    }

    // Phase 26: AutomationId Tests
    [Theory]
    [InlineData("DashboardButton")]
    [InlineData("ComputersButton")]
    [InlineData("UpdatesButton")]
    [InlineData("DiagnosticsButton")]
    [InlineData("SetHttpsButton")]
    [InlineData("SettingsButton")]
    [InlineData("HelpButton")]
    public void MainWindow_NavigationButtons_ShouldHaveAutomationId(string automationId)
    {
        var content = File.ReadAllText(GetXamlPath("MainWindow.xaml"));
        // Check for the full AutomationProperties.AutomationId syntax
        Assert.Contains($"AutomationProperties.AutomationId=\"{automationId}\"", content);
    }

    [Theory]
    [InlineData("SettingsDialog.xaml")]
    [InlineData("SyncProfileDialog.xaml")]
    [InlineData("TransferDialog.xaml")]
    [InlineData("ScheduleTaskDialog.xaml")]
    [InlineData("InstallDialog.xaml")]
    [InlineData("HttpsDialog.xaml")]
    public void Dialogs_ShouldUseCenterOwner(string dialogFile)
    {
        var content = File.ReadAllText(GetXamlPath(dialogFile));

        // Phase 26: All dialogs should center on owner window for cohesive UX
        Assert.Contains("WindowStartupLocation=\"CenterOwner\"", content);
    }

    [Fact]
    public void MainWindow_ShouldHaveAutomationIdOnInteractiveElements()
    {
        var content = File.ReadAllText(GetXamlPath("MainWindow.xaml"));

        // Verify key interactive elements have AutomationId for accessibility testing
        Assert.Contains("AutomationProperties.AutomationId=\"ExportComputersButton\"", content);
        Assert.Contains("AutomationProperties.AutomationId=\"ExportUpdatesButton\"", content);
        Assert.Contains("AutomationProperties.AutomationId=\"ClearComputerFiltersButton\"", content);
        Assert.Contains("AutomationProperties.AutomationId=\"ClearUpdateFiltersButton\"", content);
    }

    [Fact]
    public void SettingsDialog_ShouldHaveAutomationIdsOnInputs()
    {
        var content = File.ReadAllText(GetXamlPath("SettingsDialog.xaml"));

        // Verify settings inputs have AutomationId for UI automation
        Assert.Contains("AutomationProperties.AutomationId=\"DefaultSyncProfileComboBox\"", content);
        Assert.Contains("AutomationProperties.AutomationId=\"LogLevelComboBox\"", content);
        Assert.Contains("AutomationProperties.AutomationId=\"LogRetentionDaysTextBox\"", content);
    }

    [Fact]
    public void MainWindow_UpdatesList_ShouldUse_Themed_VirtualizedListView_Style()
    {
        var content = File.ReadAllText(GetXamlPath("MainWindow.xaml"));

        Assert.Contains("x:Name=\"UpdatesListView\"", content);
        Assert.Contains("BasedOn=\"{StaticResource VirtualizedListView}\"", content);
        Assert.Contains("ItemContainerStyle=\"{StaticResource DarkListViewItem}\"", content);
    }

    [Fact]
    public void MainWindow_ClientTools_ShouldUse_Compact_Hostname_Input()
    {
        var xaml = XDocument.Load(GetXamlPath("MainWindow.xaml"));
        XNamespace autoNs = "clr-namespace:System.Windows.Automation;assembly=PresentationCore";

        var hostnameBox = xaml.Descendants()
            .FirstOrDefault(e => e.Name.LocalName == "TextBox" &&
                                 (string?)e.Attribute(autoNs + "AutomationProperties.AutomationId") == "ClientHostnameTextBox");

        Assert.NotNull(hostnameBox);
        Assert.Equal("320", (string?)hostnameBox.Attribute("Width"));
        Assert.Equal("Left", (string?)hostnameBox.Attribute("HorizontalAlignment"));
    }

    [Fact]
    public void MainWindow_ErrorCodeLookup_ShouldUse_Searchable_ComboBox()
    {
        var content = File.ReadAllText(GetXamlPath("MainWindow.xaml"));

        Assert.Contains("AutomationProperties.AutomationId=\"ErrorCodeInputTextBox\"", content);
        Assert.Contains("IsTextSearchEnabled=\"True\"", content);
    }

    [Fact]
    public void MainWindow_ShouldNotNestDataTriggerInsideDataTrigger()
    {
        var xaml = XDocument.Load(GetXamlPath("MainWindow.xaml"));

        var hasNestedDataTrigger = xaml.Descendants()
            .Where(e => e.Name.LocalName == "DataTrigger")
            .Any(e => e.Descendants().Any(child => child.Name.LocalName == "DataTrigger"));

        Assert.False(hasNestedDataTrigger,
            "MainWindow.xaml must not nest DataTrigger inside DataTrigger. Use MultiDataTrigger or default setters.");
    }

    [Fact]
    public void MainWindow_ScriptGenerator_ShouldHaveComboBoxAndGenerateButton()
    {
        var content = File.ReadAllText(GetXamlPath("MainWindow.xaml"));

        Assert.Contains("AutomationProperties.AutomationId=\"ScriptOperationComboBox\"", content);
        Assert.Contains("Content=\"Generate Script\"", content);
    }

    [Fact]
    public void MainWindow_ErrorCodeLookupComboBox_ShouldUseDropdownOnlyWithoutLookupButton()
    {
        var content = File.ReadAllText(GetXamlPath("MainWindow.xaml"));

        Assert.Contains("AutomationProperties.AutomationId=\"ErrorCodeInputTextBox\"", content);
        Assert.Contains("Text=\"{Binding ErrorCodeInput, UpdateSourceTrigger=PropertyChanged}\"", content);
        Assert.Contains("IsEditable=\"True\"", content);
        Assert.DoesNotContain("Text=\"Error Code:\"", content);
        Assert.Contains("Width=\"260\"", content);
        Assert.DoesNotContain("Content=\"Lookup\"", content);
    }

    [Fact]
    public void MainWindow_PrimaryClientInputs_ShouldUseUniformInputStyles()
    {
        var content = File.ReadAllText(GetXamlPath("MainWindow.xaml"));

        Assert.Contains("AutomationProperties.AutomationId=\"ClientHostnameTextBox\"", content);
        Assert.Contains("Style=\"{StaticResource UniformInputTextBox}\"", content);
        Assert.Contains("AutomationProperties.AutomationId=\"ScriptOperationComboBox\"", content);
        Assert.Contains("Style=\"{StaticResource UniformInputComboBox}\"", content);
    }

    [Fact]
    public void MainWindow_AboutPanel_ShouldUsePackUriForGaLogo()
    {
        var content = File.ReadAllText(GetXamlPath("MainWindow.xaml"));

        Assert.Contains("pack://application:,,,/general_atomics_logo_big.ico", content);
    }

    [Fact]
    public void SettingsDialog_AdvancedSection_ShouldDefineEnoughRowsForAllControls()
    {
        var xaml = XDocument.Load(GetXamlPath("SettingsDialog.xaml"));

        var advancedGroup = xaml.Descendants()
            .FirstOrDefault(e => e.Name.LocalName == "GroupBox" &&
                                 string.Equals((string?)e.Attribute("Header"), "Advanced", StringComparison.Ordinal));

        Assert.NotNull(advancedGroup);

        var rowDefinitions = advancedGroup!
            .Descendants()
            .Where(e => e.Name.LocalName == "RowDefinition")
            .Count();

        Assert.True(rowDefinitions >= 6,
            "Advanced settings grid must define at least 6 rows (timeouts, retries, 3 fallbacks, reset button).");
    }
}
