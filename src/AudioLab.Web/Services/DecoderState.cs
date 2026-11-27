namespace AudioLab.Web.Services;

/// <summary>Whether the audio decoder is installed on the server, shared by the install button and the file chooser of the Media
/// tab, so the chooser offers the compressed types as soon as an install finishes.</summary>
public sealed class DecoderState
{
    /// <summary>Raised when <see cref="Installed"/> changes.</summary>
    public event Action? Changed;

    /// <summary>Whether a verified decoder is in place.</summary>
    public bool Installed { get; private set; }

    /// <summary>Records what the server last reported, and tells the listeners when it is a change.</summary>
    public void Set(bool installed)
    {
        if (Installed == installed)
        {
            return;
        }
        Installed = installed;
        Changed?.Invoke();
    }
}
