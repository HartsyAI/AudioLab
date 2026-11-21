using System.Text.Json.Serialization;
using AudioLab.Core.Model;

namespace AudioLab.Core.Tests;

public sealed class HardeningTests
{
    [Fact]
    public void AnUnknownFieldThatReusesAKnownNameIsRefusedSoWriteNeverEmitsADuplicate()
    {
        ProjectDocument project = WithMeta(new ClipMeta { Extra = ExtraFields.FromJson(("score", "1")) });
        ProjectFormatException error = Assert.Throws<ProjectFormatException>(() => ProjectJson.Write(project));
        Assert.Contains("meta.extra.score: an unknown field cannot reuse the name", error.Message);
        ProjectDocument inScore = WithMeta(new ClipMeta { Score = new ScoreMeta { Extra = ExtraFields.FromJson(("label", "1")) } });
        Assert.Contains("score.extra.label", Assert.Throws<ProjectFormatException>(() => ProjectJson.Write(inScore)).Message);
        ClipLyrics lyrics = new() { Text = "x", Words = [], Extra = ExtraFields.FromJson(("words", "[]")) };
        ProjectDocument inLyrics = WithMeta(new ClipMeta { Lyrics = lyrics });
        Assert.Contains("lyrics.extra.words", Assert.Throws<ProjectFormatException>(() => ProjectJson.Write(inLyrics)).Message);
    }

    [Fact]
    public void ReadingBackTheExtraFieldsOfAWrittenProjectGivesTheSameValue()
    {
        ProjectDocument project = WithMeta(new ClipMeta { Extra = ExtraFields.FromJson(("other", "{\"b\":1.0,\"a\":[2.50]}")) });
        ClipMeta back = ProjectJson.Read(ProjectJson.Write(project)).Tracks[0].Clips[1].Meta!;
        Assert.Equal(project.Tracks[0].Clips[1].Meta, back);
        Assert.Equal("{\"a\":[2.5],\"b\":1}", back.Extra["other"].GetRawText());
    }

    [Fact]
    public void ExtraFieldsAreEqualWhateverTheLayoutOrTheNumberFormOfTheirJson()
    {
        ExtraFields compact = ExtraFields.FromJson(("a", "{\"x\":1,\"y\":[1,2]}"));
        ExtraFields spaced = ExtraFields.FromJson(("a", "{ \"y\" : [ 1, 2.0 ],\n \"x\" : 1.00 }"));
        Assert.Equal(compact, spaced);
        Assert.Equal(compact.GetHashCode(), spaced.GetHashCode());
        Assert.NotEqual(compact, ExtraFields.FromJson(("a", "{\"x\":1,\"y\":[1,3]}")));
    }

    [Fact]
    public void CastingToTheDeserializationHookCannotEmptyTheExtraFields()
    {
        ScoreMeta score = new() { Extra = ExtraFields.FromJson(("a", "1")) };
        ClipLyrics lyrics = new() { Text = "x", Words = [], Extra = ExtraFields.FromJson(("a", "1")) };
        ClipMeta meta = new() { Extra = ExtraFields.FromJson(("a", "1")) };
        int hash = meta.GetHashCode();
        ((IJsonOnDeserialized)score).OnDeserialized();
        ((IJsonOnDeserialized)lyrics).OnDeserialized();
        ((IJsonOnDeserialized)meta).OnDeserialized();
        Assert.Equal(1, score.Extra.Count);
        Assert.Equal(1, lyrics.Extra.Count);
        Assert.Equal(1, meta.Extra.Count);
        Assert.Equal(hash, meta.GetHashCode());
    }

    [Fact]
    public void TooManyOrTooLargeUnknownFieldsAreRefused()
    {
        ExtraFields many = ExtraFields.FromJson([.. Enumerable.Range(0, ProjectLimits.MaxExtraFields + 1).Select(i => ($"f{i}", "1"))]);
        Assert.Throws<ProjectFormatException>(() => ProjectJson.Write(WithMeta(new ClipMeta { Extra = many })));
        string big = "\"" + new string('x', ProjectLimits.MaxExtraLength) + "\"";
        ExtraFields large = ExtraFields.FromJson(("big", big));
        ProjectFormatException error = Assert.Throws<ProjectFormatException>(() => ProjectJson.Write(WithMeta(new ClipMeta { Extra = large })));
        Assert.Contains("the unknown fields take", error.Message);
    }

