using System.Text.Json;
using AudioLab.Core.Model;

namespace AudioLab.Core.Tests;

public sealed class ImmutabilityTests
{
    [Fact]
    public void TwoDocumentsReadFromTheSameJsonAreEqual()
    {
        string json = ProjectJson.Write(SampleProject.Create(UserId.Local));
        ProjectDocument first = ProjectJson.Read(json);
        ProjectDocument second = ProjectJson.Read(json);
        Assert.Equal(first, second);
        Assert.Equal(first.GetHashCode(), second.GetHashCode());
        Assert.Equal(first.Tracks, second.Tracks);
        Assert.Equal(first.Tracks[0].Clips[0].Meta, second.Tracks[0].Clips[0].Meta);
    }

    [Fact]
    public void AReadDocumentEqualsTheDocumentThatWasWritten()
    {
        ProjectDocument original = SampleProject.Create(UserId.Local);
        Assert.Equal(original, ProjectJson.Read(ProjectJson.Write(original)));
    }

    [Fact]
    public void ADocumentThatDiffersInOneDeepValueIsNotEqual()
    {
        ProjectDocument original = SampleProject.Create(UserId.Local);
        Track changed = original.Tracks[1] with { Name = "Other" };
        ProjectDocument edited = original with { Tracks = [original.Tracks[0], changed, .. original.Tracks.Skip(2)] };
        Assert.NotEqual(original, edited);
    }

    [Fact]
    public void ADeserializedListCannotBeChangedThroughACast()
    {
        ProjectDocument document = ProjectJson.Read(ProjectJson.Write(SampleProject.Create(UserId.Local)));
        object tracks = document.Tracks;
        Assert.False(tracks is System.Collections.IList);
        Assert.Throws<InvalidCastException>(() => (List<Track>)tracks);
        object clips = document.Tracks[0].Clips;
        Assert.False(clips is System.Collections.IList);
        object steps = document.BeatPattern.Lanes[0].Steps;
        Assert.False(steps is System.Collections.IList);
    }

    [Fact]
    public void AListIsACopyOfItsSourceSoLaterChangesToTheSourceDoNotReachIt()
    {
        List<int> source = [1, 2, 3];
        ValueList<int> list = new(source);
        source.Clear();
        Assert.Equal([1, 2, 3], list);
    }

    [Fact]
    public void ListsAreEqualByItemsAndOrder()
    {
        Assert.Equal<ValueList<int>>([1, 2], [1, 2]);
        Assert.NotEqual<ValueList<int>>([1, 2], [2, 1]);
        Assert.NotEqual<ValueList<int>>([1, 2], [1, 2, 3]);
        Assert.Equal(ValueList<int>.Empty, []);
        Assert.Equal(new ValueList<int>([1, 2]).GetHashCode(), new ValueList<int>([1, 2]).GetHashCode());
        Assert.True(new ValueList<string>(["a"]) == new ValueList<string>(["a"]));
        Assert.True(new ValueList<string>(["a"]) != new ValueList<string>(["b"]));
    }

    [Fact]
    public void ExtraFieldsAreCopiesThatCannotBeChangedAndCompareByTheirJson()
    {
        ExtraFields first = ExtraFields.FromJson(("a", "{\"x\":[1,2]}"), ("b", "true"));
        ExtraFields second = ExtraFields.FromJson(("b", "true"), ("a", "{\"x\":[1,2]}"));
        Assert.Equal(first, second);
        Assert.Equal(first.GetHashCode(), second.GetHashCode());
        Assert.NotEqual(first, ExtraFields.FromJson(("a", "1")));
        Assert.NotEqual(first, ExtraFields.Empty);
        Assert.Equal(["a", "b"], first.Names);
        Assert.True(first.Contains("a"));
        Assert.Equal(2, first["a"].GetProperty("x").GetArrayLength());
        Assert.Empty(ExtraFields.From(null).Names);
    }

    [Fact]
    public void ExtraFieldsOfARecordSurviveACopyAndAnEditOfAnotherField()
    {
        ScoreMeta score = new() { Label = "A", Extra = ExtraFields.FromJson(("future", "1")) };
        ScoreMeta edited = score with { Label = "B" };
        Assert.Equal(score.Extra, edited.Extra);
        Assert.NotEqual(score, edited);
    }

    [Fact]
    public void ARecordRefusesNullExtraFields()
    {
        Assert.Throws<ArgumentNullException>(() => new ClipMeta { Extra = null! });
    }

    [Fact]
    public void ExtraFieldsKeepJsonExactlyAsWrittenAndWriteItBack()
    {
        ClipMeta meta = new() { Extra = ExtraFields.FromJson(("future", "{\"nested\":[1,2,3]}")) };
        ProjectDocument project = SampleProject.Create(UserId.Local);
        AudioClip clip = project.Tracks[0].Clips[1] with { Meta = meta };
        Track vocal = project.Tracks[0] with { Clips = [project.Tracks[0].Clips[0], clip] };
        string json = ProjectJson.Write(project with { Tracks = [vocal, .. project.Tracks.Skip(1)] });
        Assert.Contains("\"future\":{\"nested\":[1,2,3]}", json);
        Assert.Equal(meta.Extra, ProjectJson.Read(json).Tracks[0].Clips[1].Meta!.Extra);
        Assert.Equal(JsonValueKind.Object, meta.Extra["future"].ValueKind);
    }

    [Fact]
    public void AnEmptyProjectKeepsEveryPartOfItsEmptyShape()
    {
        ProjectDocument empty = SampleProject.Create(UserId.Local) with
        {
            Tracks = [],
            Groups = [],
            Markers = [],
            Sections = [],
            TempoMap = [],
        };
        ProjectDocument reloaded = ProjectJson.Read(ProjectJson.Write(empty));
        Assert.Equal(empty, reloaded);
        Assert.Empty(reloaded.Tracks);
        Assert.Equal(empty.Name, reloaded.Name);
        Assert.Equal(empty.BeatPattern.Lanes.Count, reloaded.BeatPattern.Lanes.Count);
    }
}
