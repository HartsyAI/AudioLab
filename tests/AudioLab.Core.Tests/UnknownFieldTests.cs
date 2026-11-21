using System.Text.Json.Nodes;
using AudioLab.Core.Model;
using static AudioLab.Core.Tests.TestJson;

namespace AudioLab.Core.Tests;

public sealed class UnknownFieldTests
{
    [Theory]
    [InlineData("1", "1")]
    [InlineData("1.0", "1")]
    [InlineData("1.50", "1.5")]
    [InlineData("100", "100")]
    [InlineData("1e2", "100")]
    [InlineData("-0", "0")]
    [InlineData("-1.5e3", "-1500")]
    [InlineData("0.000001", "0.000001")]
    [InlineData("1e-7", "0.0000001")]
    [InlineData("1e400", "1e400")]
    [InlineData("123456789012345678901234567890", "123456789012345678901234567890")]
    [InlineData("0.1000000000000000055511151231257827", "0.1000000000000000055511151231257827")]
    [InlineData("12345678901234567890.123456789012345678901234567890", "12345678901234567890.12345678901234567890123456789")]
    [InlineData("9007199254740993", "9007199254740993")]
    public void ANumberInAnUnknownFieldIsKeptExactlyAndWithoutTrailingZeros(string written, string kept)
    {
        ExtraFields fields = ExtraFields.FromJson(("n", written));
        Assert.Equal(kept, fields["n"].GetRawText());
    }

    [Fact]
    public void NumbersThatAreTheSameValueMakeEqualFieldSets()
    {
        Assert.Equal(ExtraFields.FromJson(("n", "1e2")), ExtraFields.FromJson(("n", "100.0")));
        Assert.NotEqual(ExtraFields.FromJson(("n", "123456789012345678901234567890")), ExtraFields.FromJson(("n", "123456789012345678901234567891")));
    }

    [Theory]
    [InlineData("1e100000", true)]
    [InlineData("1e-100000", true)]
    [InlineData("1e100001", false)]
    [InlineData("1e-100001", false)]
    [InlineData("1e99999999999999999999", false)]
    [InlineData("1e-9223372036854775808", false)]
    [InlineData("1e9223372036854775807", false)]
    public void ANumberWithAnExponentTooBigToKeepIsRefusedInsteadOfRounded(string written, bool kept)
    {
        if (kept)
        {
            Assert.NotNull(ExtraFields.FromJson(("n", written)));
        }
        else
        {
            Assert.Throws<ArgumentException>(() => ExtraFields.FromJson(("n", written)));
        }
    }

    [Theory]
    [InlineData("1e-9223372036854775808")]
    [InlineData("1e9223372036854775807")]
    [InlineData("1e99999999999999999999")]
    public void ReadRefusesAnExponentTooBigToKeepWithAProjectFormatError(string number)
    {
        JsonObject root = Parse(ProjectJson.Write(SampleProject.Create(UserId.Local)));
        Clip(root)["meta"]!["n"] = 1;
        string json = root.ToJsonString().Replace("\"n\":1", "\"n\":" + number, StringComparison.Ordinal);
        Assert.Throws<ProjectFormatException>(() => ProjectJson.Read(json));
    }

    [Fact]
    public void ABigNumberInAnUnknownFieldSurvivesReadAndWrite()
    {
        const string Big = "123456789012345678901234567890";
        JsonObject root = Parse(ProjectJson.Write(SampleProject.Create(UserId.Local)));
        Clip(root)["meta"]!["big"] = 1;
        string json = root.ToJsonString().Replace("\"big\":1", "\"big\":" + Big, StringComparison.Ordinal);
        Assert.Contains(Big, json, StringComparison.Ordinal);
        ProjectDocument read = ProjectJson.Read(json);
        Assert.Equal(Big, read.Tracks[0].Clips[0].Meta!.Extra["big"].GetRawText());
        Assert.Contains("\"big\":" + Big, ProjectJson.Write(read), StringComparison.Ordinal);
    }

    [Fact]
    public void AFieldNamedExtraIsKeptLikeAnyOtherUnknownField()
    {
        JsonObject root = Parse(ProjectJson.Write(SampleProject.Create(UserId.Local)));
        Clip(root)["meta"]!["extra"] = "kept";
        ProjectDocument read = ProjectJson.Read(root.ToJsonString());
        Assert.Equal("kept", read.Tracks[0].Clips[0].Meta!.Extra["extra"].GetString());
    }

    [Theory]
    [InlineData("meta", "x", "$.tracks[0].clips[0].meta.x")]
    [InlineData("meta.score", "y", "$.tracks[0].clips[0].meta.score.y")]
    [InlineData("meta.lyrics", "z", "$.tracks[0].clips[0].meta.lyrics.z")]
    public void ADuplicateUnknownFieldIsRefusedAndNamedByItsOwnName(string holder, string name, string path)
    {
        JsonObject root = Parse(ProjectJson.Write(SampleProject.Create(UserId.Local)));
        JsonNode node = Clip(root);
        foreach (string step in holder.Split('.'))
        {
            node = node[step]!;
        }
        node[name] = 1;
        string json = root.ToJsonString().Replace($"\"{name}\":1", $"\"{name}\":1,\"{name}\":2", StringComparison.Ordinal);
        ProjectFormatException error = Assert.Throws<ProjectFormatException>(() => ProjectJson.Read(json));
        Assert.Contains($"{path}: Duplicate property '{name}'", error.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("extensionData", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ADuplicateNameInsideAnUnknownFieldIsRefused()
    {
        JsonObject root = Parse(ProjectJson.Write(SampleProject.Create(UserId.Local)));
        Clip(root)["meta"]!["x"] = new JsonObject { ["k"] = 1 };
        string json = root.ToJsonString().Replace("\"k\":1", "\"k\":1,\"k\":2", StringComparison.Ordinal);
        ProjectFormatException error = Assert.Throws<ProjectFormatException>(() => ProjectJson.Read(json));
        Assert.Contains("Duplicate property 'k'", error.Message, StringComparison.Ordinal);
    }
}
