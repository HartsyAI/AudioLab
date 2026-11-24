using AudioLab.Core.Model;
using AudioLab.Core.Rules;
using AudioLab.Web.Components;
using AudioLab.Web.Components.Effects;
using AudioLab.Web.Interop;
using AudioLab.Web.Services;
using AudioLab.Web.Session.Effects;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace AudioLab.Web.Screens;

/// <summary>The effects tab: the insert chain of the selected track as an ordered list, with add, bypass, remove and reorder.</summary>
public sealed partial class EffectsPanel : IDisposable
{
    private const string ListId = "effects-list";

    private const string PanelId = "effects-panel";

    private const string AddMenuId = "effects-add";

    private const string Busy = "The last change is still being sent; try again in a moment.";

    private const string Gone = "That effect is no longer in the chain.";

    private static readonly IReadOnlyList<MenuItem> _kindItems =
        [.. EffectText.Kinds.Select(kind => new MenuItem(kind.ToString(), EffectText.Name(kind), string.Empty))];

    private EffectDrag? _drag;

    private string? _announcement;

    private string? _focus;

    private bool _editing;

    /// <summary>The document.</summary>
    [Parameter, EditorRequired]
    public ProjectDocument Document { get; set; } = null!;

    /// <summary>What the screen can do to the project.</summary>
    [Parameter, EditorRequired]
    public SessionActions Actions { get; set; } = null!;

    /// <summary>The selected track, or null.</summary>
    [Parameter]
    public TrackId? Selected { get; set; }

    [Inject]
    private EffectSelection Selection { get; set; } = default!;

    [Inject]
    private IKeyGuard Guard { get; set; } = default!;

    [Inject]
    private IPointerTracker Pointer { get; set; } = default!;

    [Inject]
    private IElementView View { get; set; } = default!;

    [Inject]
    private IElementFocus Focus { get; set; } = default!;

    [Inject]
    private ToastService Toasts { get; set; } = default!;

    private static IReadOnlyList<MenuItem> KindItems => _kindItems;

    // Core's words for a chain that takes no more, for the count's title.
    private static string FullText => EffectRules.AddRefusal(ProjectLimits.MaxEffects) ?? string.Empty;

    private Track? Current => Selected is { } id ? Document.Tracks.FirstOrDefault(track => track.Id == id) : null;

    // The server's words for a full chain while the project can be edited, and why it cannot be edited otherwise.
    private string? AddBlock => Current is { } track ? (Actions.CanEdit ? EffectChainRules.AddBlock(track) : Actions.Reason) : null;

    /// <inheritdoc />
    public void Dispose()
    {
        Selection.Changed -= OnSelectionChanged;
        Pointer.Stop();
    }

    /// <inheritdoc />
    protected override void OnInitialized()
    {
        Selection.Changed += OnSelectionChanged;
    }

    /// <inheritdoc />
    protected override void OnAfterRender(bool firstRender)
    {
        if (firstRender && OperatingSystem.IsBrowser())
        {
            // A knob takes the arrow keys, Home and End; without this the page would also seek with them.
            Guard.PreventNavigationKeys(PanelId, "slider");
        }
        if (_focus is not null)
        {
            Focus.Focus(_focus);
            _focus = null;
        }
        else if (Selection.TakeFocusRequest())
        {
            // Opened from a channel strip or by key: the keyboard goes to the effect that was asked for, or to the panel when it has none.
            Focus.Focus(Current is { } track && Selection.Of(track) is { } shown ? EffectRow.IdOf(shown.Id) : PanelId);
        }
    }

    private void OnSelectionChanged() => _ = InvokeAsync(StateHasChanged);

    private string? DropOf(int index)
    {
        return _drag is { Active: true } drag && drag.Target == index && drag.Target != drag.From ? (index > drag.From ? "after" : "before") : null;
    }

    private static int IndexOf(Track track, EffectId effect) => track.Effects.ToList().FindIndex(candidate => candidate.Id == effect);

