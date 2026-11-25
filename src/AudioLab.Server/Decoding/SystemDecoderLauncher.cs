using System.ComponentModel;
using System.Diagnostics;

namespace AudioLab.Server.Decoding;

/// <summary>Starts the decoder with <see cref="Process"/>: an argument list, no shell, no window, and its output redirected.</summary>
public sealed class SystemDecoderLauncher : IDecoderLauncher
{
    /// <inheritdoc />
    public IDecoderProcess Start(string fileName, IReadOnlyList<string> arguments, string workingDirectory)
    {
        ArgumentException.ThrowIfNullOrEmpty(fileName);
        ArgumentNullException.ThrowIfNull(arguments);
        ProcessStartInfo info = new(fileName)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            RedirectStandardInput = true,
            WorkingDirectory = workingDirectory,
        };
        foreach (string argument in arguments)
        {
            info.ArgumentList.Add(argument);
        }
        Process process = Process.Start(info) ?? throw new InvalidOperationException("The decoder process did not start.");
        process.StandardInput.Close();
        return new SystemDecoderProcess(process);
    }

    private sealed class SystemDecoderProcess(Process process) : IDecoderProcess
    {
        public bool HasExited => process.HasExited;

        public int ExitCode => process.ExitCode;

        public Stream StandardError => process.StandardError.BaseStream;

        public Stream StandardOutput => process.StandardOutput.BaseStream;

        public Task WaitForExitAsync(CancellationToken cancellationToken) => process.WaitForExitAsync(cancellationToken);

        public bool KillTree()
        {
            try
            {
                if (!process.HasExited)
                {
                    process.Kill(entireProcessTree: true);
                }
                return true;
            }
            catch (Exception exception) when (exception is InvalidOperationException or Win32Exception)
            {
                return process.HasExited;
            }
        }

        public void Dispose() => process.Dispose();
    }
}
