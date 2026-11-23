using AudioLab.Core.Model;
using AudioLab.Web.Services;

namespace AudioLab.Web.Screens;

/// <summary>What the session screen can do to MIDI clips; every edit answers with what it came to, so a caller can select the clip it made.</summary>
public sealed class MidiActions
{
    private const string NotAvailable = "Not available.";

    /// <summary>Adds a clip of one bar to an instrument track, at a start in seconds or after its last clip when null.</summary>
    public Func<TrackId, double?, Task<EditResult>> AddClip { get; init; } = (_, _) => Task.FromResult(EditResult.Refused(NotAvailable));

    /// <summary>Moves a clip's end by whole bars, negative to shorten.</summary>
    public Func<ClipId, int, Task<EditResult>> StepLength { get; init; } = (_, _) => Task.FromResult(EditResult.Refused(NotAvailable));

    /// <summary>The actions of an editor; <paramref name="show"/> tells the user what a refusal said.</summary>
    public static MidiActions For(MidiEditor editor, Func<Task<EditResult>, Task<EditResult>> show)
    {
        ArgumentNullException.ThrowIfNull(editor);
        ArgumentNullException.ThrowIfNull(show);
        return new MidiActions
        {
            AddClip = (track, start) => show(editor.AddClipAsync(track, start)),
            StepLength = (clip, bars) => show(editor.StepLengthAsync(clip, bars)),
        };
    }
}
