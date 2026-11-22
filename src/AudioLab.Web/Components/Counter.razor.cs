using Microsoft.AspNetCore.Components;

namespace AudioLab.Web.Components;

/// <summary>One well holding the transport readouts, cells split by a panel-coloured line.</summary>
public sealed partial class Counter : ComponentBase
{
    /// <summary>The cells.</summary>
    [Parameter]
    public RenderFragment? ChildContent { get; set; }
}
