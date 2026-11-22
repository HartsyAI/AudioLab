using Bit.Bheadless;

namespace AudioLab.Web.Components;

/// <summary>Raises toasts from any component or service; <see cref="ToastHost"/> shows them.</summary>
public sealed class ToastService(BitBhToaster toaster)
{
    /// <summary>Shows a toast that says what happened, with Undo when there is one. An error stays until dismissed.</summary>
    /// <remarks>A toast raised while a modal dialog is open cannot be pressed until it closes, so one raised there must not rely on Undo.</remarks>
    public string Show(ToastKind kind, string message, Func<Task>? undo = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(message);
        BitBhToastOptions options = new()
        {
            Variant = kind.ToString().ToLowerInvariant(),
            Title = message,
            Foreground = kind is ToastKind.Warning or ToastKind.Error,
            Duration = kind == ToastKind.Error ? 0 : null,
            ActionLabel = undo is null ? null : "Undo",
            OnAction = undo,
        };
        return toaster.Show(options);
    }
}
