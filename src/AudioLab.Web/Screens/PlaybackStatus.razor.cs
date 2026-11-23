using System.Globalization;
using AudioLab.Web.Components;
using AudioLab.Web.Services;
using Microsoft.AspNetCore.Components;

namespace AudioLab.Web.Screens;

/// <summary>The status bar item of the engine: clips loading, a clip that cannot be played, and a miss when the engine cannot keep up.</summary>
public sealed partial class PlaybackStatus : IDisposable
{
    private string? _text;
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
        (string? text, StatusDot dot) = Describe();
        string? engine = Engine();
        if (text != _text || dot != _dot || engine != _engine)
        {
            _ = InvokeAsync(() =>
            {
                _text = text;
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
            $"Engine {Playback.SampleRate / 1000.0:0.#} kHz, {Playback.BufferMilliseconds:0} ms buffer, {Playback.Dropouts} dropouts, "
            + $"render {Playback.LongestRenderMilliseconds:0.#} ms, gap {Playback.LongestGapMilliseconds:0} ms")
        : null;

    private string Interpreted() => Playback.CompiledAhead ? string.Empty : " (the engine is interpreted: docs/engine-aot.md)";

    // A miss is a caution, never red: red means recording or destroying.
    private (string? Text, StatusDot Dot) Describe()
    {
        if (Playback.Problem is { } problem)
        {
            return (problem, StatusDot.Caution);
        }
        if (Playback.Health == PlaybackHealth.Dropping)
        {
            return ($"Playback is dropping out: the audio engine could not keep up{Interpreted()}", StatusDot.Caution);
        }
        if (Playback.Health == PlaybackHealth.Strained)
        {
            return ($"Playback is close to dropping out{Interpreted()}", StatusDot.Caution);
        }
        if (Playback.LoadWarning is { } warning)
        {
            return (warning, StatusDot.Caution);
        }
        if (Playback.Phase == PlaybackPhase.Starting)
        {
            return ("Starting the audio engine", StatusDot.Caution);
        }
        string plural = Playback.Loading == 1 ? string.Empty : "s";
        return Playback.Loading > 0 ? ($"Loading {Playback.Loading} clip{plural}", StatusDot.Caution) : (null, StatusDot.Go);
    }
}
