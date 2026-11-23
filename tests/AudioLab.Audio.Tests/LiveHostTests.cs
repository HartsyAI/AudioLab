using System.Buffers.Binary;
using System.Text.Json;
using AudioLab.Audio.Live;
using AudioLab.Audio.Tests.Support;
using AudioLab.Core.Model;
using AudioLab.Core.Playback;

namespace AudioLab.Audio.Tests;

public sealed class LiveHostTests
{
    private const int Rate = 8000;
    private const int Chunk = 128;

    private sealed class FakeClips : IClipSource
    {
        public Dictionary<MediaId, PcmAudio> Audio { get; } = [];

        public List<MediaId> Fetched { get; } = [];

        public TaskCompletionSource? Gate { get; set; }

        public Exception? Failure { get; set; }

        public async Task<PcmAudio> LoadAsync(MediaId id, Func<CancellationToken, Task> pause, CancellationToken cancellationToken)
        {
            Fetched.Add(id);
            if (Gate is not null)
            {
                await Gate.Task;
            }
            return Failure is not null ? throw Failure : Audio[id];
        }
    }

    private static ProjectDocument Document(string name, params (string Track, string Media, bool Muted)[] clips)
    {
        Track[] tracks =
        [
            .. clips.Select(clip => TestProject.Track(clip.Track, TrackKind.Audio, TestProject.Clip($"c-{clip.Track}", clip.Media, 0, 2)) with
            {
                Muted = clip.Muted,
            }),
        ];
        return TestProject.Create(tracks) with { Name = name };
    }

    private static LiveHost HostOf(FakeClips clips) => new(clips, Rate, Chunk, _ => Task.CompletedTask);

    private static Task Send(LiveHost host, ProjectDocument document, long revision) =>
        host.SetDocumentAsync(ProjectJson.Write(document), revision);

    private static HostStatus Status(LiveHost host) =>
        JsonSerializer.Deserialize(host.ReadStatus(), PlaybackJsonContext.Default.HostStatus)!;

    private static FakeClips Clips(params string[] ids)
    {
        FakeClips clips = new();
        foreach (string id in ids)
        {
            clips.Audio[MediaId.From(id)] = Signals.Constant(0.5f, 2, Rate);
        }
        return clips;
    }

    [Fact]
    public async Task OnlyTheClipsThatWillBeHeardAreFetchedAndTheChunksCarryTheirTimelinePosition()
    {
        FakeClips clips = Clips("a", "b");
        LiveHost host = HostOf(clips);

        await Send(host, Document("p", ("t1", "a", false), ("t2", "b", true)), 1);
        host.Seek(1);
        host.Play();
        byte[] chunk = host.RenderChunk();

        Assert.Equal([MediaId.From("a")], clips.Fetched);
        Assert.Equal(Rate, BinaryPrimitives.ReadDoubleLittleEndian(chunk));
        Assert.Equal(1, Status(host).Revision);
        Assert.Equal(LiveHost.HeaderBytes + (Chunk * 2 * sizeof(float)), chunk.Length);
    }

    [Fact]
    public async Task ARenameOrTheClickDoesNotRebuildThePlan()
    {
        FakeClips clips = Clips("a");
        LiveHost host = HostOf(clips);
        ProjectDocument first = Document("one", ("t1", "a", false));

        await Send(host, first, 1);
        await Send(host, first with { Name = "two", Metronome = true }, 2);

        Assert.Equal(1, Status(host).Revision);
        Assert.Single(clips.Fetched);
    }

    [Fact]
    public async Task AnEditBuildsAFreshPlanFromTheCachedClips()
    {
        FakeClips clips = Clips("a");
        LiveHost host = HostOf(clips);
        ProjectDocument first = Document("p", ("t1", "a", false));

        await Send(host, first, 1);
        await Send(host, first with { MasterVolume = 0.5 }, 2);

        Assert.Equal(2, Status(host).Revision);
        Assert.Single(clips.Fetched);
    }

    [Fact]
    public async Task TheNewestDocumentWinsWhileAnOlderOneIsStillLoading()
    {
        FakeClips clips = Clips("a", "b");
        clips.Gate = new TaskCompletionSource();
        LiveHost host = HostOf(clips);

        Task older = Send(host, Document("p", ("t1", "a", false)), 1);
        await Send(host, Document("p", ("t1", "b", false)), 2);
        clips.Gate.SetResult();
        await older;

        Assert.Equal(2, Status(host).Revision);
        Assert.Equal([MediaId.From("a"), MediaId.From("b")], clips.Fetched);
    }

    [Fact]
    public async Task TheOldPlanKeepsPlayingWhileAnEditWaitsForAClipItDoesNotHaveYet()
    {
        FakeClips clips = Clips("a", "b");
        LiveHost host = HostOf(clips);
        await Send(host, Document("p", ("t1", "a", false)), 1);
        host.Play();
        host.RenderChunk();
        clips.Gate = new TaskCompletionSource();

        Task edit = Send(host, Document("p", ("t1", "a", false), ("t2", "b", false)), 2);
        byte[] chunk = host.RenderChunk();
        bool loading = Status(host).Loading > 0;
        clips.Gate.SetResult();
        await edit;

        host.RenderChunk();

        Assert.True(loading);
        Assert.Contains(chunk.Skip(LiveHost.HeaderBytes), value => value != 0);
        Assert.Equal(2, Status(host).Revision);
    }

    [Fact]
    public async Task AClipThatCannotBeFetchedIsReportedAndTheOldPlanStays()
    {
        FakeClips clips = Clips("a", "b");
        LiveHost host = HostOf(clips);
        await Send(host, Document("p", ("t1", "a", false)), 1);

        clips.Failure = new HttpRequestException("gone");
        await Send(host, Document("p", ("t1", "a", false), ("t2", "b", false)), 2);

        HostStatus status = Status(host);
        Assert.Equal(1, status.Revision);
        Assert.Contains("gone", status.Problem);
    }
}
