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
    private const int DefaultMaxTranscriptFiles = 200;

    private readonly string _transcriptDirectory;
    private readonly int _maxTranscriptFiles;
    private readonly object _sync = new();
    private long _operationCounter;
    private StreamWriter? _writer;

    public OperationTranscriptService()
        : this(DefaultTranscriptDirectory, DefaultMaxTranscriptFiles)
    {
    }

    public OperationTranscriptService(string transcriptDirectory)
        : this(transcriptDirectory, DefaultMaxTranscriptFiles)
    {
    }

    public OperationTranscriptService(string transcriptDirectory, int maxTranscriptFiles)
    {
        _transcriptDirectory = transcriptDirectory;
        _maxTranscriptFiles = maxTranscriptFiles < 1 ? 1 : maxTranscriptFiles;
    }

    public string? CurrentTranscriptPath { get; private set; }

    public string StartOperation(string operationName)
    {
        var safeOperationName = SanitizeFileName(operationName);
        var timestamp = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff");
        var sequence = Interlocked.Increment(ref _operationCounter);
        var fileName = $"{timestamp}-{sequence:D6}-{safeOperationName}.log";

        lock (_sync)
        {
            EndOperationInternal();

            Directory.CreateDirectory(_transcriptDirectory);
            var transcriptPath = Path.Combine(_transcriptDirectory, fileName);

            _writer = new StreamWriter(transcriptPath, append: false, new UTF8Encoding(false));
            CurrentTranscriptPath = transcriptPath;
            _writer.Flush();

            CleanupOldTranscripts();

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

    private void CleanupOldTranscripts()
    {
        try
        {
            var transcriptFiles = Directory
                .GetFiles(_transcriptDirectory, "*.log", SearchOption.TopDirectoryOnly)
                .OrderByDescending(Path.GetFileName, StringComparer.Ordinal)
                .ToArray();

            foreach (var oldFile in transcriptFiles.Skip(_maxTranscriptFiles))
            {
                File.Delete(oldFile);
            }
        }
        catch
        {
            // Retention cleanup is best-effort and must not block transcript creation.
        }
    }
}
