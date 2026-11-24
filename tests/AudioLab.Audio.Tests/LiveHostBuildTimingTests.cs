using System.Buffers.Binary;
using AudioLab.Audio.Live;
using AudioLab.Audio.Tests.Support;
using AudioLab.Core.Model;
using AudioLab.Core.Rules;

namespace AudioLab.Audio.Tests;

/// <summary>Holds the tests of what a chunk waits for when the plan is swapped, on a clock the test owns: a build must not stop them (D60).</summary>
public sealed class LiveHostBuildTimingTests
{
    private const int Rate = 8000;
    private const int Chunk = 128;
    private const double ChunkMilliseconds = Chunk * 1000.0 / Rate;
    private const double InsertMilliseconds = 8;
    private const int Tracks = 8;

    private sealed class Clips : IClipSource
    {
        public Task<PcmAudio> LoadAsync(MediaId id, Func<CancellationToken, Task> pause, CancellationToken cancellationToken) =>
            Task.FromResult(Signals.Constant(0.5f, 20, Rate));
    }

    // The worker's loop as the test models it: time moves only when something is built, and a chunk is made when the loop gets control at or
    // after the time it is due, which is at a pause of the build or after it.
    private sealed class Worker
    {
        private readonly SteppingTime _clock = new(TimeSpan.Zero);
        private double _now;
        private double _due;

        public Worker()
        {
            Host = new LiveHost(new Clips(), Rate, Chunk, _ =>
            {
                OnPause?.Invoke();
                Serve();
                return Task.CompletedTask;
            })
            {
                InsertBuilder = (effect, context) =>
                {
                    double cost = Cost;
                    _now += cost;
                    _clock.Advance(TimeSpan.FromMilliseconds(cost));
                    Built += cost > 0 ? 1 : 0;
                    return InsertFactory.Create(effect, context);
                },
                Time = _clock,
            };
        }

        public LiveHost Host { get; }

        public double Cost { get; set; }

        public Action? OnPause { get; set; }

        public int Built { get; private set; }

        public double Late { get; private set; }

        public double Now => _now;

        public List<(bool Audible, long Revision)> Chunks { get; } = [];

        public void Serve()
        {
            while (_due <= _now)
            {
                Late = Math.Max(Late, _now - _due);
                byte[] chunk = Host.RenderChunk();
                Chunks.Add((BinaryPrimitives.ReadDoubleLittleEndian(chunk.AsSpan(8)) == 1, Host.Engine.Revision));
                _due += ChunkMilliseconds;
            }
        }
    }

    private static ProjectDocument Document(double pan)
    {
        Track[] tracks =
        [
            .. Enumerable.Range(0, Tracks).Select(i => TestProject.Track($"t{i}", TrackKind.Audio, TestProject.Clip($"c{i}", "m", 0, 20)) with
            {
                Pan = pan,
                Effects =
                [
                    EffectRules.Create(EffectKind.Eq, EffectId.From($"eq{i}")),
                    EffectRules.Create(EffectKind.Compressor, EffectId.From($"cp{i}")),
                ],
            }),
        ];
        return TestProject.Create(tracks);
    }

    private static Task Send(Worker worker, double pan, long revision) => worker.Host.SetDocumentAsync(ProjectJson.Write(Document(pan)), revision);

    private static async Task<Worker> PlayingAsync()
    {
        Worker worker = new();
        await Send(worker, 0, 1);
        worker.Host.Play();
        worker.Serve();
        worker.Cost = InsertMilliseconds;
        return worker;
    }

    [Fact]
    public async Task AChunkWaitsForTheDearestTrackOfANewPlanNotForAllOfIt()
    {
        Worker worker = await PlayingAsync();
        double before = worker.Now;
        int chunksBefore = worker.Chunks.Count;

        await Send(worker, 0.5, 2);
        worker.Serve();

        double build = worker.Now - before;
        double oneTrack = 2 * InsertMilliseconds;
        Assert.Equal(Tracks * oneTrack, build);
        Assert.True(build > 6 * ChunkMilliseconds, "the build must be long enough that stopping the chunks for all of it would show");
        Assert.True(worker.Late <= oneTrack, $"a chunk was {worker.Late} ms late; a track costs {oneTrack}");
        Assert.True(worker.Chunks.Count - chunksBefore >= (int)(build / ChunkMilliseconds));
    }

    [Fact]
    public async Task TheOldPlanKeepsSoundingWhileTheNewOneIsBuiltAndTheSwapIsOneStep()
    {
        Worker worker = await PlayingAsync();
        int from = worker.Chunks.Count;

        await Send(worker, 0.5, 2);
        worker.Serve();
        worker.Host.RenderChunk();

        List<(bool Audible, long Revision)> during = [.. worker.Chunks.Skip(from)];
        Assert.All(during, chunk => Assert.True(chunk.Audible));
        Assert.True(during.Count(chunk => chunk.Revision == 1) >= 6, "the old plan keeps playing while the new one is built");
        long[] revisions = [.. during.Select(chunk => chunk.Revision)];
        Assert.Equal(revisions.OrderBy(revision => revision), revisions);
        Assert.Equal(2, worker.Host.Engine.Revision);
    }

    [Fact]
    public async Task ABuildOfADocumentThatANewerOneHasReplacedIsAbandonedAndNeverPlays()
    {
        Worker worker = await PlayingAsync();
        Task? third = null;
        worker.OnPause = () =>
        {
            worker.OnPause = null;
            third = Send(worker, -0.5, 3);
        };

        await Send(worker, 0.5, 2);
        await third!;
        worker.Serve();
        worker.Host.RenderChunk();

        Assert.Equal(3, worker.Host.Engine.Revision);
        Assert.DoesNotContain(worker.Chunks, chunk => chunk.Revision == 2);
        Assert.Equal(2 + (Tracks * 2), worker.Built);
    }
}
