namespace AudioLab.Web.Session;

/// <summary>Which tab of the bottom panel is open, held where the arrangement can change it and the panel can follow.</summary>
public sealed class BottomPanelState
{
    /// <summary>The mixer tab.</summary>
    public const string Mixer = "Mixer";

    /// <summary>The piano roll tab.</summary>
    public const string PianoRoll = "Piano roll";

    /// <summary>Raised when the open tab changed.</summary>
    public event Action? Changed;

    /// <summary>The name of the open tab.</summary>
    public string Active { get; private set; } = Mixer;

    /// <summary>Opens a tab by name.</summary>
    public void Show(string tab)
    {
        ArgumentNullException.ThrowIfNull(tab);
        if (tab == Active)
        {
            return;
        }
        Active = tab;
        Changed?.Invoke();
    }

    /// <summary>Goes back to the first tab, as a project that is opened starts.</summary>
    public void Reset() => Show(Mixer);
}
