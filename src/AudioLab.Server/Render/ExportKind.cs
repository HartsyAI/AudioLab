namespace AudioLab.Server.Render;

/// <summary>What an export makes.</summary>
public enum ExportKind
{
    /// <summary>The mixdown, as one WAV file.</summary>
    Mix,

    /// <summary>One WAV file for each track with sound and each fed bus, in a ZIP file.</summary>
    Stems,
}
