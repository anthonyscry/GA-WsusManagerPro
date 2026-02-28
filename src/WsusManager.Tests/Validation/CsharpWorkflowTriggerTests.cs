namespace WsusManager.Tests.Validation;

public class CsharpWorkflowTriggerTests
{
    [Fact]
    public void BuildCsharpWorkflow_FileExists()
    {
        var workflowPath = GetWorkflowPath();

        Assert.True(File.Exists(workflowPath),
            $"Expected C# workflow file at '{workflowPath}' to ensure src/** changes trigger CI checks.");
    }

    [Fact]
    public void BuildCsharpWorkflow_HasSrcPathTriggersForPushAndPullRequest()
    {
        var workflowPath = GetWorkflowPath();
        Assert.True(File.Exists(workflowPath), $"Workflow file missing: {workflowPath}");

        var content = File.ReadAllText(workflowPath);

        Assert.Contains("push:", content, StringComparison.Ordinal);
        Assert.Contains("pull_request:", content, StringComparison.Ordinal);
        Assert.Contains("'src/**'", content, StringComparison.Ordinal);
    }

    [Fact]
    public void BuildCsharpWorkflow_RunsVerifyAndPublishChecks()
    {
        var workflowPath = GetWorkflowPath();
        Assert.True(File.Exists(workflowPath), $"Workflow file missing: {workflowPath}");

        var content = File.ReadAllText(workflowPath);

        Assert.Contains("./Scripts/verify.ps1", content, StringComparison.Ordinal);
        Assert.Contains("dotnet publish src/WsusManager.App/WsusManager.App.csproj", content, StringComparison.Ordinal);
    }

    private static string GetWorkflowPath()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);

        while (dir is not null)
        {
            var solutionPath = Path.Combine(dir.FullName, "src", "WsusManager.sln");
            if (File.Exists(solutionPath))
            {
                return Path.Combine(dir.FullName, ".github", "workflows", "build-csharp.yml");
            }

            dir = dir.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate repository root from test base directory.");
    }
}
