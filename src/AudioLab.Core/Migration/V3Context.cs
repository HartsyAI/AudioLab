using AudioLab.Core.Model;

namespace AudioLab.Core.Migration;

/// <summary>What one migration run accumulates: the new ids, the audio keys met so far and the warnings.</summary>
internal sealed class V3Context(IIdService ids)
{
    private readonly Dictionary<string, MediaId> _media = new(StringComparer.Ordinal);

    public IIdService Ids { get; } = ids;

    public List<string> Warnings { get; } = [];

    public List<MediaReference> MediaReferences { get; } = [];

    /// <summary>The new id of each version 3 track, by its old id.</summary>
    public Dictionary<string, TrackId> TrackIds { get; } = new(StringComparer.Ordinal);

    /// <summary>The new id of each version 3 audio clip, by its old id; the first clip with an id keeps it.</summary>
    public Dictionary<string, ClipId> ClipIds { get; } = new(StringComparer.Ordinal);

    public void Warn(string message) => Warnings.Add(message);

    /// <summary>The media id for an audio key; every use of one key shares one id.</summary>
    public MediaId MediaFor(string blobKey)
    {
        if (!_media.TryGetValue(blobKey, out MediaId media))
        {
            media = Ids.Next<MediaId>();
            _media[blobKey] = media;
            MediaReferences.Add(new MediaReference(blobKey, media));
        }
        return media;
    }

    public static ProjectFormatException Fail(string path, string message) => new($"{path}: {message}");
}
