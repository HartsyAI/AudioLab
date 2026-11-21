using System.Text.Json.Nodes;
using AudioLab.Core.Model;
using static AudioLab.Core.Tests.TestJson;

namespace AudioLab.Core.Tests;

public sealed class ProjectJsonRejectionTests
{
    /// <summary>Each row breaks the sample project one way and names what the error must say.</summary>
    public static TheoryData<string, string, Action<JsonObject>> BrokenFiles => new()
    {
        { "version 3", "version 3", root => root["version"] = 3 },
        { "version 5", "version 5", root => root["version"] = 5 },
        { "no version", "no version", root => root.Remove("version") },
        { "version as text", "$.version", root => root["version"] = "4" },
        { "unknown project field", "$.surprise", root => root["surprise"] = 1 },
        { "unknown track field", "$.tracks[0].surprise", root => Track(root, 0)["surprise"] = 1 },
        { "unknown clip field", "$.tracks[0].clips[0].blobKey", root => Clip(root)["blobKey"] = "blob-1" },
        { "unknown instrument field", "'surprise'", root => Track(root, 1)["instrument"]!["surprise"] = 1 },
        { "missing owner", "'owner'", root => root.Remove("owner") },
        { "null owner", "$.owner", root => root["owner"] = null },
        { "empty owner", "$.owner", root => root["owner"] = string.Empty },
        { "owner that climbs out of a folder", "$.owner", root => root["owner"] = "../x" },
        { "numeric id", "$.tracks[0].id", root => Track(root, 0)["id"] = 7 },
        { "id that climbs out of a folder", "$.tracks[0].id", root => Track(root, 0)["id"] = "../../evil" },
        { "id with a space", "$.tracks[0].id", root => Track(root, 0)["id"] = "a b" },
        { "id that is too long", "$.tracks[0].id", root => Track(root, 0)["id"] = new string('a', 65) },
        { "an empty id", "$.tracks[0].id", root => Track(root, 0)["id"] = string.Empty },
        { "missing required field", "'volume'", root => Track(root, 0).AsObject().Remove("volume") },
        { "null for a non-nullable field", "'Name'", root => Track(root, 0)["name"] = null },
        { "text for a number", "$.tracks[0].pan", root => Track(root, 0)["pan"] = "left" },
        { "unknown enum name", "$.tracks[0].kind", root => Track(root, 0)["kind"] = "folder" },
        { "enum as an undefined number", "$.tracks[0].kind", root => Track(root, 0)["kind"] = 99 },
        { "enum as a defined number", "$.tracks[1].kind", root => Track(root, 1)["kind"] = 1 },
        { "enum as null", "$.tracks[0].kind", root => Track(root, 0)["kind"] = null },
        { "enum with a leading space", "$.tracks[0].kind", root => Track(root, 0)["kind"] = " audio" },
        { "enum with a trailing space", "$.tracks[2].kind", root => Track(root, 2)["kind"] = "instrument " },
        { "enum in the wrong case", "$.tracks[0].kind", root => Track(root, 0)["kind"] = "Audio" },
        { "enum as a list of names", "$.tracks[0].kind", root => Track(root, 0)["kind"] = "audio,audio" },
        { "enum as a mixed list of names", "$.tracks[3].kind", root => Track(root, 3)["kind"] = "bus,audio" },
        { "a null note in a list", "$.tracks[1].midiClips[0].notes[2]", root => Track(root, 1)["midiClips"]![0]!["notes"]!.AsArray().Add(null) },
        { "an uppercase id", "$.tracks[0].id", root => Track(root, 0)["id"] = "Trk_1" },
        { "an id that is a device name", "$.tracks[0].id", root => Track(root, 0)["id"] = "nul" },
        { "a control character in a name", "tracks[0].name", root => Track(root, 0)["name"] = "a\u0007b" },
        { "a text-direction mark in a name", "tracks[0].name", root => Track(root, 0)["name"] = "a‮b" },
        { "a line break in an icon", "tracks[4].icon", root => Track(root, 4)["icon"] = "a\nb" },
        {
            "a next note id that cannot be followed",
            "nextNoteId",
            root => Track(root, 1)["midiClips"]![0]!["nextNoteId"] = int.MaxValue
        },
        { "automation mode as a number", "mode", root => Track(root, 0)["automation"]!["mode"] = 0 },
        { "view mode as a number", "editMode", root => root["view"]!["editMode"] = 7 },
        {
            "unknown effect type",
            "$.tracks[0].effects[0]: Read unrecognized type discriminator id 'flanger'",
            root => Track(root, 0)["effects"]![0]!["type"] = "flanger"
        },
        { "effect without a type", "type", root => Track(root, 0)["effects"]![0]!.AsObject().Remove("type") },
        { "effect with a null type", "type", root => Track(root, 0)["effects"]![0]!["type"] = null },
        { "unknown instrument type", "'organ'", root => Track(root, 1)["instrument"]!["type"] = "organ" },
        { "wrong shape", "tracks", root => root["tracks"] = "none" },
        {
            "number too large for a double",
            "hpFreq",
            root => Track(root, 0)["effects"]![0]!["hpFreq"] = JsonNode.Parse("1e999")
        },
        { "pan out of range", "tracks[0].pan: 2 must be from -1 to 1", root => Track(root, 0)["pan"] = 2 },
        { "negative volume", "tracks[0].volume: -0.1 must be from 0 to 16", root => Track(root, 0)["volume"] = -0.1 },
        { "volume past the limit", "tracks[0].volume: 17", root => Track(root, 0)["volume"] = 17 },
        { "tempo of zero", "bpm: 0 must be from 1 to 1000", root => root["bpm"] = 0 },
        { "tempo that is almost zero", "bpm", root => root["bpm"] = 1e-300 },
        { "bad denominator", "timeSignature", root => root["timeSignature"]!["denominator"] = 5 },
        { "huge numerator", "timeSignature", root => root["timeSignature"]!["numerator"] = int.MaxValue },
        { "empty project name", "name", root => root["name"] = " " },
        { "project name that is too long", "name", root => root["name"] = new string('n', 201) },
        { "master volume past the limit", "masterVolume", root => root["masterVolume"] = 99 },
        {
            "duplicate track id",
            "tracks[1].id: the id trk_1 is used more than once",
            root => Track(root, 1)["id"] = "trk_1"
        },
        {
            "a send to a track that is not a bus",
            "tracks[0].sends[0].bus",
            root => Track(root, 0)["sends"]![0]!["bus"] = "trk_2"
        },
        { "a send to nothing", "trk_gone is not a bus", root => Track(root, 0)["sends"]![0]!["bus"] = "trk_gone" },
        {
            "a repeated send",
            "already sends to",
            root => Track(root, 0)["sends"]!.AsArray().Add(Track(root, 0)["sends"]![0]!.DeepClone())
        },
        { "a send level past the limit", "sends[0].level", root => Track(root, 0)["sends"]![0]!["level"] = 99 },
        {
            "a bus with a send",
            "a bus has no clips",
            root => Track(root, 3)["sends"] = Track(root, 0)["sends"]!.DeepClone()
        },
        {
            "a bus with a clip",
            "a bus has no clips",
            root => Track(root, 3)["clips"] = Track(root, 0)["clips"]!.DeepClone()
        },
        { "a group that does not exist", "tracks[4].group", root => Track(root, 4)["group"] = "grp_gone" },
        {
            "an audio clip on an instrument track",
            "an instrument track has no audio clips",
            root => Track(root, 1)["clips"] = Track(root, 0)["clips"]!.DeepClone()
        },
        {
            "a MIDI clip on an audio track",
            "an audio track has no MIDI clips",
            root => Track(root, 0)["midiClips"] = Track(root, 1)["midiClips"]!.DeepClone()
        },
        {
            "an instrument on an audio track",
            "an audio track has no MIDI clips or instrument",
            root => Track(root, 0)["instrument"] = Track(root, 1)["instrument"]!.DeepClone()
        },
        {
            "an instrument track without an instrument",
            "needs an instrument",
            root => Track(root, 1).AsObject().Remove("instrument")
        },
        {
            "a single input without its channel",
            "tracks[4].input",
            root => Track(root, 4)["input"]!.AsObject().Remove("channel")
        },
        { "an input channel on a mono input", "tracks[0].input", root => Track(root, 0)["input"]!["channel"] = 2 },
        {
            "an input device name that is too long",
            "deviceId",
            root => Track(root, 4)["input"]!["deviceId"] = new string('d', 513)
        },
        { "a take count below zero", "takeCount", root => Track(root, 0)["takeCount"] = -3 },
        { "a lane height below the minimum", "view.height", root => Track(root, 0)["view"]!["height"] = -3 },
        { "a colour that is a style", "color", root => Track(root, 0)["color"] = "red;background:url(//evil)" },
        { "a colour that is a name", "color", root => Track(root, 0)["color"] = "red" },
        {
            "an icon that is an address",
            "tracks[4].icon",
            root => Track(root, 4)["icon"] = "https://example.com/i.png"
        },
        {
            "overlapping sections",
            "sections[1].start: a section must not overlap",
            root => root["sections"]![1]!["start"] = 10
        },
        {
            "a section that never ends",
            "sections[1].end",
            root => root["sections"]![1]!["end"] = JsonNode.Parse("1e999")
        },
        { "a section past the latest time", "sections[1].end", root => root["sections"]![1]!["end"] = 2_000_000 },
        { "a section that is too short", "at least 0.1", root => root["sections"]![0]!["end"] = 0.05 },
        {
            "duplicate section ids",
            "sections[1].id",
            root => root["sections"]![1]!["id"] = root["sections"]![0]!["id"]!.GetValue<string>()
        },
        { "unordered markers", "markers[1].time", root => root["markers"]![0]!["time"] = 99 },
        {
            "duplicate marker ids",
            "markers[1].id",
            root => root["markers"]![1]!["id"] = root["markers"]![0]!["id"]!.GetValue<string>()
        },
        { "a marker without a name", "markers[0].name", root => root["markers"]![0]!["name"] = string.Empty },
        { "duplicate group ids", "groups", root => root["groups"]!.AsArray().Add(root["groups"]![0]!.DeepClone()) },
        { "a group volume past the limit", "groups[0].volume", root => root["groups"]![0]!["volume"] = 99 },
        {
            "unordered tempo changes",
            "tempoMap[1].time",
            root => root["tempoMap"]!.AsArray().Add(new JsonObject { ["id"] = "tmp_x", ["time"] = 5.0, ["bpm"] = 90.0 })
        },
        { "a tempo change at zero", "tempoMap[0].time", root => root["tempoMap"]![0]!["time"] = 0 },
        {
            "duplicate tempo change ids",
            "tempoMap",
            root => root["tempoMap"]!.AsArray().Add(
                new JsonObject { ["id"] = root["tempoMap"]![0]!["id"]!.GetValue<string>(), ["time"] = 50.0, ["bpm"] = 90.0 })
        },
        {
            "a lane with the wrong step count",
            "beatPattern.lanes[0].steps",
            root => root["beatPattern"]!["lanes"]![0]!["steps"] = new JsonArray(true)
        },
        { "a swing past one", "beatPattern.swing", root => root["beatPattern"]!["swing"] = 2 },
        { "a billion steps", "beatPattern.steps", root => root["beatPattern"]!["steps"] = 1_000_000_000 },
        {
            "duplicate lane ids",
            "beatPattern.lanes[1].id",
            root => root["beatPattern"]!["lanes"]![1]!["id"] = root["beatPattern"]!["lanes"]![0]!["id"]!.GetValue<string>()
        },
        { "a clip with no duration", "duration: 0 must be above 0", root => Clip(root)["duration"] = 0 },
        { "an offset past the duration", "nothing of the clip plays", root => Clip(root)["offset"] = 50 },
        { "a fade longer than the clip", "fadeIn", root => Clip(root)["fadeIn"] = 18 },
        { "a clip gain past the limit", "gain", root => Clip(root)["gain"] = 99 },
        { "a clip before the start", "start", root => Clip(root)["start"] = -1 },
        { "a clip with a name that is too long", "name", root => Clip(root)["name"] = new string('c', 201) },
        {
            "a clip id used twice",
            "is used more than once",
            root => Clip(root, 0, 1)["id"] = Clip(root)["id"]!.GetValue<string>()
        },
        {
            "a MIDI clip id that is also an audio clip id",
            "is used more than once",
            root => Track(root, 1)["midiClips"]![0]!["id"] = Clip(root)["id"]!.GetValue<string>()
        },
        {
            "a score parent that is not a clip",
            "meta.score.parent",
            root => Clip(root)["meta"]!["score"]!["parent"] = "clp_gone"
        },
        {
            "score versions that lead back to themselves",
            "lead back to itself",
            root => Clip(root, 0, 1)["meta"]!["score"] = new JsonObject { ["parent"] = Clip(root)["id"]!.GetValue<string>() }
        },
        { "a colour with a trailing newline", "color", root => Track(root, 0)["color"] = "#fff\n" },
        {
            "a clip that is its own stem source",
            "link to itself",
            root => Clip(root, 0, 1)["meta"]!["stemOf"]!["clipId"] = Clip(root, 0, 1)["id"]!.GetValue<string>()
        },
        {
            "a lyric word that ends before it starts",
            "words[1].e",
            root => Clip(root)["meta"]!["lyrics"]!["words"]![1]!["e"] = 0.5
        },
        {
            "a lyric word before the start",
            "words[0].s",
            root => Clip(root)["meta"]!["lyrics"]!["words"]![0]!["s"] = -1
        },
        { "a stem without a name", "stemOf.stem", root => Clip(root, 0, 1)["meta"]!["stemOf"]!["stem"] = string.Empty },
        {
            "a note out of range",
            ".pitch: 128 must be from 0 to 127",
            root => Track(root, 1)["midiClips"]![0]!["notes"]![0]!["pitch"] = 128
        },
        {
            "a velocity out of range",
            ".velocity",
            root => Track(root, 1)["midiClips"]![0]!["notes"]![0]!["velocity"] = 200
        },
        {
            "a note beyond the clip end",
            "starts inside its clip",
            root => Track(root, 1)["midiClips"]![0]!["notes"]![1]!["start"] = 4
        },
        { "a note id of zero", "note id", root => Track(root, 1)["midiClips"]![0]!["notes"]![0]!["id"] = 0 },
        {
            "a repeated note id",
            "unique in its clip",
            root => Track(root, 1)["midiClips"]![0]!["notes"]![1]!["id"] = 1
        },
        {
            "a next note id that is not above the notes",
            "below nextNoteId",
            root => Track(root, 1)["midiClips"]![0]!["nextNoteId"] = 2
        },
        {
            "a controller after the clip ends",
            "controlChanges[1].time",
            root => Track(root, 1)["midiClips"]![0]!["controlChanges"]![1]!["time"] = 9
        },
        {
            "unordered controllers",
            "controlChanges[1].time",
            root => Track(root, 1)["midiClips"]![0]!["controlChanges"]![0]!["time"] = 3
        },
        { "a MIDI clip with no length", "length", root => Track(root, 1)["midiClips"]![0]!["length"] = 0 },
        {
            "unordered automation",
            "automation.volume[1].time",
            root => Track(root, 0)["automation"]!["volume"]![0]!["time"] = 9
        },
        {
            "pan automation out of range",
            "automation.pan[0].value",
            root => Track(root, 0)["automation"]!["pan"]![0]!["value"] = 5
        },
        { "an effect parameter out of range", "mix: 7", root => Track(root, 0)["effects"]![3]!["mix"] = 7 },
        { "a delay without a division", "division", root => Track(root, 0)["effects"]![3]!["division"] = string.Empty },
        {
            "an effect id used twice",
            "is used more than once",
            root => Track(root, 0)["effects"]![1]!["id"] = Track(root, 0)["effects"]![0]!["id"]!.GetValue<string>()
        },
        {
            "a sampler root note out of range",
            "rootNote: 999",
            root => Track(root, 2)["instrument"]!["rootNote"] = 999
        },
        { "a synth cutoff out of range", "cutoff", root => Track(root, 1)["instrument"]!["cutoff"] = 5 },
        {
            "a synth preset name that is too long",
            "preset",
            root => Track(root, 1)["instrument"]!["preset"] = new string('p', 129)
        },
        { "a sampler sample that is not an id", "sample", root => Track(root, 2)["instrument"]!["sample"] = "../s" },
    };

