using AudioLab.Core.Model;
using AudioLab.Web.Session;
using Microsoft.AspNetCore.Components;

namespace AudioLab.Web.Screens;

/// <summary>The bottom panel: tabs for the selection's tools. The mixer and the piano roll have content; every other tab says so.</summary>
public sealed partial class BottomPanel : IDisposable
{
    /// <summary>The document.</summary>
    [Parameter, EditorRequired]
    public ProjectDocument Document { get; set; } = null!;

    /// <summary>What the screen can do to the project.</summary>
    [Parameter, EditorRequired]
    public SessionActions Actions { get; set; } = null!;

    /// <summary>The selected track, or null.</summary>
    [Parameter]
    public TrackId? Selected { get; set; }

    [Inject]
    private BottomPanelState Panel { get; set; } = default!;

    private static IReadOnlyList<string> Names { get; } =
        [BottomPanelState.Mixer, "Clip editor", BottomPanelState.PianoRoll, "Effects", "Instruments", "Stems", "Score", "Lyrics", "Voice agent"];

    /// <inheritdoc />
    public void Dispose()
    {
        Panel.Changed -= OnPanelChanged;
    }

    /// <inheritdoc />
    protected override void OnInitialized()
    {
        Panel.Changed += OnPanelChanged;
    }

    private string Slug => Panel.Active.ToLowerInvariant().Replace(" ", "-", StringComparison.Ordinal);

    private void OnPanelChanged() => _ = InvokeAsync(StateHasChanged);
}
