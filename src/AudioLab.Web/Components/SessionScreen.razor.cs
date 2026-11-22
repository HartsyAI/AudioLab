using AudioLab.Web.Session;
using Microsoft.AspNetCore.Components;

namespace AudioLab.Web.Components;

/// <summary>The session screen: transport, arrangement, bottom panel and status bar.</summary>
public sealed partial class SessionScreen : ComponentBase
{
    [Inject]
    private SessionModel Session { get; set; } = null!;

    /// <inheritdoc />
    protected override bool ShouldRender() => false;
}
