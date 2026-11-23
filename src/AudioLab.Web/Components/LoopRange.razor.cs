using AudioLab.Web.Services;
using AudioLab.Web.Session;
using Microsoft.AspNetCore.Components;

namespace AudioLab.Web.Components;

/// <summary>The catalogue loop range: an accent band between two accent edges in the ruler and a wash over the lanes, while the loop is on.</summary>
public sealed partial class LoopRange : IDisposable
{
    private (double Start, double End)? _drawn;

    /// <summary>Which part this draws.</summary>
    [Parameter, EditorRequired]
    public LoopRangePart Part { get; set; }

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
        _drawn = Playback.LoopRange;
        Playback.Changed += OnChanged;
    }

    private void OnChanged()
    {
        if (Playback.LoopRange != _drawn)
        {
            _ = InvokeAsync(() =>
            {
                _drawn = Playback.LoopRange;
                StateHasChanged();
            });
        }
    }
}
