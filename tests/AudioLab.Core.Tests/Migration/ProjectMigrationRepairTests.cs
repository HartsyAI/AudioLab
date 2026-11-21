using System.Diagnostics;
using System.Text.Json.Nodes;
using AudioLab.Core.Migration;
using AudioLab.Core.Model;
using static AudioLab.Core.Tests.TestJson;

namespace AudioLab.Core.Tests.Migration;

public sealed class ProjectMigrationRepairTests
{
    private static readonly UserId _owner = UserId.ForDoor("swarm", "7");

    [Theory]
    [InlineData("0")]
    [InlineData("null")]
    [InlineData("-3")]
    public void AClipWhoseLengthWasNeverStoredIsDroppedBecauseAMigrationCannotMeasureAudio(string duration)
    {
        JsonObject root = Parse(V3Fixtures.Read(V3Fixtures.Full));
        Clip(root)["duration"] = JsonNode.Parse(duration);
        MigrationResult result = Migrate(root.ToJsonString());
        Assert.Equal(2, result.Document.Tracks[0].Clips.Count);
        Assert.Contains(
            result.Warnings,
            warning => warning.StartsWith("tracks[0].clips[0] was dropped", StringComparison.Ordinal)
                && warning.Contains("length was never stored"));
    }

    [Fact]
    public void NamesLongerThanTheLimitAreCutAndSaidSo()
    {
        string longName = new('n', 250);
        JsonObject root = Parse(V3Fixtures.Read(V3Fixtures.Full));
        root["projectName"] = longName;
        Track(root, 0)["name"] = longName;
        Clip(root)["name"] = longName;
        root["markers"]![0]!["name"] = longName;
        root["beatPattern"]!["lanes"]![0]!["name"] = longName;
        root["groups"]![0]!["name"] = longName;
        MigrationResult result = Migrate(root.ToJsonString());
        ProjectDocument document = result.Document;
        string[] names =
        [
            document.Name,
            document.Tracks[0].Name,
            document.Tracks[0].Clips[0].Name,
            document.Markers.OrderByDescending(marker => marker.Name.Length).First().Name,
            document.Groups[0].Name,
        ];
        Assert.All(names, name => Assert.True(name.Length <= ProjectLimits.MaxName));
        Assert.Equal(200, document.Name.Length);
        string[] paths =
        [
            "projectName",
            "tracks[0].name",
            "tracks[0].clips[0].name",
            "markers[0].name",
            "beatPattern.lanes[0].name",
            "groups[0].name",
        ];
        foreach (string path in paths)
        {
            Assert.Contains(result.Warnings, warning => warning.StartsWith(path + " was 250 characters", StringComparison.Ordinal));
        }
    }

    [Fact]
    public void ControlAndTextDirectionCharactersAreRemovedFromANameAndSaidSo()
    {
        JsonObject root = Parse(V3Fixtures.Read(V3Fixtures.Full));
        Track(root, 0)["name"] = "Lead\u0007 vocal‮";
        MigrationResult result = Migrate(root.ToJsonString());
        Assert.Equal("Lead vocal", result.Document.Tracks[0].Name);
        Assert.Contains(
            result.Warnings,
            warning => warning.StartsWith("tracks[0].name held control or text-direction characters", StringComparison.Ordinal));
    }

    [Fact]
    public void ATimeAHairBelowZeroFromRoundingBecomesZeroButARealNegativeIsStillRefused()
    {
        JsonObject root = Parse(V3Fixtures.Read(V3Fixtures.Full));
        Clip(root)["trimEnd"] = -1e-12;
        Clip(root)["startTime"] = -0.001;
        Clip(root, 0, 1)["offset"] = -0.005;
        MigrationResult result = Migrate(root.ToJsonString());
        AudioClip clip = result.Document.Tracks[0].Clips[0];
        Assert.Equal((0.0, 0.0), (clip.TrimEnd, clip.Start));
        Assert.Contains(result.Warnings, warning => warning.StartsWith("tracks[0].clips[0].startTime was -0.001", StringComparison.Ordinal));
        Clip(root)["startTime"] = -1;
        ProjectFormatException error = Assert.Throws<ProjectFormatException>(() => Migrate(root.ToJsonString()));
        Assert.Contains(".start: -1 must be from 0", error.Message);
    }

