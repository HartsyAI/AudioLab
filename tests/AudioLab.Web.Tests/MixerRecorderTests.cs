using AudioLab.Core.Commands;
using AudioLab.Core.Model;
using AudioLab.Core.Playback;
using AudioLab.Web.Interop;
using AudioLab.Web.Screens;
using AudioLab.Web.Services;
using AudioLab.Web.Session;
using AudioLab.Web.Session.Effects;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;

namespace AudioLab.Web.Tests;

/// <summary>The track fader's grab reaches the recorder through the mixer: a touch pass records the drag and sends no track volume, and outside a
/// pass the fader sends its gain as it always did (D72, D86).</summary>
/// <remarks>Each test fails when its wire is taken out of the mixer: the grab, the drag or the release is missing from the pass, or the send skip is gone.</remarks>
public sealed class MixerRecorderTests
{
    private static readonly TrackId _track = TrackId.From("t1");

    /// <summary>A touch pass records a grab, its moves and its release on the track fader, and no track volume is sent while the pass records.</summary>
    [Fact]
    public async Task ATouchPassRecordsTheFaderDragAndSendsNoTrackVolume()
    {
        Fixture fixture = await Fixture.OpenAsync(AutomationMode.Touch);
        fixture.Transport.Tick(TransportState.Playing, 0);
        fixture.Transport.Tick(TransportState.Playing, 1.0);

        await fixture.PressAsync(300);
        fixture.Pointer.Move(0, 260);
        fixture.Transport.Tick(TransportState.Playing, 1.2);
        fixture.Pointer.Release(0, 240);
        await fixture.Barrier();
        fixture.Transport.Tick(TransportState.Playing, 1.5);
        fixture.Transport.Tick(TransportState.Stopped, 1.5);

        Assert.Empty(fixture.Sent);
        (AutomationPass pass, _, _) = Assert.Single(fixture.Passes);
        IReadOnlyList<WrittenPoint> segment = Assert.Single(pass.Segments);
        Assert.Contains(segment, point => Math.Abs(point.Time - 1.2) < 1e-6 && Math.Abs(point.Value - 1) > 1e-3);
    }

    /// <summary>Outside a pass the same fader sends its gain, and records nothing.</summary>
    [Fact]
    public async Task OutsideAPassTheFaderSendsItsGain()
    {
        Fixture fixture = await Fixture.OpenAsync(AutomationMode.Touch);

        await fixture.PressAsync(300);
        fixture.Pointer.Move(0, 260);
        fixture.Pointer.Release(0, 240);
        await fixture.Barrier();

        Assert.Single(fixture.Sent);
        Assert.Empty(fixture.Passes);
    }

    /// <summary>An arrow key during a touch pass, with no grab held, sends the gain as it always did: only the gesture of a grab is kept out of the track.</summary>
    [Fact]
    public async Task AnArrowKeyDuringAPassWithNoGrabSendsItsGain()
    {
        Fixture fixture = await Fixture.OpenAsync(AutomationMode.Touch);
        fixture.Transport.Tick(TransportState.Playing, 0);
        fixture.Transport.Tick(TransportState.Playing, 1.0);

        await fixture.KeyAsync("ArrowUp");
        await fixture.Barrier();

        Assert.Single(fixture.Sent);
    }

    private sealed class Fixture
    {
        private Fixture(RenderedMarkup mixer, FakePlaybackTransport transport, AutomationRecorder recorder, CapturingPointer pointer, List<double> sent,
            List<(AutomationPass Pass, AutomationParameter Parameter, AutomationMode? Mode)> passes)
        {
            Mixer = mixer;
            Transport = transport;
            Recorder = recorder;
            Pointer = pointer;
            Sent = sent;
            Passes = passes;
        }

        public RenderedMarkup Mixer { get; }

        public FakePlaybackTransport Transport { get; }

        public AutomationRecorder Recorder { get; }

        public CapturingPointer Pointer { get; }

        public List<double> Sent { get; }

        public List<(AutomationPass Pass, AutomationParameter Parameter, AutomationMode? Mode)> Passes { get; }

        public static async Task<Fixture> OpenAsync(AutomationMode mode)
        {
            Track track = Song.NewTrack("t1");
            track = track with { Automation = track.Automation with { Mode = mode } };
            ProjectDocument document = Song.Empty().With(track);
            List<double> sent = [];
            List<(AutomationPass, AutomationParameter, AutomationMode?)> passes = [];
            FakePlaybackTransport transport = new();
            AutomationRecorder recorder = new();
            CapturingPointer pointer = new();
            recorder.Open(
                transport,
                () => new AutomationTarget(_track, AutomationParameter.Volume, mode, () => 1.0),
                (lane, parameter, pass, after) =>
                {
                    passes.Add((pass, parameter, after));
                    return Task.CompletedTask;
                });
            SessionActions actions = new()
            {
                CanEdit = true,
                SetVolume = (_, gain) =>
                {
                    sent.Add(gain);
                    return Task.FromResult(EditResult.Refused("Not sent in a test."));
                },
            };
            RenderedMarkup mixer = await RenderedMarkup.StartAsync<MixerPanel>(
                new Dictionary<string, object?> { ["Document"] = document, ["Actions"] = actions, ["Selected"] = null },
                services =>
                {
                    services.AddSingleton<IKeyGuard>(new NoKeyGuard());
                    services.AddSingleton<IPointerTracker>(pointer);
                    services.AddSingleton(new TrackColorMap([.. Enumerable.Repeat("#ee6d86", 12)]));
                    services.AddScoped<SessionSelection>();
                    services.AddScoped<EffectSelection>();
                    services.AddScoped<BottomPanelState>();
                    services.AddScoped<EffectsNavigator>();
                    services.AddSingleton(recorder);
                });
            return new Fixture(mixer, transport, recorder, pointer, sent, passes);
        }

        public Task PressAsync(double clientY) =>
            Mixer.RaiseOnAsync("fader-t1", "onpointerdown", new PointerEventArgs { Button = 0, ClientY = clientY });

        public Task KeyAsync(string key) => Mixer.RaiseOnAsync("fader-t1", "onkeydown", new KeyboardEventArgs { Key = key });

        // A barrier on the renderer: the handlers that a pointer or key started have run once it returns.
        public Task Barrier() => Mixer.RunAsync(() => { });
    }

    // A pointer the test drives: it keeps the callbacks of the drag that began, and forgets them when stopped.
    private sealed class CapturingPointer : IPointerTracker
    {
        private Action<double, double>? _move;

        private Action<double, double>? _end;

        public bool Shift => false;

        public void Begin(Action<double, double> move, Action<double, double> end, Action cancel)
        {
            (_move, _end) = (move, end);
        }

        public void Stop()
        {
            (_move, _end) = (null, null);
        }

        public void Move(double x, double y) => _move?.Invoke(x, y);

        public void Release(double x, double y) => _end?.Invoke(x, y);
    }
}
