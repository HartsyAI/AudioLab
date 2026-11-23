using AudioLab.Audio;
using AudioLab.Core.Commands;
using AudioLab.Core.Model;
using AudioLab.Server.Render;
using Microsoft.Extensions.Logging.Abstractions;
using static AudioLab.Server.Tests.MediaTestSupport;

namespace AudioLab.Server.Tests;

public sealed class UserMediaSourceTests
{
    private static readonly CancellationToken _none = CancellationToken.None;

    private static UserMediaSource SourceOf(TestStore store, UserId user, long limit = 1L << 30, CancellationToken token = default)
    {
        return new UserMediaSource(store.Media, user, limit, NullLogger.Instance, token);
    }

    private static async Task<UserMediaSource> ReadySourceAsync(
        TestStore store,
        UserId user,
        long limit = 1L << 30,
        CancellationToken token = default)
    {
        await store.Media.PrepareAsync(user, _none);
        return SourceOf(store, user, limit, token);
    }

    [Theory]
    [InlineData(SampleEncoding.Pcm16, 1)]
    [InlineData(SampleEncoding.Pcm16, 2)]
    [InlineData(SampleEncoding.Pcm24, 2)]
    [InlineData(SampleEncoding.Pcm32, 2)]
    [InlineData(SampleEncoding.Float32, 3)]
    public async Task EverySampleOfEveryAcceptedFormatIsDecodedExactly(SampleEncoding encoding, int channels)
    {
        // 20000 frames cross two of the source's blocks, and a value of each sign and each extreme is in the first frames.
        const int Frames = 20000;
        short[] samples = new short[Frames * channels];
        short[] corners = [short.MinValue, short.MaxValue, 0, 1, -1, 12345, -12345];
        for (int index = 0; index < samples.Length; index++)
        {
            samples[index] = index < corners.Length ? corners[index] : (short)(((index * 7919) % 65536) - 32768);
        }
        using TempFolder folder = new();
        await using TestStore store = TestStore.On(folder.Path);
        MediaRecord record = await store.UploadAsync(Users.Alice, TestWav.FromShorts(encoding, channels, 44100, samples));
        UserMediaSource source = await ReadySourceAsync(store, Users.Alice);

        Assert.True(source.TryGet(record.Id, out PcmAudio? audio));

        Assert.Equal(44100, audio.SampleRate);
        Assert.Equal(channels, audio.ChannelCount);
        Assert.Equal(Frames, audio.Frames);
        for (int index = 0; index < samples.Length; index++)
        {
            Assert.Equal(samples[index] / 32768f, audio.Channel(index % channels)[index / channels]);
        }
    }

    [Fact]
    public async Task AnotherUsersFileAndAMissingOneAreBothNotAvailable()
    {
        using TempFolder folder = new();
        await using TestStore store = TestStore.On(folder.Path);
        MediaRecord foreign = await store.UploadAsync(Users.Bob, Wav());
        await store.Media.PrepareAsync(Users.Bob, _none);
        UserMediaSource bob = SourceOf(store, Users.Bob);
        UserMediaSource alice = await ReadySourceAsync(store, Users.Alice);

        Assert.True(bob.TryGet(foreign.Id, out _));
        Assert.False(alice.TryGet(foreign.Id, out PcmAudio? stolen));
        Assert.Null(stolen);
        Assert.False(alice.TryGet(MediaId.From("never-uploaded"), out _));
    }

    [Fact]
    public async Task ACancelledTokenStopsTheDecode()
    {
        using TempFolder folder = new();
        await using TestStore store = TestStore.On(folder.Path);
        MediaRecord record = await store.UploadAsync(Users.Alice, Wav());
        using CancellationTokenSource cancel = new();
        UserMediaSource source = await ReadySourceAsync(store, Users.Alice, token: cancel.Token);
        await cancel.CancelAsync();

        Assert.Throws<OperationCanceledException>(() => source.TryGet(record.Id, out _));
    }

    [Fact]
    public async Task AFileThatWouldDecodeToMoreThanTheLimitIsRefusedBeforeAnythingIsAllocated()
    {
        using TempFolder folder = new();
        await using TestStore store = TestStore.On(folder.Path);
        MediaRecord record = await store.UploadAsync(Users.Alice, Wav(frames: 24000));
        long exactly = 24000L * 2 * sizeof(float);

        Assert.True((await ReadySourceAsync(store, Users.Alice, limit: exactly)).TryGet(record.Id, out _));
        UserMediaSource tooSmall = await ReadySourceAsync(store, Users.Alice, limit: exactly - 1);
        Assert.Throws<RenderLimitException>(() => tooSmall.TryGet(record.Id, out _));
    }

