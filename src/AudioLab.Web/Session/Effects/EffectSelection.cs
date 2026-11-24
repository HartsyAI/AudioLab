using AudioLab.Core.Model;

namespace AudioLab.Web.Session.Effects;

/// <summary>Which effect of the selected track's chain the effects tab shows the settings of: a view state of this client.</summary>
public sealed class EffectSelection
{
    /// <summary>Raised when the selection changed.</summary>
    public event Action? Changed;

    private bool _focusRequested;

    /// <summary>The selected effect, or null for the first of the chain.</summary>
    public EffectId? Effect { get; private set; }

    /// <summary>Asks the effects tab to move the keyboard focus to the selected effect when it is drawn.</summary>
    public void RequestFocus() => _focusRequested = true;

    /// <summary>Whether focus was asked for since the last call; asking is spent by answering.</summary>
    public bool TakeFocusRequest()
    {
        bool asked = _focusRequested;
        _focusRequested = false;
        return asked;
    }

    /// <summary>Selects an effect.</summary>
    public void Select(EffectId effect)
    {
        if (Effect == effect)
        {
            return;
        }
        Effect = effect;
        Changed?.Invoke();
    }

    /// <summary>The effect of the chain that is selected: the chosen one when the chain holds it, else the first, else none.</summary>
    public Effect? Of(Track track)
    {
        ArgumentNullException.ThrowIfNull(track);
        return track.Effects.FirstOrDefault(effect => effect.Id == Effect) ?? track.Effects.FirstOrDefault();
    }
}
