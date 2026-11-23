using AudioLab.Core.Model;

namespace AudioLab.Audio;

/// <summary>Raised when preparing a render finds clips whose media the source cannot supply.</summary>
public sealed class MissingMediaException : Exception
{
    /// <summary>Names the media that is missing.</summary>
    public MissingMediaException(IReadOnlyList<MediaId> missing)
        : base($"Media not available: {string.Join(", ", missing.Take(8))}{(missing.Count > 8 ? ", ..." : string.Empty)}.")
    {
        Missing = missing;
    }

    /// <summary>The ids the source could not supply, once each.</summary>
    public IReadOnlyList<MediaId> Missing { get; }
}
