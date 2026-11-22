using Microsoft.AspNetCore.Components;

namespace AudioLab.Web.Components;

/// <summary>What a control does when it is clicked: nothing while it is disabled, so a disabled control that stays focusable never acts.</summary>
internal static class Activation
{
    /// <summary>Raises the callback unless the control is disabled; the markup also stops the click's default action, so a submit button submits nothing.</summary>
    /// <param name="disabled">Whether the control does nothing now.</param>
    /// <param name="callback">What the control raises when it is activated.</param>
    /// <returns>The task of the callback, or a completed one.</returns>
    public static Task RunAsync(bool disabled, EventCallback callback)
    {
        return disabled ? Task.CompletedTask : callback.InvokeAsync();
    }
}