    private static string NameAt(Track track, int index) => EffectText.Name(EffectRules.KindOf(track.Effects[index]));

    private Task AddAsync(MenuItem item)
    {
        return Enum.TryParse(item.Id, out EffectKind kind) ? AddKindAsync(kind) : Task.CompletedTask;
    }

    private async Task AddKindAsync(EffectKind kind)
    {
        if (Current is not { } track)
        {
            return;
        }
        if (AddBlock is { } block)
        {
            Say(block);
            return;
        }
        if (_editing)
        {
            Say(Busy);
            return;
        }
        _editing = true;
        try
        {
            EditResult result = await Actions.Effects.Add(track.Id, kind);
            if (result.Succeeded && result.Created.Count > 0)
            {
                Say($"Added {EffectText.Name(kind)} at the end of the chain.");
                Follow(EffectRow.IdOf(EffectId.From(result.Created[0])));
            }
        }
        finally
        {
            _editing = false;
        }
    }

    private async Task SetEnabledAsync((EffectId Effect, bool Enabled) change)
    {
        if (Current is not { } track)
        {
            return;
        }
        if (_editing)
        {
            Say(Busy);
            return;
        }
        _editing = true;
        try
        {
            EditResult result = await Actions.Effects.SetEnabled(track.Id, change.Effect, change.Enabled);
            if (result.Succeeded && IndexOf(track, change.Effect) is >= 0 and int index)
            {
                Say($"{NameAt(track, index)} {EffectChainRules.Position(index, track.Effects.Count)} {(change.Enabled ? "on" : "bypassed")}.");
            }
        }
        finally
        {
            _editing = false;
        }
    }

    private async Task MoveAsync((EffectId Effect, int Places) move)
    {
        if (Current is not { } track)
        {
            return;
        }
        int from = IndexOf(track, move.Effect);
        if (from < 0)
        {
            Say(Gone);
            return;
        }
        int to = Math.Clamp(from + move.Places, 0, track.Effects.Count - 1);
        if (to == from)
        {
            Say(EffectChainRules.MoveBlock(from, track.Effects.Count, move.Places < 0 ? MoveDirection.Up : MoveDirection.Down) ?? string.Empty);
            return;
        }
        if (_editing)
        {
            Say(Busy);
            return;
        }
        _editing = true;
        try
        {
            EditResult result = await Actions.Effects.Move(track.Id, move.Effect, to - from);
            if (result.Succeeded)
            {
                Say($"{NameAt(track, from)} moved to {EffectChainRules.Position(to, track.Effects.Count)}.");
                Follow(EffectRow.IdOf(move.Effect));
            }
        }
        finally
        {
            _editing = false;
        }
    }

    private async Task RemoveAsync(EffectId effect)
    {
        if (Current is not { } track)
        {
            return;
        }
        int index = IndexOf(track, effect);
        if (index < 0)
        {
            Say(Gone);
            return;
        }
        if (_editing)
        {
            Say(Busy);
            return;
        }
        string name = NameAt(track, index);
        EffectId? next = track.Effects.Count > 1 ? track.Effects[index == track.Effects.Count - 1 ? index - 1 : index + 1].Id : null;
        _editing = true;
        try
        {
            EditResult result = await Actions.Effects.Remove(track.Id, effect);
            if (result.Succeeded)
            {
                Say($"{name} removed.");
                Toasts.Show(ToastKind.Info, $"{name} removed.", Actions.Undo);
                // The row that took its place keeps the keyboard's place; with none left the panel does.
                Follow(next is { } neighbour ? EffectRow.IdOf(neighbour) : PanelId);
            }
        }
        finally
        {
            _editing = false;
        }
    }

