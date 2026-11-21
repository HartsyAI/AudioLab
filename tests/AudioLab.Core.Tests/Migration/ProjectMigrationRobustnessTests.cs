using System.Globalization;
using System.Text.Json.Nodes;
using AudioLab.Core.Migration;
using AudioLab.Core.Model;
using static AudioLab.Core.Tests.TestJson;

namespace AudioLab.Core.Tests.Migration;

public sealed class ProjectMigrationRobustnessTests
{
    private static readonly UserId _owner = UserId.ForDoor("swarm", "7");

    /// <summary>Each row puts a null where the extension always writes an object.</summary>
    public static TheoryData<string, Action<JsonObject>> NullElements => new()
    {
        { "tracks", root => root["tracks"]!.AsArray().Add(null) },
        { "markers", root => root["markers"]!.AsArray().Add(null) },
        { "sections", root => root["sections"]!.AsArray().Add(null) },
        { "groups", root => root["groups"]!.AsArray().Add(null) },
        { "tempoMap", root => root["tempoMap"]!.AsArray().Add(null) },
        { "beatPattern.lanes", root => root["beatPattern"]!["lanes"]!.AsArray().Add(null) },
        { "tracks[0].clips", root => Track(root, 0)["clips"]!.AsArray().Add(null) },
        { "tracks[1].midiClips", root => Track(root, 1)["midiClips"]!.AsArray().Add(null) },
        { "tracks[1].midiClips[0].notes", root => Track(root, 1)["midiClips"]![0]!["notes"]!.AsArray().Add(null) },
        { "tracks[1].midiClips[0].cc", root => Track(root, 1)["midiClips"]![0]!["cc"]!.AsArray().Add(null) },
        { "tracks[0].fx", root => Track(root, 0)["fx"]!.AsArray().Add(null) },
        { "tracks[0].sends", root => Track(root, 0)["sends"]!.AsArray().Add(null) },
        { "tracks[0].automation.volume", root => Track(root, 0)["automation"]!["volume"]!.AsArray().Add(null) },
        { "tracks[0].automation.pan", root => Track(root, 0)["automation"]!["pan"]!.AsArray().Add(null) },
    };

    [Theory]
    [MemberData(nameof(NullElements))]
    public void ANullWhereAnObjectBelongsIsRefusedWithAnErrorAndNotACrash(string list, Action<JsonObject> addNull)
    {
        JsonObject root = Parse(V3Fixtures.Read(V3Fixtures.Full));
        addNull(root);
        ProjectFormatException error = Assert.Throws<ProjectFormatException>(() => Migrate(root.ToJsonString()));
        Assert.Contains(list, error.Message);
    }

    [Theory]
    [InlineData("1e999")]
    [InlineData("\"loud\"")]
    [InlineData("true")]
    public void AnEffectParameterThatIsNotAFiniteNumberIsRefusedWithItsPath(string value)
    {
        JsonObject root = Parse(V3Fixtures.Read(V3Fixtures.Full));
        Track(root, 0)["fx"]![0]!["params"]!["hpFreq"] = JsonNode.Parse(value);
        ProjectFormatException error = Assert.Throws<ProjectFormatException>(() => Migrate(root.ToJsonString()));
        Assert.Contains("tracks[0].fx[0].params.hpFreq:", error.Message);
    }

    [Fact]
    public void ASynthParameterOutsideItsKnobRangeIsRefusedByTheValidator()
    {
        JsonObject root = Parse(V3Fixtures.Read(V3Fixtures.Full));
        Track(root, 1)["instrument"]!["cutoff"] = -5;
        ProjectFormatException error = Assert.Throws<ProjectFormatException>(() => Migrate(root.ToJsonString()));
        Assert.Contains("tracks[1].instrument.cutoff: -5 must be from 100 to 12000", error.Message);
    }

    [Theory]
    [InlineData(1_500_000_000)]
    [InlineData(257)]
    [InlineData(-4)]
    public void ADrumPatternWithTooManyStepsIsRefusedBeforeAnythingIsSizedByIt(int steps)
    {
        JsonObject root = Parse(V3Fixtures.Read(V3Fixtures.Full));
        root["beatPattern"]!["steps"] = steps;
        ProjectFormatException error = Assert.Throws<ProjectFormatException>(() => Migrate(root.ToJsonString()));
        Assert.Contains("beatPattern.steps:", error.Message);
    }

