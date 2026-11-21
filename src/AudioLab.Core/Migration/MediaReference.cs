using AudioLab.Core.Model;

namespace AudioLab.Core.Migration;

/// <summary>A media file a migrated project refers to, with the key the version 3 project used for its audio.</summary>
/// <param name="BlobKey">The version 3 key of the audio, as it was in the project and its saved audio files.</param>
/// <param name="Media">The id the migrated project uses instead. One per distinct key.</param>
public readonly record struct MediaReference(string BlobKey, MediaId Media);
