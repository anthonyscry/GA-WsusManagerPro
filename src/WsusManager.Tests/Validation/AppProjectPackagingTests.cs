using System.Xml.Linq;

namespace WsusManager.Tests.Validation;

public class AppProjectPackagingTests
{
    [Fact]
    public void WsusManagerAppProject_IncludesScriptsContentItem()
    {
        var doc = XDocument.Load(GetAppProjectPath());

        var scriptsItem = FindContentInclude(doc, "..\\..\\Scripts\\");

        Assert.NotNull(scriptsItem);
    }

    [Fact]
    public void WsusManagerAppProject_IncludesModulesContentItem()
    {
        var doc = XDocument.Load(GetAppProjectPath());

        var modulesItem = FindContentInclude(doc, "..\\..\\Modules\\");

        Assert.NotNull(modulesItem);
    }

    [Fact]
    public void WsusManagerAppProject_ScriptsContentCopiesToOutputAndPublish()
    {
        var doc = XDocument.Load(GetAppProjectPath());
        var scriptsItem = FindContentInclude(doc, "..\\..\\Scripts\\");

        Assert.NotNull(scriptsItem);
        Assert.Equal("PreserveNewest", GetChildValue(scriptsItem!, "CopyToOutputDirectory"));
        Assert.Equal("PreserveNewest", GetChildValue(scriptsItem!, "CopyToPublishDirectory"));
        Assert.True(bool.TryParse(GetChildValue(scriptsItem!, "ExcludeFromSingleFile"), out var excluded) && excluded,
            "Scripts content must stay external so fallback path resolution can find files next to the EXE.");
    }

    [Fact]
    public void WsusManagerAppProject_ModulesContentCopiesToOutputAndPublish()
    {
        var doc = XDocument.Load(GetAppProjectPath());
        var modulesItem = FindContentInclude(doc, "..\\..\\Modules\\");

        Assert.NotNull(modulesItem);
        Assert.Equal("PreserveNewest", GetChildValue(modulesItem!, "CopyToOutputDirectory"));
        Assert.Equal("PreserveNewest", GetChildValue(modulesItem!, "CopyToPublishDirectory"));
        Assert.True(bool.TryParse(GetChildValue(modulesItem!, "ExcludeFromSingleFile"), out var excluded) && excluded,
            "Modules content must stay external so PowerShell script imports work at runtime.");
    }

    private static string GetAppProjectPath()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);

        while (dir is not null)
        {
            var solutionPath = Path.Combine(dir.FullName, "src", "WsusManager.sln");
            if (File.Exists(solutionPath))
            {
                return Path.Combine(dir.FullName, "src", "WsusManager.App", "WsusManager.App.csproj");
            }

            dir = dir.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate repository root from test base directory.");
    }

    private static XElement? FindContentInclude(XDocument doc, string includePrefix)
    {
        return doc.Descendants()
            .FirstOrDefault(element =>
                element.Name.LocalName == "Content" &&
                ((string?)element.Attribute("Include"))?.StartsWith(includePrefix, StringComparison.OrdinalIgnoreCase) == true);
    }

    private static string? GetChildValue(XElement element, string childName)
    {
        return element.Elements().FirstOrDefault(child => child.Name.LocalName == childName)?.Value;
    }
}
