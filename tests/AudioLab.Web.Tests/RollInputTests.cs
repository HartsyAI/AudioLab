using AudioLab.Core.Model;
using AudioLab.Web.Components.PianoRoll;
using AudioLab.Web.Interop;
using AudioLab.Web.Services;
using AudioLab.Web.Session.Roll;
using Microsoft.Extensions.DependencyInjection;

namespace AudioLab.Web.Tests;

/// <summary>Where the roll's keys go, what holds the pointer, and how the keyboard reaches the keys and the lanes.</summary>
public sealed class RollInputTests
{
    private static bool Collides(Shortcut roll, Shortcut other)
    {
        return string.Equals(roll.Key, other.Key, StringComparison.OrdinalIgnoreCase)
            && roll.Ctrl == other.Ctrl && roll.Shift == other.Shift && roll.Alt == other.Alt;
    }

    private static KeyChord Chord(Shortcut shortcut, bool repeat = false) => new(shortcut.Key, shortcut.Ctrl, shortcut.Shift, shortcut.Alt, repeat);

    /// <summary>Seven chords are the roll's and the session's (Shift with an arrow, Alt with an arrow, Enter); the roll has them first.</summary>
    [Fact]
    public void TheRollHasItsKeysBeforeThePagesAndTheyShareSevenChords()
    {
        string[] shared =
        [
            .. Shortcuts.Roll.Where(roll => Shortcuts.All.Any(other => other.Scope == Shortcuts.Session && Collides(roll, other)))
                .Select(roll => roll.Id),
        ];

        Assert.Equal(
            [
                Shortcuts.RollExtendPrevious.Id, Shortcuts.RollExtendNext.Id, Shortcuts.RollMoveEarlier.Id, Shortcuts.RollMoveLater.Id,
                Shortcuts.RollTransposeUp.Id, Shortcuts.RollTransposeDown.Id, Shortcuts.RollAddNote.Id,
            ],
            shared);
        PianoRollKeys keys = new() { Handler = chord => RollKeyPolicy.Find(chord) is not null };
        bool transportHeard = false;
        Func<KeyChord, bool> transport = _ =>
        {
            transportHeard = true;
            return true;
        };

        Assert.True(keys.Route(Chord(Shortcuts.SeekForward), transport));
        Assert.False(transportHeard);
        keys.Handler = _ => false;
        Assert.True(keys.Route(Chord(Shortcuts.SeekForward), transport));
        Assert.True(transportHeard);
    }

    /// <summary>A key held down repeats only the keys that send nothing; an edit runs once, and no key runs under a gesture.</summary>
    [Fact]
    public void AHeldKeyRepeatsOnlyTheKeysThatSendNothingAndNoKeyRunsUnderAGesture()
    {
        foreach (Shortcut shortcut in Shortcuts.Roll)
        {
            bool walks = new[]
            {
                Shortcuts.RollPrevious, Shortcuts.RollNext, Shortcuts.RollAbove, Shortcuts.RollBelow, Shortcuts.RollExtendPrevious,
                Shortcuts.RollExtendNext, Shortcuts.RollExtendAbove, Shortcuts.RollExtendBelow, Shortcuts.RollInsertEarlier,
                Shortcuts.RollInsertLater, Shortcuts.RollZoomIn, Shortcuts.RollZoomOut,
            }.Contains(shortcut);

            Assert.True(RollKeyPolicy.Runs(shortcut, Chord(shortcut), gestureActive: false), shortcut.Id);
            Assert.Equal(walks, RollKeyPolicy.Runs(shortcut, Chord(shortcut, repeat: true), gestureActive: false));
            Assert.False(RollKeyPolicy.Runs(shortcut, Chord(shortcut), gestureActive: true), shortcut.Id);
        }
        Assert.Null(RollKeyPolicy.Find(new KeyChord("F9", false, false, false)));
        Assert.Equal(Shortcuts.RollDelete, RollKeyPolicy.Find(Chord(Shortcuts.RollDelete)));
    }

    /// <summary>A gesture that is released keeps its preview, and the next press does not wait for it; history waits for it.</summary>
    [Fact]
    public void AReleasedGestureKeepsItsPreviewWithoutHoldingThePointer()
    {
        RollDragState drags = new();
        VelocityDrag first = new(ClipId.From("m"), [], 56, 28);
        VelocityDrag second = new(ClipId.From("m"), [], 56, 14);
        int changes = 0;
        drags.Changed += () => changes++;

        drags.Begin(first);
        Assert.Same(first, drags.Current);
        drags.Land(first);

        Assert.Null(drags.Current);
        Assert.True(drags.Busy);
        Assert.Same(first, drags.Latest<VelocityDrag>());
        drags.Begin(second);
        Assert.Same(second, drags.Latest<VelocityDrag>());
        Assert.Equal([first, second], drags.Shown.OfType<VelocityDrag>());
        drags.End(first);
        drags.End(second);
        Assert.False(drags.Busy);
        Assert.Null(drags.Latest<VelocityDrag>());
        Assert.True(changes >= 5);
    }

    /// <summary>A select all leaves no pedal event selected, so Delete takes the notes and not an event that is no longer shown selected.</summary>
    [Fact]
    public void SelectAllLeavesNoPedalEventSelected()
    {
        MidiNote note = new(new NoteId(1), 60, 1, 0.5, 90);
        MidiClip clip = Song.Midi("m", 0, 4, note) with { ControlChanges = [new MidiControlChange(1, 64, 127)] };
        PianoRollState state = new();
        state.Bind(clip.Id);
        state.SelectPedal(1);

        state.SelectAll(clip);

        Assert.Null(state.SelectedPedal);
        Assert.Single(state.Selected);
    }

    /// <summary>The keys and both lanes are one tab stop each, named for what they do, so the keyboard reaches them.</summary>
    [Fact]
    public async Task TheKeysAndBothLanesAreOneTabStopEach()
    {
        MidiClip clip = Song.Midi("m", 0, 4, new MidiNote(new NoteId(1), 60, 1, 0.5, 90));
        RollGeometry geometry = new(80, 12, clip.Length);
        Action<IServiceCollection> services = collection => collection.AddSingleton<RollDragState>().AddSingleton<PianoRollState>();

        RenderedMarkup keys = await RenderedMarkup.OfAsync<PianoKeys>(new Dictionary<string, object?> { ["Geometry"] = geometry }, services);
        RenderedMarkup velocity = await RenderedMarkup.OfAsync<VelocityLane>(
            new Dictionary<string, object?> { ["Clip"] = clip, ["Geometry"] = geometry, ["ColorToken"] = "var(--track-1)" },
            services);
        RenderedMarkup pedal = await RenderedMarkup.OfAsync<PedalLane>(
            new Dictionary<string, object?> { ["Clip"] = clip, ["Geometry"] = geometry },
            services);

        foreach (RenderedMarkup rendered in new[] { keys, velocity, pedal })
        {
            Assert.Equal(["0"], rendered.Values("tabindex"));
            Assert.Equal(["group"], rendered.Values("role"));
        }
        Assert.Contains("Piano keys", Assert.Single(keys.Values("aria-label")), StringComparison.Ordinal);
        Assert.Contains("Control with Up or Down", Assert.Single(velocity.Values("aria-label")), StringComparison.Ordinal);
        Assert.Contains("Delete removes", Assert.Single(pedal.Values("aria-label")), StringComparison.Ordinal);
    }
}
