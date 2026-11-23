using System.Globalization;
using AudioLab.Core.Model;
using AudioLab.Core.Rules;
using AudioLab.Web.Interop;
using AudioLab.Web.Session;
using AudioLab.Web.Session.Roll;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace AudioLab.Web.Components.PianoRoll;

/// <summary>The bars of the clip along the top of the roll, numbered as the arrangement numbers them; a press puts the insert point there.</summary>
public sealed partial class RollRuler : IDisposable
{
    private const double MinLabelPixels = 36;

    private const int LabelLimit = 600;

    private IReadOnlyList<(double X, string Text, bool Strong)> _labels = [];

    private (ProjectDocument, MidiClip, RollGeometry)? _built;

    /// <summary>The document, for its tempo map and time signature.</summary>
    [Parameter, EditorRequired]
    public ProjectDocument Document { get; set; } = null!;

    /// <summary>The clip.</summary>
    [Parameter, EditorRequired]
    public MidiClip Clip { get; set; } = null!;

    /// <summary>The scale.</summary>
    [Parameter]
    public RollGeometry Geometry { get; set; }

    [Inject]
    private PianoRollState State { get; set; } = default!;

    [Inject]
    private IElementView View { get; set; } = default!;

    private IReadOnlyList<(double X, string Text, bool Strong)> Labels => _labels;

    /// <inheritdoc />
    public void Dispose()
    {
        State.Changed -= OnChanged;
    }

    /// <inheritdoc />
    protected override void OnInitialized()
    {
        State.Changed += OnChanged;
    }

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        if (_built is { } built && ReferenceEquals(built.Item1, Document) && ReferenceEquals(built.Item2, Clip) && built.Item3 == Geometry)
        {
            return;
        }
        _built = (Document, Clip, Geometry);
        TempoMap tempo = TempoMap.From(Document);
        double beatsPerBar = MeterRules.BeatsPerBar(Document.TimeSignature);
        double startBeat = tempo.BeatsAt(Clip.Start);
        List<(double, string, bool)> labels = [];
        double lastX = double.NegativeInfinity;
        for (double bar = Math.Ceiling((startBeat / beatsPerBar) - 1e-9); labels.Count < LabelLimit; bar++)
        {
            double time = tempo.TimeAtBeat(bar * beatsPerBar);
            if (time > Clip.Start + Clip.Length + 1e-9)
            {
                break;
            }
            double x = Geometry.X(time - Clip.Start);
            if (x - lastX >= MinLabelPixels)
            {
                labels.Add((x, ((int)bar + 1).ToString(CultureInfo.InvariantCulture), (int)bar % 4 == 0));
                lastX = x;
            }
        }
        _labels = labels;
    }

    private void OnChanged() => _ = InvokeAsync(StateHasChanged);

    // A press puts the insert point on the grid line nearest to it.
    private void Press(PointerEventArgs press)
    {
        if (press.Button != 0 || View.Bounds("roll-ruler") is not { } box)
        {
            return;
        }
        double seconds = new GridSnap(Document).Snap(Clip.Start + Geometry.Seconds(press.ClientX - box.Left)) - Clip.Start;
        State.MoveInsert(Math.Clamp(seconds, 0, Clip.Length));
    }
}
