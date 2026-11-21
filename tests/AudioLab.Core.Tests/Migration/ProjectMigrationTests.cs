using System.Text.Json.Nodes;
using AudioLab.Core.Migration;
using AudioLab.Core.Model;

namespace AudioLab.Core.Tests.Migration;

public sealed class ProjectMigrationTests
{
    private static readonly UserId _owner = UserId.ForDoor("swarm", "7");

    [Fact]
    public void TheProjectIsVersion4AndBelongsToTheOwnerItArrivedWith()
    {
        ProjectDocument document = Migrate().Document;
        Assert.Equal(ProjectDocument.CurrentVersion, document.Version);
        Assert.Equal(_owner, document.Owner);
        Assert.Equal("Synthetic v3 project", document.Name);
        Assert.Equal(96, document.Bpm);
        Assert.False(document.MasterLimiter);
        Assert.True(document.Metronome);
        Assert.Equal(0.7, document.MasterVolume);
        Assert.Equal(new TimeSignature(6, 8), document.TimeSignature);
        ProjectView expected = new() { Ruler = RulerMode.Beats, EditMode = EditMode.Shuffle, Grid = "1/8" };
        Assert.Equal(expected, document.View);
    }

    [Fact]
    public void TheSameProjectMigratedForTwoUsersSharesNothing()
    {
        string json = V3Fixtures.Read(V3Fixtures.Full);
        MigrationResult alice = ProjectMigration.FromV3(json, UserId.ForDoor("swarm", "alice"), new IdService());
        MigrationResult bob = ProjectMigration.FromV3(json, UserId.ForDoor("swarm", "bob"), new IdService());
        Assert.Equal(UserId.ForDoor("swarm", "alice"), alice.Document.Owner);
        Assert.Equal(UserId.ForDoor("swarm", "bob"), bob.Document.Owner);
        Assert.Empty(alice.Media.Select(media => media.Media).Intersect(bob.Media.Select(media => media.Media)));
        Assert.NotEqual(alice.Document.Id, bob.Document.Id);
    }

    [Fact]
    public void EveryIdIsAServerIdAndNoVersion3IdSurvives()
    {
        string json = ProjectJson.Write(Migrate().Document);
        foreach (string old in new[] { "track-", "clip-", "blob-", "group-", "midi-a-1", "marker-x", "section-", "tempo-a" })
        {
            Assert.DoesNotContain(old, json);
        }
    }

    [Fact]
    public void SendsFollowTheirBusToItsNewIdAndDanglingOnesAreDropped()
    {
        MigrationResult result = Migrate();
        IReadOnlyList<Track> tracks = result.Document.Tracks;
        TrackId bus = tracks[3].Id;
        Assert.Equal(new Send(bus, 0.4, true, false), Assert.Single(tracks[0].Sends));
        Assert.Equal(new Send(bus, 0.25, false, true), Assert.Single(tracks[1].Sends));
        Assert.Empty(tracks[3].Sends);
        Assert.Contains(result.Warnings, warning => warning.Contains("its bus is not in the project"));
        Assert.Contains(result.Warnings, warning => warning.Contains("its target is not a bus"));
        Assert.Contains(result.Warnings, warning => warning.Contains("already sends to that bus"));
        Assert.Contains(result.Warnings, warning => warning.Contains("a bus cannot send"));
    }

    [Fact]
    public void ArmAndMonitorAreAlwaysOffAfterMigration()
    {
        Track vocal = Migrate().Document.Tracks[0];
        Assert.False(vocal.Armed);
        Assert.False(vocal.Monitor);
        Assert.Equal(new TrackInput("mic-1", InputChannelMode.Single, 2), vocal.Input);
    }

    [Fact]
    public void AudioKeysBecomeOneMediaIdEachWhereverTheyAreUsed()
    {
        MigrationResult result = Migrate();
        Assert.Equal(["blob-1-100", "blob-2-200", "blob-9-900"], result.Media.Select(media => media.BlobKey));
        MediaId shared = result.Media[0].Media;
        IReadOnlyList<AudioClip> clips = result.Document.Tracks[0].Clips;
        Assert.Equal(shared, clips[0].Media);
        Assert.Equal(shared, clips[1].Media);
        Assert.Equal(result.Media[1].Media, clips[2].Media);
        Assert.Equal(shared, ((SamplerInstrument)result.Document.Tracks[2].Instrument!).Sample);
        Assert.Equal(shared, result.Document.BeatPattern.Lanes[0].Media);
        Assert.Equal(result.Media[2].Media, result.Document.BeatPattern.Lanes[1].Media);
    }

