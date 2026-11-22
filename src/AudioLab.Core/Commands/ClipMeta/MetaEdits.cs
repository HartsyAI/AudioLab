using AudioLab.Core.Model;
using AudioLab.Core.Rules;

namespace AudioLab.Core.Commands;

/// <summary>Finding an audio clip and changing its metadata, for the lyrics and the score and stem handlers.</summary>
internal static class MetaEdits
{
    /// <summary>A refusal when the id is not an audio clip of the project: not found, or a MIDI clip, which has no metadata.</summary>
    public static CommandResult? Audio(ProjectDocument document, ClipId id, out AudioClip clip)
    {
        clip = null!;
        if (ClipEdits.Find(document, id) is not { } at)
        {
            return ClipEdits.NoClip(id);
        }
        if (!at.IsAudio)
        {
            return ClipEdits.WrongKind(wantsAudio: true);
        }
        clip = document.Tracks[at.Track].Clips[at.AudioIndex];
        return null;
    }

    /// <summary>The clip with its lyrics replaced, or removed with null.</summary>
    public static AudioClip WithLyrics(AudioClip clip, ClipLyrics? lyrics) => With(clip, meta => meta with { Lyrics = lyrics });

    /// <summary>The clip with its score replaced, or removed with null.</summary>
    public static AudioClip WithScore(AudioClip clip, ScoreMeta? score) => With(clip, meta => meta with { Score = score });

    /// <summary>The clip with its stem link replaced, or removed with null.</summary>
    public static AudioClip WithStem(AudioClip clip, StemOf? stem) => With(clip, meta => meta with { StemOf = stem });

    /// <summary>The document with one audio clip replaced.</summary>
    public static ProjectDocument Replace(ProjectDocument document, AudioClip clip)
    {
        ValueList<Track> tracks =
        [
            .. document.Tracks.Select(track => track.Clips.Any(candidate => candidate.Id == clip.Id)
                ? track with { Clips = [.. track.Clips.Select(candidate => candidate.Id == clip.Id ? clip : candidate)] }
                : track),
        ];
        return document with { Tracks = tracks };
    }

    /// <summary>The document with a clip's lyrics replaced, and those of each vocal stem separated from the clip following them.</summary>
    /// <remarks>
    /// The stem's lyrics are rebased to start at 0 where the clip's visible part starts, or removed with them.
    /// The muted original no longer shows them on the strip.
    /// </remarks>
    public static ProjectDocument SetLyrics(ProjectDocument document, AudioClip clip, ClipLyrics? lyrics)
    {
        ProjectDocument edited = Replace(document, WithLyrics(clip, lyrics));
        ClipLyrics? carried = lyrics is null ? null : LyricsRules.Rebase(lyrics, -clip.Offset);
        AudioClip Follow(AudioClip candidate) => IsVocalStemOf(candidate, clip.Id) ? WithLyrics(candidate, carried) : candidate;
        ValueList<Track> tracks =
        [
            .. edited.Tracks.Select(track => track.Clips.Any(candidate => IsVocalStemOf(candidate, clip.Id))
                ? track with { Clips = [.. track.Clips.Select(Follow)] }
                : track),
        ];
        return edited with { Tracks = tracks };
    }

    private static AudioClip With(AudioClip clip, Func<ClipMeta, ClipMeta> change) => clip with { Meta = Tidy(change(clip.Meta ?? new ClipMeta())) };

    // A clip with no content left in its metadata has none, as the old DAW kept it.
    private static ClipMeta? Tidy(ClipMeta meta)
    {
        return meta.Score is null && meta.Lyrics is null && meta.StemOf is null && meta.Extra.IsEmpty ? null : meta;
    }

    private static bool IsVocalStemOf(AudioClip candidate, ClipId source)
    {
        return candidate.Meta?.StemOf is { ClipId: { } from } link && from == source && LyricsRules.IsVocalStem(link.Stem);
    }
}
