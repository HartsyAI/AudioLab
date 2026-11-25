using System.Diagnostics;
using AudioLab.Audio.Tests.Support;
using AudioLab.Core.Model;
using AudioLab.Server.Decoding;

namespace AudioLab.Server.Tests;

/// <summary>Stands in for the ffmpeg process. Each mode is one way a decode can end: a valid WAV, an error with no output, a WAV
/// that is not one, an error with a large stderr, or a process that never exits until it is killed. <see
/// cref="Mode.HangProcess"/> starts a real child process, so a test can check that the kill really ended it. Every
/// start and kill is counted.</summary>
internal sealed class FakeDecoderLauncher : IDecoderLauncher
{
    public enum Mode
    {
        Succeed,
        Fail,
        InvalidWav,
        NoisyStderr,
        Hang,
        HangProcess,
    }

    // A child process that keeps running for longer than any test waits, so only a kill ends it.
    private const string LongRunningFile = "ping.exe";
    private const string LongRunningArguments = "-n 300 127.0.0.1";

    public Mode Behaviour { get; set; } = Mode.Succeed;

    public int Starts { get; private set; }

    public int Kills { get; private set; }

    public IReadOnlyList<string> LastArguments { get; private set; } = [];

    public int? SpawnedProcessId { get; private set; }

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
            Mode.InvalidWav => WritesNotWav(output),
            Mode.NoisyStderr => new FakeProcess(this, exited: true, exitCode: 1, stderr: new string('e', 200 * 1024)),
            Mode.Hang => new FakeProcess(this, exited: false, exitCode: 0),
            _ => StartRealProcess(),
        };
    }

    private static FakeProcess Succeeded(string output)
    {
        File.WriteAllBytes(output, TestWav.Build(SampleEncoding.Pcm16, 2, 48000, 24000));
        return new FakeProcess(null, exited: true, exitCode: 0);
    }

    private static FakeProcess WritesNotWav(string output)
    {
        File.WriteAllBytes(output, "exit code zero, but this is not a RIFF file at all"u8.ToArray());
        return new FakeProcess(null, exited: true, exitCode: 0);
    }

    private RealProcess StartRealProcess()
    {
        Process process = Process.Start(new ProcessStartInfo(LongRunningFile, LongRunningArguments)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        }) ?? throw new InvalidOperationException("The test process did not start.");
        SpawnedProcessId = process.Id;
        return new RealProcess(this, process);
    }

    private sealed class FakeProcess : IDecoderProcess
    {
        private readonly FakeDecoderLauncher? _owner;
        private readonly TaskCompletionSource _exit = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public FakeProcess(FakeDecoderLauncher? owner, bool exited, int exitCode, string stderr = "")
        {
            _owner = owner;
            HasExited = exited;
            ExitCode = exitCode;
            StandardError = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(stderr));
            if (exited)
            {
                _exit.TrySetResult();
            }
        }

        public bool HasExited { get; private set; }

        public int ExitCode { get; private set; }

        public Stream StandardError { get; }

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

    private sealed class RealProcess(FakeDecoderLauncher owner, Process process) : IDecoderProcess
    {
        public bool HasExited => process.HasExited;

        public int ExitCode => process.ExitCode;

        public Stream StandardError => process.StandardError.BaseStream;

        public Stream StandardOutput => process.StandardOutput.BaseStream;

        public Task WaitForExitAsync(CancellationToken cancellationToken) => process.WaitForExitAsync(cancellationToken);

        public bool KillTree()
        {
            owner.Kills++;
            try
            {
                if (!process.HasExited)
                {
                    process.Kill(entireProcessTree: true);
                }
            }
            catch (InvalidOperationException)
            {
                // It exited between the check and the kill, which is what a kill is for.
            }
            return true;
        }

        public void Dispose() => process.Dispose();
    }
}
