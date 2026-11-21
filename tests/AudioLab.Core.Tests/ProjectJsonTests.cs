using System.Text.Json;
using System.Text.Json.Nodes;
using AudioLab.Core.Model;

namespace AudioLab.Core.Tests;

public sealed class ProjectJsonTests
{
    [Fact]
    public void AProjectWithEveryFeatureWritesAndReadsBackIdentically()
    {
        ProjectDocument original = SampleProject.Create(UserId.Local);
        string json = ProjectJson.Write(original);
        ProjectDocument reloaded = ProjectJson.Read(json);
        Assert.Equal(json, ProjectJson.Write(reloaded));
        Assert.Equal(original.Tracks.Count, reloaded.Tracks.Count);
        Assert.Equal(original.Tracks[0].Clips[0].Meta!.Lyrics!.Words, reloaded.Tracks[0].Clips[0].Meta!.Lyrics!.Words);
        Assert.Equal(original.Tracks[1].MidiClips[0].Notes, reloaded.Tracks[1].MidiClips[0].Notes);
        Assert.Equal(original.Tracks[0].Sends, reloaded.Tracks[0].Sends);
        Assert.Equal(original.TempoMap, reloaded.TempoMap);
    }

    [Fact]
    public void EveryEffectAndInstrumentKeepsItsType()
    {
        ProjectDocument reloaded = ProjectJson.Read(ProjectJson.Write(SampleProject.Create(UserId.Local)));
        Assert.Equal(
            [typeof(EqEffect), typeof(CompressorEffect), typeof(ReverbEffect), typeof(DelayEffect), typeof(SaturationEffect)],
            reloaded.Tracks[0].Effects.Select(effect => effect.GetType()));
        Assert.IsType<SynthInstrument>(reloaded.Tracks[1].Instrument);
        Assert.IsType<SamplerInstrument>(reloaded.Tracks[2].Instrument);
        Assert.Equal(new TrackInput("usb-mic", InputChannelMode.Single, 2), reloaded.Tracks[4].Input);
    }

    [Fact]
    public void FreeFormClipMetaSurvivesWithFieldsThisVersionDoesNotKnow()
    {
        ProjectDocument reloaded = ProjectJson.Read(ProjectJson.Write(SampleProject.Create(UserId.Local)));
        ScoreMeta score = reloaded.Tracks[0].Clips[0].Meta!.Score!;
        Assert.Equal("{\"nested\":[1,2,{\"deep\":true}]}", score.Extra!["futureField"].GetRawText());
        Assert.Equal(4294967295, score.Seed);
        Assert.Equal(reloaded.Tracks[0].Clips[1].Id, score.Parent);
        Assert.Equal(reloaded.Tracks[0].Clips[0].Id, reloaded.Tracks[0].Clips[1].Meta!.StemOf!.Value.ClipId);
    }

    [Fact]
    public void LyricsKeepFieldsThisVersionDoesNotKnow()
    {
        ProjectDocument reloaded = ProjectJson.Read(ProjectJson.Write(SampleProject.Create(UserId.Local)));
        ClipLyrics lyrics = reloaded.Tracks[0].Clips[0].Meta!.Lyrics!;
        Assert.Equal("future", lyrics.Extra!["alignedBy"].GetString());
    }

    [Fact]
    public void AnEmptyOrNullClipLinkInMetaReadsAsNone()
    {
        JsonObject root = TestJson.Parse(ProjectJson.Write(SampleProject.Create(UserId.Local)));
        JsonNode meta = TestJson.Clip(root)["meta"]!;
        meta["score"]!["parent"] = string.Empty;
        TestJson.Clip(root, 0, 1)["meta"]!["stemOf"]!["clipId"] = null;
        ProjectDocument reloaded = ProjectJson.Read(root.ToJsonString());
        Assert.Null(reloaded.Tracks[0].Clips[0].Meta!.Score!.Parent);
        Assert.Null(reloaded.Tracks[0].Clips[1].Meta!.StemOf!.Value.ClipId);
    }

    [Fact]
    public void TheWireFormatIsCamelCaseWithLowerCaseEnumsAndTypeDiscriminators()
    {
        string json = ProjectJson.Write(SampleProject.Create(UserId.Local));
        Assert.Contains("\"version\":4", json);
        Assert.Contains("\"masterLimiter\":true", json);
        Assert.Contains("\"kind\":\"instrument\"", json);
        Assert.Contains("\"type\":\"synth\"", json);
        Assert.Contains("\"type\":\"delay\"", json);
        Assert.Contains("\"w\":\"la\"", json);
    }

    [Fact]
    public void EachOwnerKeepsItsOwnProjectAndMedia()
    {
        UserId alice = UserId.ForDoor("swarm", "alice");
        UserId bob = UserId.ForDoor("swarm", "bob");
        Assert.Equal(alice, ProjectJson.Read(ProjectJson.Write(SampleProject.Create(alice))).Owner);
        Assert.Equal(bob, ProjectJson.Read(ProjectJson.Write(SampleProject.Create(bob))).Owner);
        Assert.Equal(UserId.Local, ProjectJson.Read(ProjectJson.Write(SampleProject.Create(UserId.Local))).Owner);
        MediaRecord media = new() { Id = MediaId.From("med_1"), Owner = bob, ContentType = "audio/wav", Length = 1234 };
        string json = JsonSerializer.Serialize(media, ProjectJsonContext.Default.MediaRecord);
        Assert.Equal(media, JsonSerializer.Deserialize(json, ProjectJsonContext.Default.MediaRecord));
    }

    [Fact]
    public void AnEmptyProjectIsValid()
    {
        ProjectDocument empty = SampleProject.Create(UserId.Local) with { Tracks = [], Groups = [], Markers = [], Sections = [], TempoMap = [] };
        Assert.Equal(empty.Tracks, ProjectJson.Read(ProjectJson.Write(empty)).Tracks);
    }
}
