using System.Globalization;
using AudioLab.Core.Model;
using AudioLab.Web.Interop;
using AudioLab.Web.Services;
using AudioLab.Web.Session;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace AudioLab.Web.Components.Lyrics;

/// <summary>One word of the lyrics: its text and times, the bar it covers on the source's scale, and the drag and keys that move it.</summary>
/// <remarks>It redraws alone when it becomes or stops being the word under the playhead, and it ends its own drag when it goes.</remarks>
public sealed partial class LyricWordRow : IDisposable
{
    private const double Threshold = 3;

    private const double MinBarWidth = 2;

    private LyricWordPart? _part;

    private double _originX;

    private LyricWord _began;

    private double _previewStart;

    private double _previewEnd;

    private bool _active;

    private bool _current;

    private bool _disposed;

    private string? _held;

    /// <summary>The element id of a word's row.</summary>
    public static string IdOf(int index) => $"lyric-word-{index}";

    /// <summary>The word's place in the lyrics, counted from 0.</summary>
    [Parameter, EditorRequired]
    public int Index { get; set; }

    /// <summary>The lyrics the word is in: Core's rule holds a word between its neighbours, so the preview needs them all.</summary>
    [Parameter, EditorRequired]
    public ClipLyrics Lyrics { get; set; } = null!;

    /// <summary>The scale from source seconds to pixels.</summary>
    [Parameter, EditorRequired]
    public TimelineGeometry Geometry { get; set; }

    /// <summary>The width of the strip the bar sits in, in pixels.</summary>
    [Parameter, EditorRequired]
    public double Width { get; set; }

    /// <summary>Whether the lyrics may be edited now.</summary>
    [Parameter]
    public bool Editable { get; set; }

    /// <summary>Why they may not, which a key press says.</summary>
    [Parameter]
    public string? Reason { get; set; }

    /// <summary>Whether this row is the one the list stops at in the tab order; the others are reached with the arrow keys.</summary>
    [Parameter]
    public bool TabStop { get; set; }

    /// <summary>Raised with the edit a drag or a key asks for.</summary>
    [Parameter]
    public EventCallback<LyricWordEdit> OnCommit { get; set; }

    /// <summary>Says a sentence to the user, such as why an edit was refused.</summary>
    [Parameter]
    public EventCallback<string> OnSay { get; set; }

    /// <summary>Raised when the row takes the focus, with its index.</summary>
    [Parameter]
    public EventCallback<int> OnFocusWord { get; set; }

    /// <summary>Raised when an arrow key asks for the row before or after, with its index.</summary>
    [Parameter]
    public EventCallback<int> OnStep { get; set; }

    [Inject]
    private LyricsCursor Cursor { get; set; } = default!;

    [Inject]
    private IPointerTracker Pointer { get; set; } = default!;

    [Inject]
    private LyricsDrags Drags { get; set; } = default!;

    private LyricWord Word => Lyrics.Words[Index];

    private double ShownStart => _active ? _previewStart : Word.Start;

    private double ShownEnd => _active ? _previewEnd : Word.End;

    private double BarLeft => Geometry.X(ShownStart);

    private double BarWidth => Math.Max(Geometry.Width(ShownEnd - ShownStart), MinBarWidth);

    private string Description => $"Word {Index + 1}, {Word.Word}, from {Clock(ShownStart)} to {Clock(ShownEnd)} seconds"
        + (Word.Unsure == true ? ", unsure" : string.Empty);

    private static string Hint =>
        $"{Shortcuts.LyricsWordEarlier.Display} or {Shortcuts.LyricsWordLater.Display} moves the word. "
        + $"{Shortcuts.LyricsStartEarlier.Display} or {Shortcuts.LyricsStartLater.Display} moves its start. "
        + $"{Shortcuts.LyricsEndEarlier.Display} or {Shortcuts.LyricsEndLater.Display} moves its end.";

    /// <inheritdoc />
    public void Dispose()
    {
        _disposed = true;
        Cursor.Changed -= OnCursorChanged;
        if (_part is not null)
        {
            Pointer.Stop();
            Drags.End(this);
        }
    }

    /// <inheritdoc />
    protected override void OnInitialized()
    {
        Cursor.Changed += OnCursorChanged;
        _current = Cursor.Index == Index;
    }

    private static string Clock(double seconds) => seconds.ToString("0.000", CultureInfo.InvariantCulture);

    // Only the word that becomes or stops being the one under the playhead is drawn again.
    private void OnCursorChanged()
    {
        bool now = Cursor.Index == Index;
        if (now != _current)
        {
            _current = now;
            _ = InvokeAsync(StateHasChanged);
        }
    }

    private Task FocusAsync() => OnFocusWord.InvokeAsync(Index);

    private async Task PressAsync(MouseEventArgs press, LyricWordPart part)
    {
        if (press.Button != 0 || _part is not null || _disposed)
        {
            return;
        }
        if (!Editable)
        {
            await OnSay.InvokeAsync(Reason ?? "The project cannot be edited now.");
            return;
        }
        _part = part;
        _originX = press.ClientX;
        _began = Word;
        _previewStart = Word.Start;
        _previewEnd = Word.End;
        Drags.Begin(this);
        Pointer.Begin(Move, Release, Cancel);
    }

