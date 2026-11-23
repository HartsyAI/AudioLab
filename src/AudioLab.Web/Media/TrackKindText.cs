using AudioLab.Core.Model;

namespace AudioLab.Web.Media;

/// <summary>Names a kind of track in a sentence, with the right article.</summary>
internal static class TrackKindText
{
    /// <summary>The kind as a phrase, such as "an instrument track" or "a bus track".</summary>
    public static string Phrase(TrackKind kind)
    {
        string name = kind.ToString().ToLowerInvariant();
        return $"{(name[0] is 'a' or 'e' or 'i' or 'o' or 'u' ? "an" : "a")} {name} track";
    }
}
