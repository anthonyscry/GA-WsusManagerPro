using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using WsusManager.Core.Services.Interfaces;

namespace WsusManager.Core.Services;

/// <summary>
/// Creates and appends transcript files for each long-running operation.
/// File path format is deterministic per operation id + operation name.
/// </summary>
public sealed class OperationTranscriptService : IOperationTranscriptService
{
    private const string TranscriptSubfolder = "Transcripts";
    private const string DefaultOperationName = "Operation";
    private readonly string _transcriptsDirectory;

    public OperationTranscriptService(string logDirectory)
    {
        _transcriptsDirectory = Path.Combine(logDirectory, TranscriptSubfolder);
    }

    public async Task WriteLineAsync(
        Guid operationId,
        string operationName,
        string line,
        CancellationToken ct)
    {
        var path = GetTranscriptPath(operationId, operationName);
        Directory.CreateDirectory(_transcriptsDirectory);

        await File.AppendAllTextAsync(
            path,
            line + Environment.NewLine,
            ct).ConfigureAwait(false);
    }

    public string GetTranscriptPath(Guid operationId, string operationName)
    {
        var safeName = SanitizeFileName(string.IsNullOrWhiteSpace(operationName)
            ? DefaultOperationName
            : operationName);
        var fileName = $"{safeName}_{operationId:D}.log";
        return Path.Combine(_transcriptsDirectory, fileName);
    }

    private static string SanitizeFileName(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return DefaultOperationName;
        }

        var invalidChars = Regex.Escape(new string(Path.GetInvalidFileNameChars().Distinct().ToArray()));
        var replaced = Regex.Replace(value, $"[{invalidChars}]+", "_", RegexOptions.Compiled);
        var sanitized = replaced.Trim('_');
        return string.IsNullOrWhiteSpace(sanitized) ? DefaultOperationName : sanitized;
    }
}
