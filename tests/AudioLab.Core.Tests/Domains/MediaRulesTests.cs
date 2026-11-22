using AudioLab.Core.Commands;
using AudioLab.Core.Model;
using AudioLab.Core.Rules;

namespace AudioLab.Core.Tests;

public sealed class MediaRulesTests
{
    private static readonly UserId _other = UserId.ForDoor("swarm", "someoneelse");

    private readonly FakeCatalog _catalog = new();
    private readonly CommandRig _rig = new();
    private readonly MediaRecord _kick = TestMedia.Record("med_kick", UserId.Local, 2.5);
    private readonly TrackId _track;

    public MediaRulesTests()
    {
        _track = TrackId.From(_rig.Created(new AddTrack { Project = _rig.Id, Kind = TrackKind.Audio })[0]);
        _catalog.Add(_kick);
    }

    private ProjectDocument Add(string media, double duration)
    {
        return _rig.Ok(new AddClip { Project = _rig.Id, Track = _track, Media = MediaId.From(media), Duration = duration });
    }

    private CommandError? Verify(ProjectDocument before, ProjectDocument after) => MediaRules.VerifyChange(before, after, _catalog);

    [Fact]
    public void ANewReferenceIsRefusedNotFoundWhetherTheFileIsMissingOrAnotherUsers()
    {
        _catalog.Add(TestMedia.Record("med_theirs", _other, 2.5));
        ProjectDocument before = _rig.Project;

        CommandError? missing = Verify(before, Add("med_nothing", 2.5));
        _rig.History.Undo();
        CommandError? foreign = Verify(before, Add("med_theirs", 2.5));

        Assert.Equal(CommandErrorCode.NotFound, missing?.Code);
        Assert.Equal(missing, foreign);
        Assert.Equal(MediaRules.NotFoundMessage, missing?.Message);
    }

