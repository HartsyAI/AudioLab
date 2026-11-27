namespace AudioLab.Server.Decoding;

/// <summary>Starts the decoder process. Arguments are an argument array, never a shell string; a fake stands in for it in tests.</summary>
public interface IDecoderLauncher
{
    /// <summary>Starts <paramref name="fileName"/> with the given arguments, with no shell and no window.</summary>
    public IDecoderProcess Start(string fileName, IReadOnlyList<string> arguments, string workingDirectory);
}

