using System.Text.Json.Nodes;
using AudioLab.Core.Model;
using static AudioLab.Core.Tests.TestJson;

namespace AudioLab.Core.Tests;

public sealed class UnpairedSurrogateTests
{
    private const string Marker = "MARKER";

    private const string Backslash = "\\";

    /// <summary>JSON text for a high surrogate with no low one after it, which no UTF-8 text can hold.</summary>
    private static readonly string _lone = Backslash + "ud800";

    private static readonly string _loneLow = Backslash + "udc00";

    public static TheoryData<string, Action<JsonObject>> Placements => new()
    {
        { "a project name", root => root["name"] = Marker },
        { "a track name", root => Track(root, 0)["name"] = Marker },
        { "a clip name", root => Clip(root)["name"] = Marker },
        { "the value of an unknown field", root => Clip(root)["meta"]!["extra"] = Marker },
        { "the name of an unknown field", root => Clip(root)["meta"]![Marker] = 1 },
        { "a value deep inside an unknown field", root => Clip(root)["meta"]!["extra"] = new JsonArray(new JsonObject { ["k"] = Marker }) },
        { "a name deep inside an unknown field", root => Clip(root)["meta"]!["extra"] = new JsonObject { [Marker] = 1 } },
    };

    [Theory]
    [MemberData(nameof(Placements))]
    public void AnUnpairedSurrogateInAFileIsRefusedWithAProjectFormatError(string where, Action<JsonObject> place)
    {
        JsonObject root = Parse(ProjectJson.Write(SampleProject.Create(UserId.Local)));
        place(root);
        string json = root.ToJsonString().Replace(Marker, _lone, StringComparison.Ordinal);
        Assert.True(json.Contains(_lone, StringComparison.Ordinal), where);
        ProjectFormatException error = Assert.Throws<ProjectFormatException>(() => ProjectJson.Read(json));
        Assert.False(string.IsNullOrWhiteSpace(error.Message), where);
        Assert.DoesNotContain("AudioLab.Core.", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ALowSurrogateAloneIsRefusedToo()
    {
        JsonObject root = Parse(ProjectJson.Write(SampleProject.Create(UserId.Local)));
        root["name"] = Marker;
        string json = root.ToJsonString().Replace(Marker, _loneLow, StringComparison.Ordinal);
        Assert.Throws<ProjectFormatException>(() => ProjectJson.Read(json));
    }

    [Fact]
    public void AnUnpairedSurrogateInAMediaRecordIsRefusedWithAProjectFormatError()
    {
        MediaRecord record = new()
        {
            Id = MediaId.From("med_1"),
            Owner = UserId.Local,
            ContentType = "audio/wav",
            Length = 10,
        };
        string json = MediaJson.Write(record).Replace("audio/wav", "audio/" + _lone, StringComparison.Ordinal);
        Assert.Throws<ProjectFormatException>(() => MediaJson.Read(json));
    }

    [Theory]
    [InlineData("udc00")]
    [InlineData("ud800x")]
    [InlineData("ud800<raw>")]
    [InlineData("udc00<raw>")]
    public void EveryWayToLeaveASurrogateUnpairedIsRefusedInAnUnknownValueAndName(string template)
    {
        // A raw lone surrogate cannot sit in an attribute (the compiler turns it into U+FFFD), so it is added here.
        string escape = template.Replace("<raw>", ((char)0xD800).ToString(), StringComparison.Ordinal);
        JsonObject root = Parse(ProjectJson.Write(SampleProject.Create(UserId.Local)));
        Clip(root)["meta"]!["extra"] = Marker;
        string asValue = root.ToJsonString().Replace(Marker, Backslash + escape, StringComparison.Ordinal);
        Assert.Throws<ProjectFormatException>(() => ProjectJson.Read(asValue));
        root = Parse(ProjectJson.Write(SampleProject.Create(UserId.Local)));
        Clip(root)["meta"]![Marker] = 1;
        string asName = root.ToJsonString().Replace(Marker, Backslash + escape, StringComparison.Ordinal);
        Assert.Throws<ProjectFormatException>(() => ProjectJson.Read(asName));
    }

    [Theory]
    [InlineData("\"<escape>\"")]
    [InlineData("[\"a\", \"<escape>\"]")]
    [InlineData("{\"k\": \"<escape>\"}")]
    public void ExtraFieldsRefusesAnEscapedLoneSurrogateInAValueWithAnArgumentException(string template)
    {
        string json = template.Replace("<escape>", _lone, StringComparison.Ordinal);
        ArgumentException error = Assert.Throws<ArgumentException>(() => ExtraFields.FromJson(("x", json)));
        Assert.IsType<InvalidOperationException>(error.InnerException);
    }
    [Fact]
    public void APairedSurrogateEscapeInAnUnknownValueIsKept()
    {
        JsonObject root = Parse(ProjectJson.Write(SampleProject.Create(UserId.Local)));
        Clip(root)["meta"]!["extra"] = Marker;
        string json = root.ToJsonString().Replace(Marker, Backslash + "ud83c" + Backslash + "udfb8", StringComparison.Ordinal);
        ProjectDocument read = ProjectJson.Read(json);
        Assert.Equal("" + (char)0xD83C + (char)0xDFB8, read.Tracks[0].Clips[0].Meta!.Extra["extra"].GetString());
    }

    [Fact]
    public void ARawUnpairedSurrogateCharacterInTheInputIsRefusedWithAProjectFormatError()
    {
        JsonObject root = Parse(ProjectJson.Write(SampleProject.Create(UserId.Local)));
        root["name"] = Marker;
        string json = root.ToJsonString().Replace(Marker, "a" + (char)0xD800 + "b", StringComparison.Ordinal);
        Assert.Throws<ProjectFormatException>(() => ProjectJson.Read(json));
    }

    [Fact]
    public void ARawUnpairedSurrogateCharacterInAMediaRecordIsRefusedWithAProjectFormatError()
    {
        MediaRecord record = new()
        {
            Id = MediaId.From("med_1"),
            Owner = UserId.Local,
            ContentType = "audio/wav",
            Length = 10,
        };
        string json = MediaJson.Write(record).Replace("audio/wav", "audio/" + (char)0xD800, StringComparison.Ordinal);
        Assert.Throws<ProjectFormatException>(() => MediaJson.Read(json));
    }

    [Fact]
    public void ADocumentWithAnUnpairedSurrogateIsNotWritten()
    {
        string lone = "a" + (char)0xD800 + "b";
        ProjectDocument project = SampleProject.Create(UserId.Local);
        ProjectFormatException error = Assert.Throws<ProjectFormatException>(() => ProjectJson.Write(project with { Name = lone }));
        Assert.Contains("name", error.Message, StringComparison.Ordinal);
        Track track = project.Tracks[0] with { Name = lone };
        Assert.Throws<ProjectFormatException>(() => ProjectJson.Write(project with { Tracks = [track, .. project.Tracks.Skip(1)] }));
    }

    [Fact]
    public void AnUnknownFieldNamedWithAnUnpairedSurrogateIsRefusedWhenBuilt()
    {
        string lone = "a" + (char)0xD800;
        using System.Text.Json.JsonDocument one = System.Text.Json.JsonDocument.Parse("1");
        Assert.Throws<ArgumentException>(() => ExtraFields.From([KeyValuePair.Create(lone, one.RootElement)]));
    }

    [Fact]
    public void APairedSurrogateIsCleanText()
    {
        string pair = "a pair: " + (char)0xD83C + (char)0xDFB8;
        Assert.True(TextRules.IsClean(pair));
        Assert.True(TextRules.IsWellFormed(pair));
    }

    [Fact]
    public void ASurrogateWithoutItsPartnerIsNotCleanText()
    {
        char high = (char)0xD83C;
        char low = (char)0xDFB8;
        string[] broken = [$"a{high}", $"{high}b", $"{low}", $"{low}{high}", $"{high}{high}{low}x{low}"];
        Assert.All(broken, text => Assert.False(TextRules.IsClean(text), text));
        Assert.All(broken, text => Assert.False(TextRules.IsWellFormed(text), text));
        Assert.True(TextRules.IsClean($"{high}{low}{high}{low}"));
    }
}
