using System.Globalization;
using System.Text;
using AudioLab.Core.Model;
using AudioLab.Web.Session.Roll;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace AudioLab.Web.Components.PianoRoll;

/// <summary>The sustain pedal of the clip: the stretches where it is down, and a line for each event, which can be pressed, moved and removed.</summary>
public sealed partial class PedalLane : IDisposable
{
    private MidiClip? _built;

    private double _length = -1;

    private string _down = string.Empty;

    private string _on = string.Empty;

    private string _off = string.Empty;

    /// <summary>The clip.</summary>
    [Parameter, EditorRequired]
    public MidiClip Clip { get; set; } = null!;

    /// <summary>The scale.</summary>
    [Parameter]
    public RollGeometry Geometry { get; set; }

    /// <summary>The time of the selected event, or null.</summary>
    [Parameter]
    public double? SelectedTime { get; set; }

    /// <summary>Raised with the press when the primary button goes down on the lane.</summary>
    [Parameter]
    public EventCallback<PointerEventArgs> OnPress { get; set; }

    [Inject]
    private RollDragState Drags { get; set; } = default!;

    private PedalDrag? Drag => Drags.Current as PedalDrag;

    /// <inheritdoc />
    public void Dispose()
    {
        Drags.Changed -= OnChanged;
    }

    /// <inheritdoc />
    protected override void OnInitialized()
    {
        Drags.Changed += OnChanged;
    }

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        if (_built is not null && _length == Geometry.Length && (ReferenceEquals(_built, Clip) || _built.Equals(Clip)))
        {
            return;
        }
        _built = Clip;
        _length = Geometry.Length;
        StringBuilder down = new();
        StringBuilder on = new();
        StringBuilder off = new();
        double? pressed = null;
        foreach (MidiControlChange change in Clip.ControlChanges.Where(change => change.Controller == PianoRollState.PedalController))
        {
            bool isDown = change.Value >= 64;
            (isDown ? on : off).Append(Line(change.Time));
            if (isDown && pressed is null)
            {
                pressed = change.Time;
            }
            else if (!isDown && pressed is { } from)
            {
                Region(down, from, change.Time);
                pressed = null;
            }
        }
        if (pressed is { } open)
        {
            Region(down, open, Clip.Length);
        }
        _down = down.ToString();
        _on = on.ToString();
        _off = off.ToString();
    }

    private static string Line(double time) => "M" + Format(time) + " 0V1";

    private static void Region(StringBuilder path, double from, double to)
    {
        path.Append('M').Append(Format(from)).Append(" 0H").Append(Format(to)).Append("V1H").Append(Format(from)).Append('z');
    }

    private static string Format(double value) => value.ToString("0.####", CultureInfo.InvariantCulture);

    private void OnChanged() => _ = InvokeAsync(StateHasChanged);
}