    // The drag follows the pointer. The preview is Core's rule for the word, so what is drawn is what the server will keep.
    private void Move(double x, double y)
    {
        if (_disposed || _part is not { } part)
        {
            return;
        }
        double dx = x - _originX;
        if (!_active && Math.Abs(dx) < Threshold)
        {
            return;
        }
        _active = true;
        double seconds = dx / Geometry.PixelsPerSecond;
        (double? start, double? end) = LyricsTiming.Targets(Lyrics, Index, part, seconds);
        LyricWord kept = LyricsTiming.Preview(Lyrics, Index, start, end);
        _previewStart = kept.Start;
        _previewEnd = kept.End;
        _held = part == LyricWordPart.Body
            ? LyricsTiming.MoveHeldText(Lyrics, Index, seconds)
            : LyricsTiming.HeldText(Lyrics, Index, start, end);
        _ = InvokeAsync(StateHasChanged);
    }

    // A release sends one edit, unless the word has changed since the drag began (another client may have edited the lyrics meanwhile).
    private void Release(double x, double y)
    {
        if (_disposed || _part is not { } part)
        {
            return;
        }
        bool moved = _active && Differs(part, _previewStart, _previewEnd);
        bool same = LyricsTiming.SameWord(Lyrics, Index, _began);
        LyricWordEdit? edit = moved && same ? EditOf(part, _previewStart, _previewEnd, _held) : null;
        Reset();
        _ = InvokeAsync(StateHasChanged);
        if (moved && !same)
        {
            _ = OnSay.InvokeAsync("The word changed while it was being dragged, so the move was not sent.");
        }
        if (edit is { } made)
        {
            _ = OnCommit.InvokeAsync(made);
        }
    }

    private void Cancel()
    {
        if (_disposed)
        {
            return;
        }
        Reset();
        _ = InvokeAsync(StateHasChanged);
    }

    private void Reset()
    {
        _part = null;
        _active = false;
        _held = null;
        Drags.End(this);
    }

    // A key moves the word by one step through the same rule and edit as a drag; plain arrows move the focus to the row before or after.
    private async Task KeyAsync(KeyboardEventArgs key)
    {
        if (key.Repeat)
        {
            return;
        }
        if (Step(key) is { } move)
        {
            await MoveByKeyAsync(move.Part, move.Seconds);
            return;
        }
        if (key.CtrlKey || key.MetaKey || key.ShiftKey || key.AltKey)
        {
            return;
        }
        if (key.Key == "ArrowUp")
        {
            await OnStep.InvokeAsync(Index - 1);
        }
        else if (key.Key == "ArrowDown")
        {
            await OnStep.InvokeAsync(Index + 1);
        }
    }

    private async Task MoveByKeyAsync(LyricWordPart part, double seconds)
    {
        if (!Editable)
        {
            await OnSay.InvokeAsync(Reason ?? "The project cannot be edited now.");
            return;
        }
        (double? start, double? end) = LyricsTiming.Targets(Lyrics, Index, part, seconds);
        LyricWord kept = LyricsTiming.Preview(Lyrics, Index, start, end);
        if (!Differs(part, kept.Start, kept.End))
        {
            return;
        }
        string? held = part == LyricWordPart.Body
            ? LyricsTiming.MoveHeldText(Lyrics, Index, seconds)
            : LyricsTiming.HeldText(Lyrics, Index, start, end);
        await OnCommit.InvokeAsync(EditOf(part, kept.Start, kept.End, held));
    }

    private (LyricWordPart Part, double Seconds)? Step(KeyboardEventArgs key)
    {
        if (Shortcuts.LyricsWordEarlier.Matches(key))
        {
            return (LyricWordPart.Body, -LyricsTiming.Nudge);
        }
        if (Shortcuts.LyricsWordLater.Matches(key))
        {
            return (LyricWordPart.Body, LyricsTiming.Nudge);
        }
        if (Shortcuts.LyricsStartEarlier.Matches(key))
        {
            return (LyricWordPart.Start, -LyricsTiming.Nudge);
        }
        if (Shortcuts.LyricsStartLater.Matches(key))
        {
            return (LyricWordPart.Start, LyricsTiming.Nudge);
        }
        if (Shortcuts.LyricsEndEarlier.Matches(key))
        {
            return (LyricWordPart.End, -LyricsTiming.Nudge);
        }
        return Shortcuts.LyricsEndLater.Matches(key) ? (LyricWordPart.End, LyricsTiming.Nudge) : null;
    }

    // Whether the part the edit moves now differs from the word as it is.
    private bool Differs(LyricWordPart part, double start, double end)
    {
        bool moved = Math.Abs(start - Word.Start) > double.Epsilon;
        bool stretched = Math.Abs(end - Word.End) > double.Epsilon;
        return part switch
        {
            LyricWordPart.Body => moved || stretched,
            LyricWordPart.Start => moved,
            _ => stretched,
        };
    }

    private LyricWordEdit EditOf(LyricWordPart part, double start, double end, string? held) => part switch
    {
        LyricWordPart.Body => new LyricWordEdit(Index, start, end, held),
        LyricWordPart.Start => new LyricWordEdit(Index, start, null, held),
        _ => new LyricWordEdit(Index, null, end, held),
    };
}
