import re

with open('src/WsusManager.App/Views/MainWindow.xaml', 'r') as f:
    content = f.read()

pattern = r'''                                    <Grid Margin="0,0,0,8">
                                        <Grid\.ColumnDefinitions>
                                            <ColumnDefinition Width="Auto"/>
                                            <ColumnDefinition Width="8"/>
                                            <ColumnDefinition Width="Auto"/>
                                        </Grid\.ColumnDefinitions>
                                        <ComboBox Grid\.Column="0"
                                                  Width="260"
                                                  Style="\{StaticResource UniformInputComboBox\}"
                                                  automation:AutomationProperties\.AutomationId="ScriptOperationComboBox"
                                                  ItemsSource="\{Binding ScriptOperations\}"
                                                  SelectedItem="\{Binding SelectedScriptOperation\}"/>
                                        <Button Grid\.Column="2" Content="Generate Script" Style="\{StaticResource BtnSec\}"
                                                automation:AutomationProperties\.AutomationId="GenerateScriptButton"
                                                Padding="16,8"
                                                Command="\{Binding GenerateScriptCommand\}"
                                                ToolTip="Generate a PowerShell script file for the selected operation"/>
                                    </Grid>'''

replacement = '''                                    <Grid Margin="0,0,0,8">
                                        <Grid.ColumnDefinitions>
                                            <ColumnDefinition Width="Auto"/>
                                            <ColumnDefinition Width="Auto"/>
                                        </Grid.ColumnDefinitions>
                                        <ComboBox Grid.Column="0"
                                                  Margin="0,0,8,0"
                                                  Width="260"
                                                  Style="{StaticResource UniformInputComboBox}"
                                                  automation:AutomationProperties.AutomationId="ScriptOperationComboBox"
                                                  ItemsSource="{Binding ScriptOperations}"
                                                  SelectedItem="{Binding SelectedScriptOperation}"/>
                                        <Button Grid.Column="1" Content="Generate Script" Style="{StaticResource BtnSec}"
                                                automation:AutomationProperties.AutomationId="GenerateScriptButton"
                                                Padding="16,8"
                                                Command="{Binding GenerateScriptCommand}"
                                                ToolTip="Generate a PowerShell script file for the selected operation"/>
                                    </Grid>'''

content_new = re.sub(pattern, replacement, content, count=1)

if content_new != content:
    with open('src/WsusManager.App/Views/MainWindow.xaml', 'w') as f:
        f.write(content_new)
    print("Replaced!")
else:
    print("Not replaced, pattern not matched.")
