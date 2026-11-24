using System.Buffers.Binary;
using AudioLab.Audio;
using AudioLab.Audio.Tests.Support;
using AudioLab.Core.Commands;
using AudioLab.Core.Model;
using AudioLab.Server.Render;
using Microsoft.Extensions.Logging.Abstractions;
using static AudioLab.Server.Tests.MediaTestSupport;

namespace AudioLab.Server.Tests;

public sealed class UserMediaSourceTests
{
    private static readonly CancellationToken _none = CancellationToken.None;

    private static RenderOptions Options(long perFile = 1L << 30, long perRender = 1L << 31) =>
        new() { MaxDecodedBytesPerFile = perFile, MaxDecodedBytesPerRender = perRender };

    private static UserMediaSource SourceOf(TestStore store, UserId user, int rate, RenderOptions? options = null, CancellationToken token = default)
    {
        return new UserMediaSource(store.Media, user, rate, options ?? Options(), NullLogger.Instance, token);
    }

    private static async Task<UserMediaSource> ReadyAsync(
        TestStore store,
        UserId user,
        int rate = 48000,
        RenderOptions? options = null,
        CancellationToken token = default)
    {
        await store.Media.PrepareAsync(user, _none);
        return SourceOf(store, user, rate, options, token);
    }

    // A file whose samples are the integers given, in the container of the encoding, so that no low bit is zero by construction.
    private static byte[] WavOf(SampleEncoding encoding, int channels, int rate, int[] samples)
    {
        int width = encoding == SampleEncoding.Pcm16 ? 2 : encoding == SampleEncoding.Pcm24 ? 3 : 4;
        byte[] data = new byte[samples.Length * width];
        for (int index = 0; index < samples.Length; index++)
        {
            Span<byte> at = data.AsSpan(index * width, width);
            switch (encoding)
            {
                case SampleEncoding.Pcm16:
                    BinaryPrimitives.WriteInt16LittleEndian(at, (short)samples[index]);
                    break;
                case SampleEncoding.Pcm24:
                    at[0] = (byte)samples[index];
                    at[1] = (byte)(samples[index] >> 8);
                    at[2] = (byte)(samples[index] >> 16);
                    break;
                case SampleEncoding.Pcm32:
                    BinaryPrimitives.WriteInt32LittleEndian(at, samples[index]);
                    break;
                default:
                    BinaryPrimitives.WriteSingleLittleEndian(at, BitConverter.Int32BitsToSingle(samples[index]));
                    break;
            }
        }
        return TestWav.Riff(TestWav.Format(encoding, channels, rate), TestWav.Chunk("data", data));
    }

    private static float Expected(SampleEncoding encoding, int value) => encoding switch
    {
        SampleEncoding.Pcm16 => value / 32768f,
        SampleEncoding.Pcm24 => value / 8388608f,
        SampleEncoding.Pcm32 => (float)(value / 2147483648.0),
        _ => BitConverter.Int32BitsToSingle(value),
    };

    [Theory]
    [InlineData(SampleEncoding.Pcm16, 1)]
    [InlineData(SampleEncoding.Pcm16, 2)]
    [InlineData(SampleEncoding.Pcm24, 2)]
    [InlineData(SampleEncoding.Pcm32, 2)]
    [InlineData(SampleEncoding.Float32, 2)]
    public async Task EverySampleOfEveryAcceptedFormatIsDecodedExactlyIncludingTheLowBits(SampleEncoding encoding, int channels)
    {
        // 20000 frames cross two of the source's blocks; the values have their low bits set and both signs.
        const int Frames = 20000;
        int range = encoding switch
        {
            SampleEncoding.Pcm16 => 30000,
            SampleEncoding.Pcm24 => 8_000_000,
            SampleEncoding.Pcm32 => 2_000_000_000,
            _ => 0x3F000000,
        };
        int[] samples = [.. Enumerable.Range(0, Frames * channels).Select(index => ((index * 7919 % range) | 1) * (index % 3 == 0 ? -1 : 1))];
        if (encoding == SampleEncoding.Float32)
        {
            samples = [.. samples.Select(value => Math.Abs(value))];
        }
        using TempFolder folder = new();
        await using TestStore store = TestStore.On(folder.Path);
        MediaRecord record = await store.UploadAsync(Users.Alice, WavOf(encoding, channels, 44100, samples));
        UserMediaSource source = await ReadyAsync(store, Users.Alice, 44100);

        Assert.True(source.TryGet(record.Id, out PcmAudio? audio));

        Assert.Equal((44100, channels, Frames), (audio.SampleRate, audio.ChannelCount, audio.Frames));
        for (int index = 0; index < samples.Length; index++)
        {
            Assert.Equal(Expected(encoding, samples[index]), audio.Channel(index % channels)[index / channels]);
        }
    }

