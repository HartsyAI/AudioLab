using AudioLab.Web.Services;

namespace AudioLab.Web.Screens;

/// <summary>What the mixer can do to the master; every edit answers with what it came to, so a caller can follow it.</summary>
public sealed class MasterActions
{
    private const string NotAvailable = "Not available.";

    /// <summary>Sets the master fader, from 0 up.</summary>
    public Func<double, Task<EditResult>> SetVolume { get; init; } = _ => Task.FromResult(EditResult.Refused(NotAvailable));

    /// <summary>Switches the master limiter on or off.</summary>
    public Func<bool, Task<EditResult>> SetLimiter { get; init; } = _ => Task.FromResult(EditResult.Refused(NotAvailable));

    /// <summary>The actions of an editor; <paramref name="show"/> tells the user what a refusal said.</summary>
    public static MasterActions For(MasterEditor editor, Func<Task<EditResult>, Task<EditResult>> show)
    {
        ArgumentNullException.ThrowIfNull(editor);
        ArgumentNullException.ThrowIfNull(show);
        return new MasterActions
        {
            SetVolume = gain => show(editor.SetVolumeAsync(gain)),
            SetLimiter = enabled => show(editor.SetLimiterAsync(enabled)),
        };
    }
}
