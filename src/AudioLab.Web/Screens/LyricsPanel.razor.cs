using System.Globalization;
using System.Text;
using AudioLab.Core.Model;
using AudioLab.Core.Rules;
using AudioLab.Web.Components;
using AudioLab.Web.Components.Lyrics;
using AudioLab.Web.Services;
using AudioLab.Web.Session;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;

namespace AudioLab.Web.Screens;

/// <summary>The lyrics tab: the text of the selected audio clip, its words with their times, and the edits that time them.</summary>
public sealed partial class LyricsPanel : IDisposable
{
    private const string NoTimes = "The words have no times yet.";

    private string _draft = string.Empty;

    private string _saved = string.Empty;

    private ClipId? _clip;

    private AudioClip? _followedClip;

    private ClipLyrics? _followedLyrics;

    private string _shift = "0";

    private const long MaxLrcBytes = ProjectLimits.MaxLongText * 4L;

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

    private (Track Track, AudioClip Clip)? Found => Selection.Clip is { } clip ? LyricsTiming.Find(Document, clip) : null;

    private ClipLyrics? Lyrics => Found?.Clip.Meta?.Lyrics;

    private IReadOnlyList<LyricWord> Words => Lyrics is { } lyrics ? lyrics.Words : [];

    private bool Timed => LyricsRules.IsTimed(Lyrics);

    private TimelineGeometry Geometry => Zoom.Geometry;

    // The strip covers the whole source of the clip, so a word out past the visible part is still on it.
    private double StripWidth => Found is { } found ? Geometry.Width(Math.Max(found.Clip.Duration, Words.Count > 0 ? Words[^1].End : 0)) : 0;

    private string CountText => Words.Count == 1 ? "1 word" : $"{Words.Count} words";

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

    private bool ShiftDisabled => !Timed || !Actions.CanEdit;

    private string? ShiftBlock => !Actions.CanEdit ? Actions.Reason : Timed ? null : NoTimes;

    private bool ClearDisabled => !Timed || !Actions.CanEdit;

    private string? ClearBlock => !Actions.CanEdit ? Actions.Reason : Timed ? null : NoTimes;

    private string EmptyMessage => Selection.Clip is { } id && MidiClipPlan.Find(Document, id) is not null
        ? "Lyrics live on audio clips, and this is a MIDI clip. Select an audio clip in the arrangement."
        : "Select an audio clip in the arrangement to see and edit its lyrics.";

    /// <inheritdoc />
    public void Dispose()
    {
        Selection.Changed -= OnSelectionChanged;
    }

    /// <inheritdoc />
    protected override void OnInitialized()
    {
        Selection.Changed += OnSelectionChanged;
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
            _draft = server;
            _saved = server;
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
        if (!ReferenceEquals(clip, _followedClip) || !ReferenceEquals(lyrics, _followedLyrics))
        {
            _followedClip = clip;
            _followedLyrics = lyrics;
            Cursor.Follow(clip, lyrics);
        }
    }

    private void OnSelectionChanged() => _ = InvokeAsync(StateHasChanged);

    private Task SayAsync(string text)
    {
        _announcement = text;
        StateHasChanged();
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
            await SayAsync($"Word {edit.Word + 1} moved.");
        }
    }

    private async Task ShiftAsync()
    {
        if (Found is not { } found || Lyrics is not { } lyrics)
        {
            return;
        }
        if (!double.TryParse(_shift, NumberStyles.Float, CultureInfo.InvariantCulture, out double seconds) || !double.IsFinite(seconds))
        {
            await SayAsync("Enter the seconds to move the words by, such as 0.5 or -0.5.");
            return;
        }
        // The first word stops at 0, so a move earlier than its start is held there, and the text says so.
        double first = lyrics.Words.Min(word => word.Start);
        if (seconds < -first)
        {
            await SayAsync($"The first word stops at 0 seconds, so the words move earlier by {first.ToString("0.000", CultureInfo.InvariantCulture)}.");
        }
        EditResult result = await Actions.Lyrics.Shift(found.Clip.Id, seconds);
        if (result.Succeeded && seconds >= -first)
        {
            await SayAsync($"Words moved by {seconds.ToString("0.###", CultureInfo.InvariantCulture)} seconds.");
        }
    }

    // An LRC file is read whole, up to the most text a clip may hold; the server then says whether its lines fit.
    private async Task ImportChosenAsync(InputFileChangeEventArgs chosen)
    {
        if (Found is not { } found)
        {
            return;
        }
        IBrowserFile file = chosen.File;
        if (file.Size > MaxLrcBytes)
        {
            Toasts.Show(ToastKind.Warning, "That file is larger than a clip may hold as lyrics, so it was not read.");
            return;
        }
        string text;
        using (Stream stream = file.OpenReadStream(MaxLrcBytes))
        using (StreamReader reader = new(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true))
        {
            text = await reader.ReadToEndAsync();
        }
        EditResult result = await Actions.Lyrics.ImportLrc(found.Clip.Id, text);
        if (result.Succeeded)
        {
            await SayAsync(string.Create(CultureInfo.InvariantCulture, $"Lyrics imported from {file.Name}."));
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
}