    [Fact]
    public async Task OnlyTheFirstTwoChannelsAreKeptAndTheLimitCountsOnlyThose()
    {
        using TempFolder folder = new();
        await using TestStore store = TestStore.On(folder.Path);
        MediaRecord record = await store.UploadAsync(Users.Alice, TestWav.Build(SampleEncoding.Pcm16, 6, 8000, 8000));
        long twoChannels = 8000L * 2 * sizeof(float);

        UserMediaSource fits = await ReadyAsync(store, Users.Alice, 8000, Options(perFile: twoChannels));
        UserMediaSource tooSmall = await ReadyAsync(store, Users.Alice, 8000, Options(perFile: twoChannels - 1));

        Assert.True(fits.TryGet(record.Id, out PcmAudio? audio));
        Assert.Equal(2, audio.ChannelCount);
        Assert.Throws<RenderLimitException>(() => tooSmall.TryGet(record.Id, out _));
    }

    [Fact]
    public async Task AnotherUsersFileAndAMissingOneAreBothNotAvailable()
    {
        using TempFolder folder = new();
        await using TestStore store = TestStore.On(folder.Path);
        MediaRecord foreign = await store.UploadAsync(Users.Bob, Wav());
        UserMediaSource bob = await ReadyAsync(store, Users.Bob);
        UserMediaSource alice = await ReadyAsync(store, Users.Alice);

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
        UserMediaSource source = await ReadyAsync(store, Users.Alice, 48000, token: cancel.Token);
        await cancel.CancelAsync();

        Assert.Throws<OperationCanceledException>(() => source.TryGet(record.Id, out _));
        Assert.Equal(0, source.HeldBytes);
    }

    [Theory]
    [InlineData(8000, true)]
    [InlineData(48000, true)]
    [InlineData(192000, false)]
    public async Task TheLimitIsWorkedOutAtTheRendersRateAsWellAsTheFilesOwn(int renderRate, bool passes)
    {
        // One second of 8 kHz mono: 32 KB as it is, 192 KB at 48 kHz and 768 KB at 192 kHz, against a limit of 500,000 bytes.
        using TempFolder folder = new();
        await using TestStore store = TestStore.On(folder.Path);
        MediaRecord record = await store.UploadAsync(Users.Alice, TestWav.Build(SampleEncoding.Pcm16, 1, 8000, 8000));
        UserMediaSource source = await ReadyAsync(store, Users.Alice, renderRate, Options(perFile: 500_000));

        if (passes)
        {
            Assert.True(source.TryGet(record.Id, out PcmAudio? audio));
            Assert.Equal(renderRate, audio.SampleRate);
            Assert.Equal(renderRate, audio.Frames);
        }
        else
        {
            Assert.Throws<RenderLimitException>(() => source.TryGet(record.Id, out _));
            Assert.Equal(0, source.HeldBytes);
        }
    }

