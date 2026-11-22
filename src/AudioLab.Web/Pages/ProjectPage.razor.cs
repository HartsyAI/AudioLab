using Microsoft.AspNetCore.Components;

namespace AudioLab.Web.Pages;

/// <summary>The route of one open project; the session screen replaces this placeholder.</summary>
public sealed partial class ProjectPage
{
    /// <summary>The project's id, from the address.</summary>
    [Parameter]
    public string Id { get; set; } = string.Empty;
}
