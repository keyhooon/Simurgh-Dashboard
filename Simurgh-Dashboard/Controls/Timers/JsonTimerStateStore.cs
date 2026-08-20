using System.IO;
using System.Text.Json;

namespace SimurghDashboard.Controls.Timers;

public sealed class JsonTimerStateStore : ITimerStateStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly string _rootDirectory;

    public JsonTimerStateStore(string rootDirectory)
    {
        if (string.IsNullOrWhiteSpace(rootDirectory))
        {
            throw new ArgumentException("Root directory is required.", nameof(rootDirectory));
        }

        _rootDirectory = rootDirectory;
        Directory.CreateDirectory(_rootDirectory);
    }

    public async Task<TimerSnapshot?> LoadAsync(
        string timerId,
        CancellationToken cancellationToken = default)
    {
        var path = GetFilePath(timerId);
        if (!File.Exists(path))
        {
            return null;
        }

        await using var stream = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 4096,
            useAsync: true);

        return await JsonSerializer.DeserializeAsync<TimerSnapshot>(
            stream,
            SerializerOptions,
            cancellationToken);
    }

    public async Task SaveAsync(
        string timerId,
        TimerSnapshot snapshot,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        var path = GetFilePath(timerId);
        var directory = Path.GetDirectoryName(path)
                        ?? throw new InvalidOperationException("Unable to resolve timer state directory.");

        Directory.CreateDirectory(directory);

        var tempPath = path + ".tmp";
        var backupPath = path + ".bak";

        await using (var stream = new FileStream(
                         tempPath,
                         FileMode.Create,
                         FileAccess.Write,
                         FileShare.None,
                         bufferSize: 4096,
                         useAsync: true))
        {
            await JsonSerializer.SerializeAsync(stream, snapshot, SerializerOptions, cancellationToken);
            await stream.FlushAsync(cancellationToken);
        }

        if (File.Exists(path))
        {
            File.Replace(tempPath, path, backupPath, ignoreMetadataErrors: true);

            if (File.Exists(backupPath))
            {
                File.Delete(backupPath);
            }
        }
        else
        {
            File.Move(tempPath, path);
        }
    }

    public Task DeleteAsync(string timerId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var path = GetFilePath(timerId);
        if (File.Exists(path))
        {
            File.Delete(path);
        }

        var backupPath = path + ".bak";
        if (File.Exists(backupPath))
        {
            File.Delete(backupPath);
        }

        return Task.CompletedTask;
    }

    private string GetFilePath(string timerId)
    {
        if (string.IsNullOrWhiteSpace(timerId))
        {
            throw new ArgumentException("Timer id is required.", nameof(timerId));
        }

        var safeName = SanitizeFileName(timerId);
        return Path.Combine(_rootDirectory, safeName + ".json");
    }

    private static string SanitizeFileName(string value)
    {
        var invalidChars = Path.GetInvalidFileNameChars();
        var buffer = value.Trim().ToCharArray();

        for (var index = 0; index < buffer.Length; index++)
        {
            if (Array.IndexOf(invalidChars, buffer[index]) >= 0)
            {
                buffer[index] = '_';
            }
        }

        var result = new string(buffer);
        return string.IsNullOrWhiteSpace(result) ? "timer" : result;
    }
}