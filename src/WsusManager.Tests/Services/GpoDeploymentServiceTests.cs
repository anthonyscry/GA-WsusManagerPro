using Moq;
using WsusManager.Core.Logging;
using WsusManager.Core.Services;

namespace WsusManager.Tests.Services;

/// <summary>
/// Tests for GpoDeploymentService: source directory validation,
/// destination directory creation, instruction text content, and file copying.
/// </summary>
public class GpoDeploymentServiceTests
{
    private readonly Mock<ILogService> _mockLog = new();

    private GpoDeploymentService CreateService(string? destinationPath = null) =>
        destinationPath is null
            ? new(_mockLog.Object)
            : new(_mockLog.Object, destinationPath);

    [Fact]
    public async Task Deploy_Returns_Failure_When_Source_Not_Found()
    {
        var service = CreateService();
        var moved = new List<(string Original, string Backup)>();

        try
        {
            foreach (var path in service.GetSearchPaths())
            {
                if (!Directory.Exists(path))
                {
                    continue;
                }

                var backup = $"{path}.bak-{Guid.NewGuid():N}";
                Directory.Move(path, backup);
                moved.Add((path, backup));
            }

            var result = await service.DeployGpoFilesAsync("WSUS01");

            Assert.False(result.Success);
            Assert.Contains("DomainController", result.Message);
        }
        finally
        {
            foreach (var entry in moved)
            {
                if (Directory.Exists(entry.Backup) && !Directory.Exists(entry.Original))
                {
                    Directory.Move(entry.Backup, entry.Original);
                }
            }
        }
    }

