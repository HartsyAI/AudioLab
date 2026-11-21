using Bit.Bheadless;

namespace AudioLab.Web.Components;

/// <summary>Raises toasts from any component or service; <see cref="AlToastHost"/> shows them.</summary>
public sealed class AlToastService(BitBhToaster toaster)
{
    /// <summary>Shows a toast that says what happened, with Undo when there is one. An error stays until dismissed.</summary>
    public string Show(AlToastKind kind, string message, Func<Task>? undo = null)
    {
        BitBhToastOptions options = new()
        {
            Variant = kind.ToString().ToLowerInvariant(),
            Title = message,
            Foreground = kind is AlToastKind.Warning or AlToastKind.Error,
            Duration = kind == AlToastKind.Error ? 0 : null,
            ActionLabel = undo is null ? null : "Undo",
            OnAction = undo,
        };
        return toaster.Show(options);
    }
}
