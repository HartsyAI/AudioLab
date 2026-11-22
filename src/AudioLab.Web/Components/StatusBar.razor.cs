using System.Globalization;
using AudioLab.Web.Playback;
using Microsoft.AspNetCore.Components;

namespace AudioLab.Web.Components;

/// <summary>The status bar: connection, jobs, and the engine's rate, buffer fill, load and dropouts.</summary>
public sealed partial class StatusBar : ComponentBase, IDisposable
{
    private DateTime _lastRender = DateTime.MinValue;

    [Inject]
    private PlaybackRun Playback { get; set; } = null!;

    [Inject]
    private SpikeOptions Options { get; set; } = null!;

    private string RateText => Playback.SampleRate > 0 ? string.Create(CultureInfo.InvariantCulture, $"{Playback.SampleRate / 1000:0.#} kHz") : "no engine";

    private string FillText => Playback.Stats.Primed
        ? string.Create(CultureInfo.InvariantCulture, $"Buffer {Playback.Stats.Fill * 1000 / Playback.SampleRate:0} ms")
        : "Buffer empty";

    private string LoadText => string.Create(CultureInfo.InvariantCulture, $"Engine {Playback.Utilisation * 100:0}%");

    private string DropoutText => string.Create(CultureInfo.InvariantCulture, $"{Playback.Stats.Events} dropouts");

    /// <inheritdoc />
    public void Dispose() => Playback.Changed -= OnChanged;

    /// <inheritdoc />
    protected override void OnInitialized() => Playback.Changed += OnChanged;

    private Task Start() => Playback.RunAsync();

    private void OnChanged()
    {
        if (DateTime.UtcNow - _lastRender < TimeSpan.FromSeconds(1))
        {
            return;
        }
        _lastRender = DateTime.UtcNow;
        _ = InvokeAsync(StateHasChanged);
    }
}