    [Fact]
    public async Task Deploy_Returns_Invalid_Host_Failure_Before_Source_Check()
    {
        var service = CreateService();

        var result = await service.DeployGpoFilesAsync("bad host name");

        Assert.False(result.Success);
        Assert.DoesNotContain("DomainController", result.Message, StringComparison.Ordinal);
        Assert.Contains("valid host or IP", result.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Deploy_Writes_Wrapper_File_And_Uses_Https_Port_In_Instruction_Text()
    {
        var destinationDir = Path.Combine(Path.GetTempPath(), $"wsus-gpo-dest-{Guid.NewGuid():N}");
        var service = CreateService(destinationDir);
        var sourceDir = Path.Combine(AppContext.BaseDirectory, GpoDeploymentService.SourceDirectoryName);
        var sourceDirExisted = Directory.Exists(sourceDir);
        var markerFileName = $"marker-{Guid.NewGuid():N}.txt";
        var markerSourcePath = Path.Combine(sourceDir, markerFileName);
        var markerDestinationPath = Path.Combine(destinationDir, markerFileName);
        var wrapperPath = Path.Combine(destinationDir, "Run-WsusGpoSetup.ps1");
        var progress = new CaptureProgress();

        try
        {
            Directory.CreateDirectory(sourceDir);
            File.WriteAllText(markerSourcePath, "marker");

            var result = await service.DeployGpoFilesAsync(
                "WSUS01",
                httpPort: 8530,
                httpsPort: 7443,
                progress: progress,
                ct: CancellationToken.None);

            Assert.True(result.Success);
            Assert.NotNull(result.Data);
            Assert.Contains("https://WSUS01:7443", result.Data!, StringComparison.Ordinal);
            Assert.True(File.Exists(markerDestinationPath));
            Assert.True(File.Exists(wrapperPath));
            Assert.Contains(progress.Messages, m =>
                m.Contains("Generated wrapper script:", StringComparison.Ordinal) &&
                m.Contains("Run-WsusGpoSetup.ps1", StringComparison.Ordinal));
        }
        finally
        {
            if (File.Exists(markerSourcePath)) File.Delete(markerSourcePath);
            if (Directory.Exists(destinationDir)) Directory.Delete(destinationDir, true);
            if (!sourceDirExisted && Directory.Exists(sourceDir) && !Directory.EnumerateFileSystemEntries(sourceDir).Any())
            {
                Directory.Delete(sourceDir);
            }
        }
    }

    [Fact]
    public void BuildInstructionText_Contains_Required_Steps()
    {
        var text = GpoDeploymentService.BuildInstructionText("WSUS01", 8530);

        Assert.Contains(@"C:\WSUS\WSUS GPO", text);
        Assert.Contains("Domain Controller", text);
        Assert.Contains("Set-WsusGroupPolicy.ps1", text);
        Assert.Contains("http://WSUS01:8530", text);
        Assert.Contains("gpupdate /force", text);
        Assert.Contains("wuauclt /detectnow", text);
    }

    [Fact]
    public void BuildInstructionText_Contains_Wrapper_And_Http_Https_Examples()
    {
        var text = GpoDeploymentService.BuildInstructionText("WSUS01", 8530);

        Assert.Contains("Run-WsusGpoSetup.ps1", text);
        Assert.Contains("http://WSUS01:8530", text);
        Assert.Contains("https://WSUS01:8531", text);
        Assert.Contains("-UseHttps", text);
    }

    [Fact]
    public void BuildWrapperScriptText_Contains_Required_Template_Content()
    {
        var script = GpoDeploymentService.BuildWrapperScriptText("WSUS01", 8530, 8531);

        Assert.Contains("param(", script);
        Assert.Contains("[switch]$UseHttps", script);
        Assert.Contains("Win32_ComputerSystem", script);
        Assert.Contains("Set-WsusGroupPolicy.ps1", script);
        Assert.Contains("-WsusServerUrl", script);
        Assert.Contains("-BackupPath", script);
        Assert.Contains("[string]$BackupPath = (Join-Path -Path $PSScriptRoot -ChildPath \"WSUS GPOs\")", script);
        Assert.Contains("'http://WSUS01:8530'", script);
        Assert.Contains("'https://WSUS01:8531'", script);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("bad host name")]
    [InlineData("wsus/01")]
    public void BuildWrapperScriptText_Throws_For_Invalid_Hostname(string hostname)
    {
        Assert.Throws<ArgumentException>(() => GpoDeploymentService.BuildWrapperScriptText(hostname, 8530, 8531));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(8530)]
    [InlineData(65535)]
    public void NormalizePort_Returns_Candidate_For_Valid_Port(int candidate)
    {
        var normalized = GpoDeploymentService.NormalizePort(candidate, 8530);

        Assert.Equal(candidate, normalized);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(0)]
    [InlineData(65536)]
    public void NormalizePort_Returns_Fallback_For_Invalid_Port(int candidate)
    {
        const int fallback = 8530;

        var normalized = GpoDeploymentService.NormalizePort(candidate, fallback);

        Assert.Equal(fallback, normalized);
    }

    [Fact]
    public void CopyDirectory_Copies_Files_Recursively()
    {
        var sourceDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        var destDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());

        try
        {
            // Create source structure
            Directory.CreateDirectory(Path.Combine(sourceDir, "SubDir"));
            File.WriteAllText(Path.Combine(sourceDir, "file1.txt"), "content1");
            File.WriteAllText(Path.Combine(sourceDir, "SubDir", "file2.txt"), "content2");

            var count = GpoDeploymentService.CopyDirectory(sourceDir, destDir, null, CancellationToken.None);

            Assert.Equal(2, count);
            Assert.True(File.Exists(Path.Combine(destDir, "file1.txt")));
            Assert.True(File.Exists(Path.Combine(destDir, "SubDir", "file2.txt")));
            Assert.Equal("content1", File.ReadAllText(Path.Combine(destDir, "file1.txt")));
            Assert.Equal("content2", File.ReadAllText(Path.Combine(destDir, "SubDir", "file2.txt")));
        }
        finally
        {
            if (Directory.Exists(sourceDir)) Directory.Delete(sourceDir, true);
            if (Directory.Exists(destDir)) Directory.Delete(destDir, true);
        }
    }

    [Fact]
    public void CopyDirectory_Overwrites_Existing_Files()
    {
        var sourceDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        var destDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());

        try
        {
            Directory.CreateDirectory(sourceDir);
            Directory.CreateDirectory(destDir);
            File.WriteAllText(Path.Combine(sourceDir, "file.txt"), "new content");
            File.WriteAllText(Path.Combine(destDir, "file.txt"), "old content");

            GpoDeploymentService.CopyDirectory(sourceDir, destDir, null, CancellationToken.None);

            Assert.Equal("new content", File.ReadAllText(Path.Combine(destDir, "file.txt")));
        }
        finally
        {
            if (Directory.Exists(sourceDir)) Directory.Delete(sourceDir, true);
            if (Directory.Exists(destDir)) Directory.Delete(destDir, true);
        }
    }

