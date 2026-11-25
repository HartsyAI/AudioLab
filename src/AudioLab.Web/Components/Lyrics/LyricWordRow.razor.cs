using System.Globalization;
using AudioLab.Core.Model;
using AudioLab.Web.Interop;
using AudioLab.Web.Services;
using AudioLab.Web.Session;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace AudioLab.Web.Components.Lyrics;

/// <summary>One word of the lyrics: its text and times, the bar it covers on the source's scale, and the drag and keys that move it.</summary>
/// <remarks>It listens to the cursor and draws itself again only when it becomes or stops being the word under the playhead.</remarks>
public sealed partial class LyricWordRow : IDisposable
{
    private const double Threshold = 3;

    private const double MinBarWidth = 2;

    private LyricWordPart? _part;

    private double _originX;

    private double _originStart;

    private double _originEnd;

    private double _previewStart;

    private double _previewEnd;

    private bool _active;

    private bool _current;

    private string? _held;

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

    /// <summary>Raised with the edit a drag or a key asks for.</summary>
    [Parameter]
    public EventCallback<LyricWordEdit> OnCommit { get; set; }

    /// <summary>Says a sentence to the user, such as why an edit was held.</summary>
    [Parameter]
    public EventCallback<string> OnSay { get; set; }

    [Inject]
    private LyricsCursor Cursor { get; set; } = default!;

    [Inject]
    private IPointerTracker Pointer { get; set; } = default!;

    private LyricWord Word => Lyrics.Words[Index];

    private double ShownStart => _active ? _previewStart : Word.Start;

    private double ShownEnd => _active ? _previewEnd : Word.End;

    private double BarLeft => Geometry.X(ShownStart);

    private double BarWidth => Math.Max(Geometry.Width(ShownEnd - ShownStart), MinBarWidth);

    private static string Hint => "Ctrl+Arrow moves the word; Ctrl+Shift+Arrow moves its start; Ctrl+Up and Ctrl+Down move its end.";

    private string Description => $"Word {Index + 1}, {Word.Word}, from {Clock(ShownStart)} to {Clock(ShownEnd)} seconds"
        + (Word.Unsure == true ? ", unsure" : string.Empty);

    /// <inheritdoc />
    public void Dispose()
    {
        Cursor.Changed -= OnCursorChanged;
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

    private async Task PressAsync(MouseEventArgs press, LyricWordPart part)
    {
        if (press.Button != 0 || _part is not null)
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
        _originStart = Word.Start;
        _originEnd = Word.End;
        _previewStart = Word.Start;
        _previewEnd = Word.End;
        Pointer.Begin(Move, Release, Cancel);
    }

    // The drag follows the pointer. The preview is Core's rule for the word, so what is drawn is what the server will keep.
    private void Move(double x, double y)
    {
        if (_part is not { } part)
        {
            return;
        }
        double dx = x - _originX;
        if (!_active && Math.Abs(dx) < Threshold)
        {
            return;
        }
        _active = true;
        Targets(part, dx / Geometry.PixelsPerSecond, out double? start, out double? end);
        LyricWord kept = LyricsTiming.Preview(Lyrics, Index, start, end);
        _previewStart = kept.Start;
        _previewEnd = kept.End;
        _held = LyricsTiming.HeldText(Lyrics, Index, start, end);
        _ = InvokeAsync(StateHasChanged);
    }

    // A release that moved the word sends one edit; a press that never travelled, or a release where the word began, sends nothing.
    private void Release(double x, double y)
    {
        if (_part is not { } part)
        {
            return;
        }
        LyricWordEdit? edit = _active && Differs(part, _previewStart, _previewEnd) ? EditOf(part, _previewStart, _previewEnd) : null;
        string? held = _held;
        Reset();
        _ = InvokeAsync(StateHasChanged);
        if (held is not null)
        {
            _ = OnSay.InvokeAsync(held);
        }
        if (edit is { } made)
        {
            _ = OnCommit.InvokeAsync(made);
        }
    }

    private void Cancel()
    {
        Reset();
        _ = InvokeAsync(StateHasChanged);
    }

    private void Reset()
    {
        _part = null;
        _active = false;
        _held = null;
    }

    // A key moves the word by one step through the same preview and edit as a drag.
    private async Task KeyAsync(KeyboardEventArgs key)
    {
        if (key.Repeat || Step(key) is not { } move)
        {
            return;
        }
        if (!Editable)
        {
            await OnSay.InvokeAsync(Reason ?? "The project cannot be edited now.");
            return;
        }
        _originStart = Word.Start;
        _originEnd = Word.End;
        Targets(move.Part, move.Seconds, out double? wantedStart, out double? wantedEnd);
        LyricWord kept = LyricsTiming.Preview(Lyrics, Index, wantedStart, wantedEnd);
        if (!Differs(move.Part, kept.Start, kept.End))
        {
            return;
        }
        if (LyricsTiming.HeldText(Lyrics, Index, wantedStart, wantedEnd) is { } held)
        {
            await OnSay.InvokeAsync(held);
        }
        await OnCommit.InvokeAsync(EditOf(move.Part, kept.Start, kept.End));
    }

    // The wanted start and end of a part moved by seconds from where the word was when the move began.
    private void Targets(LyricWordPart part, double seconds, out double? start, out double? end)
    {
        (start, end) = part switch
        {
            LyricWordPart.Body => ((double?)(_originStart + seconds), (double?)(_originEnd + seconds)),
            LyricWordPart.Start => ((double?)(_originStart + seconds), null),
            _ => (null, (double?)(_originEnd + seconds)),
        };
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

    private LyricWordEdit EditOf(LyricWordPart part, double start, double end) => part switch
    {
        LyricWordPart.Body => new LyricWordEdit(Index, start, end),
        LyricWordPart.Start => new LyricWordEdit(Index, start, null),
        _ => new LyricWordEdit(Index, null, end),
    };
}
