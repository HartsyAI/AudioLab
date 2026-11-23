using System.Collections.Concurrent;
using AudioLab.Core.Model;

namespace AudioLab.Server.Media;

/// <summary>Rebuilds the peak file of a media file from its bytes, one rebuild at a time for each file however many ask.</summary>
/// <remarks>Peaks are derived data. The bytes are checked as strictly as at upload, so a file whose bytes fail is reported, not hidden.</remarks>
public sealed class PeakRebuilder
{
    private readonly ILogger<PeakRebuilder> _logger;
    private readonly Func<Task>? _beforeBuild;
    private readonly ConcurrentDictionary<string, Lazy<Task>> _running = new(StringComparer.Ordinal);
    private int _rebuilds;

    /// <summary>Creates the rebuilder; <paramref name="beforeBuild"/> runs before each rebuild starts, which a test uses to hold one open.</summary>
    public PeakRebuilder(ILogger<PeakRebuilder> logger, Func<Task>? beforeBuild = null)
    {
        _logger = logger;
        _beforeBuild = beforeBuild;
    }

    /// <summary>How many rebuilds have run to the end since the server started.</summary>
    public int Rebuilds => Volatile.Read(ref _rebuilds);

    /// <summary>The rebuild of the peaks at <paramref name="peaksPath"/> from the bytes at <paramref name="dataPath"/>: the one already running, or a new one.</summary>
    /// <returns>A task that ends when the peak file is in place; it fails with <see cref="MediaRejectedException"/> when the bytes are not an accepted WAV.</returns>
    public Task EnsureAsync(string dataPath, string peaksPath)
    {
        Lazy<Task> run = _running.GetOrAdd(peaksPath, _ => new Lazy<Task>(() => Task.Run(() => RunAsync(dataPath, peaksPath))));
        Task task = run.Value;
        _ = task.ContinueWith(_ => _running.TryRemove(new KeyValuePair<string, Lazy<Task>>(peaksPath, run)), TaskScheduler.Default);
        return task;
    }

    private async Task RunAsync(string dataPath, string peaksPath)
    {
        if (_beforeBuild is not null)
        {
            await _beforeBuild();
        }
        PeakData peaks;
        await using (FileStream data = MediaStore.OpenForReading(dataPath))
        {
            WavFormat format = WavReader.Read(data);
            await WavReader.RequireFiniteAsync(data, format, CancellationToken.None);
            peaks = await PeakComputer.ComputeAsync(data, format, CancellationToken.None);
        }
        string temp = $"{peaksPath}.{Guid.NewGuid():N}.tmp";
        try
        {
            FileStreamOptions options = new()
            {
                Mode = FileMode.CreateNew,
                Access = FileAccess.Write,
                Share = FileShare.None,
                Options = FileOptions.Asynchronous,
            };
            await using (FileStream file = new(temp, options))
            {
                await file.WriteAsync(PeakFile.Write(peaks));
                file.Flush(flushToDisk: true);
            }
            File.Move(temp, peaksPath, overwrite: true);
        }
        finally
        {
            File.Delete(temp);
        }
        Interlocked.Increment(ref _rebuilds);
        MediaLog.PeaksRebuilt(_logger, Path.GetFileName(peaksPath));
    }
}
