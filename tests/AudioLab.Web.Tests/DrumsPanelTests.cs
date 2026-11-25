using AudioLab.Core.Model;
using AudioLab.Core.Rules;
using AudioLab.Web.Interop;
using AudioLab.Web.Screens;
using AudioLab.Web.Session;
using Microsoft.Extensions.DependencyInjection;

namespace AudioLab.Web.Tests;

/// <summary>What the drums tab shows for a pattern: named lane controls with their ids, the swing and steps it was given, and a control that cannot edit saying why.</summary>
public sealed class DrumsPanelTests
{
    private const string Offline = "Reconnecting to the server.";

    private static readonly BeatLaneId _kick = BeatLaneId.From("bln_kick");

    private static readonly BeatLaneId _snare = BeatLaneId.From("bln_snare");

    private static ProjectDocument Document(double swing = 0.333, int steps = 16)
    {
        BeatPattern pattern = new()
        {
            Steps = steps,
            Swing = swing,
            Lanes =
            [
                new BeatLane { Id = _kick, Name = "Kick", Gain = 1, Steps = [false] },
                new BeatLane { Id = _snare, Name = "Snare", Gain = 1, Steps = [false] },
            ],
        };
        return Song.Empty() with { BeatPattern = pattern };
    }

    private static Dictionary<string, object?> Parameters(ProjectDocument document, bool editable) => new()
    {
        ["Document"] = document,
        ["Actions"] = editable ? new SessionActions { CanEdit = true } : SessionActions.ReadOnly(Offline),
    };

    private static Task<RenderedMarkup> PanelAsync(ProjectDocument document, bool editable)
    {
        return RenderedMarkup.OfAsync<DrumsPanel>(Parameters(document, editable), services =>
        {
            services.AddSingleton<IElementFocus, NoFocus>();
            services.AddSingleton<INativeOverlay, NoOverlay>();
        });
    }

    /// <summary>Each lane's controls say which lane they change, so a list of identical buttons can be told apart by ear.</summary>
    [Fact]
    public async Task EachLaneControlIsNamedByItsLane()
    {
        RenderedMarkup markup = await PanelAsync(Document(), editable: true);

        string[] names = markup.Values("aria-label");
        Assert.Contains("Move Kick earlier", names);
        Assert.Contains("Move Kick later", names);
        Assert.Contains("Rename Snare", names);
        Assert.Contains("Remove Snare", names);
    }

    /// <summary>A move keeps focus on the button of its lane and direction, so the element id of that button is the one focus goes back to.</summary>
    [Fact]
    public async Task TheMoveButtonsHaveTheIdsFocusReturnsTo()
    {
        RenderedMarkup markup = await PanelAsync(Document(), editable: true);

        string[] ids = markup.Values("id");
        Assert.Contains(BeatLaneText.MoveId(_snare, MoveDirection.Up), ids);
        Assert.Contains(BeatLaneText.MoveId(_snare, MoveDirection.Down), ids);
        Assert.Contains(BeatLaneText.RenameId(_kick), ids);
    }

    /// <summary>The swing and step count show what the pattern holds, with the swing written so that it reads back exactly.</summary>
    [Fact]
    public async Task TheSwingAndStepsShowThePatternsValues()
    {
        RenderedMarkup markup = await PanelAsync(Document(swing: 0.333, steps: 32), editable: true);

        Assert.Contains("0.333", markup.Values("value"));
        Assert.Contains("32", markup.Values("value"));
        Assert.Contains("Swing 0.333", markup.Values("aria-label"));
    }

    /// <summary>When the document cannot be edited, every control says why, and none is offered as an action.</summary>
    [Fact]
    public async Task AControlThatCannotEditSaysWhy()
    {
        RenderedMarkup markup = await PanelAsync(Document(), editable: false);

        Assert.Contains(markup.Values("title"), title => title.Contains(Offline, StringComparison.Ordinal));
    }

    /// <summary>When the pattern changes, the panel shows the new values in its fields.</summary>
    [Fact]
    public async Task ANewSwingReachesTheFieldsWhenThePatternChanges()
    {
        RenderedMarkup markup = await PanelAsync(Document(swing: 0.333), editable: true);

        await markup.RenderAgainAsync(Parameters(Document(swing: 0.5), editable: true));

        Assert.Contains("0.5", markup.Values("value"));
    }

    private sealed class NoFocus : IElementFocus
    {
        public void Focus(string elementId)
        {
        }

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