    [Fact]
    public void ClipMetaIsCarriedWithoutLossAndItsClipLinksFollowTheNewIds()
    {
        MigrationResult result = Migrate();
        IReadOnlyList<AudioClip> clips = result.Document.Tracks[0].Clips;
        ScoreMeta score = clips[0].Meta!.Score!;
        Assert.Equal("X:1\nK:C\nCDEF|", score.Abc);
        Assert.Equal(4294967295, score.Seed);
        Assert.Equal("Version A", score.Label);
        Assert.Equal(3, score.Extra!["futureThing"].GetProperty("nested").GetArrayLength());
        Assert.Equal(clips[1].Id, score.Parent);
        ClipLyrics lyrics = clips[0].Meta!.Lyrics!;
        Assert.Equal("[Verse]\nla la", lyrics.Text);
        Assert.Equal([new LyricWord("la", 0.5, 0.9, 0), new LyricWord("la", 1, 1.01, 0, true)], lyrics.Words);
        Assert.Equal(new StemOf(clips[0].Id, "vocals"), clips[1].Meta!.StemOf);
        Assert.Equal("future", lyrics.Extra!["alignedBy"].GetString());
        Assert.Null(clips[1].Meta!.Score!.Parent);
    }

    [Fact]
    public void AClipLinkToAClipThatIsNotInTheProjectIsClearedAndTheStemNameKept()
    {
        MigrationResult result = Migrate();
        ClipMeta orphan = result.Document.Tracks[0].Clips[2].Meta!;
        Assert.Null(orphan.Score!.Parent);
        Assert.Equal(new StemOf(null, "drums"), orphan.StemOf);
        Assert.Contains(
            result.Warnings,
            warning => warning.Contains("meta.score.parent") && warning.Contains("cleared"));
    }

    [Fact]
    public void AClipWithNoAudioIsDroppedAsTheExtensionDoesOnOpen()
    {
        MigrationResult result = Migrate();
        Assert.Equal(3, result.Document.Tracks[0].Clips.Count);
        Assert.Contains(
            result.Warnings,
            warning => warning.Contains("clips[3] was dropped") && warning.Contains("no audio key"));
    }

    [Fact]
    public void TimesBeforeTheStartOfAClipAreRepairedAndSaidSo()
    {
        IReadOnlyList<string> warnings = Migrate().Warnings;
        Assert.Contains(warnings, warning => warning.Contains("2 notes starting before the clip"));
        Assert.Contains(warnings, warning => warning.Contains("1 controller events before the clip"));
    }

    [Fact]
    public void FieldsOfTheOtherInstrumentKindAreIgnoredWithAWarning()
    {
        MigrationResult result = Migrate();
        Assert.Contains(
            result.Warnings,
            warning => warning.StartsWith("tracks[1].instrument is a synth but had sampler fields (rootNote, oneShot)", StringComparison.Ordinal));
        Assert.Contains(
            result.Warnings,
            warning => warning.StartsWith("tracks[2].instrument is a sampler but had synth fields (preset)", StringComparison.Ordinal));
        Assert.IsType<SynthInstrument>(result.Document.Tracks[1].Instrument);
        Assert.IsType<SamplerInstrument>(result.Document.Tracks[2].Instrument);
    }

    [Fact]
    public void TempoChangesAreCheckedSortedAndMadeUnique()
    {
        MigrationResult result = Migrate();
        Assert.Equal(
            [(4.0, 100.0), (8.0, 120.0)],
            result.Document.TempoMap.Select(change => (change.Time, change.Bpm)));
        Assert.Contains(result.Warnings, warning => warning.Contains("tempoMap[4] is a second change at 8 seconds"));
    }

