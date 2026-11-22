using System.Text.Json;
using AudioLab.Core.Api;

namespace AudioLab.Core.Tests;

public sealed class ApiJsonContextTests
{
    [Fact]
    public void ARecordReadsFromItsOwnCamelCaseMembers()
    {
        const string Text = """{"id":"prj_1","name":"Song","modified":"2026-11-22T10:00:00+00:00"}""";

        ProjectSummary? summary = JsonSerializer.Deserialize(Text, ApiJsonContext.Default.ProjectSummary);

        Assert.Equal(("prj_1", "Song"), (summary?.Id, summary?.Name));
    }

    [Fact]
    public void AMemberTheRecordDoesNotHaveIsRefused()
    {
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize("""{"id":"i","revision":1,"extra":true}""", ApiJsonContext.Default.SaveResult));
    }

    [Fact]
    public void AMemberThatIsMissingIsRefused()
    {
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize("""{"id":"i"}""", ApiJsonContext.Default.SaveResult));
    }

    [Fact]
    public void ANullWhereTheRecordHoldsNoNullIsRefused()
    {
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize("""{"projects":null,"refused":0}""", ApiJsonContext.Default.ProjectListing));
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize("""{"id":null,"revision":1}""", ApiJsonContext.Default.SaveResult));
    }

    [Fact]
    public void ADuplicatedMemberIsRefused()
    {
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize("""{"id":"a","revision":1,"revision":2}""", ApiJsonContext.Default.SaveResult));
    }

    [Fact]
    public void ANumberWrittenAsTextIsRefused()
    {
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize("""{"id":"a","revision":"7"}""", ApiJsonContext.Default.SaveResult));
    }

    [Fact]
    public void ARequestMayLeaveItsNameOut()
    {
        Assert.Null(JsonSerializer.Deserialize("{}", ApiJsonContext.Default.CreateProjectRequest)?.Name);
        Assert.Null(JsonSerializer.Deserialize("{}", ApiJsonContext.Default.DuplicateProjectRequest)?.Name);
    }
}
