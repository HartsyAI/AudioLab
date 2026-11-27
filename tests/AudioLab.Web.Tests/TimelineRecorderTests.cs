using System.Reflection;
using AudioLab.Core.Commands;
using AudioLab.Core.Model;
using AudioLab.Core.Playback;
using AudioLab.Core.Realtime;
using AudioLab.Web.Interop;
using AudioLab.Web.Screens;
using AudioLab.Web.Services;
using AudioLab.Web.Session;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;

namespace AudioLab.Web.Tests;

/// <summary>The lane drag's wires to the recorder: a held control records and moves no point, a cancel drops the pass, and a lane that changes releases it (D86).</summary>
/// <remarks>Each test fails when its wire is taken out of <see cref="Timeline"/>: the pass it starts keeps recording, or the commit is missing its segment.</remarks>
public sealed class TimelineRecorderTests
{
    private static readonly TrackId _track = TrackId.From("t1");

    private static readonly IReadOnlyList<AutomationPoint> _points =
    [
        new AutomationPoint(1, 0.5),
        new AutomationPoint(2, 0.2),
        new AutomationPoint(3, 0.8),
    ];

    private static readonly AutomationLanePlan _plan = new(AutomationParameter.Volume, TimelineGeometry.Default, TrackLayout.AutomationHeight);

    /// <summary>Pressing a point of a lane whose touch pass is recording grabs the control; the moves and the release record the held value and send no point move.</summary>
    [Fact]
    public async Task ADragOfARecordingLaneRecordsTheHeldValueAndMovesNoPoint()
    {
        Fixture fixture = await Fixture.OpenAsync(Lane(AutomationMode.Touch, _points));
        fixture.Transport.Tick(TransportState.Playing, 0);
        fixture.Transport.Tick(TransportState.Playing, 1.0);

        await fixture.PressPointAsync(index: 1);
        await fixture.MoveAsync(0.6);
        fixture.Transport.Tick(TransportState.Playing, 1.2);
        fixture.Transport.Tick(TransportState.Playing, 1.5);
        await fixture.ReleaseAsync();
        fixture.Transport.Tick(TransportState.Playing, 2.0);
        fixture.Transport.Tick(TransportState.Stopped, 2.0);
        await fixture.CommitsAsync();

        Assert.Equal(["automation_write_commit"], fixture.Hub.Sent.Select(sent => sent.Command));
        (AutomationPass pass, _, _) = Assert.Single(fixture.Passes);
        IReadOnlyList<WrittenPoint> segment = Assert.Single(pass.Segments);
        Assert.Equal(1.5, segment[^1].Time, 6);
        Assert.Equal(0.6, segment[^1].Value, 2);
        // A sample between the move and the release holds the moved value, so the move is recorded, not only the release.
        Assert.Contains(segment, point => Math.Abs(point.Time - 1.2) < 1e-6 && Math.Abs(point.Value - 0.6) < 1e-2);
    }

    /// <summary>A press on an empty part of a lane whose touch pass is recording grabs the lane there: the moves and the release record the held value, and no point is added.</summary>
    [Fact]
    public async Task AnEmptyLaneGrabRecordsTheHeldValueAndAddsNoPoint()
    {
        Fixture fixture = await Fixture.OpenAsync(Lane(AutomationMode.Touch, _points));
        fixture.Transport.Tick(TransportState.Playing, 0);
        fixture.Transport.Tick(TransportState.Playing, 1.0);

        await fixture.PressEmptyAsync(time: 1.5, value: 0.9);
        await fixture.MoveAsync(0.6);
        fixture.Transport.Tick(TransportState.Playing, 1.2);
        fixture.Transport.Tick(TransportState.Playing, 1.5);
        await fixture.ReleaseAsync();
        fixture.Transport.Tick(TransportState.Playing, 2.0);
        fixture.Transport.Tick(TransportState.Stopped, 2.0);
        await fixture.CommitsAsync();

        Assert.Equal(["automation_write_commit"], fixture.Hub.Sent.Select(sent => sent.Command));
        (AutomationPass pass, _, _) = Assert.Single(fixture.Passes);
        IReadOnlyList<WrittenPoint> segment = Assert.Single(pass.Segments);
        Assert.Contains(segment, point => Math.Abs(point.Time - 1.2) < 1e-6 && Math.Abs(point.Value - 0.6) < 1e-2);
    }

