using AudioLab.Core.Transcription;

namespace AudioLab.Server.Transcription;

/// <summary>The registry of D98: each entry the server knows, joined with the model installed for it.</summary>
public sealed class SpeechModels(IEnumerable<SpeechModelEntry> entries, IEnumerable<ISpeechModel> models) : ISpeechModels
{
    private readonly Dictionary<string, ISpeechModel> _installed = BuildInstalled(models);
    private readonly List<string> _choosable = entries
        .Where(entry => entry.Capability == SpeechModelCatalog.SpeechCapability && models.Any(model => model.ModelId == entry.Id))
        .Select(entry => entry.Id)
        .Distinct(StringComparer.Ordinal)
        .ToList();

    /// <inheritdoc />
    public IReadOnlyList<string> Choosable => _choosable;

    /// <inheritdoc />
    public ISpeechModel? Find(string modelId)
    {
        ArgumentNullException.ThrowIfNull(modelId);
        return _installed.GetValueOrDefault(modelId);
    }

    private static Dictionary<string, ISpeechModel> BuildInstalled(IEnumerable<ISpeechModel> models)
    {
        Dictionary<string, ISpeechModel> installed = new(StringComparer.Ordinal);
        foreach (ISpeechModel model in models)
        {
            installed.TryAdd(model.ModelId, model);
        }
        return installed;
    }
}
