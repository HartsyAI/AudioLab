namespace AudioLab.Server.Decoding;

/// <summary>A started decoder process: its error output, and the means to wait for it or stop it.</summary>
public interface IDecoderProcess : IDisposable
{
    /// <summary>Whether the process has exited.</summary>
    public bool HasExited { get; }

    /// <summary>The exit code, once it has exited.</summary>
    public int ExitCode { get; }

    /// <summary>The process's error output; read it so the process never blocks on a full pipe.</summary>
    public Stream StandardError { get; }

    /// <summary>The process's standard output, which the decoder does not use; it is drained and dropped.</summary>
    public Stream StandardOutput { get; }

    /// <summary>Waits until the process has exited.</summary>
    public Task WaitForExitAsync(CancellationToken cancellationToken);

    /// <summary>Kills the process and everything it started. Returns false when it could not be killed.</summary>
    public bool KillTree();
}