    /// <summary>A pointer cancel of an empty-lane grab drops the pass it fed: nothing is committed.</summary>
    [Fact]
    public async Task ACancelledEmptyLaneGrabDropsThePass()
    {
        Fixture fixture = await Fixture.OpenAsync(Lane(AutomationMode.Touch, _points));
        fixture.Transport.Tick(TransportState.Playing, 0);
        fixture.Transport.Tick(TransportState.Playing, 1.0);
        await fixture.PressEmptyAsync(time: 1.5, value: 0.9);

        await fixture.CancelAsync();
        Assert.False(fixture.Recorder.Passing);
        fixture.Transport.Tick(TransportState.Playing, 1.5);
        fixture.Transport.Tick(TransportState.Stopped, 1.5);
        await fixture.CommitsAsync();

        Assert.Empty(fixture.Passes);
        Assert.Empty(fixture.Hub.Sent);
    }

    /// <summary>A pointer cancel while a drag records drops the pass: nothing is committed and the transport keeps playing.</summary>
    [Fact]
    public async Task ACancelledRecordingDragDropsThePass()
    {
        Fixture fixture = await Fixture.OpenAsync(Lane(AutomationMode.Touch, _points));
        fixture.Transport.Tick(TransportState.Playing, 0);
        fixture.Transport.Tick(TransportState.Playing, 1.0);
        await fixture.PressPointAsync(index: 1);

        await fixture.CancelAsync();
        Assert.False(fixture.Recorder.Passing);
        fixture.Transport.Tick(TransportState.Playing, 1.5);
        fixture.Transport.Tick(TransportState.Stopped, 1.5);
        await fixture.CommitsAsync();

        Assert.Empty(fixture.Passes);
        Assert.Empty(fixture.Hub.Sent);
    }

    /// <summary>A lane that changes under a recording drag releases the held value, so the pass stops holding it at that moment.</summary>
    [Fact]
    public async Task ALaneThatChangesUnderARecordingDragReleasesIt()
    {
        Fixture fixture = await Fixture.OpenAsync(Lane(AutomationMode.Touch, _points));
        fixture.Transport.Tick(TransportState.Playing, 0);
        fixture.Transport.Tick(TransportState.Playing, 1.0);
        await fixture.PressPointAsync(index: 1);

        fixture.Transport.Tick(TransportState.Playing, 1.5);
        await fixture.ChangeLaneAsync(Lane(AutomationMode.Touch, [_points[0], _points[2]]));
        fixture.Transport.Tick(TransportState.Playing, 2.0);
        fixture.Transport.Tick(TransportState.Stopped, 2.0);
        await fixture.CommitsAsync();

        (AutomationPass pass, _, _) = Assert.Single(fixture.Passes);
        Assert.All(pass.Segments.SelectMany(segment => segment), point => Assert.True(point.Time <= 1.5 + 1e-9, $"a point at {point.Time}"));
    }

    private static Track Lane(AutomationMode mode, IReadOnlyList<AutomationPoint> points)
    {
        Track track = Song.NewTrack("t1");
        return track with
        {
            Automation = track.Automation with { Mode = mode, Volume = [.. points] },
            View = track.View with { AutomationVisible = true },
        };
    }

    // A timeline on an open project, with a recorder that a test starts by hand, and a pointer the test drives.
    private sealed class Fixture
    {
        private Fixture(RenderedMarkup render, FakeProjectHub hub, FakePlaybackTransport transport, AutomationRecorder recorder, CapturingPointer pointer,
            AutomationEditor editor, SessionActions actions, List<(AutomationPass Pass, AutomationParameter Parameter, AutomationMode? Mode)> passes,
            List<Task> commits)
        {
            Render = render;
            Hub = hub;
            Transport = transport;
            Recorder = recorder;
            Pointer = pointer;
            Editor = editor;
            Actions = actions;
            Passes = passes;
            Commits = commits;
        }

        public RenderedMarkup Render { get; }

        public FakeProjectHub Hub { get; }

        public FakePlaybackTransport Transport { get; }

        public AutomationRecorder Recorder { get; }

        public CapturingPointer Pointer { get; }

        public AutomationEditor Editor { get; }

        public SessionActions Actions { get; }

        public List<(AutomationPass Pass, AutomationParameter Parameter, AutomationMode? Mode)> Passes { get; }

        public List<Task> Commits { get; }

        public Timeline Timeline => (Timeline)Render.Instance!;