    [Fact]
    public void MarkersAreSortedAndNoneStartsBeforeTheSong()
    {
        IReadOnlyList<Marker> markers = Migrate().Document.Markers;
        Assert.Equal(["Early", "Marker 2", "Chorus"], markers.Select(marker => marker.Name));
        Assert.Equal([0.0, 2.0, 30.0], markers.Select(marker => marker.Time));
        Assert.Equal("#f4a261", markers[2].Color);
    }

    [Fact]
    public void SectionsAreCleanedTheWayTheExtensionCleansThemOnOpen()
    {
        IReadOnlyList<Section> sections = Migrate().Document.Sections;
        Assert.Equal(
            [("Intro", 0.0, 12.0, "#f4a261"), ("Section 2", 12.0, 40.0, "#f2c94c"), ("Drop", 40.0, 60.0, "#ef6f91")],
            sections.Select(section => (section.Name, section.Start, section.End, section.Color)));
    }

    [Fact]
    public void GroupsKeepTheirSettingsAndTracksKeepOnlyGroupsThatExist()
    {
        MigrationResult result = Migrate();
        IReadOnlyList<TrackGroup> groups = result.Document.Groups;
        Assert.Equal(["Drums", "Group 2"], groups.Select(group => group.Name));
        TrackGroup drums = groups[0];
        Assert.Equal(
            ("Drums", "#7a8cff", true, false, false, true, 0.5),
            (drums.Name, drums.Color, drums.Collapsed, drums.Tint, drums.Muted, drums.Soloed, drums.Volume));
        Assert.Equal("#7a8cff", groups[1].Color);
        Assert.Equal(1.5, groups[1].Volume);
        Assert.Equal(groups[0].Id, result.Document.Tracks[0].Group);
        Assert.Null(result.Document.Tracks[1].Group);
        Assert.Contains(result.Warnings, warning => warning.Contains("group that does not exist"));
    }

    [Fact]
    public void ADroppedIconIsReportedAndAValidOneKept()
    {
        MigrationResult result = Migrate();
        Assert.Equal("🎤", result.Document.Tracks[0].Icon);
        Assert.Null(result.Document.Tracks[1].Icon);
        Assert.Contains(result.Warnings, warning => warning.Contains("icon"));
    }

    [Fact]
    public void TheDrumPatternKeepsItsStepsAndSizesEveryLaneToTheStepCount()
    {
        BeatPattern pattern = Migrate().Document.BeatPattern;
        Assert.Equal(4, pattern.Steps);
        Assert.Equal(0.2, pattern.Swing);
        Assert.Equal([true, false, true, false], pattern.Lanes[0].Steps);
        Assert.Equal([false, false, false, false], pattern.Lanes[1].Steps);
        Assert.Equal(0.9, pattern.Lanes[0].Gain);
    }

    [Fact]
    public void MidiClipsKeepTheirNotesWithFreshIdsAndOrderedControllers()
    {
        MigrationResult result = Migrate();
        MidiClip clip = result.Document.Tracks[1].MidiClips[0];
        Assert.Equal(2, clip.Length);
        Assert.Equal(new NoteId(4), clip.NextNoteId);
        Assert.Equal(
            [
                new MidiNote(new NoteId(1), 60, 0, 0.5, 100),
                new MidiNote(new NoteId(2), 64, 0.5, 1, 100),
                new MidiNote(new NoteId(3), 67, 0, 0.75, 90),
            ],
            clip.Notes);
        Assert.Equal(
            [new MidiControlChange(0, 64, 127), new MidiControlChange(0, 1, 10), new MidiControlChange(2, 64, 0)],
            clip.ControlChanges);
        Assert.True(clip.Muted);
        Assert.Equal("#4fcdb8", clip.Color);
    }

    [Fact]
    public void AMidiClipShorterThanTheMinimumGrowsToItAndLosesNotesThatNeverPlayed()
    {
        MigrationResult result = Migrate();
        MidiClip tiny = result.Document.Tracks[1].MidiClips[1];
        Assert.Equal(0.05, tiny.Length);
        Assert.Empty(tiny.Notes);
        Assert.Equal(NoteId.First, tiny.NextNoteId);
        Assert.Contains(result.Warnings, warning => warning.Contains("1 notes starting after the clip end"));
    }

