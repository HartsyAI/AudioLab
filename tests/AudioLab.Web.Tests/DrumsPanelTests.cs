using AudioLab.Core.Model;
using AudioLab.Core.Rules;
using AudioLab.Web.Interop;
using AudioLab.Web.Screens;
using AudioLab.Web.Services;
using AudioLab.Web.Session;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;

namespace AudioLab.Web.Tests;

/// <summary>What the drums tab does with a pattern: its drafts, its focus, its refusals, its keys, and what its markup names.</summary>
public sealed class DrumsPanelTests
{
    private const string Offline = "Reconnecting to the server.";

    private static readonly BeatLaneId _kick = BeatLaneId.From("bln_kick");

    private static readonly BeatLaneId _snare = BeatLaneId.From("bln_snare");

    private static BeatPattern Pattern(double swing = 0.333, int steps = 16) => new()
    {
        Steps = steps,
        Swing = swing,
        Lanes =
        [
            new BeatLane { Id = _kick, Name = "Kick", Gain = 1, Steps = [false] },
            new BeatLane { Id = _snare, Name = "Snare", Gain = 1, Steps = [false] },
        ],
    };

    private static ProjectDocument Document(BeatPattern pattern) => Song.Empty() with { BeatPattern = pattern };

    private static BeatLane Lane(BeatLaneId id, string name) => new() { Id = id, Name = name, Gain = 1, Steps = [false] };

    private static SessionActions Editable(BeatActions? beats = null)
    {

        return new SessionActions { CanEdit = true, Beats = beats ?? new BeatActions() };
    }

    private static DrumsPanel Panel(ProjectDocument document, SessionActions actions, FakeFocus? focus = null)
    {
        DrumsPanel panel = new() { Focus = focus ?? new FakeFocus() };
        panel.Take(document, actions);
        panel.FollowPattern();
        return panel;
    }

    private static KeyboardEventArgs CtrlUp(bool repeat = false) => new() { Key = "ArrowUp", CtrlKey = true, Repeat = repeat };

    private static Task<RenderedMarkup> MarkupAsync(ProjectDocument document, SessionActions actions)
    {
        return RenderedMarkup.OfAsync<DrumsPanel>(new Dictionary<string, object?>
        {
            ["Document"] = document,
            ["Actions"] = actions,
        }, services =>
        {
            services.AddSingleton<IElementFocus>(new FakeFocus());
            services.AddSingleton<INativeOverlay, NoOverlay>();
        });
    }

    /// <summary>A typed swing survives another client's change to a track, even when the pattern arrives as a new object.</summary>
    [Fact]
    public void ATypedSwingSurvivesAnotherClientsChange()
    {
        ProjectDocument document = Document(Pattern(swing: 0.333));
        DrumsPanel panel = Panel(document, Editable());
        panel.SwingChanged("0.7");

        // Another client's mute: the document is re-read, so its pattern is a new object with the same values.
        panel.Take(document with
        {
            BeatPattern = Pattern(swing: 0.333),
            Tracks = [Song.NewTrack("t1") with { Muted = true }],
        }, Editable());

        Assert.Equal("0.7", panel.SwingDraft);
    }

    /// <summary>When the swing itself changes, the field shows the new value.</summary>
    [Fact]
    public void TheDraftFollowsAChangeOfSwing()
    {
        DrumsPanel panel = Panel(Document(Pattern(swing: 0.333)), Editable());
        panel.SwingChanged("0.7");

        panel.Take(Document(Pattern(swing: 0.5)), Editable());

        Assert.Equal("0.5", panel.SwingDraft);
    }

    /// <summary>A move keeps focus on the button of the lane and direction that moved, so focus lands on that button.</summary>
    [Fact]
    public async Task AMoveFocusesTheMovedLanesButton()
    {
        DrumsPanel panel = Panel(Document(Pattern()), Editable(new BeatActions
        {
            MoveLane = (_, _) => Task.FromResult(EditResult.Applied(true, [])),
        }));

        await panel.MoveAsync(Lane(_snare, "Snare"), MoveDirection.Up);

        Assert.Equal(BeatLaneText.MoveId(_snare, MoveDirection.Up), panel.PendingFocus);
    }

    /// <summary>A refused move leaves focus where it was.</summary>
    [Fact]
    public async Task ARefusedMoveDoesNotMoveFocus()
    {
        DrumsPanel panel = Panel(Document(Pattern()), Editable(new BeatActions
        {
            MoveLane = (_, _) => Task.FromResult(EditResult.Refused("There is no drum lane with that id.")),
        }));

        await panel.MoveAsync(Lane(_snare, "Snare"), MoveDirection.Up);

        Assert.Null(panel.PendingFocus);
    }

    /// <summary>A rename the server refuses shows its words in the dialog, and the dialog stays open.</summary>
    [Fact]
    public async Task ARenameRefusedByTheServerShowsItsWordsInTheDialog()
    {
        DrumsPanel panel = Panel(Document(Pattern()), Editable(new BeatActions
        {
            RenameLane = (_, _) => Task.FromResult(EditResult.Refused("There is no drum lane with the id bln_kick.")),
        }));
        panel.BeginRename(Lane(_kick, "Kick"));

        await panel.RenameAsync();

        Assert.Equal("There is no drum lane with the id bln_kick.", panel.RenameError);
        Assert.True(panel.RenameOpen);
    }

    /// <summary>When the rename dialog closes, focus goes back to the Rename button of the lane it was opened from, once.</summary>
    [Fact]
    public async Task FocusReturnsToTheRenameButtonWhenTheDialogCloses()
    {
        FakeFocus focus = new();
        DrumsPanel panel = Panel(Document(Pattern()), Editable(), focus);
        panel.BeginRename(Lane(_kick, "Kick"));

        panel.CloseRename();
        await panel.RestoreRenameFocusAsync();
        await panel.RestoreRenameFocusAsync();

        Assert.Equal([BeatLaneText.RenameId(_kick)], focus.Focused);
    }

