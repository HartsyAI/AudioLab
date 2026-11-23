using System.Globalization;
using AudioLab.Core.Model;
using AudioLab.Web.Components;
using AudioLab.Web.Services;
using AudioLab.Web.Session;
using Microsoft.AspNetCore.Components;

namespace AudioLab.Web.Screens;

/// <summary>The counter of the transport bar: the time and the bar and beat at the playhead, and the tempo and meter of the project.</summary>
public sealed partial class TransportCounter : IDisposable
{
    private IReadOnlyList<CounterCell> _cells = [];

    /// <summary>The document, for the tempo, the meter and the bar grid.</summary>
    [Parameter, EditorRequired]
    public ProjectDocument Document { get; set; } = null!;

    [Inject]
    private PlaybackService Playback { get; set; } = default!;

    /// <inheritdoc />
    public void Dispose()
    {
        Playback.Changed -= OnChanged;
    }

    /// <inheritdoc />
    protected override void OnInitialized()
    {
        Playback.Changed += OnChanged;
    }

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        _cells = Cells();
    }

    // The position changes forty times a second and the text far less often than that, so only a new text is drawn.
    private void OnChanged()
    {
        IReadOnlyList<CounterCell> now = Cells();
        if (!now.SequenceEqual(_cells))
        {
            _ = InvokeAsync(() =>
            {
                _cells = now;
                StateHasChanged();
            });
        }
    }

    private IReadOnlyList<CounterCell> Cells() =>
    [
        new("TIME", TimeText.Counter(Playback.Position), true),
        new("BARS", BarGrid.Position(Document, Playback.Position), true),
        new("TEMPO", Document.Bpm.ToString("0.00", CultureInfo.InvariantCulture)),
        new("METER", $"{Document.TimeSignature.Numerator}/{Document.TimeSignature.Denominator}"),
    ];
}