    private Task KeyAsync((EffectId Effect, KeyboardEventArgs Key) press)
    {
        KeyboardEventArgs key = press.Key;
        bool ours = Shortcuts.EffectRemove.Matches(key) || Shortcuts.EffectBypass.Matches(key) || Shortcuts.EffectEarlier.Matches(key)
            || Shortcuts.EffectLater.Matches(key);
        if (key.Repeat || !ours)
        {
            return Task.CompletedTask;
        }
        if (!Actions.CanEdit)
        {
            // A key that does nothing says why, as a disabled button does in its title.
            Say(Actions.Reason ?? "The project cannot be edited now.");
            return Task.CompletedTask;
        }
        if (Shortcuts.EffectRemove.Matches(key))
        {
            return RemoveAsync(press.Effect);
        }
        if (Shortcuts.EffectBypass.Matches(key) && Current is { } track && IndexOf(track, press.Effect) is >= 0 and int index)
        {
            return SetEnabledAsync((press.Effect, !track.Effects[index].Enabled));
        }
        if (Shortcuts.EffectEarlier.Matches(key))
        {
            return MoveAsync((press.Effect, -1));
        }
        return Shortcuts.EffectLater.Matches(key) ? MoveAsync((press.Effect, 1)) : Task.CompletedTask;
    }

    private async Task SetSettingAsync((EffectId Effect, string Name, double Value) change)
    {
        if (!TryHold(change.Effect, out Track? track, out int index))
        {
            return;
        }
        EditResult result = await Actions.Effects.SetParameter(track!.Id, change.Effect, change.Name, change.Value);
        if (result.Succeeded && EffectRules.Find(track.Effects[index], change.Name) is { } setting)
        {
            Say($"{Subject(track, index)} {setting.Label} {EffectValueText.Of(setting, change.Value)}.");
        }
    }

    private async Task SetSyncAsync((EffectId Effect, bool Sync, string? Division) change)
    {
        if (!TryHold(change.Effect, out Track? track, out int index))
        {
            return;
        }
        EditResult result = await Actions.Effects.SetDelaySync(track!.Id, change.Effect, change.Sync, change.Division);
        if (result.Succeeded)
        {
            Say(change.Sync
                ? $"{Subject(track, index)} follows the tempo{(change.Division is null ? string.Empty : $", {change.Division}")}."
                : $"{Subject(track, index)} keeps its own time.");
        }
    }

    private async Task ApplyPresetAsync((EffectId Effect, string Preset) change)
    {
        if (!TryHold(change.Effect, out Track? track, out int index))
        {
            return;
        }
        if (_editing)
        {
            Say(Busy);
            return;
        }
        _editing = true;
        try
        {
            EditResult result = await Actions.Effects.ApplyPreset(track!.Id, change.Effect, change.Preset);
            if (result.Succeeded)
            {
                Say($"{change.Preset} applied to {Subject(track, index)}.");
            }
        }
        finally
        {
            _editing = false;
        }
    }

    // A knob that was dragged on an effect that is gone must not set the effect that took its place: the edit is dropped, and the user is told.
    private Task AbandonedAsync()
    {
        Toasts.Show(ToastKind.Warning, "The effect changed while a knob was being dragged, so that edit was not sent.");
        return Task.CompletedTask;
    }

    // Whether an edit of an effect can be sent now: the project can be edited and the effect is still in the chain; if not, it says why.
    private bool TryHold(EffectId effect, out Track? track, out int index)
    {
        track = Current;
        index = track is null ? -1 : IndexOf(track, effect);
        if (track is null)
        {
            return false;
        }
        if (!Actions.CanEdit)
        {
            Say(Actions.Reason ?? "The project cannot be edited now.");
            return false;
        }
        if (index < 0)
        {
            Toasts.Show(ToastKind.Warning, "That effect is no longer in the chain, so the change was not sent.");
            return false;
        }
        return true;
    }

    private static string Subject(Track track, int index) => $"{NameAt(track, index)} {index + 1}";

    private Task PanelKeyAsync(KeyboardEventArgs key)
    {
        if (Shortcuts.EffectPresets.Matches(key) && Current is { } selected && Selection.Of(selected) is not null && Actions.CanEdit)
        {
            Focus.FocusIn(EffectSettings.PresetMenuId, "button");
            return Task.CompletedTask;
        }
        if (key.Repeat)
        {
            return Task.CompletedTask;
        }
        if (Shortcuts.EffectAddMenu.Matches(key) && Current is not null)
        {
            if (AddBlock is { } block)
            {
                Say(block);
            }
            else
            {
                Focus.FocusIn(AddMenuId, "button");
            }
        }
        return Task.CompletedTask;
    }

