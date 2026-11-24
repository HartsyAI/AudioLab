using AudioLab.Core.Model;
using AudioLab.Core.Rules;
using AudioLab.Web.Session.Effects;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace AudioLab.Web.Components.Effects;

/// <summary>The catalogue effect row: one effect of a chain with its place, name and bypass, and the buttons that move or remove it.</summary>
public sealed partial class EffectRow
{
    private (Effect? Effect, int Index, int Count, bool Editable, string? Reason, bool Dragging, string? Drop) _shown;

    /// <summary>The effect.</summary>
    [Parameter, EditorRequired]
    public Effect Effect { get; set; } = null!;

    /// <summary>Its place in the chain, from 0.</summary>
    [Parameter]
    public int Index { get; set; }

    /// <summary>How many effects the chain has.</summary>
    [Parameter]
    public int Count { get; set; }

    /// <summary>Whether the row's controls act; when not, they are disabled and say why.</summary>
    [Parameter]
    public bool Editable { get; set; }

    /// <summary>Why the controls do nothing now, for their titles; null when they act.</summary>
    [Parameter]
    public string? Reason { get; set; }

    /// <summary>Whether this row is the one being dragged.</summary>
    [Parameter]
    public bool Dragging { get; set; }

    /// <summary>Where a dragged effect would land relative to this row: "before" or "after", or null.</summary>
    [Parameter]
    public string? Drop { get; set; }

    /// <summary>Raised with the effect and whether it should now be on.</summary>
    [Parameter]
    public EventCallback<(EffectId Effect, bool Enabled)> OnEnabled { get; set; }

    /// <summary>Raised with the effect and the places to move it, negative for earlier.</summary>
    [Parameter]
    public EventCallback<(EffectId Effect, int Places)> OnMove { get; set; }

    /// <summary>Raised with the effect when remove is pressed.</summary>
    [Parameter]
    public EventCallback<EffectId> OnRemove { get; set; }

    /// <summary>Raised with the effect and the press when its handle is pressed.</summary>
    [Parameter]
    public EventCallback<(EffectId Effect, PointerEventArgs Press)> OnGrab { get; set; }

    /// <summary>Raised with the effect and the key when a key is pressed in the row.</summary>
    [Parameter]
    public EventCallback<(EffectId Effect, KeyboardEventArgs Key)> OnKey { get; set; }

    private string ElementId => IdOf(Effect.Id);

    private string Name => EffectText.Name(EffectRules.KindOf(Effect));

    private string Place => (Index + 1).ToString(System.Globalization.CultureInfo.InvariantCulture);

    private string Description => $"{Name}, effect {EffectChainRules.Position(Index, Count)}, {(Effect.Enabled ? "on" : "bypassed")}";

    private string GripHint => Editable ? "Drag to move" : Reason ?? "Not available.";

    private string? EarlierReason => !Editable ? Reason : EffectChainRules.MoveBlock(Index, Count, MoveDirection.Up);

    private string? LaterReason => !Editable ? Reason : EffectChainRules.MoveBlock(Index, Count, MoveDirection.Down);

    /// <summary>The id of the row's element, which the panel focuses again after the row moved.</summary>
    public static string IdOf(EffectId effect) => $"effect-row-{effect.Value}";

    /// <inheritdoc />
    protected override bool ShouldRender()
    {
        (Effect?, int, int, bool, string?, bool, string?) now = (Effect, Index, Count, Editable, Reason, Dragging, Drop);
        bool same = _shown == now;
        _shown = now;
        return !same;
    }

    private static string Flag(bool on) => on ? "true" : "false";

    private Task ToggleAsync() => OnEnabled.InvokeAsync((Effect.Id, !Effect.Enabled));

    private Task EarlierAsync() => OnMove.InvokeAsync((Effect.Id, -1));

    private Task LaterAsync() => OnMove.InvokeAsync((Effect.Id, 1));

    private Task RemoveAsync() => OnRemove.InvokeAsync(Effect.Id);

    private Task GrabAsync(PointerEventArgs press)
    {
        // Only a mouse or a pen lifts a row, so a finger still scrolls the list; the buttons are the way for a touch.
        return Editable && press.Button == 0 && press.PointerType is "mouse" or "pen" ? OnGrab.InvokeAsync((Effect.Id, press)) : Task.CompletedTask;
    }

    private Task KeyAsync(KeyboardEventArgs key) => OnKey.InvokeAsync((Effect.Id, key));
}
