import re

with open('src/WsusManager.Tests/KeyboardNavigationTests.cs', 'r') as f:
    content = f.read()

pattern = r'    \[Fact\]\s*public void MainWindow_ScriptGeneratorComboBox_ShouldUseCompactWidth\(\)\s*\{\s*var content = File\.ReadAllText\(GetXamlPath\("MainWindow\.xaml"\)\);\s*Assert\.Contains\("AutomationProperties\.AutomationId=\\"ScriptOperationComboBox\\"", content\);\s*Assert\.Contains\("Width=\\"260\\"", content\);\s*Assert\.DoesNotContain\("Text=\\"Operation:\\"", content\);\s*Assert\.Contains\("AutomationProperties\.AutomationId=\\"GenerateScriptButton\\"", content\);\s*Assert\.Contains\("Grid\.Column=\\"2\\" Content=\\"Generate Script\\"", content\);\s*\}'

new_test = '''    [Fact]
    public void MainWindow_ScriptGenerator_ShouldRemoveOperationLabel_AndUseSingleRow()
    {
        var content = File.ReadAllText(GetXamlPath("MainWindow.xaml"));

        Assert.DoesNotContain("Text=\\"Operation:\\"", content);
        Assert.Contains("AutomationProperties.AutomationId=\\"ScriptOperationComboBox\\"", content);
        Assert.Contains("Width=\\"260\\"", content);
        Assert.Contains("Grid.Column=\\"1\\" Content=\\"Generate Script\\"", content);
    }'''

content = re.sub(pattern, new_test, content, count=1)

with open('src/WsusManager.Tests/KeyboardNavigationTests.cs', 'w') as f:
    f.write(content)

print("Replaced!")
