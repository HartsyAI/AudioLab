using System.Globalization;
using AudioLab.Core.Model;
using AudioLab.Core.Rules;
using AudioLab.Web.Components;
using AudioLab.Web.Components.Lyrics;
using AudioLab.Web.Interop;
using AudioLab.Web.Services;
using AudioLab.Web.Session;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;

namespace AudioLab.Web.Screens;

/// <summary>The lyrics tab: the text of the selected audio clip, its words with their times, and the edits that time them.</summary>
public sealed partial class LyricsPanel
{
    private const string PanelId = "lyrics-panel";

    private const string NoTimes = "The words have no times yet.";

    private const long MaxLrcBytes = ProjectLimits.MaxLongText * 4L;

    private string _draft = string.Empty;

    private string _saved = string.Empty;

    private ClipId? _clip;

    private AudioClip? _followedClip;

    private ClipLyrics? _followedLyrics;

    private string? _countedText;

    private int _textWords;

    private int _stop;

    private string _shift = "0";

    private string? _announcement;

    /// <summary>The document.</summary>
    [Parameter, EditorRequired]
    public ProjectDocument Document { get; set; } = null!;

    /// <summary>What the screen can do to the project.</summary>
    [Parameter, EditorRequired]
    public SessionActions Actions { get; set; } = null!;

    [Inject]
    private SessionSelection Selection { get; set; } = default!;

    [Inject]
    private TimelineZoom Zoom { get; set; } = default!;

    [Inject]
    private LyricsCursor Cursor { get; set; } = default!;

    [Inject]
    private ToastService Toasts { get; set; } = default!;

    [Inject]
    private IKeyGuard Guard { get; set; } = default!;

    [Inject]
    private ILyricsModels Models { get; set; } = default!;

    [Inject]
    private IElementFocus Focus { get; set; } = default!;

    private (Track Track, AudioClip Clip)? Found => Selection.Clip is { } clip ? LyricsTiming.Find(Document, clip) : null;

    private ClipLyrics? Lyrics => Found?.Clip.Meta?.Lyrics;

    private IReadOnlyList<LyricWord> Words => Lyrics is { } lyrics ? lyrics.Words : [];

    private bool Timed => LyricsRules.IsTimed(Lyrics);

    private TimelineGeometry Geometry => Zoom.Geometry;

    // The strip covers the whole source of the clip, so a word out past the visible part is still on it.
    private double StripWidth => Found is { } found ? Geometry.Width(Math.Max(found.Clip.Duration, Words.Count > 0 ? Words[^1].End : 0)) : 0;

    private string CountText
    {
        get
        {
            string noun = _textWords == 1 ? "word" : "words";
            return $"{_textWords} {noun}" + (Timed ? string.Empty : ", untimed");
        }
    }

    private string SourceText => Lyrics?.Source switch
    {
        LyricsRules.ManualSource => "Typed by hand",
        LyricsRules.EstimatedSource => "Spread evenly",
        LyricsRules.AlignedSource => "Aligned to the audio",
        LyricsRules.TranscribedSource => "Transcribed",
        LyricsRules.LrcSource => "From an LRC file",
        LyricsRules.GeneratedSource => "Generated",
        _ => string.Empty,
    };

    private string TextCaption => _draft == _saved ? "Saved" : "Unsaved changes";

    // The shift is what the field says; a field that is not a number, or zero, moves nothing.
    private bool TryShift(out double seconds)
    {
        return double.TryParse(_shift, NumberStyles.Float, CultureInfo.InvariantCulture, out seconds) && double.IsFinite(seconds) && seconds != 0;
    }

    private bool ShiftDisabled => !Timed || !Actions.CanEdit || !TryShift(out _);

    private string? ShiftBlock => !Actions.CanEdit ? Actions.Reason
        : !Timed ? NoTimes
        : TryShift(out _) ? null
        : "Enter a number of seconds other than 0.";

    private bool ClearDisabled => !Timed || !Actions.CanEdit;

    private bool EstimateDisabled => !Actions.CanEdit || _draft != _saved || _textWords == 0;

    private string? EstimateBlock => !Actions.CanEdit ? Actions.Reason
        : _draft != _saved ? "Save the text first, so the estimate spreads the words you see."
        : _textWords == 0 ? "Write some lyrics first; there are no words to spread."
        : null;

    private string? ClearBlock => !Actions.CanEdit ? Actions.Reason : Timed ? null : NoTimes;

    private string EmptyMessage => Selection.Clip is { } id && MidiClipPlan.Find(Document, id) is not null
        ? "Lyrics live on audio clips, and this is a MIDI clip. Select an audio clip in the arrangement."
        : "Select an audio clip in the arrangement to see and edit its lyrics.";