    [Fact]
    public async Task TheFileLimitHoldsAtTheFilesOwnRateToTheByte()
    {
        using TempFolder folder = new();
        await using TestStore store = TestStore.On(folder.Path);
        MediaRecord record = await store.UploadAsync(Users.Alice, Wav(frames: 24000));
        long exactly = 24000L * 2 * sizeof(float);

        UserMediaSource fits = await ReadyAsync(store, Users.Alice, 48000, Options(perFile: exactly));
        UserMediaSource tooSmall = await ReadyAsync(store, Users.Alice, 48000, Options(perFile: exactly - 1));

        Assert.True(fits.TryGet(record.Id, out _));
        Assert.Throws<RenderLimitException>(() => tooSmall.TryGet(record.Id, out _));
    }

    [Fact]
    public async Task TheMediaOfOneRenderAreHeldToABudgetAndEachDistinctFileCountsOnce()
    {
        using TempFolder folder = new();
        await using TestStore store = TestStore.On(folder.Path);
        MediaRecord[] files = [.. await Task.WhenAll(Enumerable.Range(0, 3).Select(_ => store.UploadAsync(Users.Alice, Wav(frames: 8000))))];
        long each = 8000L * 2 * sizeof(float);
        UserMediaSource source = await ReadyAsync(store, Users.Alice, 48000, Options(perRender: (2 * each) + 1000));

        Assert.True(source.TryGet(files[0].Id, out PcmAudio? first));
        Assert.True(source.TryGet(files[0].Id, out PcmAudio? again));
        Assert.True(source.TryGet(files[1].Id, out _));
        Assert.Same(first, again);
        Assert.Equal((2, 2 * each), (source.DecodedFiles, source.HeldBytes));
        Assert.Throws<RenderLimitException>(() => source.TryGet(files[2].Id, out _));
        Assert.Equal(2 * each, source.HeldBytes);
    }

    [Fact]
    public async Task ManyClipsOfTwoFilesDecodeTwiceEvenWhenTheEnginesCacheKeepsNothing()
    {
        using TempFolder folder = new();
        await using TestStore store = TestStore.On(folder.Path, registry: CoreCommands.CreateRegistry());
        MediaRecord a = await store.UploadAsync(Users.Alice, TestWav.Build(SampleEncoding.Pcm16, 2, 44100, 22050, (_, _) => 0.25));
        MediaRecord b = await store.UploadAsync(Users.Alice, TestWav.Build(SampleEncoding.Pcm16, 2, 44100, 22050, (_, _) => 0.125));
        ProjectId project = await NewProjectAsync(store, Users.Alice);
        for (int index = 0; index < 12; index++)
        {
            _ = await AddClipAsync(store, Users.Alice, project, index % 2 == 0 ? a : b);
        }
        ProjectDocument document = (await store.Open.OpenAsync(Users.Alice, project, _none)).Snapshot.Document;
        UserMediaSource source = await ReadyAsync(store, Users.Alice, 48000);
        // A cache that holds one byte evicts every entry as soon as the next arrives.
        MediaCache cache = new(source, 48000, 1);

        RenderedAudio audio = OfflineRenderer.Render(document, source, new OfflineOptions { Cache = cache, IncludeTail = false });

        Assert.Equal(2, source.DecodedFiles);
        Assert.Equal(48000 / 2, audio.Frames);
        Assert.Equal(2L * 24000 * 2 * sizeof(float), source.HeldBytes);
    }