    [Fact]
    public void ManyUnnamedGroupsAreNamedInLinearTime()
    {
        JsonObject root = Parse(V3Fixtures.Read(V3Fixtures.Minimal));
        JsonArray groups = [];
        for (int i = 0; i < 1000; i++)
        {
            groups.Add(new JsonObject { ["id"] = $"group-{i}", ["name"] = "" });
        }
        root["groups"] = groups;
        Stopwatch watch = Stopwatch.StartNew();
        MigrationResult result = Migrate(root.ToJsonString());
        Assert.Equal("Group 1", result.Document.Groups[0].Name);
        Assert.Equal("Group 1000", result.Document.Groups[999].Name);
        Assert.True(watch.Elapsed < TimeSpan.FromSeconds(2), watch.Elapsed.ToString());
    }

    [Fact]
    public void UnnamedGroupsSkipTheNamesOthersAlreadyUse()
    {
        JsonObject root = Parse(V3Fixtures.Read(V3Fixtures.Minimal));
        root["groups"] = new JsonArray(
            new JsonObject { ["id"] = "g1", ["name"] = "Group 2" },
            new JsonObject { ["id"] = "g2", ["name"] = "" },
            new JsonObject { ["id"] = "g3", ["name"] = "" });
        MigrationResult result = Migrate(root.ToJsonString());
        Assert.Equal(["Group 2", "Group 3", "Group 4"], result.Document.Groups.Select(group => group.Name));
    }

    [Theory]
    [InlineData("groups", 1025)]
    [InlineData("markers", 100_001)]
    [InlineData("tracks", 1025)]
    public void AListLongerThanTheProjectKeepsIsRefusedBeforeAnyOfItIsConverted(string list, int count)
    {
        JsonObject root = Parse(V3Fixtures.Read(V3Fixtures.Minimal));
        JsonArray items = [];
        for (int i = 0; i < count; i++)
        {
            items.Add(new JsonObject());
        }
        root[list] = items;
        ProjectFormatException error = Assert.Throws<ProjectFormatException>(() => Migrate(root.ToJsonString()));
        Assert.StartsWith(list + ":", error.Message);
    }

    [Fact]
    public void ANoteThatLastsNoTimeIsDroppedWithAWarning()
    {
        JsonObject root = Parse(V3Fixtures.Read(V3Fixtures.Full));
        Track(root, 1)["midiClips"]![0]!["notes"]![0]!["dur"] = 0;
        MigrationResult result = Migrate(root.ToJsonString());
        Assert.Contains(result.Warnings, warning => warning.Contains("tracks[1].midiClips[0] had 2 notes that last no time"));
    }

