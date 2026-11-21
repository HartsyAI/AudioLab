using AudioLab.Core.Model;

namespace AudioLab.Core.Migration;

/// <summary>A media file a migrated project refers to: what the store needs to create its owned record.</summary>
/// <param name="BlobKey">The version 3 key of the audio, as it was in the project and its saved audio files.</param>
/// <param name="Media">The id the migrated project uses instead. One per distinct key.</param>
/// <param name="ContentType">The media type of the file.</param>
/// <param name="Length">The size of the file in bytes.</param>
/// <param name="InlineBase64">The audio as base64 text when an old project carried it in its JSON; else null.</param>
public readonly record struct MediaReference(
    string BlobKey,
    MediaId Media,
    string ContentType,
    long Length,
    string? InlineBase64 = null);
