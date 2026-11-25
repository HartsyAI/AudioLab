using AudioLab.Core.Transcription;

namespace AudioLab.Server.Transcription;

/// <summary>The speech models installed on this server, by id (D98). A model is used only when it is installed; the entries say what
/// each one is for.</summary>
public interface ISpeechModels
{
    /// <summary>The installed model with this id, or null when it is not installed.</summary>
    public ISpeechModel? Find(string modelId);

    /// <summary>The ids of the installed models that a user may choose, in catalog order.</summary>
    public IReadOnlyList<string> Choosable { get; }
}
