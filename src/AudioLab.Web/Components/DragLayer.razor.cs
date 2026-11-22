using System.Globalization;
using AudioLab.Web.Session;
using Microsoft.AspNetCore.Components;

namespace AudioLab.Web.Components;

/// <summary>Shows a drag in progress as DOM: the lifted clip under the pointer and where it would land. Only this layer redraws on a pointer move.</summary>
public sealed partial class DragLayer : ComponentBase, IDisposable
{
    [Inject]
    private DragController Drag { get; set; } = null!;

    [Inject]
    private TimelineView View { get; set; } = null!;

    /// <inheritdoc />
    public void Dispose() => Drag.Changed -= OnChanged;

    /// <inheritdoc />
    protected override void OnInitialized() => Drag.Changed += OnChanged;

    private static string Format(double value) => value.ToString("0.##", CultureInfo.InvariantCulture);

    private void OnChanged(ClipModel? clip) => InvokeAsync(StateHasChanged);
}
