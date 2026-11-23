using AudioLab.Audio.Live;
using AudioLab.Audio.Tests.Support;
using AudioLab.Core.Model;

namespace AudioLab.Audio.Tests;

public sealed class RingProtocolTests
{
    private const int Rate = 8000;
    private const int Chunk = 512;
    private const int Slots = 4;
    private const int Quantum = 128;

    private sealed class ConstantClips : IClipSource
    {
        public Task<PcmAudio> LoadAsync(MediaId id, Func<CancellationToken, Task> pause, CancellationToken cancellationToken) =>
            Task.FromResult(Signals.Constant(0.25f, 20, Rate));
    }

    private static async Task<LiveHost> HostWithPlanAsync()
    {
        LiveHost host = new(new ConstantClips(), Rate, Chunk, _ => Task.CompletedTask);
        Track track = TestProject.Track("t", TrackKind.Audio, TestProject.Clip("c", "m", 0, 20));
        await host.SetDocumentAsync(ProjectJson.Write(TestProject.Create(track)), 1);
        return host;
    }

    // One quantum of the audio thread, then the worker answers every credit it is owed unless it is stalled.
    private static void Tick(LiveHost host, WorkletModel model, bool workerRuns)
    {
        model.Process(Quantum);
        while (workerRuns && model.Credits > 0)
        {
            model.Push((byte[])host.RenderChunk().Clone());
        }
    }

    private static WorkletModel Started(LiveHost host)
    {
        WorkletModel model = new(Chunk, Slots);
        model.Connect();
        Tick(host, model, workerRuns: true);
        return model;
    }

    [Fact]
    public async Task AWorkerThatKeepsUpFeedsTheRingThroughEveryTransportChangeWithoutADropoutOrAnOverflow()
    {
        LiveHost host = await HostWithPlanAsync();
        WorkletModel model = Started(host);

        Dictionary<int, Action> changes = new()
        {
            [40] = host.Play,
            [140] = () => host.Seek(3),
            [200] = host.Pause,
            [260] = host.Play,
            [330] = host.Stop,
        };

        for (int tick = 0; tick < 400; tick++)
        {
            if (changes.TryGetValue(tick, out Action? change))
            {
                change();
            }
            Tick(host, model, workerRuns: true);
        }

        Assert.True(model.Primed);
        Assert.Equal(0, model.Events);
        Assert.Equal(0, model.Missed);
        Assert.Equal(0, model.Credits);
        Assert.InRange(model.Fill, (Slots - 1) * Chunk, Slots * Chunk);
    }

    [Fact]
    public async Task SilenceTheEngineChoseIsNotADropoutEvenWhenTheWorkerStalls()
    {
        LiveHost host = await HostWithPlanAsync();
        WorkletModel model = Started(host);

        for (int tick = 0; tick < 100; tick++)
        {
            Tick(host, model, workerRuns: false);
        }

        Assert.Equal(0, model.Events);
        Assert.Equal(0, model.Missed);
    }

    [Fact]
    public async Task AStallWhilePlayingIsOneEventAndCountsTheFramesOfSilence()
    {
        LiveHost host = await HostWithPlanAsync();
        WorkletModel model = Started(host);
        host.Play();
        for (int tick = 0; tick < 50; tick++)
        {
            Tick(host, model, workerRuns: true);
        }

        for (int tick = 0; tick < 40; tick++)
        {
            Tick(host, model, workerRuns: false);
        }
        for (int tick = 0; tick < 50; tick++)
        {
            Tick(host, model, workerRuns: true);
        }

        Assert.Equal(1, model.Events);
        Assert.True(model.Missed > 0);
        Assert.Equal(0, model.Fill % Quantum);
    }

    [Fact]
    public async Task ARingThatFitsTheBudgetRidesOutAStallShorterThanItHolds()
    {
        LiveHost host = await HostWithPlanAsync();
        WorkletModel model = Started(host);
        host.Play();
        for (int tick = 0; tick < 50; tick++)
        {
            Tick(host, model, workerRuns: true);
        }

        // Four chunks are 16 quanta; the ring is never below three, so a stall of 8 quanta leaves audio in it.
        for (int tick = 0; tick < 8; tick++)
        {
            Tick(host, model, workerRuns: false);
        }
        for (int tick = 0; tick < 50; tick++)
        {
            Tick(host, model, workerRuns: true);
        }

        Assert.Equal(0, model.Events);
    }

    [Fact]
    public async Task ThePositionTheWorkletReportsFollowsThePlayheadThroughASeekAndReadsAtMostAChunkPastALoopEnd()
    {
        LiveHost host = await HostWithPlanAsync();
        WorkletModel model = Started(host);
        host.SetLoop(true, 1, 2);
        host.Seek(1.5);
        host.Play();
        List<double> seen = [];

        for (int tick = 0; tick < 400; tick++)
        {
            Tick(host, model, workerRuns: true);
            seen.Add(model.Position);
        }

        Assert.Contains(seen, position => position >= 1.9 * Rate && position < 2 * Rate);
        // The chunk that holds the wrap is tagged with its first frame, so its tail reads up to a chunk past the loop end; the page folds it back.
        Assert.All(seen.Skip(60), position => Assert.InRange(position, 1 * Rate, (2 * Rate) + Chunk));
    }
}
