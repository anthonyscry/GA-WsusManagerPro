namespace WsusManager.Tests.Integration;

public sealed class AppPackagingTests
{
    [Fact]
    public void AppProject_ShouldInclude_SetWsusHttpsScript_ForOutputAndPublish()
    {
        var projectPath = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "../../../../WsusManager.App/WsusManager.App.csproj"));

        Assert.True(File.Exists(projectPath), $"Expected app project at {projectPath}");

        var projectXml = File.ReadAllText(projectPath);

        Assert.Contains("Set-WsusHttps.ps1", projectXml, StringComparison.Ordinal);
    }
}
