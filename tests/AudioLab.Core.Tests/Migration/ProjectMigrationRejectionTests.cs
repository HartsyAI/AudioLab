using System.Text.Json.Nodes;
using AudioLab.Core.Migration;
using AudioLab.Core.Model;
using static AudioLab.Core.Tests.TestJson;

namespace AudioLab.Core.Tests.Migration;

public sealed class ProjectMigrationRejectionTests
{
    /// <summary>What each broken file's error must mention, by the same description.</summary>
    private static readonly Dictionary<string, string> _expected = new()
    {
        ["version 1"] = "older than version 3",
        ["version 2"] = "older than version 3",
        ["no version"] = "no version",
        ["version 4"] = "already version 4",
        ["version 5"] = "newer than",
        ["unknown project field"] = "'extra'",
        ["unknown track field"] = "'surprise'",
        ["unknown clip field"] = "'blob'",
        ["unknown group field"] = "'surprise'",
        ["unknown effect type"] = "flanger",
        ["unknown effect parameter"] = "params.wobble",
        ["effect parameter of the wrong type"] = "params.sync",
        ["duplicate track id"] = "used by two tracks",
        ["missing track id"] = "tracks[0].id",
        ["unknown track kind"] = "tracks[0].kind:",
        ["unknown input channels"] = "tracks[0].input.channels:",
        ["input channel zero"] = "tracks[0].input.channels:",
        ["unknown instrument type"] = "synth or sampler",
        ["unknown oscillator"] = "tracks[1].instrument.osc1:",
        ["note without pitch"] = "tracks[1].midiClips[0].notes[0].pitch:",
        ["note pitch out of range"] = ".pitch: 200 must be from 0 to 127",
        ["unknown automation mode"] = "tracks[0].automationMode:",
        ["unknown ruler"] = "rulerMode:",
        ["time signature of three numbers"] = "timeSignature:",
        ["time signature of 4/3"] = "timeSignature: the numerator",
        ["tempo out of range"] = "bpm: 2000 must be from 1 to 1000",
    };

    /// <summary>Each row breaks the synthetic version 3 project one way.</summary>
    public static TheoryData<string, Action<JsonObject>> BrokenFiles => new()
    {
        { "version 1", root => root["version"] = 1 },
        { "version 2", root => root["version"] = 2 },
        { "no version", root => root.Remove("version") },
        { "version 4", root => root["version"] = 4 },
        { "version 5", root => root["version"] = 5 },
        { "unknown project field", root => root["extra"] = 1 },
        { "unknown track field", root => Track(root, 0)["surprise"] = 1 },
        { "unknown clip field", root => Clip(root)["blob"] = "x" },
        { "unknown group field", root => root["groups"]![0]!["surprise"] = 1 },
        { "unknown effect type", root => Track(root, 0)["fx"]![0]!["type"] = "flanger" },
        { "unknown effect parameter", root => Track(root, 0)["fx"]![0]!["params"]!["wobble"] = 1 },
        { "effect parameter of the wrong type", root => Track(root, 0)["fx"]![1]!["params"]!["sync"] = "yes" },
        { "duplicate track id", root => Track(root, 1)["id"] = "track-1" },
        { "missing track id", root => Track(root, 0).AsObject().Remove("id") },
        { "unknown track kind", root => Track(root, 0)["kind"] = "folder" },
        { "unknown input channels", root => Track(root, 0)["input"]!["channels"] = "quad" },
        { "input channel zero", root => Track(root, 0)["input"]!["channels"] = 0 },
        { "unknown instrument type", root => Track(root, 1)["instrument"]!["type"] = "organ" },
        { "unknown oscillator", root => Track(root, 1)["instrument"]!["osc1"] = "noise" },
        { "note without pitch", root => Track(root, 1)["midiClips"]![0]!["notes"]![0]!.AsObject().Remove("pitch") },
        { "note pitch out of range", root => Track(root, 1)["midiClips"]![0]!["notes"]![0]!["pitch"] = 200 },
        { "unknown automation mode", root => Track(root, 0)["automationMode"] = "scribble" },
        { "unknown ruler", root => root["rulerMode"] = "ticks" },
        { "time signature of three numbers", root => root["timeSignature"] = new JsonArray(4, 4, 4) },
        { "time signature of 4/3", root => root["timeSignature"] = new JsonArray(4, 3) },
        { "tempo out of range", root => root["bpm"] = 2000 },
    };

    [Theory]
    [MemberData(nameof(BrokenFiles))]
    public void AProjectTheMigrationCannotPreserveIsRefusedAndTheErrorSaysWhy(
        string description,
        Action<JsonObject> breakIt)
    {
        JsonObject root = Parse(V3Fixtures.Read(V3Fixtures.Full));
        breakIt(root);
        ProjectFormatException error = Assert.Throws<ProjectFormatException>(() => Migrate(root.ToJsonString()));
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
        Assert.Throws<ProjectFormatException>(() => Migrate(text));
    }

    [Fact]
    public void AProjectWithoutAnOwnerIsRefusedBeforeAnythingIsRead()
    {
        Assert.Throws<ArgumentException>(() => ProjectMigration.FromV3(V3Fixtures.Read(V3Fixtures.Minimal), default, new SequentialIdService()));
    }

    [Fact]
    public void AProjectRefusedForItsVersionAllocatesNoIds()
    {
        SequentialIdService ids = new();
        JsonObject root = Parse(V3Fixtures.Read(V3Fixtures.Full));
        root["version"] = 2;
        Assert.Throws<ProjectFormatException>(() => ProjectMigration.FromV3(root.ToJsonString(), UserId.ForDoor("swarm", "u"), ids));
        Assert.Equal("prj_1", ids.Next<ProjectId>().Value);
    }

    private static MigrationResult Migrate(string json)
    {
        return ProjectMigration.FromV3(json, UserId.ForDoor("swarm", "7"), new SequentialIdService());
    }
}