        public static async Task<Fixture> OpenAsync(Track track)
        {
            ProjectDocument document = Song.Empty().With(track);
            FakeProjectHub hub = new() { JoinDocument = ProjectJson.Write(document) };
            LiveProject live = new(new FakeHubFactory(hub), new CommandSender(CoreCommands.CreateRegistry()));
            await live.OpenAsync("song");
            hub.Calls.Clear();
            hub.Reply = new CommandReply(true, true, 2, [], []);
            AutomationEditor editor = new(live);
            List<(AutomationPass, AutomationParameter, AutomationMode?)> passes = [];
            List<Task> commits = [];
            SessionActions actions = new()
            {
                CanEdit = true,
                Automation = AutomationActions.For(editor, show => show, () => live.Document),
            };
            FakePlaybackTransport transport = new();
            AutomationRecorder recorder = new();
            CapturingPointer pointer = new();
            recorder.Open(
                transport,
                () => new AutomationTarget(_track, AutomationParameter.Volume, AutomationMode.Touch, () => 0.4),
                (lane, parameter, pass, after) =>
                {
                    passes.Add((pass, parameter, after));
                    Task commit = editor.CommitPassAsync(lane, parameter, pass, after);
                    commits.Add(commit);
                    return commit;
                });
            RenderedMarkup render = await RenderedMarkup.StartAsync<Timeline>(
                new Dictionary<string, object?> { ["Document"] = live.Document, ["Actions"] = actions },
                services =>
                {
                    services.AddSingleton<IPointerTracker>(pointer);
                    services.AddSingleton<IElementMetrics>(new OriginMetrics());
                    services.AddSingleton(recorder);
                    SessionServices.Add(services, new HttpClient { BaseAddress = new Uri("http://example.test/") });
                });
            return new Fixture(render, hub, transport, recorder, pointer, editor, actions, passes, commits);
        }

        public Task PressEmptyAsync(double time, double value)
        {
            Track track = Timeline.Document.Tracks.Single();
            double top = TrackLayout.Of(Timeline.Document).AutomationTop(0);
            PointerEventArgs press = new() { ClientX = _plan.X(time), ClientY = top + _plan.Y(value) };
            return Render.RunAsync(() => InvokeTimeline("AutomationPointDown", (track.Id, press)));
        }

        public async Task PressPointAsync(int index)
        {
            Track track = Timeline.Document.Tracks.Single();
            double top = TrackLayout.Of(Timeline.Document).AutomationTop(0);
            AutomationPoint point = _points[index];
            PointerEventArgs press = new() { ClientX = _plan.X(point.Time), ClientY = top + _plan.Y(point.Value) };
            await Render.RunAsync(() => InvokeTimeline("AutomationPointDown", (track.Id, press)));
        }

        public Task MoveAsync(double value)
        {
            double top = TrackLayout.Of(Timeline.Document).AutomationTop(0);
            return Render.RunAsync(() => Pointer.Move!(_plan.X(2) + 20, top + _plan.Y(value)));
        }

        public Task ReleaseAsync()
        {
            return Render.RunAsync(() => Pointer.End!(_plan.X(2) + 20, TrackLayout.Of(Timeline.Document).AutomationTop(0) + _plan.Y(0.6)));
        }

        public Task CancelAsync() => Render.RunAsync(() => Pointer.Cancel!());

        public Task ChangeLaneAsync(Track track)
        {
            ProjectDocument document = Song.Empty().With(track);
            return Render.RenderAgainAsync(new Dictionary<string, object?> { ["Document"] = document, ["Actions"] = Actions });
        }

        public Task CommitsAsync() => Task.WhenAll(Commits);

        private void InvokeTimeline(string name, object argument)
        {
            typeof(Timeline).GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(Timeline, [argument]);
        }
    }

    // A pointer the test drives: it keeps the three callbacks of the drag that began, and forgets them when stopped.
    private sealed class CapturingPointer : IPointerTracker
    {
        public Action<double, double>? Move { get; private set; }

        public Action<double, double>? End { get; private set; }

        public Action? Cancel { get; private set; }

        public bool Shift => false;

        public void Begin(Action<double, double> move, Action<double, double> end, Action cancel)
        {
            Move = move;
            End = end;
            Cancel = cancel;
        }

        public void Stop()
        {
            Move = null;
            End = null;
            Cancel = null;
        }
    }

    // The lane's surface sits at the top left of the page, so a lane's pixels are the client's.
    private sealed class OriginMetrics : IElementMetrics
    {
        public (double Left, double Top)? Origin(string elementId) => (0, 0);

        public bool IsOver(string elementId, double clientX, double clientY) => false;
    }
}
