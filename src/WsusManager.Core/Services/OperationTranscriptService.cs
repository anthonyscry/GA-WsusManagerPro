using System.Text;
using WsusManager.Core.Services.Interfaces;

namespace WsusManager.Core.Services;

/// <summary>
/// File-based operation transcript writer.
/// Creates one transcript file per operation under the transcript directory.
/// </summary>
public sealed class OperationTranscriptService : IOperationTranscriptService, IDisposable
{
    private const string DefaultTranscriptDirectory = @"C:\WSUS\Logs\Transcripts";

    private readonly string _transcriptDirectory;
    private readonly object _sync = new();
    private StreamWriter? _writer;

    public OperationTranscriptService()
        : this(DefaultTranscriptDirectory)
    {
    }

    public OperationTranscriptService(string transcriptDirectory)
    {
        _transcriptDirectory = transcriptDirectory;
    }

    public string? CurrentTranscriptPath { get; private set; }

    public string StartOperation(string operationName)
    {
        var safeOperationName = SanitizeFileName(operationName);
        var timestamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
        var fileName = $"{timestamp}-{safeOperationName}.log";

        lock (_sync)
        {
            EndOperationInternal();

            Directory.CreateDirectory(_transcriptDirectory);
            var transcriptPath = Path.Combine(_transcriptDirectory, fileName);

            _writer = new StreamWriter(transcriptPath, append: false, new UTF8Encoding(false));
            CurrentTranscriptPath = transcriptPath;
            _writer.Flush();

            return transcriptPath;
        }
    }

    public void WriteLine(string line)
    {
        lock (_sync)
        {
            if (_writer is null)
            {
                return;
            }

            _writer.WriteLine(line);
            _writer.Flush();
        }
    }

    public void EndOperation()
    {
        lock (_sync)
        {
            EndOperationInternal();
        }
    }

    public void Dispose()
    {
        EndOperation();
    }

    private void EndOperationInternal()
    {
        _writer?.Dispose();
        _writer = null;
        CurrentTranscriptPath = null;
    }

    private static string SanitizeFileName(string operationName)
    {
        if (string.IsNullOrWhiteSpace(operationName))
        {
            return "operation";
        }

        var invalidChars = Path.GetInvalidFileNameChars();
        var chars = operationName
            .Trim()
            .Select(c => invalidChars.Contains(c) ? '-' : c)
            .ToArray();

        var sanitized = new string(chars).Replace(' ', '-');
        return string.IsNullOrWhiteSpace(sanitized) ? "operation" : sanitized;
    }
}