    [Theory]
    [MemberData(nameof(BrokenFiles))]
    public void AFileThatBreaksTheFormatIsRefusedAndTheErrorSaysWhere(string description, string expected, Action<JsonObject> breakIt)
    {
        JsonObject root = Parse(ProjectJson.Write(SampleProject.Create(UserId.Local)));
        breakIt(root);
        ProjectFormatException error = Assert.Throws<ProjectFormatException>(() => ProjectJson.Read(root.ToJsonString()));
        Assert.True(error.Message.Contains(expected, StringComparison.Ordinal), $"{description}: {error.Message}");
    }

    [Theory]
    [InlineData("")]
    [InlineData("not json")]
    [InlineData("[]")]
    [InlineData("null")]
    [InlineData("{\"version\":")]
    public void TextThatIsNotAProjectObjectIsRefused(string text)
    {
        Assert.Throws<ProjectFormatException>(() => ProjectJson.Read(text));
    }

    [Fact]
    public void ADuplicatePropertyIsRefusedInsteadOfTheLastOneWinning()
    {
        string json = ProjectJson.Write(SampleProject.Create(UserId.Local));
        string doubled = json.Replace("\"owner\":\"desktop-local\"", "\"owner\":\"swarm-eve\",\"owner\":\"desktop-local\"", StringComparison.Ordinal);
        Assert.NotEqual(json, doubled);
        Assert.Throws<ProjectFormatException>(() => ProjectJson.Read(doubled));
    }