    [Theory]
    [InlineData("tracks[0].clips[0].gain: 99", "tracks[0].clips['clip-1'].gain: 99 must be from 0 to 16")]
    [InlineData("tracks[0].fx[0].hpFreq: 5", "tracks[0].fx[0].hpFreq: 5 must be from 20 to 1000")]
    public void ARefusalNamesTheValueByItsVersion3IdOrPosition(string field, string expected)
    {
        JsonObject root = Parse(V3Fixtures.Read(V3Fixtures.Full));
        if (field.StartsWith("tracks[0].clips[0].gain", StringComparison.Ordinal))
        {
            Clip(root)["gain"] = 99;
        }
        else
        {
            Track(root, 0)["fx"]![0]!["params"]!["hpFreq"] = 5;
        }
        ProjectFormatException error = Assert.Throws<ProjectFormatException>(() => Migrate(root.ToJsonString()));
        Assert.Contains(expected, error.Message);
        Assert.DoesNotContain("clp_", error.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("fxn_", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ARefusalOfAMidiClipNamesItByItsPositionInTheList()
    {
        JsonObject root = Parse(V3Fixtures.Read(V3Fixtures.Full));
        Track(root, 1)["midiClips"]![0]!["notes"]![0]!["pitch"] = 200;
        ProjectFormatException error = Assert.Throws<ProjectFormatException>(() => Migrate(root.ToJsonString()));
        Assert.Contains("tracks[1].midiClips[0].notes[", error.Message);
    }

    [Theory]
    [InlineData("audio", "blob-1-100", "5")]
    [InlineData("audio", "blob-1-100", "[]")]
    [InlineData("blobs", "blob-x", "5")]
    public void AnEntryOfTheWrongShapeIsRefusedWithoutNamingAnInternalType(string list, string key, string value)
    {
        JsonObject root = Parse(V3Fixtures.Read(V3Fixtures.Full));
        root[list] ??= new JsonObject();
        root[list]![key] = JsonNode.Parse(value);
        ProjectFormatException error = Assert.Throws<ProjectFormatException>(() => Migrate(root.ToJsonString()));
        Assert.DoesNotContain("AudioLab.Core", error.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("V3", error.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("\"audio\": {", "\"audio\": { \"blob-1-100\": { \"type\": \"audio/wav\", \"size\": 1 },")]
    [InlineData("\"hpFreq\": 90,", "\"hpFreq\": 90, \"hpFreq\": 91,")]
    [InlineData("\"deviceId\": \"mic-1\",", "\"deviceId\": \"mic-1\", \"deviceId\": \"mic-2\",")]
    public void ADuplicateKeyAnywhereInTheFileIsRefused(string find, string replace)
    {
        string full = V3Fixtures.Read(V3Fixtures.Full);
        string json = full.Replace(find, replace, StringComparison.Ordinal);
        Assert.NotEqual(full, json);
        Assert.Throws<ProjectFormatException>(() => Migrate(json));
    }

    [Theory]
    [InlineData("audio.blob-1-100", "blobKey")]
    [InlineData("tracks[0].input", "blobKey")]
    [InlineData("tracks[0].sends[0]", "blobKey")]
    [InlineData("tracks[0].automation.volume[0]", "blobKey")]
    [InlineData("tracks[1].midiClips[0].notes[0]", "blobKey")]
    [InlineData("tracks[1].midiClips[0].cc[0]", "blobKey")]
    [InlineData("tracks[0].clips[0].meta.lyrics.words[0]", "blobKey")]
    [InlineData("groups[0]", "blobKey")]
    [InlineData("beatPattern", "blobKey")]
    public void AnUnknownKeyInsideAnyRecordOfTheFileIsRefused(string at, string key)
    {
        JsonObject root = Parse(V3Fixtures.Read(V3Fixtures.Full));
        JsonNode node = root;
        foreach (string step in at.Split('.'))
        {
            node = Step(node, step);
        }
        node[key] = 1;
        ProjectFormatException error = Assert.Throws<ProjectFormatException>(() => Migrate(root.ToJsonString()));
        Assert.Contains("'blobKey'", error.Message);
    }

    [Fact]
    public void ADuplicateKeyInsideAnEffectParameterBagIsRefused()
    {
        string full = V3Fixtures.Read(V3Fixtures.Full);
        string json = full.Replace("\"midGain\": 2.5", "\"midGain\": 2.5, \"midGain\": 3", StringComparison.Ordinal);
        Assert.NotEqual(full, json);
        Assert.Throws<ProjectFormatException>(() => Migrate(json));
    }

    private static JsonNode Step(JsonNode node, string step)
    {
        int open = step.IndexOf('[', StringComparison.Ordinal);
        if (open < 0)
        {
            return node[step]!;
        }
        JsonNode list = node[step[..open]]!;
        return list[int.Parse(step[(open + 1)..^1], System.Globalization.CultureInfo.InvariantCulture)]!;
    }

    private static MigrationResult Migrate(string json)
    {
        return ProjectMigration.FromV3(json, _owner, new SequentialIdService());
    }
}
