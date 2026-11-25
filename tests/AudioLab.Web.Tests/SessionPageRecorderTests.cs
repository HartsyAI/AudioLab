using System.Reflection;
using AudioLab.Core.Model;
using AudioLab.Core.Playback;
using AudioLab.Web.Interop;
using AudioLab.Web.Pages;
using AudioLab.Web.Services;
using AudioLab.Web.Session;
using Microsoft.Extensions.DependencyInjection;

namespace AudioLab.Web.Tests;

/// <summary>The page's wires to the recorder: Escape, a project closed, and a connection lost each end a pass as they should (D86).</summary>
/// <remarks>Each test fails when its wire is taken out of <see cref="SessionPage"/>: the pass it starts is still recording afterwards.</remarks>
public sealed class SessionPageRecorderTests
{
    private static readonly TrackId _track = TrackId.From("t1");

    private static readonly HostStatus _playing = new(TransportState.Playing, 0, 48000, 1, 0, null, 0, 0, 0, 0, default, [], false);

    private static readonly HostStatus _stopped = new(TransportState.Stopped, 0, 48000, 1, 0, null, 0, 0, 0, 0, default, [], false);

    /// <summary>Escape ends a pass that is recording, silently; the transport keeps playing and nothing is committed.</summary>
    [Fact]
    public async Task EscapeEndsARecordingPassWithoutCommittingIt()
    {
        OpenedPage opened = await OpenPageAsync();
        await StartPassAsync(opened);
        Assert.True(opened.Recorder.Passing);

        bool handled = await opened.Render.Dispatcher.InvokeAsync(() => Press(opened.Page, new KeyChord("Escape", false, false, false)));

        Assert.True(handled);
        Assert.False(opened.Recorder.Passing);
        opened.Host.Report(_stopped);
        Assert.Empty(opened.Hub.Sent);
        Assert.Empty(opened.Notices);
        await opened.DisposeAsync();
    }

    /// <summary>Closing the project ends a recording pass silently, before the engine is let go, so nothing is committed and no warning shows.</summary>
    [Fact]
    public async Task ClosingTheProjectEndsARecordingPassSilently()
    {
        OpenedPage opened = await OpenPageAsync();
        await StartPassAsync(opened);
        Assert.True(opened.Recorder.Passing);

        await opened.Render.Dispatcher.InvokeAsync(() => opened.Page.DisposeAsync().AsTask());

        Assert.False(opened.Recorder.Passing);
        Assert.Empty(opened.Notices);
        Assert.Empty(opened.Hub.Sent);
        await opened.DisposeAsync();
    }

    /// <summary>A lost connection ends a recording pass without a commit, and says why.</summary>
    [Fact]
    public async Task ALostConnectionEndsARecordingPassAndSaysWhy()
    {
        OpenedPage opened = await OpenPageAsync();
        await StartPassAsync(opened);
        Assert.True(opened.Recorder.Passing);

        await opened.Render.Dispatcher.InvokeAsync(() => opened.Hub.Move(HubState.Disconnected));

        Assert.False(opened.Recorder.Passing);
        Assert.Equal(["The connection was lost, so the pass was not written."], opened.Notices);
        opened.Host.Report(_stopped);
        Assert.Empty(opened.Hub.Sent);
        await opened.DisposeAsync();
    }

    private static async Task StartPassAsync(OpenedPage opened)
    {
        await opened.Playback.PlayAsync();
        opened.Host.Report(_playing);
    }

    private static bool Press(SessionPage page, KeyChord chord) =>
        (bool)typeof(SessionPage).GetMethod("Press", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(page, [chord])!;

    // A project with one lane in touch, which the screen may choose in these tests, opened on a page with a fake engine and a fake hub.
    private static async Task<OpenedPage> OpenPageAsync()
    {
        Track track0 = Song.NewTrack("t1");
        Track track = track0 with { Automation = track0.Automation with { Mode = AutomationMode.Touch } };
        FakeProjectHub hub = new() { JoinDocument = ProjectJson.Write(Song.Empty().With(track)) };
        FakeAudioHost host = new();
        HttpClient http = new() { BaseAddress = new Uri("http://example.test/") };
        PlaybackService playback = new(host, http, TimeProvider.System);
        AutomationRecorder recorder = new();
        List<string> notices = [];
        recorder.Notice += notices.Add;
        RenderedMarkup render = await RenderedMarkup.StartAsync<SessionPage>(
            new Dictionary<string, object?> { ["Id"] = "song" },
            services =>
            {
                services.AddSingleton<IAudioHost>(host);
                services.AddSingleton<IProjectHubFactory>(new FakeHubFactory(hub));
                services.AddSingleton(playback);
                services.AddSingleton(recorder);
                services.AddSingleton<IAutomationModeRules>(new AllModesAvailable());
                SessionServices.Add(services, http);
            });
        return new OpenedPage(render, (SessionPage)render.Instance!, hub, host, playback, recorder, notices);
    }

    private sealed class OpenedPage(
        RenderedMarkup render,
        SessionPage page,
        FakeProjectHub hub,
        FakeAudioHost host,
        PlaybackService playback,
        AutomationRecorder recorder,
        List<string> notices)
    {
        public RenderedMarkup Render { get; } = render;

        public SessionPage Page { get; } = page;

        public FakeProjectHub Hub { get; } = hub;

        public FakeAudioHost Host { get; } = host;

        public PlaybackService Playback { get; } = playback;

        public AutomationRecorder Recorder { get; } = recorder;

        public List<string> Notices { get; } = notices;

        public async Task DisposeAsync()
        {
            await Render.DisposeServicesAsync();
        }
    }

    private sealed class AllModesAvailable : IAutomationModeRules
    {
        public string? UnavailableReason(AutomationMode mode) => null;
    }
}