    [Fact]
    public void ALeadingByteOrderMarkIsIgnored()
    {
        string json = ProjectJson.Write(SampleProject.Create(UserId.Local));
        Assert.Equal(json, ProjectJson.Write(ProjectJson.Read("﻿" + json)));
    }

    [Fact]
    public void ADocumentThatBreaksARuleIsNotWritten()
    {
        ProjectDocument broken = SampleProject.Create(UserId.Local) with { Bpm = double.NaN };
        ProjectFormatException error = Assert.Throws<ProjectFormatException>(() => ProjectJson.Write(broken));
        Assert.Contains("bpm", Assert.Single(error.Problems));
    }

    [Fact]
    public void ANonFiniteEffectParameterIsRefusedByWriteInsteadOfThrowingFromTheSerializer()
    {
        ProjectDocument project = SampleProject.Create(UserId.Local);
        Track vocal = project.Tracks[0];
        Effect broken = ((EqEffect)vocal.Effects[0]) with { HpFreq = double.PositiveInfinity };
        ProjectDocument document = project with { Tracks = [vocal with { Effects = [broken] }, .. project.Tracks.Skip(1)] };
        Assert.Throws<ProjectFormatException>(() => ProjectJson.Write(document));
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

    [Fact]
    public void AFileWithThousandsOfProblemsGivesAShortError()
    {
        JsonObject root = Parse(ProjectJson.Write(SampleProject.Create(UserId.Local)));
        JsonArray points = Track(root, 0)["automation"]!["volume"]!.AsArray();
        points.Clear();
        for (int i = 0; i < 20_000; i++)
        {
            points.Add(new JsonObject { ["time"] = -1.0, ["value"] = 9.0 });
        }
        ProjectFormatException error = Assert.Throws<ProjectFormatException>(() => ProjectJson.Read(root.ToJsonString()));
        Assert.Equal(ProjectLimits.MaxProblems + 1, error.Problems.Count);
        Assert.StartsWith("and ", error.Problems[^1], StringComparison.Ordinal);
        Assert.True(error.Message.Length < 20_000);
    }
}
