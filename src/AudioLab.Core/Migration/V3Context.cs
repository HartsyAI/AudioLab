using System.Globalization;
using AudioLab.Core.Model;

namespace AudioLab.Core.Migration;

/// <summary>What one migration run accumulates: the new ids, the audio keys met so far and the warnings.</summary>
internal sealed class V3Context(IIdService ids, V3Audio audio, List<string> warnings)
{
    private readonly Dictionary<string, MediaId> _media = new(StringComparer.Ordinal);

    /// <summary>The service every new id comes from.</summary>
    public IIdService Ids { get; } = ids;

    /// <summary>The audio the project can reach.</summary>
    public V3Audio Audio { get; } = audio;

    /// <summary>Everything changed or dropped so far, one sentence each.</summary>
    public List<string> Warnings { get; } = warnings;

    /// <summary>The media files met so far, in the order they were first used.</summary>
    public List<MediaReference> MediaReferences { get; } = [];

    /// <summary>The new id of each version 3 track, by its old id.</summary>
    public Dictionary<string, TrackId> TrackIds { get; } = new(StringComparer.Ordinal);

    /// <summary>The new id of each version 3 audio clip, by its old id; the first clip with an id keeps it.</summary>
    public Dictionary<string, ClipId> ClipIds { get; } = new(StringComparer.Ordinal);

    /// <summary>An error that names the value it is about.</summary>
    public static ProjectFormatException Fail(string path, string message) => new($"{path}: {message}");

    /// <summary>A number as it appears in a warning: the same in every culture.</summary>
    public static string Num(double value) => value.ToString(CultureInfo.InvariantCulture);

    /// <summary>The items of a list, which must not hold a null; a missing list is empty.</summary>
    public static List<T> Items<T>(List<T>? items, string path)
        where T : class
    {
        if (items is null)
        {
            return [];
        }
        for (int i = 0; i < items.Count; i++)
        {
            if ((object?)items[i] is null)
            {
                throw Fail($"{path}[{i}]", "is null.");
            }
        }
        return items;
    }

    /// <summary>Records a change or a drop.</summary>
    public void Warn(string message) => Warnings.Add(message);

    /// <summary>Whether an audio file is listed for the key.</summary>
    /// <exception cref="ProjectFormatException">The project says nothing about which audio exists.</exception>
    public bool IsAvailable(string blobKey, string path)
    {
        if (!Audio.HasList)
        {
            throw Fail(path, "refers to audio, but the file lists none; supply the audio files it was saved with.");
        }
        return Audio.Find(blobKey) is not null;
    }

    /// <summary>The media id for an audio key, or null when no file is listed for it; one key shares one id.</summary>
    /// <exception cref="ProjectFormatException">The project says nothing about which audio exists.</exception>
    public MediaId? MediaFor(string blobKey, string path)
    {
        if (_media.TryGetValue(blobKey, out MediaId media))
        {
            return media;
        }
        if (!IsAvailable(blobKey, path))
        {
            return null;
        }
        V3AudioFile file = Audio.Find(blobKey)!;
        media = Ids.Next<MediaId>();
        _media[blobKey] = media;
        MediaReferences.Add(new MediaReference(blobKey, media, file.ContentType, file.Length, file.InlineBase64));
        return media;
    }

    /// <summary>A colour to keep: the value when it is well formed, else the fallback, with a warning.</summary>
    public string? Color(string? value, string? fallback, string where)
    {
        if (string.IsNullOrEmpty(value))
        {
            return fallback;
        }
        if (ProjectColor.IsValid(value))
        {
            return value;
        }
        string shown = value.Length > 32 ? value[..32] + "..." : value;
        string outcome = fallback is null ? "it was dropped" : "the default was used";
        Warn($"{where} was '{shown}', which is not #rgb, #rrggbb or #rrggbbaa; {outcome}.");
        return fallback;
    }
}
