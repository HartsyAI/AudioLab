using AudioLab.Core.Model;
using AudioLab.Web.Services;

namespace AudioLab.Web.Screens;

/// <summary>What the session screen can do to a clip's lyrics; every edit answers with what it came to, and a refusal is shown as words.</summary>
public sealed class LyricsActions
{
    private const string NotAvailable = "Not available.";

    /// <summary>Sets the text of a clip's lyrics.</summary>
    public Func<ClipId, string, Task<EditResult>> SetText { get; init; } = (_, _) => Task.FromResult(EditResult.Refused(NotAvailable));

    /// <summary>Sets the start, the end or both of one word, in seconds of the clip's source audio.</summary>
    public Func<ClipId, int, double?, double?, Task<EditResult>> SetWordTime { get; init; } =
        (_, _, _, _) => Task.FromResult(EditResult.Refused(NotAvailable));

    /// <summary>Moves every word of a clip's lyrics by seconds, negative for earlier.</summary>
    public Func<ClipId, double, Task<EditResult>> Shift { get; init; } = (_, _) => Task.FromResult(EditResult.Refused(NotAvailable));

    /// <summary>Drops the word times of a clip's lyrics.</summary>
    public Func<ClipId, Task<EditResult>> ClearTiming { get; init; } = _ => Task.FromResult(EditResult.Refused(NotAvailable));

    /// <summary>The actions of an editor; <paramref name="show"/> tells the user what a refusal said.</summary>
    public static LyricsActions For(LyricsEditor editor, Func<Task<EditResult>, Task<EditResult>> show)
    {
        ArgumentNullException.ThrowIfNull(editor);
        ArgumentNullException.ThrowIfNull(show);
        return new LyricsActions
        {
            SetText = (clip, text) => show(editor.SetTextAsync(clip, text)),
            SetWordTime = (clip, word, start, end) => show(editor.SetWordTimeAsync(clip, word, start, end)),
            Shift = (clip, seconds) => show(editor.ShiftAsync(clip, seconds)),
            ClearTiming = clip => show(editor.ClearTimingAsync(clip)),
        };
    }
}
