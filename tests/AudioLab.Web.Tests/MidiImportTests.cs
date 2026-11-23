using System.Net;
using AudioLab.Core.Api;
using AudioLab.Core.Commands;
using AudioLab.Core.Model;
using AudioLab.Core.Realtime;
using AudioLab.Web.Services;
using AudioLab.Web.Session;

namespace AudioLab.Web.Tests;

/// <summary>Importing a .mid file through the seam: the stream reaches it, the clip is selectable once the page holds it, a refusal shows.</summary>
public sealed class MidiImportTests
{
    private static readonly TrackId _track = TrackId.From("keys");

    private readonly FakeProjectHub _hub = new()
    {
        JoinDocument = ProjectJson.Write(Song.Empty().With(Song.WithMidi("keys", Song.Midi("a", 0, 4)))),
    };

    private async Task<LiveProject> OpenAsync()
    {
        LiveProject live = new(new FakeHubFactory(_hub), new CommandSender(CoreCommands.CreateRegistry()));
        await live.OpenAsync("song");
        _hub.Calls.Clear();
        return live;
    }

    /// <summary>The file's stream, the name and the start reach the seam; the outcome waits for the revision that holds the clip.</summary>
    [Fact]
    public async Task TheStreamReachesTheSeamAndTheOutcomeWaitsForTheRevisionThatHoldsTheClip()
    {
        await using LiveProject live = await OpenAsync();
        ProjectDocument withClip = Song.Empty().With(Song.WithMidi("keys", Song.Midi("a", 0, 4), Song.Midi("made", 4, 2)));
        FakeTransfer transfer = new()
        {
            Answer = new MidiImportResult("made", 7, 3, 0, 2, 120, 0, 0, 0),
            AfterAnswer = () => _hub.Push(new ProjectChange("song", 7, ProjectJson.Write(withClip), "midi_clip_import")),
        };
        using MemoryStream file = new([1, 2, 3]);

        MidiImportOutcome outcome = await new MidiEditor(live).ImportAsync(transfer, _track, "Verse", 4, file);

        Assert.True(outcome.Edit.Succeeded);
        Assert.Equal(["made"], outcome.Edit.Created);
        Assert.True(live.Revision >= 7);
        Assert.NotNull(MidiClipPlan.Find(live.Document!, ClipId.From("made")));
        Assert.Equal(("Verse", 4d, 3L), (transfer.Name, transfer.Start, transfer.Length));
        Assert.Empty(_hub.Sent);
        Assert.Null(outcome.Notice);
    }

    /// <summary>When the revision does not arrive, the clip is not offered for selection, and the user is told it is on its way.</summary>
    [Fact]
    public async Task WhenTheRevisionHasNotArrivedTheClipIsNotOfferedAndTheUserIsTold()
    {
        await using LiveProject live = await OpenAsync();
        using CancellationTokenSource gone = new();
        FakeTransfer transfer = new() { Answer = new MidiImportResult("made", 99, 3, 0, 2, 120, 0, 0, 0), AfterAnswer = gone.Cancel };
        using MemoryStream file = new([1]);

        MidiImportOutcome outcome = await new MidiEditor(live).ImportAsync(transfer, _track, "Verse", 4, file, gone.Token);

        Assert.True(outcome.Edit.Succeeded);
        Assert.Empty(outcome.Edit.Created);
        Assert.Contains("will appear when its change reaches this page", outcome.Notice, StringComparison.Ordinal);
    }

    /// <summary>A refusal of the server is the outcome's reason as the server wrote it, and nothing is selected.</summary>
    [Fact]
    public async Task ARefusalIsTheServersReasonAndSelectsNothing()
    {
        await using LiveProject live = await OpenAsync();
        ApiException refusal = new(HttpStatusCode.UnprocessableEntity, "Not an accepted MIDI file: it is format 2.");
        FakeTransfer transfer = new() { Refusal = refusal };
        using MemoryStream file = new([1]);

        MidiImportOutcome outcome = await new MidiEditor(live).ImportAsync(transfer, _track, "Verse", 4, file);

        Assert.False(outcome.Edit.Succeeded);
        Assert.Equal("Not an accepted MIDI file: it is format 2.", outcome.Edit.Refusal);
        Assert.Empty(outcome.Edit.Created);
    }

    /// <summary>With no implementation the seam is not called, and the reason is the outcome's.</summary>
    [Fact]
    public async Task WithNoImplementationTheSeamIsNotCalledAndItsReasonIsTheOutcomes()
    {
        await using LiveProject live = await OpenAsync();
        UnavailableMidiTransfer transfer = new();
        using MemoryStream file = new([1]);

        MidiImportOutcome outcome = await new MidiEditor(live).ImportAsync(transfer, _track, "Verse", 4, file);

        Assert.False(outcome.Edit.Succeeded);
        Assert.Equal(transfer.Unavailable, outcome.Edit.Refusal);
        Assert.Null(transfer.ExportAddress(ProjectId.From("song"), ClipId.From("a")));
        await Assert.ThrowsAsync<InvalidOperationException>(() => transfer.ImportAsync(ProjectId.From("song"), _track, "x", 0, file));
    }

    private sealed class FakeTransfer : IMidiTransfer
    {
        public MidiImportResult? Answer { get; set; }

        public ApiException? Refusal { get; set; }

        public Action? AfterAnswer { get; set; }

        public string Name { get; private set; } = string.Empty;

        public double Start { get; private set; }

        public long Length { get; private set; }

        public string? Unavailable => null;

        public Task<MidiImportResult> ImportAsync(
            ProjectId project,
            TrackId track,
            string name,
            double start,
            Stream file,
            CancellationToken cancellationToken = default)
        {
            Name = name;
            Start = start;
            Length = file.Length;
            if (Refusal is { } refusal)
            {
                throw refusal;
            }
            AfterAnswer?.Invoke();
            return Task.FromResult(Answer!);
        }

        public Uri? ExportAddress(ProjectId project, ClipId clip) => new($"/api/projects/{project.Value}/clips/{clip.Value}/midi", UriKind.Relative);
    }
}
