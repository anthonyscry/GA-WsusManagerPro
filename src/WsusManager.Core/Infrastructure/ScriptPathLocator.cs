namespace WsusManager.Core.Infrastructure;

/// <summary>
/// Resolves fallback script file locations for packaged and portable deployments.
/// Search order keeps app-base precedence and bounded app-base parents.
/// </summary>
internal static class ScriptPathLocator
{
    private const string ScriptsFolderName = "Scripts";

    internal static string[] GetScriptSearchPaths(string scriptName, int maxParentDepth = 6)
    {
        if (string.IsNullOrWhiteSpace(scriptName))
            return [];

        var paths = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var roots = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var root in GetSearchRoots())
        {
            if (!roots.Add(root))
                continue;

            AddScriptCandidates(paths, seen, root, scriptName);

            var parent = new DirectoryInfo(root).Parent;
            for (var i = 0; i < maxParentDepth && parent is not null; i++)
            {
                AddScriptCandidates(paths, seen, parent.FullName, scriptName);
                parent = parent.Parent;
            }
        }

        return [.. paths];
    }

    internal static string? LocateScript(string scriptName, int maxParentDepth = 6)
    {
        foreach (var path in GetScriptSearchPaths(scriptName, maxParentDepth))
        {
            if (File.Exists(path))
                return path;
        }

        return null;
    }

    private static void AddScriptCandidates(
        ICollection<string> paths,
        ISet<string> seen,
        string baseDirectory,
        string scriptName)
    {
        AddPath(paths, seen, Path.Combine(baseDirectory, ScriptsFolderName, scriptName));
        AddPath(paths, seen, Path.Combine(baseDirectory, scriptName));
    }

    private static void AddPath(ICollection<string> paths, ISet<string> seen, string path)
    {
        var fullPath = Path.GetFullPath(path);
        if (seen.Add(fullPath))
            paths.Add(fullPath);
    }

    private static IEnumerable<string> GetSearchRoots()
    {
        yield return Path.GetFullPath(AppContext.BaseDirectory);

        var processPath = Environment.ProcessPath;
        if (string.IsNullOrWhiteSpace(processPath))
            yield break;

        var processDirectory = Path.GetDirectoryName(processPath);
        if (string.IsNullOrWhiteSpace(processDirectory))
            yield break;

        yield return Path.GetFullPath(processDirectory);
    }
}
