using System.Text.Json.Nodes;
using AudioLab.Core.Model;
using static AudioLab.Core.Tests.TestJson;

namespace AudioLab.Core.Tests;

public sealed class ProjectJsonRejectionTests
{
    /// <summary>What each broken file's error must mention, by the same description.</summary>
    private static readonly Dictionary<string, string> _expected = new()
    {
        ["version 3"] = "version 3",
        ["version 5"] = "version 5",
        ["no version"] = "no version",
        ["version as text"] = "$.version",
        ["unknown project field"] = "'surprise'",
        ["unknown track field"] = "'surprise'",
        ["unknown clip field"] = "'blobKey'",
        ["unknown instrument field"] = "'surprise'",
        ["missing owner"] = "'owner'",
        ["null owner"] = "UserId",
        ["empty owner"] = "UserId",
        ["numeric id"] = "TrackId",
        ["missing required field"] = "'volume'",
        ["null for a non-nullable field"] = "'Name'",
        ["text for a number"] = "$.tracks[0].pan",
        ["unknown enum value"] = "$.tracks[0].kind",
        ["unknown effect type"] = "'flanger'",
        ["unknown instrument type"] = "'organ'",
        ["wrong shape"] = "$.tracks",
        ["pan out of range"] = "tracks[0].pan: 2 must be from -1 to 1",
        ["negative volume"] = "tracks[0].volume: -0.1 must be at least 0",
        ["tempo out of range"] = "bpm: 0 must be above 0 and at most 1000",
        ["bad denominator"] = "timeSignature",
        ["duplicate track id"] = "tracks[1].id: the id trk_1 is used more than once",
        ["a send to a track that is not a bus"] = "tracks[0].sends[0].bus",
        ["a send to nothing"] = "trk_gone is not a bus",
        ["a group that does not exist"] = "tracks[4].group",
        ["a bus with a clip"] = "a bus has no clips",
        ["overlapping sections"] = "sections[1].start: a section must not overlap",
        ["unordered markers"] = "markers[1].time",
        ["a lane with the wrong step count"] = "beatPattern.lanes[0].steps",
        ["a note out of range"] = ".pitch: 128 must be from 0 to 127",
        ["an empty id"] = "TrackId",
        ["a single input without its channel"] = "tracks[4].input",
    };

    /// <summary>Each row breaks the sample project one way and names what the error must point at.</summary>
    public static TheoryData<string, Action<JsonObject>> BrokenFiles => new()
    {
        { "version 3", root => root["version"] = 3 },
        { "version 5", root => root["version"] = 5 },
        { "no version", root => root.Remove("version") },
        { "version as text", root => root["version"] = "4" },
        { "unknown project field", root => root["surprise"] = 1 },
        { "unknown track field", root => Track(root, 0)["surprise"] = 1 },
        { "unknown clip field", root => Clip(root)["blobKey"] = "blob-1" },
        { "unknown instrument field", root => Track(root, 1)["instrument"]!["surprise"] = 1 },
        { "missing owner", root => root.Remove("owner") },
        { "null owner", root => root["owner"] = null },
        { "empty owner", root => root["owner"] = string.Empty },
        { "numeric id", root => Track(root, 0)["id"] = 7 },
        { "missing required field", root => Track(root, 0).AsObject().Remove("volume") },
        { "null for a non-nullable field", root => Track(root, 0)["name"] = null },
        { "text for a number", root => Track(root, 0)["pan"] = "left" },
        { "unknown enum value", root => Track(root, 0)["kind"] = "folder" },
        { "unknown effect type", root => Track(root, 0)["effects"]![0]!["type"] = "flanger" },
        { "unknown instrument type", root => Track(root, 1)["instrument"]!["type"] = "organ" },
        { "wrong shape", root => root["tracks"] = "none" },
        { "pan out of range", root => Track(root, 0)["pan"] = 2 },
        { "negative volume", root => Track(root, 0)["volume"] = -0.1 },
        { "tempo out of range", root => root["bpm"] = 0 },
        { "bad denominator", root => root["timeSignature"]!["denominator"] = 5 },
        { "duplicate track id", root => Track(root, 1)["id"] = Track(root, 0)["id"]!.GetValue<string>() },
        { "a send to a track that is not a bus", root => Track(root, 0)["sends"]![0]!["bus"] = Track(root, 1)["id"]!.GetValue<string>() },
        { "a send to nothing", root => Track(root, 0)["sends"]![0]!["bus"] = "trk_gone" },
        { "a group that does not exist", root => Track(root, 4)["group"] = "grp_gone" },
        { "a bus with a clip", root => Track(root, 3)["clips"] = Track(root, 0)["clips"]!.DeepClone() },
        { "overlapping sections", root => root["sections"]![1]!["start"] = 10 },
        { "unordered markers", root => root["markers"]![0]!["time"] = 99 },
        { "a lane with the wrong step count", root => root["beatPattern"]!["lanes"]![0]!["steps"] = new JsonArray(true) },
        { "a note out of range", root => Track(root, 1)["midiClips"]![0]!["notes"]![0]!["pitch"] = 128 },
        { "an empty id", root => Track(root, 0)["id"] = string.Empty },
        { "a single input without its channel", root => Track(root, 4)["input"]!.AsObject().Remove("channel") },
    };

    [Theory]
    [MemberData(nameof(BrokenFiles))]
    public void AFileThatBreaksTheFormatIsRefusedAndTheErrorSaysWhere(string description, Action<JsonObject> breakIt)
    {
        JsonObject root = Parse(ProjectJson.Write(SampleProject.Create(UserId.Local)));
        breakIt(root);
        ProjectFormatException error = Assert.Throws<ProjectFormatException>(() => ProjectJson.Read(root.ToJsonString()));
        Assert.Contains(_expected[description], error.Message);
    }

    [Fact]
    public void EveryBrokenFileHasAnExpectedMessage()
    {
        Assert.Equal(BrokenFiles.Select(row => (string)row[0]).Order(), _expected.Keys.Order());
    }

    [Theory]
    [InlineData("")]
    [InlineData("not json")]
    [InlineData("[]")]
    [InlineData("{\"version\":")]
    public void TextThatIsNotAProjectObjectIsRefused(string text)
    {
        Assert.Throws<ProjectFormatException>(() => ProjectJson.Read(text));
    }

    [Fact]
    public void ADocumentThatBreaksARuleIsNotWritten()
    {
        ProjectDocument broken = SampleProject.Create(UserId.Local) with { Bpm = double.NaN };
        ProjectFormatException error = Assert.Throws<ProjectFormatException>(() => ProjectJson.Write(broken));
        Assert.Contains("bpm", Assert.Single(error.Problems));
    }

    [Fact]
    public void EveryProblemIsReportedWithItsPath()
    {
        JsonObject root = Parse(ProjectJson.Write(SampleProject.Create(UserId.Local)));
        Track(root, 0)["pan"] = 2;
        Track(root, 0)["volume"] = -1;
        ProjectFormatException error = Assert.Throws<ProjectFormatException>(() => ProjectJson.Read(root.ToJsonString()));
        Assert.Contains(error.Problems, problem => problem.StartsWith("tracks[0].pan", StringComparison.Ordinal));
        Assert.Contains(error.Problems, problem => problem.StartsWith("tracks[0].volume", StringComparison.Ordinal));
    }
}