    [Fact]
    public async Task BytesThatNoLongerMatchTheirRecordAreRefusedForGoodAndNeverHalfDecoded()
    {
        using TempFolder folder = new();
        await using TestStore store = TestStore.On(folder.Path);
        MediaRecord record = await store.UploadAsync(Users.Alice, Wav());
        UserMediaSource source = await ReadyAsync(store, Users.Alice);
        string data = store.Paths.MediaDataFile(Users.Alice, record.Id);
        byte[] bytes = await File.ReadAllBytesAsync(data);
        // The same length and a header that is still a valid WAV file: the rate and the byte rate say 44.1 kHz instead of 48 kHz.
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(24), 44100);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(28), 44100 * 4);
        await File.WriteAllBytesAsync(data, bytes);

        Assert.False(source.TryGet(record.Id, out PcmAudio? audio));

        Assert.Null(audio);
        Assert.Null(await store.FindAsync(Users.Alice, record.Id));
        Assert.Equal(1, store.Media.List(Users.Alice).Refused);
        Assert.False(source.TryGet(record.Id, out _));
    }

    [Fact]
    public async Task AFloatFileChangedOnDiskToHoldNotANumberIsRefusedForGoodAndNeverReachesTheEngine()
    {
        using TempFolder folder = new();
        await using TestStore store = TestStore.On(folder.Path);
        MediaRecord record = await store.UploadAsync(Users.Alice, TestWav.Build(SampleEncoding.Float32, 2, 48000, 24000));
        UserMediaSource source = await ReadyAsync(store, Users.Alice);
        string data = store.Paths.MediaDataFile(Users.Alice, record.Id);
        byte[] bytes = await File.ReadAllBytesAsync(data);
        // The same size and header, and one sample in the middle of the data that is NaN: the upload's own check no longer applies.
        long sample = AudioLab.Audio.Wav.WavReader.Read(new MemoryStream(bytes)).DataOffset + (4 * 1000);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan((int)sample, 4), 0x7FC00000);
        await File.WriteAllBytesAsync(data, bytes);

        Assert.False(source.TryGet(record.Id, out _));

        Assert.Null(await store.FindAsync(Users.Alice, record.Id));
        Assert.Equal(0, source.HeldBytes);
    }

    [Fact]
    public async Task BytesThatAreCutOrMissingWhileTheRecordRemainsAreRefusedForGood()
    {
        using TempFolder folder = new();
        await using TestStore store = TestStore.On(folder.Path);
        MediaRecord cut = await store.UploadAsync(Users.Alice, Wav());
        MediaRecord gone = await store.UploadAsync(Users.Alice, Wav());
        UserMediaSource source = await ReadyAsync(store, Users.Alice);
        string data = store.Paths.MediaDataFile(Users.Alice, cut.Id);
        await File.WriteAllBytesAsync(data, (await File.ReadAllBytesAsync(data))[..^2]);
        File.Delete(store.Paths.MediaDataFile(Users.Alice, gone.Id));

        Assert.False(source.TryGet(cut.Id, out _));
        Assert.False(source.TryGet(gone.Id, out _));

        Assert.Null(await store.FindAsync(Users.Alice, cut.Id));
        Assert.Null(await store.FindAsync(Users.Alice, gone.Id));
        Assert.Equal(2, store.Media.List(Users.Alice).Refused);
    }

    [Fact]
    public async Task AFileWhoseDeleteHasBegunIsStillReadAndOneWhoseBytesAreAlreadyGoneIsNotRefused()
    {
        using TempFolder folder = new();
        await using TestStore store = TestStore.On(folder.Path);
        MediaRecord reading = await store.UploadAsync(Users.Alice, Wav());
        MediaRecord deleting = await store.UploadAsync(Users.Alice, Wav());
        UserMediaSource source = await ReadyAsync(store, Users.Alice);
        _ = store.Media.BeginDelete(Users.Alice, reading.Id);
        _ = store.Media.BeginDelete(Users.Alice, deleting.Id);
        File.Delete(store.Paths.MediaDataFile(Users.Alice, deleting.Id));

        Assert.True(source.TryGet(reading.Id, out _));
        Assert.False(source.TryGet(deleting.Id, out _));

        Assert.NotNull(store.Media.FindIncludingPendingDelete(Users.Alice, deleting.Id));
        Assert.Equal(0, store.Media.List(Users.Alice).Refused);
    }

    [Fact]
    public async Task AProjectRendersFromTheUsersOwnMediaAtTheRendersRate()
    {
        using TempFolder folder = new();
        await using TestStore store = TestStore.On(folder.Path, registry: CoreCommands.CreateRegistry());
        // 48 kHz stereo file of a constant 0.25 on both channels: it must come out at the track's fader level at 44.1 kHz.
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
    public async Task EachRenderGetsASourceAndACacheOfItsOwn()
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
