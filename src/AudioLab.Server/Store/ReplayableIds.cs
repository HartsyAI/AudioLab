using AudioLab.Core.Model;

namespace AudioLab.Server.Store;

/// <summary>An id service that can record the ids a handler asks for and hand the same ones to a second run of it.</summary>
/// <remarks>Not thread safe: a session uses it under its own lock.</remarks>
public sealed class ReplayableIds(IIdService inner) : IIdService
{
    private List<string>? _recording;
    private Queue<string>? _replay;

    /// <summary>Starts recording the ids handed out.</summary>
    public void Record() => _recording = [];

    /// <summary>Stops recording and returns the ids handed out since <see cref="Record"/>.</summary>
    public IReadOnlyList<string> StopRecording()
    {
        List<string> recorded = _recording ?? [];
        _recording = null;
        return recorded;
    }

    /// <summary>Makes the next run hand out <paramref name="ids"/> in order, while they last, then fresh ones.</summary>
    public void Replay(IReadOnlyList<string> ids) => _replay = new Queue<string>(ids);

    /// <summary>Goes back to handing out fresh ids.</summary>
    public void EndReplay() => _replay = null;

    /// <inheritdoc />
    public TId Next<TId>()
        where TId : struct, IAllocatedId<TId>
    {
        if (_replay is { Count: > 0 } && _replay.Peek().StartsWith(TId.Prefix + "_", StringComparison.Ordinal))
        {
            return TId.From(_replay.Dequeue());
        }
        TId id = inner.Next<TId>();
        _recording?.Add(id.Value);
        return id;
    }
}
