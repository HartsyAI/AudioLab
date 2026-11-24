using System.Globalization;
using AudioLab.Web.Components;
using AudioLab.Web.Services;
using Microsoft.AspNetCore.Components;

namespace AudioLab.Web.Screens;

/// <summary>The status bar item of the engine: clips loading, a clip that cannot be played, and a miss when the engine cannot keep up.</summary>
public sealed partial class PlaybackStatus : IDisposable
{
    private string? _text;
    private bool _alert;
    private string? _engine;
    private StatusDot _dot = StatusDot.Caution;

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
        (string? text, StatusDot dot, bool alert) = Describe();
        string? engine = Engine();
        if (text != _text || dot != _dot || alert != _alert || engine != _engine)
        {
            _ = InvokeAsync(() =>
            {
                _text = text;
                _alert = alert;
                _dot = dot;
                _engine = engine;
                StateHasChanged();
            });
        }
    }

    // What the engine is running at and how it is doing, for anyone who wants the numbers behind a miss.
    private string? Engine() => Playback.Phase == PlaybackPhase.Ready
        ? string.Create(
            CultureInfo.InvariantCulture,
            $"Engine {(Playback.CompiledAhead ? "ahead-of-time" : "interpreted")}, {Playback.SampleRate / 1000.0:0.#} kHz, "
            + $"{Playback.BufferMilliseconds:0} ms buffer, {Playback.Dropouts} dropouts, "
            + $"render {Playback.LongestRenderMilliseconds:0.#} ms, gap {Playback.LongestGapMilliseconds:0} ms{Clock()}")
        : null;

    // The audio clock against the wall clock, once ten seconds have been compared; 100% is right.
    private string Clock() => Playback.ClockRatio is { } ratio
        ? ", clock " + Math.Round(ratio * 100).ToString(CultureInfo.InvariantCulture) + "%"
        : string.Empty;

    private string Interpreted() => Playback.CompiledAhead ? string.Empty : " (the engine is interpreted: docs/engine-aot.md)";

    // A miss is a caution, never red: red means recording or destroying. An alert is what a screen reader is told about as it comes;
    // the progress of loading and starting is not, which would be too much.
    private (string? Text, StatusDot Dot, bool Alert) Describe()
    {
        if (Playback.Problem is { } problem)
        {
            return (problem, StatusDot.Caution, true);
        }
        if (Playback.Health == PlaybackHealth.Dropping)
        {
            return ($"Playback is dropping out: the audio engine could not keep up{Interpreted()}", StatusDot.Caution, true);
        }
        if (Playback.Health == PlaybackHealth.Strained)
        {
            return ($"Playback is close to dropping out{Interpreted()}", StatusDot.Caution, true);
        }
        if (Playback.Health == PlaybackHealth.Drifting)
        {
            return (Playback.HealthMessage ?? "Playback is at the wrong speed", StatusDot.Caution, true);
        }
        if (Playback.LoadWarning is { } warning)
        {
            return (warning, StatusDot.Caution, false);
        }
        if (Playback.Phase == PlaybackPhase.Starting)
        {
            return ("Starting the audio engine", StatusDot.Caution, false);
        }
        string plural = Playback.Loading == 1 ? string.Empty : "s";
        return Playback.Loading > 0 ? ($"Loading {Playback.Loading} clip{plural}", StatusDot.Caution, false) : (null, StatusDot.Go, false);
    }
}