    /// <inheritdoc />
    protected override void OnAfterRender(bool firstRender)
    {
        if (firstRender && OperatingSystem.IsBrowser())
        {
            // The word rows take the arrow keys and Ctrl with them; without this the page would also scroll and go back or forward.
            Guard.PreventNavigationKeys(PanelId, "listitem");
        }
    }

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        AudioClip? clip = Found?.Clip;
        ClipLyrics? lyrics = Lyrics;
        string server = lyrics?.Text ?? string.Empty;
        if (clip?.Id != _clip)
        {
            _clip = clip?.Id;
            _announcement = null;
            _draft = server;
            _saved = server;
            _stop = 0;
        }
        else if (server != _saved)
        {
            // The text the user has not changed follows the project; a draft they are writing is kept.
            if (_draft == _saved)
            {
                _draft = server;
            }
            _saved = server;
        }
        if (!ReferenceEquals(server, _countedText))
        {
            _countedText = server;
            _textWords = LyricsRules.WordsOf(server).Count;
        }
        _stop = Math.Min(_stop, Math.Max(0, Words.Count - 1));
        if (!ReferenceEquals(clip, _followedClip) || !ReferenceEquals(lyrics, _followedLyrics))
        {
            _followedClip = clip;
            _followedLyrics = lyrics;
            Cursor.Follow(clip, lyrics);
        }
    }

    private Task SayAsync(string text)
    {
        _announcement = text;
        StateHasChanged();
        return Task.CompletedTask;
    }

    // The tab stop follows the row the user last reached, so the list is one tab stop and the arrows move through it.
    private Task FocusWordAsync(int index)
    {
        _stop = index;
        return Task.CompletedTask;
    }

    private Task StepAsync(int index)
    {
        if (index >= 0 && index < Words.Count)
        {
            _stop = index;
            Focus.Focus(LyricWordRow.IdOf(index));
        }
        return Task.CompletedTask;
    }

    private async Task SaveTextAsync()
    {
        if (Found is not { } found)
        {
            return;
        }
        EditResult result = await Actions.Lyrics.SetText(found.Clip.Id, _draft);
        if (result.Succeeded)
        {
            await SayAsync(LyricsRules.WordsOf(_draft).Count == 0 ? "No words, so the lyrics were removed." : "Lyrics saved.");
        }
    }

    private async Task CommitAsync(LyricWordEdit edit)
    {
        if (Found is not { } found)
        {
            return;
        }
        EditResult result = await Actions.Lyrics.SetWordTime(found.Clip.Id, edit.Word, edit.Start, edit.End);
        if (result.Succeeded)
        {
            await SayAsync(edit.Held ?? $"Word {edit.Word + 1} moved.");
        }
    }

    private async Task ShiftAsync()
    {
        if (Found is not { } found || Lyrics is not { } lyrics)
        {
            return;
        }
        if (!TryShift(out double seconds))
        {
            await SayAsync("Enter the seconds to move the words by, such as 0.5 or -0.5.");
            return;
        }
        // The first word stops at 0, so a move earlier than its start is held there, and the text says so.
        double first = lyrics.Words.Min(word => word.Start);
        if (seconds < -first)
        {
            string firstText = first.ToString("0.000", CultureInfo.InvariantCulture);
            await SayAsync($"The first word stops at 0 seconds, so the words move earlier by {firstText}.");
        }
        EditResult result = await Actions.Lyrics.Shift(found.Clip.Id, seconds);
        if (result.Succeeded && seconds >= -first)
        {
            await SayAsync($"Words moved by {seconds.ToString("0.###", CultureInfo.InvariantCulture)} seconds.");
        }
    }

    private async Task ClearAsync()
    {
        if (Found is not { } found)
        {
            return;
        }
        EditResult result = await Actions.Lyrics.ClearTiming(found.Clip.Id);
        if (result.Succeeded)
        {
            const string Cleared = "Word times cleared. The text is kept.";
            await SayAsync(Cleared);
            Toasts.Show(ToastKind.Info, Cleared, Actions.Undo);
        }
    }

    private async Task ImportChosenAsync(InputFileChangeEventArgs chosen)
    {
        if (Found is not { } found)
        {
            return;
        }
        IBrowserFile file = chosen.File;
        (string? text, string? refusal) = await LrcFile.ReadPickedAsync(file);
        if (refusal is not null || text is null)
        {
            Toasts.Show(ToastKind.Warning, refusal ?? LrcFile.Unreadable);
            return;
        }
        EditResult result = await Actions.Lyrics.ImportLrc(found.Clip.Id, text);
        if (result.Succeeded)
        {
            await SayAsync(string.Create(CultureInfo.InvariantCulture, $"Lyrics imported from {file.Name}."));
        }
    }

    private async Task EstimateAsync()
    {
        if (Found is not { } found)
        {
            return;
        }
        int count = LyricsRules.WordsOf(found.Clip.Meta?.Lyrics?.Text).Count;
        EditResult result = await Actions.Lyrics.Estimate(found.Clip.Id);
        if (result.Succeeded)
        {
            string said = string.Create(
                CultureInfo.InvariantCulture,
                $"Spread {count} words evenly over {ClipRules.VisibleLength(found.Clip):0.0} seconds from {found.Clip.Start:0.0} s.");
            await SayAsync(said);
            Toasts.Show(ToastKind.Info, said, Actions.Undo);
        }
    }

}
