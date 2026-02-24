using WsusManager.Core.Services;

namespace WsusManager.Tests.Services;

public class OperationTranscriptServiceTests : IDisposable
{
    private readonly string _tempDir;

    public OperationTranscriptServiceTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"WsusManagerTranscriptTests_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_tempDir, recursive: true);
        }
        catch
        {
            // Ignore cleanup errors in test environments.
        }
    }

    [Fact]
    public async Task WriteLineAsync_CreatesOperationTranscriptFile()
    {
        var service = new OperationTranscriptService(_tempDir);
        var opId = Guid.NewGuid();

        await service.WriteLineAsync(opId, "Diagnostics", "[Step 1/3] Start", CancellationToken.None);
        var file = service.GetTranscriptPath(opId, "Diagnostics");

        Assert.True(File.Exists(file));
        Assert.Contains("[Step 1/3] Start", File.ReadAllText(file), StringComparison.Ordinal);
    }

    [Fact]
    public async Task WriteLineAsync_ParallelWrites_ProduceAllLines()
    {
        var service = new OperationTranscriptService(_tempDir);
        var opId = Guid.NewGuid();

        var tasks = Enumerable.Range(1, 50)
            .Select(i => service.WriteLineAsync(opId, "Diagnostics", $"line-{i}", CancellationToken.None));

        await Task.WhenAll(tasks).ConfigureAwait(false);

        var file = service.GetTranscriptPath(opId, "Diagnostics");
        Assert.True(File.Exists(file));

        var content = File.ReadAllText(file);
        for (var i = 1; i <= 50; i++)
        {
            Assert.Contains($"line-{i}", content, StringComparison.Ordinal);
        }
    }
}
