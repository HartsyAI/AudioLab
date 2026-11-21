using System.Buffers.Text;
using AudioLab.Core.Model;

namespace AudioLab.Core.Migration;

/// <summary>The audio files a version 3 project can reach: its audio list, its inline blobs and what the caller supplies.</summary>
internal sealed class V3Audio
{
    private const string DefaultType = "audio/wav";

    private const string UnknownType = "application/octet-stream";

    private readonly Dictionary<string, V3AudioFile> _files = new(StringComparer.Ordinal);

    private V3Audio(bool hasList) => HasList = hasList;

    /// <summary>Whether anything says which audio exists; without it no clip can be tied to a file.</summary>
    public bool HasList { get; }

    /// <summary>The audio the project lists, carries inline or the caller supplies; the project's own list wins.</summary>
    public static V3Audio From(
        V3Project project,
        IReadOnlyDictionary<string, MediaFileInfo>? supplied,
        List<string> warnings)
    {
        V3Audio audio = new(project.Audio is not null || project.Blobs is not null || supplied is not null);
        foreach (KeyValuePair<string, MediaFileInfo> file in supplied ?? new Dictionary<string, MediaFileInfo>())
        {
            string path = $"supplied audio '{file.Key}'";
            audio.Add(file.Key, file.Value.ContentType, file.Value.Length, null, path, warnings);
        }
        foreach (KeyValuePair<string, V3InlineBlob> blob in project.Blobs ?? [])
        {
            string path = $"blobs.{blob.Key}";
            V3InlineBlob value = blob.Value ?? throw V3Context.Fail(path, "is null.");
            if (value.B64 is null || !Base64.IsValid(value.B64, out int length))
            {
                throw V3Context.Fail($"{path}.b64", "is missing or is not base64, so the audio cannot be kept.");
            }
            audio.Add(blob.Key, value.Type, length, value.B64, path, warnings);
        }
        foreach (KeyValuePair<string, V3AudioInfo> info in project.Audio ?? [])
        {
            V3AudioInfo value = info.Value ?? throw V3Context.Fail($"audio.{info.Key}", "is null.");
            audio.Add(info.Key, value.Type, value.Size ?? 0, null, $"audio.{info.Key}", warnings);
        }
        return audio;
    }

    /// <summary>The file for an audio key, or null when none is listed for it or the listed one has no bytes.</summary>
    public V3AudioFile? Find(string blobKey) => _files.GetValueOrDefault(blobKey);

    private void Add(string key, string? type, long length, string? inline, string path, List<string> warnings)
    {
        if (length < 1)
        {
            _files.Remove(key);
            warnings.Add($"{path} has no bytes, so it counts as missing.");
            return;
        }
        _files[key] = new V3AudioFile(ContentType(type, path, warnings), length, inline);
    }

    // A browser blob type may carry parameters such as codecs; the store keeps the bare media type.
    private static string ContentType(string? type, string path, List<string> warnings)
    {
        if (string.IsNullOrWhiteSpace(type))
        {
            return DefaultType;
        }
        string bare = type.Split(';')[0].Trim().ToLowerInvariant();
        if (MediaValidator.IsContentType(bare))
        {
            return bare;
        }
        string shown = type.Length > 40 ? type[..40] + "..." : type;
        warnings.Add($"{path} has the type '{shown}', which is not a media type; {UnknownType} was used.");
        return UnknownType;
    }
}