    [Fact]
    public async Task BytesThatChangedOnDiskAreNotAvailableAndNeverHalfDecoded()
    {
        using TempFolder folder = new();
        await using TestStore store = TestStore.On(folder.Path);
        MediaRecord record = await store.UploadAsync(Users.Alice, Wav());
        UserMediaSource source = await ReadySourceAsync(store, Users.Alice);
        string data = store.Paths.MediaDataFile(Users.Alice, record.Id);
        byte[] bytes = await File.ReadAllBytesAsync(data);

        await File.WriteAllBytesAsync(data, bytes[..^2]);
        Assert.False(source.TryGet(record.Id, out PcmAudio? cut));
        Assert.Null(cut);

        // Same length but another format in the header: the record no longer describes the bytes.
        byte[] other = (byte[])bytes.Clone();
        other[22] = 1;
        await File.WriteAllBytesAsync(data, other);
        Assert.False(source.TryGet(record.Id, out _));

        await File.WriteAllBytesAsync(data, bytes);
        Assert.True(source.TryGet(record.Id, out _));
    }

    [Fact]
    public async Task AProjectRendersFromTheUsersOwnMediaAtTheRendersRate()
    {
        using TempFolder folder = new();
        await using TestStore store = TestStore.On(folder.Path, registry: CoreCommands.CreateRegistry());
        // 48 kHz stereo file of a constant 0.25 on both channels: it must come out at the track's fader level after the engine's resampler.
        MediaRecord record = await store.UploadAsync(Users.Alice, TestWav.Build(SampleEncoding.Float32, 2, 48000, 48000, (_, _) => 0.25));
        ProjectId project = await NewProjectAsync(store, Users.Alice);
        _ = await AddClipAsync(store, Users.Alice, project, record);
        ProjectDocument document = (await store.Open.OpenAsync(Users.Alice, project, _none)).Snapshot.Document;
        RenderMediaProvider provider = new(store.Media, new RenderOptions(), NullLogger<RenderMediaProvider>.Instance);

        RenderMedia media = await provider.OpenAsync(Users.Alice, 44100, _none);
        OfflineOptions options = new() { SampleRate = 44100, Cache = media.Cache, IncludeTail = false };
        RenderedAudio audio = OfflineRenderer.Render(document, media.Source, options);

        Assert.InRange(audio.Frames, 44099, 44101);
        float middle = audio.Left[audio.Frames / 2];
        Assert.Equal(0.25 * document.Tracks[0].Volume, middle, 1e-3);
        Assert.Equal(middle, audio.Right[audio.Frames / 2]);
    }

    [Fact]
    public async Task AnotherUsersMediaFailsThePlanAsMissingMedia()
    {
        using TempFolder folder = new();
        await using TestStore store = TestStore.On(folder.Path, registry: CoreCommands.CreateRegistry());
        MediaRecord own = await store.UploadAsync(Users.Alice, Wav());
        MediaRecord foreign = await store.UploadAsync(Users.Bob, Wav());
        ProjectId project = await NewProjectAsync(store, Users.Alice);
        _ = await AddClipAsync(store, Users.Alice, project, own);
        ProjectDocument document = (await store.Open.OpenAsync(Users.Alice, project, _none)).Snapshot.Document;
        RenderMediaProvider provider = new(store.Media, new RenderOptions(), NullLogger<RenderMediaProvider>.Instance);

        // A project document naming a file of another user cannot arise through a command; the source still refuses it.
        Track Retarget(Track track) => track with { Clips = [.. track.Clips.Select(clip => clip with { Media = foreign.Id })] };
        ProjectDocument stolen = document with { Tracks = [.. document.Tracks.Select(Retarget)] };
        RenderMedia media = await provider.OpenAsync(Users.Alice, 48000, _none);

        OfflineOptions options = new() { Cache = media.Cache };

        MissingMediaException missing = Assert.Throws<MissingMediaException>(() => OfflineRenderer.Render(stolen, media.Source, options));
        Assert.Equal([foreign.Id], missing.Missing);
    }

    [Fact]
    public async Task EachRenderGetsACacheOfItsOwn()
    {
        using TempFolder folder = new();
        await using TestStore store = TestStore.On(folder.Path);
        MediaRecord record = await store.UploadAsync(Users.Alice, Wav());
        RenderMediaProvider provider = new(store.Media, new RenderOptions(), NullLogger<RenderMediaProvider>.Instance);

        RenderMedia first = await provider.OpenAsync(Users.Alice, 48000, _none);
        Assert.NotNull(first.Cache.Get(record.Id));
        RenderMedia second = await provider.OpenAsync(Users.Alice, 48000, _none);
        RenderMedia bobs = await provider.OpenAsync(Users.Bob, 48000, _none);

        Assert.NotSame(first.Cache, second.Cache);
        Assert.Equal(0, second.Cache.Bytes);
        Assert.Null(bobs.Cache.Get(record.Id));
    }
}
