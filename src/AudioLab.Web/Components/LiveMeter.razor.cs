using AudioLab.Core.Model;
using AudioLab.Core.Playback;
using AudioLab.Web.Services;
using AudioLab.Web.Session;
using Microsoft.AspNetCore.Components;

namespace AudioLab.Web.Components;

/// <summary>The catalogue meter fed from the engine: a track level, or the master level, drawn as it arrives.</summary>
public sealed partial class LiveMeter : IDisposable
{
    private double _left;
    private double _right;

    /// <summary>The track to show, or null for the master.</summary>
    [Parameter]
    public TrackId? Track { get; set; }

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

    private void OnChanged()
    {
        LevelPair level = Track is { } track ? Playback.LevelOf(track) : Playback.Master;
        double left = MeterScale.Height(level.Left);
        double right = MeterScale.Height(level.Right);
        if (Math.Abs(left - _left) > 0.004 || Math.Abs(right - _right) > 0.004)
        {
            _ = InvokeAsync(() =>
            {
                _left = left;
                _right = right;
                StateHasChanged();
            });
        }
    }
}