    [Fact]
    public void AClipsDurationMustBeTheFilesWithinTheStatedTolerance()
    {
        ProjectDocument before = _rig.Project;
        Assert.Equal(0.001, MediaRules.DurationTolerance);

        Assert.Null(Verify(before, Add("med_kick", 2.5)));
        _rig.History.Undo();
        Assert.Null(Verify(before, Add("med_kick", 2.5009)));
        _rig.History.Undo();
        Assert.Null(Verify(before, Add("med_kick", 2.4991)));
        _rig.History.Undo();

        CommandError? longer = Verify(before, Add("med_kick", 2.5011));
        _rig.History.Undo();
        CommandError? shorter = Verify(before, Add("med_kick", 2.4989));
        Assert.Equal(CommandErrorCode.Rejected, longer?.Code);
        Assert.Equal(CommandErrorCode.Rejected, shorter?.Code);
        Assert.Contains("2.5011", longer?.Message, StringComparison.Ordinal);
        Assert.Contains("2.5", longer?.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AChangeThatNamesNoNewMediaAsksTheCatalogNothing()
    {
        ProjectDocument before = Add("med_kick", 2.5);
        ProjectDocument after = _rig.Ok(new SetTempoBpm { Project = _rig.Id, Bpm = 99 });

        Assert.Null(MediaRules.VerifyChange(before, after, new ThrowingCatalog()));
        Assert.Null(MediaRules.VerifyChange(before, before, new ThrowingCatalog()));
    }

    [Fact]
    public void AMediaFileADocumentAlreadyNamesIsNotAskedAboutAgainWhenAClipMoves()
    {
        ProjectDocument before = Add("med_kick", 2.5);
        ClipId clip = before.Tracks[0].Clips[0].Id;
        ProjectDocument moved = _rig.Ok(new MoveClip { Project = _rig.Id, Clip = clip, Start = 10 });

        Assert.Null(MediaRules.VerifyChange(before, moved, new FakeCatalog()));
    }

    [Fact]
    public void ACopyOfAClipWhoseFileIsBeingDeletedIsNotRefusedButANewClipOfAnUnnamedFileIs()
    {
        ProjectDocument before = Add("med_kick", 2.5);
        ClipId clip = before.Tracks[0].Clips[0].Id;
        _catalog.PendingDelete.Add(_kick.Id);
        ProjectDocument copied = _rig.Ok(new DuplicateClip { Project = _rig.Id, Clip = clip });

        Assert.Null(Verify(before, copied));
        _rig.History.Undo();
        _catalog.Add(TestMedia.Record("med_snare", UserId.Local, 1));
        _catalog.PendingDelete.Add(MediaId.From("med_snare"));
        ProjectDocument other = Add("med_snare", 1);
        Assert.Equal(CommandErrorCode.NotFound, Verify(before, other)?.Code);
    }

    [Fact]
    public void ADrumLaneAndASamplerAreCheckedWhenTheyGainAFileAndNotWhenTheyLoseOne()
    {
        TrackId sampler = TrackId.From(_rig.Created(new AddTrack { Project = _rig.Id, Kind = TrackKind.Instrument, Preset = "Sampler" })[0]);
        BeatLaneId lane = BeatLaneId.From(_rig.Created(new AddBeatLane { Project = _rig.Id })[0]);
        ProjectDocument withLane = _rig.Project;

        ProjectDocument laneMissing = _rig.Ok(new SetBeatLaneMedia { Project = _rig.Id, Lane = lane, Media = MediaId.From("med_nothing") });
        Assert.Equal(CommandErrorCode.NotFound, Verify(withLane, laneMissing)?.Code);
        _rig.History.Undo();
        ProjectDocument laneKnown = _rig.Ok(new SetBeatLaneMedia { Project = _rig.Id, Lane = lane, Media = _kick.Id });
        Assert.Null(Verify(withLane, laneKnown));
        ProjectDocument laneCleared = _rig.Ok(new SetBeatLaneMedia { Project = _rig.Id, Lane = lane });
        Assert.Null(MediaRules.VerifyChange(laneKnown, laneCleared, new FakeCatalog()));

        MediaId nothing = MediaId.From("med_nothing");
        ProjectDocument sampleMissing = _rig.Ok(new SetSamplerSample { Project = _rig.Id, Track = sampler, Sample = nothing, SampleName = "x" });
        Assert.Equal(CommandErrorCode.NotFound, Verify(laneCleared, sampleMissing)?.Code);
    }

    [Fact]
    public void ALoadedDocumentIsCheckedClipByClipLaneByLaneAndSamplerBySampler()
    {
        TrackId sampler = TrackId.From(_rig.Created(new AddTrack { Project = _rig.Id, Kind = TrackKind.Instrument, Preset = "Sampler" })[0]);
        _rig.Ok(new SetSamplerSample { Project = _rig.Id, Track = sampler, Sample = MediaId.From("med_gone"), SampleName = "gone" });
        BeatLaneId lane = BeatLaneId.From(_rig.Created(new AddBeatLane { Project = _rig.Id })[0]);
        _rig.Ok(new SetBeatLaneMedia { Project = _rig.Id, Lane = lane, Media = MediaId.From("med_gone") });
        Add("med_kick", 9);
        Add("med_gone", 1);

        IReadOnlyList<string> problems = MediaRules.DocumentProblems(_rig.Project, _catalog);

        Assert.Equal(4, problems.Count);
        Assert.Contains(
            problems,
            problem => problem.StartsWith("track ", StringComparison.Ordinal) && problem.Contains("sampler", StringComparison.Ordinal));
        Assert.Contains(problems, problem => problem.StartsWith("lane ", StringComparison.Ordinal));
        Assert.Contains(problems, problem => problem.Contains("9 s", StringComparison.Ordinal));
        Assert.Equal(3, problems.Count(problem => problem.Contains("no such media file", StringComparison.Ordinal)));
    }

    [Fact]
    public void ALoadedDocumentThatIsRightPassesEvenWhileItsFileIsBeingDeleted()
    {
        Add("med_kick", 2.5);
        _catalog.PendingDelete.Add(_kick.Id);

        Assert.Empty(MediaRules.DocumentProblems(_rig.Project, _catalog));
        Assert.Empty(MediaRules.DocumentProblems(BlankProject.Create(), new ThrowingCatalog()));
    }

    private sealed class FakeCatalog : IMediaCatalog
    {
        private readonly Dictionary<(UserId, MediaId), MediaRecord> _records = [];

        public HashSet<MediaId> PendingDelete { get; } = [];

        public void Add(MediaRecord record) => _records[(record.Owner, record.Id)] = record;

        public MediaRecord? Find(UserId owner, MediaId id)
        {
            return !PendingDelete.Contains(id) && _records.TryGetValue((owner, id), out MediaRecord? record) ? record : null;
        }

        public MediaRecord? FindIncludingPendingDelete(UserId owner, MediaId id)
        {
            return _records.TryGetValue((owner, id), out MediaRecord? record) ? record : null;
        }
    }

    private sealed class ThrowingCatalog : IMediaCatalog
    {
        public MediaRecord? Find(UserId owner, MediaId id) => throw new InvalidOperationException("The catalog must not be asked.");

        public MediaRecord? FindIncludingPendingDelete(UserId owner, MediaId id) => Find(owner, id);
    }
}