    /// <summary>A held key does not repeat a move.</summary>
    [Fact]
    public async Task AHeldKeyDoesNotRepeatAMove()
    {
        int moves = 0;
        DrumsPanel panel = Panel(Document(Pattern()), Editable(new BeatActions
        {
            MoveLane = (_, _) =>
            {
                moves++;
                return Task.FromResult(EditResult.Applied(true, []));
            },
        }));

        await panel.KeyAsync(Lane(_snare, "Snare"), CtrlUp(repeat: true));

        Assert.Equal(0, moves);
    }

    /// <summary>A read-only project says why a lane key does nothing, in the status line.</summary>
    [Fact]
    public async Task AReadOnlyProjectSaysWhyALaneKeyDoesNothing()
    {
        int moves = 0;
        DrumsPanel panel = Panel(Document(Pattern()), new SessionActions
        {
            CanEdit = false,
            Reason = Offline,
            Beats = new BeatActions
            {
                MoveLane = (_, _) =>
                {
                    moves++;
                    return Task.FromResult(EditResult.Applied(true, []));
                },
            },
        });

        await panel.KeyAsync(Lane(_snare, "Snare"), CtrlUp());

        Assert.Equal(0, moves);
        Assert.Equal(Offline, panel.Announcement);
    }

    /// <summary>A second move while the first is on its way is not sent.</summary>
    [Fact]
    public async Task ASecondMoveWhileTheFirstIsOnItsWayIsNotSent()
    {
        TaskCompletionSource<EditResult> reply = new();
        int moves = 0;
        DrumsPanel panel = Panel(Document(Pattern()), Editable(new BeatActions
        {
            MoveLane = (_, _) =>
            {
                moves++;
                return reply.Task;
            },
        }));
        BeatLane snare = Lane(_snare, "Snare");

        Task first = panel.MoveAsync(snare, MoveDirection.Up);
        await panel.MoveAsync(snare, MoveDirection.Up);
        reply.SetResult(EditResult.Applied(true, []));
        await first;

        Assert.Equal(1, moves);
    }

    /// <summary>A second rename while the first is on its way is not sent.</summary>
    [Fact]
    public async Task ASecondRenameWhileTheFirstIsOnItsWayIsNotSent()
    {
        TaskCompletionSource<EditResult> reply = new();
        int renames = 0;
        DrumsPanel panel = Panel(Document(Pattern()), Editable(new BeatActions
        {
            RenameLane = (_, _) =>
            {
                renames++;
                return reply.Task;
            },
        }));
        panel.BeginRename(Lane(_kick, "Kick"));

        Task first = panel.RenameAsync();
        await panel.RenameAsync();
        reply.SetResult(EditResult.Applied(true, []));
        await first;

        Assert.Equal(1, renames);
    }

    /// <summary>Each lane's controls say which lane they change, so a list of identical buttons can be told apart by ear.</summary>
    [Fact]
    public async Task EachLaneControlIsNamedByItsLane()
    {
        RenderedMarkup markup = await MarkupAsync(Document(Pattern()), Editable());

        string[] names = markup.Values("aria-label");
        Assert.Contains("Move Kick earlier", names);
        Assert.Contains("Move Kick later", names);
        Assert.Contains("Rename Snare", names);
        Assert.Contains("Remove Snare", names);
    }

    /// <summary>The move buttons carry the ids that focus returns to.</summary>
    [Fact]
    public async Task TheMoveButtonsHaveTheIdsFocusReturnsTo()
    {
        RenderedMarkup markup = await MarkupAsync(Document(Pattern()), Editable());

        string[] ids = markup.Values("id");
        Assert.Contains(BeatLaneText.MoveId(_snare, MoveDirection.Up), ids);
        Assert.Contains(BeatLaneText.MoveId(_snare, MoveDirection.Down), ids);
        Assert.Contains(BeatLaneText.RenameId(_kick), ids);
    }

    /// <summary>The swing and step count show what the pattern holds, with the swing written so that it reads back exactly.</summary>
    [Fact]
    public async Task TheSwingAndStepsShowThePatternsValues()
    {
        RenderedMarkup markup = await MarkupAsync(Document(Pattern(swing: 0.333, steps: 32)), Editable());

        Assert.Contains("0.333", markup.Values("value"));
        Assert.Contains("32", markup.Values("value"));
        Assert.Contains("Swing 0.333", markup.Values("aria-label"));
    }

    /// <summary>When the document cannot be edited, every control says why in its title.</summary>
    [Fact]
    public async Task AControlThatCannotEditSaysWhy()
    {
        RenderedMarkup markup = await MarkupAsync(Document(Pattern()), SessionActions.ReadOnly(Offline));

        Assert.Contains(markup.Values("title"), title => title.Contains(Offline, StringComparison.Ordinal));
    }

    private sealed class FakeFocus : IElementFocus
    {
        public List<string> Focused { get; } = [];

        public void Focus(string elementId) => Focused.Add(elementId);

        public void FocusIn(string containerId, string selector)
        {
        }
    }

    private sealed class NoOverlay : INativeOverlay
    {
        public void ShowModal(string dialogId)
        {
        }

        public void OnClosed(string dialogId, Action closed)
        {
        }

        public void KeepOpenWhileInnerOverlayIsExpanded(string dialogId)
        {
        }

        public void Close(string dialogId)
        {
        }

        public void KeepAboveModals(string popoverId)
        {
        }
    }
}