    [Fact]
    public void AColourThatIsNotHexIsReplacedAndSaidSo()
    {
        JsonObject root = TestJson.Parse(V3Fixtures.Read(V3Fixtures.Full));
        TestJson.Track(root, 0)["color"] = "red;background:url(//evil)";
        TestJson.Clip(root)["color"] = "rgb(1,2,3)";
        MigrationResult result = ProjectMigration.FromV3(root.ToJsonString(), _owner, new SequentialIdService());
        Assert.Equal(ProjectDefaults.TrackColor(0), result.Document.Tracks[0].Color);
        Assert.Null(result.Document.Tracks[0].Clips[0].Color);
        Assert.Contains(
            result.Warnings,
            warning => warning.Contains("tracks[0].color") && warning.Contains("default was used"));
    }

    [Fact]
    public void EffectsAndInstrumentsTakeTheExtensionsDefaultsForWhatWasLeftOut()
    {
        IReadOnlyList<Track> tracks = Migrate().Document.Tracks;
        EqEffect eq = Assert.IsType<EqEffect>(tracks[0].Effects[0]);
        Assert.Equal((90.0, 2.5, 200.0, 20000.0), (eq.HpFreq, eq.MidGain, eq.LowFreq, eq.LpFreq));
        DelayEffect delay = Assert.IsType<DelayEffect>(tracks[0].Effects[1]);
        Assert.False(delay.Enabled);
        Assert.True(delay.Sync);
        Assert.Equal("1/8.", delay.Division);
        CompressorEffect compressor = Assert.IsType<CompressorEffect>(tracks[1].Effects[0]);
        Assert.Equal((-24.0, 4.0), (compressor.Threshold, compressor.Ratio));
        Assert.False(Assert.IsType<ReverbEffect>(tracks[1].Effects[1]).Enabled);
        SynthInstrument synth = Assert.IsType<SynthInstrument>(tracks[1].Instrument);
        Assert.Equal((OscillatorWave.Triangle, 0.005, 0.5), (synth.Osc1, synth.Attack, synth.Gain));
        SamplerInstrument sampler = Assert.IsType<SamplerInstrument>(tracks[2].Instrument);
        Assert.Equal(("snare.wav", 60, true), (sampler.SampleName, sampler.RootNote, sampler.OneShot));
    }

    [Fact]
    public void AutomationIsOrderedAndTheLaneChoicesAreKept()
    {
        Track vocal = Migrate().Document.Tracks[0];
        Assert.Equal([new AutomationPoint(1, 0.8), new AutomationPoint(4, 0.2)], vocal.Automation.Volume);
        Assert.Equal(AutomationMode.Touch, vocal.Automation.Mode);
        TrackView expected = new() { Height = 80, AutomationVisible = true, AutomationParameter = AutomationParameter.Pan };
        Assert.Equal(expected, vocal.View);
        Assert.Equal(2, vocal.TakeCount);
    }

    [Fact]
    public void TheMigratedProjectWritesAndReadsBackIdentically()
    {
        string json = ProjectJson.Write(Migrate().Document);
        Assert.Equal(json, ProjectJson.Write(ProjectJson.Read(json)));
    }

    [Fact]
    public void TheSameInputAndIdsGiveTheSameProject()
    {
        MigrationResult first = Migrate();
        MigrationResult second = Migrate();
        Assert.Equal(ProjectJson.Write(first.Document), ProjectJson.Write(second.Document));
        Assert.Equal(first.Media, second.Media);
        Assert.Equal(first.Warnings, second.Warnings);
    }

    [Fact]
    public void TheSmallestProjectGetsTheExtensionsDefaults()
    {
        MigrationResult result = Migrate(V3Fixtures.Minimal);
        Assert.Equal("Untitled project", result.Document.Name);
        Assert.Equal(120, result.Document.Bpm);
        Assert.Equal(new TimeSignature(4, 4), result.Document.TimeSignature);
        Assert.True(result.Document.MasterLimiter);
        Assert.Equal(EditMode.Grid, result.Document.View.EditMode);
        Assert.Equal(16, result.Document.BeatPattern.Steps);
        Assert.Empty(result.Document.Tracks);
        Assert.Empty(result.Media);
    }

    private static MigrationResult Migrate(string fixture = V3Fixtures.Full)
    {
        return ProjectMigration.FromV3(V3Fixtures.Read(fixture), _owner, new SequentialIdService());
    }
}