    // A press on a handle may become a drag; nothing is sent until the release, and only a drag that ends elsewhere sends anything.
    private Task GrabAsync((EffectId Effect, PointerEventArgs Press) grab)
    {
        if (!Actions.CanEdit || Current is not { } track || _drag is not null)
        {
            return Task.CompletedTask;
        }
        if (_editing)
        {
            Say(Busy);
            return Task.CompletedTask;
        }
        int from = IndexOf(track, grab.Effect);
        if (from < 0 || Rows(track) is not { } rows)
        {
            return Task.CompletedTask;
        }
        EffectDrag drag = new(grab.Effect.Value, from, track.Effects.Count, grab.Press.ClientY - rows.Top);
        _drag = drag;
        // The list scrolls under the pointer, so where it is in the list is read again on every move and on the release.
        Pointer.Begin(
            (x, y) => DragMoved(track.Id, drag, y),
            (x, y) =>
            {
                DragMoved(track.Id, drag, y);
                _ = InvokeAsync(() => ReleaseAsync(track.Id, drag));
            },
            () => Finish(drag));
        return Task.CompletedTask;
    }

    private void DragMoved(TrackId trackId, EffectDrag drag, double y)
    {
        Track? track = Document.Tracks.FirstOrDefault(candidate => candidate.Id == trackId);
        if (track is null || Rows(track) is not { } rows)
        {
            return;
        }
        (int target, bool active) = (drag.Target, drag.Active);
        drag.Move(y - rows.Top, rows.Pitch);
        if (drag.Target != target || drag.Active != active)
        {
            _ = InvokeAsync(StateHasChanged);
        }
    }

    // Where the first row is and how far apart the rows are, read from the rows themselves: the list may be taller than its rows.
    private (double Top, double Pitch)? Rows(Track track)
    {
        if (track.Effects.Count < 2
            || View.Bounds(EffectRow.IdOf(track.Effects[0].Id)) is not { } first
            || View.Bounds(EffectRow.IdOf(track.Effects[1].Id)) is not { } second)
        {
            return null;
        }
        return (first.Top, second.Top - first.Top);
    }

    private void Finish(EffectDrag drag)
    {
        if (ReferenceEquals(_drag, drag))
        {
            _drag = null;
            _ = InvokeAsync(StateHasChanged);
        }
    }

    private async Task ReleaseAsync(TrackId track, EffectDrag drag)
    {
        if (drag.Commit() is null)
        {
            Finish(drag);
            return;
        }
        // The chain may have changed since the press (another client): the target is a place in the chain as it is now.
        Track? now = Document.Tracks.FirstOrDefault(candidate => candidate.Id == track);
        if (now is null || EffectChainRules.StepsTo(now, EffectId.From(drag.Effect), drag.Target) is not { } steps)
        {
            Toasts.Show(ToastKind.Warning, "The effect was removed while it was being dragged, so nothing was moved.");
            Finish(drag);
            return;
        }
        if (steps == 0)
        {
            Finish(drag);
            return;
        }
        if (!Actions.CanEdit)
        {
            Toasts.Show(ToastKind.Warning, "The connection to the server was lost during the drag, so the effect was not moved.");
            Finish(drag);
            return;
        }
        // The drop mark stays where the effect landed until the server has answered and the new order is drawn.
        StateHasChanged();
        try
        {
            await MoveAsync((EffectId.From(drag.Effect), steps));
        }
        finally
        {
            Finish(drag);
        }
    }

    private void Say(string text)
    {
        _announcement = text;
        StateHasChanged();
    }

    private void Follow(string id)
    {
        _focus = id;
        StateHasChanged();
    }
}
