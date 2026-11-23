using AudioLab.Web.Media;
using Microsoft.AspNetCore.Components;

namespace AudioLab.Web.Screens;

/// <summary>The label that follows the pointer while a media file is dragged, and says why the file cannot land where the pointer is.</summary>
public sealed partial class MediaDragLayer : IDisposable
{
    [Inject]
    private MediaDragState State { get; set; } = default!;

    /// <inheritdoc />
    public void Dispose()
    {
        State.Changed -= OnChanged;
    }

    /// <inheritdoc />
    protected override void OnInitialized()
    {
        State.Changed += OnChanged;
    }

    private void OnChanged() => StateHasChanged();
}