    [Fact]
    public void AProblemIsCutShortSoAnErrorNeverEchoesAWholeAttack()
    {
        string json = ProjectJson.Write(SampleProject.Create(UserId.Local));
        string attack = new('z', 3_000_000);
        string withUnknown = json.Replace("{\"version\":4", "{\"" + attack + "\":1,\"version\":4", StringComparison.Ordinal);
        Assert.NotEqual(json, withUnknown);
        ProjectFormatException error = Assert.Throws<ProjectFormatException>(() => ProjectJson.Read(withUnknown));
        Assert.True(error.Message.Length < 2000, error.Message.Length.ToString());
    }

    [Fact]
    public void ProblemsCopiedIntoAnExceptionAreNotAffectedByLaterChangesToTheSource()
    {
        List<string> source = ["first"];
        ProjectFormatException error = new("The project is wrong.", source);
        source.Add("second");
        source[0] = "changed";
        Assert.Equal(["first"], error.Problems);
        Assert.Throws<NotSupportedException>(() => ((IList<string>)error.Problems).Add("x"));
    }

    [Fact]
    public void AWriteWithANullWhereAValueIsRequiredIsRefusedWithAnError()
    {
        ProjectDocument project = SampleProject.Create(UserId.Local);
        Assert.Throws<ProjectFormatException>(() => ProjectJson.Write(project with { Tracks = null! }));
        Assert.Throws<ProjectFormatException>(() => ProjectJson.Write(project with { BeatPattern = null! }));
        Assert.Throws<ProjectFormatException>(() => ProjectJson.Write(project with { Tracks = [null!] }));
        Track track = project.Tracks[0] with { Automation = null! };
        Assert.Throws<ProjectFormatException>(() => ProjectJson.Write(project with { Tracks = [track] }));
    }

    [Fact]
    public void ATrackWithTooManyEffectsIsRefused()
    {
        ProjectDocument project = SampleProject.Create(UserId.Local);
        EqEffect effect = (EqEffect)project.Tracks[0].Effects[0];
        IdService ids = new();
        Effect[] effects = [.. Enumerable.Range(0, ProjectLimits.MaxEffects + 1).Select(_ => effect with { Id = ids.Next<EffectId>() })];
        Track vocal = project.Tracks[0] with { Effects = [.. effects] };
        ProjectFormatException error = Assert.Throws<ProjectFormatException>(
            () => ProjectJson.Write(project with { Tracks = [vocal, .. project.Tracks.Skip(1)] }));
        Assert.Contains("effects: 65 items is more than the 64", error.Message);
    }

    [Fact]
    public void ANoteMayRingOnPastTheEndOfItsClip()
    {
        ProjectDocument project = SampleProject.Create(UserId.Local);
        Track keys = project.Tracks[1];
        MidiClip clip = keys.MidiClips[0];
        MidiNote held = clip.Notes[0] with { Start = clip.Length - 0.5, Duration = 30 };
        MidiClip edited = clip with { Notes = [held, .. clip.Notes.Skip(1)] };
        ProjectDocument document = project with { Tracks = [project.Tracks[0], keys with { MidiClips = [edited] }, .. project.Tracks.Skip(2)] };
        Assert.Equal(held, ProjectJson.Read(ProjectJson.Write(document)).Tracks[1].MidiClips[0].Notes[0]);
    }

    [Fact]
    public void ADoorAndItsUserNeverRunIntoAnotherPair()
    {
        Assert.Throws<ArgumentException>(() => UserId.ForDoor("swarm-a", "b"));
        Assert.Throws<ArgumentException>(() => UserId.ForDoor("swarm", "a-b"));
        Assert.Throws<ArgumentException>(() => UserId.ForDoor("Swarm", "a"));
        Assert.Throws<ArgumentException>(() => UserId.ForDoor("swarm", "a_b"));
        Assert.Throws<ArgumentException>(() => UserId.ForDoor("swarm", string.Empty));
        Assert.NotEqual(UserId.ForDoor("swarm", "local"), UserId.Local);
    }

    [Theory]
    [InlineData("conx", true)]
    [InlineData("con", false)]
    [InlineData("nul", false)]
    [InlineData("aux", false)]
    [InlineData("com1", false)]
    [InlineData("lpt9", false)]
    [InlineData("Foo", false)]
    [InlineData("foo", true)]
    public void IdsAreLowerCaseAndNeverADeviceName(string id, bool valid)
    {
        Assert.Equal(valid, IdRules.IsValid(id));
    }

    private static ProjectDocument WithMeta(ClipMeta meta)
    {
        ProjectDocument project = SampleProject.Create(UserId.Local);
        Track vocal = project.Tracks[0];
        AudioClip clip = vocal.Clips[1] with { Meta = meta };
        Track edited = vocal with { Clips = [vocal.Clips[0] with { Meta = null }, clip] };
        return project with { Tracks = [edited, .. project.Tracks.Skip(1)] };
    }
}