    [Fact]
    public void ADrumPatternWithTooManyLanesIsRefused()
    {
        JsonObject root = Parse(V3Fixtures.Read(V3Fixtures.Full));
        JsonArray lanes = root["beatPattern"]!["lanes"]!.AsArray();
        for (int i = 0; i < 130; i++)
        {
            lanes.Add(lanes[0]!.DeepClone());
        }
        ProjectFormatException error = Assert.Throws<ProjectFormatException>(() => Migrate(root.ToJsonString()));
        Assert.Contains("beatPattern.lanes:", error.Message);
    }

    [Theory]
    [InlineData("de-DE")]
    [InlineData("fr-FR")]
    [InlineData("tr-TR")]
    public void WarningsAndTheProjectAreTheSameInEveryCulture(string culture)
    {
        JsonObject root = Parse(V3Fixtures.Read(V3Fixtures.Full));
        Track(root, 1)["midiClips"]![0]!["notes"]![0]!["pitch"] = 60.5;
        string json = root.ToJsonString();
        MigrationResult baseline = WithCulture("en-US", () => Migrate(json));
        MigrationResult other = WithCulture(culture, () => Migrate(json));
        Assert.Equal(baseline.Warnings, other.Warnings);
        Assert.Equal(ProjectJson.Write(baseline.Document), ProjectJson.Write(other.Document));
        Assert.Contains(other.Warnings, warning => warning.Contains("was 60.5 and was rounded to 61"));
        Assert.Contains(other.Warnings, warning => warning.Contains("at 8 seconds"));
        Assert.Contains(other.Warnings, warning => warning.Contains("shorter than 0.1 seconds"));
    }

    [Fact]
    public void ADuplicateKeyIsRefusedInsteadOfTheLastOneWinning()
    {
        string full = V3Fixtures.Read(V3Fixtures.Full);
        string json = full.Replace("\"bpm\": 96,", "\"bpm\": 96, \"bpm\": 200,", StringComparison.Ordinal);
        Assert.NotEqual(full, json);
        Assert.Throws<ProjectFormatException>(() => Migrate(json));
    }

    [Theory]
    [InlineData("bpm", "\"120\"")]
    [InlineData("masterVolume", "\"1\"")]
    [InlineData("projectName", "7")]
    [InlineData("metronome", "\"yes\"")]
    [InlineData("tracks", "{}")]
    [InlineData("markers", "\"none\"")]
    public void AValueOfTheWrongJsonTypeIsRefused(string field, string value)
    {
        JsonObject root = Parse(V3Fixtures.Read(V3Fixtures.Full));
        root[field] = JsonNode.Parse(value);
        Assert.Throws<ProjectFormatException>(() => Migrate(root.ToJsonString()));
    }

    [Fact]
    public void ATrackThatSendsToItselfLosesTheSendBecauseItIsNotABus()
    {
        JsonObject root = Parse(V3Fixtures.Read(V3Fixtures.Full));
        JsonObject self = new() { ["busId"] = "track-1", ["level"] = 1.0, ["pre"] = false, ["muted"] = false };
        Track(root, 0)["sends"]!.AsArray().Add(self);
        MigrationResult result = Migrate(root.ToJsonString());
        Assert.Single(result.Document.Tracks[0].Sends);
        Assert.Contains(
            result.Warnings,
            warning => warning.Contains("tracks[0].sends[4] was dropped: its target is not a bus"));
    }

    [Fact]
    public void AScoreThatIsItsOwnParentLosesTheLink()
    {
        JsonObject root = Parse(V3Fixtures.Read(V3Fixtures.Full));
        Clip(root)["meta"]!["score"]!["parent"] = "clip-1";
        MigrationResult result = Migrate(root.ToJsonString());
        Assert.Null(result.Document.Tracks[0].Clips[0].Meta!.Score!.Parent);
        Assert.Contains(result.Warnings, warning => warning.Contains("'Take 1' lead back to it"));
    }

