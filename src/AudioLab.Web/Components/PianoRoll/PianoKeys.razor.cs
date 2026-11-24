using AudioLab.Core.Rules;
using AudioLab.Web.Session.Roll;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace AudioLab.Web.Components.PianoRoll;

/// <summary>The keys at the left of the piano roll, one row for each pitch, the highest at the top; a key is heard by pointer or keyboard.</summary>
/// <remarks>
/// The keys are one tab stop. Up and Down move the pitch that has the cursor (the pitch of the next note typed), and Enter or Space plays it for as
/// long as the key is held. The reason nothing can be heard, when that is so, is text the keys are described by, so a screen reader says it.
/// </remarks>
public sealed partial class PianoKeys
{
    private const string ReasonId = "roll-keys-reason";

    private const string KeysLabel = "Piano keys. Up and Down choose the pitch of the next note; Enter or Space plays it while held.";

    private (string?, int)? _shown;

    /// <summary>The scale; only the row height matters here.</summary>
    [Parameter]
    public RollGeometry Geometry { get; set; }

    /// <summary>Why a key cannot be heard now, which the keys show as their title and are described by; null when it can.</summary>
    [Parameter]
    public string? Reason { get; set; }

    /// <summary>The pitch that has the cursor, which is the pitch of the next note typed from the keyboard.</summary>
    [Parameter]
    public int Pitch { get; set; }

    /// <summary>Raised with the press when the primary button goes down on the keys.</summary>
    [Parameter]
    public EventCallback<PointerEventArgs> OnPress { get; set; }

    /// <summary>Raised when a key of the keyboard goes down while the keys have the focus.</summary>
    [Parameter]
    public EventCallback<KeyboardEventArgs> OnKeyDown { get; set; }

    /// <summary>Raised when a key of the keyboard goes up while the keys have the focus.</summary>
    [Parameter]
    public EventCallback<KeyboardEventArgs> OnKeyUp { get; set; }

    /// <summary>Raised when the keys lose the focus, which lets go of a note held from the keyboard.</summary>
    [Parameter]
    public EventCallback<FocusEventArgs> OnBlur { get; set; }

    /// <inheritdoc />
    protected override bool ShouldRender()
    {
        (string?, int) now = (Reason, Pitch);
        bool draw = _shown != now;
        _shown = now;
        return draw;
    }

    private static string Name(int pitch) => NoteRules.Name(pitch);

    private static string Label(int pitch) => pitch % 12 == 0 ? Name(pitch) : string.Empty;
}
