using AudioLab.Core.Commands;
using AudioLab.Core.Model;

namespace AudioLab.Web.Services;

/// <summary>The edits of a clip's lyrics the session screen makes, each one command sent by wire name; the server's reasons come back as words.</summary>
public sealed class LyricsEditor(LiveProject live)
{
    /// <summary>Sets the text of a clip's lyrics; text with no words removes them.</summary>
    public Task<EditResult> SetTextAsync(ClipId clip, string text)
    {
        return EditRunner.RunAsync(live, project => live.SendAsync(new SetLyricsText { Project = project, Clip = clip, Text = text }));
    }

    /// <summary>Sets the start, the end or both of one word, as one gesture and so one undo step.</summary>
    public Task<EditResult> SetWordTimeAsync(ClipId clip, int word, double? start, double? end)
    {
        return EditRunner.RunAsync(live, async project =>
        {
            string gesture = live.NewGesture();
            try
            {
                return await live.SendAsync(
                    new SetLyricsWordTime { Project = project, Clip = clip, Word = word, Start = start, End = end },
                    gesture);
            }
            finally
            {
                await live.EndGestureAsync(gesture);
            }
        });
    }

    /// <summary>Moves every word of a clip's lyrics by a number of seconds, negative for earlier.</summary>
    public Task<EditResult> ShiftAsync(ClipId clip, double seconds)
    {
        return EditRunner.RunAsync(live, project => live.SendAsync(new ShiftLyrics { Project = project, Clip = clip, Seconds = seconds }));
    }

    /// <summary>Gives a clip the lyrics of an LRC file, each line timed from its own start; the server says why a file is refused.</summary>
    public Task<EditResult> ImportLrcAsync(ClipId clip, string lrc)
    {
        ArgumentNullException.ThrowIfNull(lrc);
        return EditRunner.RunAsync(live, project => live.SendAsync(new ImportLyricsLrc { Project = project, Clip = clip, Lrc = lrc }));
    }

    /// <summary>Drops the word times of a clip's lyrics and keeps the text.</summary>
    public Task<EditResult> ClearTimingAsync(ClipId clip)
    {
        return EditRunner.RunAsync(live, project => live.SendAsync(new ClearLyricsTiming { Project = project, Clip = clip }));
    }
}
