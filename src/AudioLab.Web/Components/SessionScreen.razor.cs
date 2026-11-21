using AudioLab.Web.Bench;
using AudioLab.Web.Session;
using Microsoft.AspNetCore.Components;

namespace AudioLab.Web.Components;

/// <summary>The session screen: transport, arrangement, bottom panel and status bar.</summary>
public sealed partial class SessionScreen : ComponentBase
{
    [Inject]
    private SessionModel Session { get; set; } = null!;

    [Inject]
    private SpikeOptions Options { get; set; } = null!;

    [Inject]
    private BenchRunner Bench { get; set; } = null!;

    /// <inheritdoc />
    protected override bool ShouldRender() => false;

    /// <inheritdoc />
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender && Options.Bench)
        {
            await Task.Delay(3000);
            await Bench.RunAsync();
        }
    }
}
