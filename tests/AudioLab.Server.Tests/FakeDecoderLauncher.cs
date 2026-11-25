using AudioLab.Audio.Tests.Support;
using AudioLab.Core.Model;
using AudioLab.Server.Decoding;

namespace AudioLab.Server.Tests;

/// <summary>
/// Stands in for the ffmpeg process. It writes a valid WAV, exits with an error and writes nothing, or never exits until it is killed,
/// and it counts the starts and the kills so a test can see what the decoder did.
/// </summary>
internal sealed class FakeDecoderLauncher : IDecoderLauncher
{
    public enum Mode
    {
        Succeed,
        Fail,
        Hang,
    }

    public Mode Behaviour { get; set; } = Mode.Succeed;

    public int Starts { get; private set; }

    public int Kills { get; private set; }

    public IReadOnlyList<string> LastArguments { get; private set; } = [];

    public IDecoderProcess Start(string fileName, IReadOnlyList<string> arguments, string workingDirectory)
    {
        Starts++;
        LastArguments = arguments;
        // ffmpeg is given the output path as its last argument, so the fake writes there the way ffmpeg would.
        string output = arguments[^1];
        return Behaviour switch
        {
            Mode.Succeed => Succeeded(output),
            Mode.Fail => new FakeProcess(this, exited: true, exitCode: 1),
            _ => new FakeProcess(this, exited: false, exitCode: 0),
        };
    }

    private static FakeProcess Succeeded(string output)
    {
        File.WriteAllBytes(output, TestWav.Build(SampleEncoding.Pcm16, 2, 48000, 24000));
        return new FakeProcess(null, exited: true, exitCode: 0);
    }

    private sealed class FakeProcess : IDecoderProcess
    {
        private readonly FakeDecoderLauncher? _owner;
        private readonly TaskCompletionSource _exit = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public FakeProcess(FakeDecoderLauncher? owner, bool exited, int exitCode)
        {
            _owner = owner;
            HasExited = exited;
            ExitCode = exitCode;
            if (exited)
            {
                _exit.TrySetResult();
            }
        }

        public bool HasExited { get; private set; }

        public int ExitCode { get; private set; }

        public Stream StandardError { get; } = new MemoryStream();

        public Stream StandardOutput { get; } = new MemoryStream();

        public Task WaitForExitAsync(CancellationToken cancellationToken) => _exit.Task.WaitAsync(cancellationToken);

        public bool KillTree()
        {
            if (_owner is not null)
            {
                _owner.Kills++;
            }
            HasExited = true;
            ExitCode = -1;
            _exit.TrySetResult();
            return true;
        }

        public void Dispose()
        {
            StandardError.Dispose();
            StandardOutput.Dispose();
        }
    }
}