    [Fact]
    public void ScoreVersionsThatLeadBackToEachOtherAreBrokenAtOneLink()
    {
        JsonObject root = Parse(V3Fixtures.Read(V3Fixtures.Full));
        Clip(root, 0, 1)["meta"]!["score"]!["parent"] = "clip-1";
        MigrationResult result = Migrate(root.ToJsonString());
        IReadOnlyList<AudioClip> clips = result.Document.Tracks[0].Clips;
        int links = clips.Count(clip => clip.Meta?.Score?.Parent is not null);
        Assert.Equal(1, links);
        Assert.Single(result.Warnings, warning => warning.Contains("lead back to it"));
    }

    [Fact]
    public void AStoredFadeLongerThanThePlayingPartIsShortenedAsTheExtensionDoesWhenItRenders()
    {
        MigrationResult result = Migrate(V3Fixtures.Read(V3Fixtures.Full));
        AudioClip clip = result.Document.Tracks[0].Clips[2];
        Assert.Equal((5.0, 5.0), (clip.FadeIn, clip.FadeOut));
        Assert.Contains(result.Warnings, warning => warning.StartsWith("tracks[0].clips[2].fadeIn was 7 seconds", StringComparison.Ordinal));
        Assert.Contains(result.Warnings, warning => warning.StartsWith("tracks[0].clips[2].fadeOut was 9 seconds", StringComparison.Ordinal));
    }

    [Fact]
    public void FadesThatFitAreKeptEvenWhenTogetherTheyAreLongerThanTheClip()
    {
        JsonObject root = Parse(V3Fixtures.Read(V3Fixtures.Full));
        Clip(root, 0, 2)["fadeIn"] = 4;
        Clip(root, 0, 2)["fadeOut"] = 4;
        MigrationResult result = Migrate(root.ToJsonString());
        Assert.Equal((4.0, 4.0), (result.Document.Tracks[0].Clips[2].FadeIn, result.Document.Tracks[0].Clips[2].FadeOut));
        Assert.DoesNotContain(result.Warnings, warning => warning.Contains("fadeIn was"));
    }

    [Fact]
    public void AnEffectWithNoEnabledFlagIsOffBecauseTheExtensionCopiesTheFlagAsItIs()
    {
        JsonObject root = Parse(V3Fixtures.Read(V3Fixtures.Full));
        Track(root, 1)["fx"]![0]!.AsObject().Remove("enabled");
        Assert.False(Migrate(root.ToJsonString()).Document.Tracks[1].Effects[0].Enabled);
    }

    [Theory]
    [InlineData(60.5, 61)]
    [InlineData(61.5, 62)]
    [InlineData(60.4, 60)]
    public void FractionalNoteNumbersRoundTheWayJavaScriptRoundsThem(double pitch, int expected)
    {
        JsonObject root = Parse(V3Fixtures.Read(V3Fixtures.Full));
        Track(root, 1)["midiClips"]![0]!["notes"]![0]!["pitch"] = pitch;
        Assert.Equal(expected, Migrate(root.ToJsonString()).Document.Tracks[1].MidiClips[0].Notes[0].Pitch);
    }

    [Fact]
    public void WarningsNameTheValueTheyAreAbout()
    {
        IReadOnlyList<string> warnings = Migrate(V3Fixtures.Read(V3Fixtures.Full)).Warnings;
        Assert.Contains(warnings, warning => warning.StartsWith("tempoMap[2]", StringComparison.Ordinal));
        Assert.Contains(warnings, warning => warning.StartsWith("sections[2]", StringComparison.Ordinal));
        Assert.Contains(warnings, warning => warning.StartsWith("sections[1]", StringComparison.Ordinal));
        Assert.Contains(warnings, warning => warning.StartsWith("markers[2]", StringComparison.Ordinal));
        Assert.Contains(warnings, warning => warning.StartsWith("tracks[0].clips[3]", StringComparison.Ordinal));
        Assert.Contains(warnings, warning => warning.StartsWith("tracks[0].sends[1]", StringComparison.Ordinal));
    }

    private static MigrationResult Migrate(string json)
    {
        return ProjectMigration.FromV3(json, _owner, new SequentialIdService());
    }

    private static MigrationResult WithCulture(string name, Func<MigrationResult> run)
    {
        CultureInfo before = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo(name);
            return run();
        }
        finally
        {
            CultureInfo.CurrentCulture = before;
        }
    }
}
