using WsusManager.Core.Services;

namespace WsusManager.Tests.Services;

public sealed class OperationTranscriptServiceTests : IDisposable
{
    private readonly string _tempDirectory;

    public OperationTranscriptServiceTests()
    {
        _tempDirectory = Path.Combine(Path.GetTempPath(), $"WsusManagerTranscriptTests_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDirectory);
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_tempDirectory, recursive: true);
        }
        catch
        {
            // Ignore cleanup failures for locked files.
        }
    }

    [Fact]
    public void StartOperation_ShouldCreateTranscriptFile()
    {
        using var service = new OperationTranscriptService(_tempDirectory);

        var transcriptPath = service.StartOperation("Diagnostics");

        Assert.True(File.Exists(transcriptPath));
        Assert.StartsWith(_tempDirectory, transcriptPath, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void WriteLine_ShouldAppendLinesToCurrentTranscript()
    {
        using var service = new OperationTranscriptService(_tempDirectory);
        var transcriptPath = service.StartOperation("Export");

        service.WriteLine("First line");
        service.WriteLine("Second line");
        service.EndOperation();

        var content = File.ReadAllText(transcriptPath);
        Assert.Contains("First line", content);
        Assert.Contains("Second line", content);
    }

    [Fact]
    public void StartOperation_Twice_ShouldCreateSeparateFilesPerOperation()
    {
        using var service = new OperationTranscriptService(_tempDirectory);

        var firstPath = service.StartOperation("Repair Health");
        service.WriteLine("repair line");

        var secondPath = service.StartOperation("Deep Cleanup");
        service.WriteLine("cleanup line");
        service.EndOperation();

        Assert.NotEqual(firstPath, secondPath);

        var firstContent = File.ReadAllText(firstPath);
        var secondContent = File.ReadAllText(secondPath);

        Assert.Contains("repair line", firstContent);
        Assert.DoesNotContain("cleanup line", firstContent);
        Assert.Contains("cleanup line", secondContent);
    }

    [Fact]
    public void StartOperation_SameOperationWithinSameSecond_ShouldStillCreateUniquePath()
    {
        using var service = new OperationTranscriptService(_tempDirectory);

        var firstPath = service.StartOperation("Diagnostics");
        var secondPath = service.StartOperation("Diagnostics");

        Assert.NotEqual(firstPath, secondPath);
        Assert.True(File.Exists(firstPath));
        Assert.True(File.Exists(secondPath));
    }

    [Fact]
    public void StartOperation_ShouldApplyRetentionPolicy()
    {
        using var service = new OperationTranscriptService(_tempDirectory, maxTranscriptFiles: 2);

        service.StartOperation("Operation One");
        service.StartOperation("Operation Two");
        service.StartOperation("Operation Three");
        service.EndOperation();

        var transcripts = Directory.GetFiles(_tempDirectory, "*.log", SearchOption.TopDirectoryOnly);
        Assert.Equal(2, transcripts.Length);
    }
}