    [Fact]
    public void SourceDirectoryName_Is_DomainController()
    {
        Assert.Equal("DomainController", GpoDeploymentService.SourceDirectoryName);
    }

    [Fact]
    public void DefaultDestination_Is_Correct()
    {
        Assert.Equal(@"C:\WSUS\WSUS GPO", GpoDeploymentService.DefaultDestination);
    }

    [Fact]
    public void GetSearchPaths_Returns_Two_Paths()
    {
        var service = CreateService();
        var paths = service.GetSearchPaths();

        Assert.Equal(2, paths.Length);
        Assert.All(paths, p => Assert.Contains(GpoDeploymentService.SourceDirectoryName, p));
    }

    [Fact]
    public void LocateSourceDirectory_Returns_Null_When_Directory_Missing()
    {
        var service = CreateService();
        var moved = new List<(string Original, string Backup)>();

        try
        {
            foreach (var path in service.GetSearchPaths())
            {
                if (!Directory.Exists(path))
                {
                    continue;
                }

                var backup = $"{path}.bak-{Guid.NewGuid():N}";
                Directory.Move(path, backup);
                moved.Add((path, backup));
            }

            var result = service.LocateSourceDirectory();

            Assert.Null(result);
        }
        finally
        {
            foreach (var entry in moved)
            {
                if (Directory.Exists(entry.Backup) && !Directory.Exists(entry.Original))
                {
                    Directory.Move(entry.Backup, entry.Original);
                }
            }
        }
    }

    [Fact]
    public void CopyDirectory_Returns_Zero_For_Empty_Source()
    {
        var sourceDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        var destDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());

        try
        {
            Directory.CreateDirectory(sourceDir);

            var count = GpoDeploymentService.CopyDirectory(sourceDir, destDir, null, CancellationToken.None);

            Assert.Equal(0, count);
        }
        finally
        {
            if (Directory.Exists(sourceDir)) Directory.Delete(sourceDir, true);
            if (Directory.Exists(destDir)) Directory.Delete(destDir, true);
        }
    }

    [Fact]
    public void CopyDirectory_Reports_Progress_Periodically()
    {
        var sourceDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        var destDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());

        try
        {
            Directory.CreateDirectory(sourceDir);
            File.WriteAllText(Path.Combine(sourceDir, "file1.txt"), "content1");
            File.WriteAllText(Path.Combine(sourceDir, "file2.txt"), "content2");

            var progress = new CaptureProgress();

            var count = GpoDeploymentService.CopyDirectory(sourceDir, destDir, progress, CancellationToken.None);

            Assert.Equal(2, count);
            Assert.NotEmpty(progress.Messages);
            Assert.Contains(progress.Messages, message => message.Contains("Copied:", StringComparison.Ordinal));
        }
        finally
        {
            if (Directory.Exists(sourceDir)) Directory.Delete(sourceDir, true);
            if (Directory.Exists(destDir)) Directory.Delete(destDir, true);
        }
    }

    [Fact]
    public void CopyDirectory_Throws_When_Cancellation_Requested_During_Copy()
    {
        var sourceDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        var destDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());

        try
        {
            Directory.CreateDirectory(sourceDir);
            File.WriteAllText(Path.Combine(sourceDir, "file1.txt"), "content1");
            File.WriteAllText(Path.Combine(sourceDir, "file2.txt"), "content2");
            File.WriteAllText(Path.Combine(sourceDir, "file3.txt"), "content3");

            using var cts = new CancellationTokenSource();
            var progress = new CancelOnFirstProgress(cts);

            Assert.Throws<OperationCanceledException>(() =>
                GpoDeploymentService.CopyDirectory(sourceDir, destDir, progress, cts.Token));
        }
        finally
        {
            if (Directory.Exists(sourceDir)) Directory.Delete(sourceDir, true);
            if (Directory.Exists(destDir)) Directory.Delete(destDir, true);
        }
    }

    private sealed class CaptureProgress : IProgress<string>
    {
        public List<string> Messages { get; } = [];

        public void Report(string value)
        {
            Messages.Add(value);
        }
    }

    private sealed class CancelOnFirstProgress(CancellationTokenSource cts) : IProgress<string>
    {
        private int _reportCount;

        public void Report(string value)
        {
            if (Interlocked.Increment(ref _reportCount) == 1)
            {
                cts.Cancel();
            }
        }
    }
}
