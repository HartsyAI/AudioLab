using AudioLab.Web.Services;
using AudioLab.Web.Session;
using Microsoft.AspNetCore.Components;

namespace AudioLab.Web.Components;

/// <summary>The catalogue playhead: a line down the lanes and a cap in the bars row, at the engine position; drawn, not transitioned.</summary>
public sealed partial class Playhead : IDisposable
{
    private double _left;

    /// <summary>Which part this draws.</summary>
    [Parameter, EditorRequired]
    public PlayheadPart Part { get; set; }

    /// <summary>The scale from seconds to pixels.</summary>
    [Parameter]
    public TimelineGeometry Geometry { get; set; } = TimelineGeometry.Default;

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
        _left = Place();
        Playback.Changed += OnChanged;
    }

    // Only a move of a whole pixel is drawn: the status arrives 25 times a second, and most of them are the same pixel.
    private void OnChanged()
    {
        if (Place() != _left)
        {
            _ = InvokeAsync(() =>
            {
                _left = Place();
                StateHasChanged();
            });
        }
    }

    private double Place() => Math.Round(Geometry.X(Playback.Position));
}
