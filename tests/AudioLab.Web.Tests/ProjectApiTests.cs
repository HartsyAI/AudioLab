using System.Net;
using AudioLab.Core;
using AudioLab.Core.Api;
using AudioLab.Core.Model;
using AudioLab.Web.Services;

namespace AudioLab.Web.Tests;

public sealed class ProjectApiTests
{
    private static readonly Uri _site = new("http://audiolab.test/audiolab/");

    private static ProjectApi Api(StubHandler handler) => new(new HttpClient(handler) { BaseAddress = _site });

    [Fact]
    public async Task TheListIsReadFromTheRouteUnderThePathPrefix()
    {
        StubHandler handler = new(
            HttpStatusCode.OK,
            "{\"projects\":[{\"id\":\"p1\",\"name\":\"Song\",\"modified\":\"2026-11-22T10:00:00+00:00\"}],\"refused\":2}");

        ProjectListing listing = await Api(handler).ListAsync();

        Assert.Equal("/audiolab/api/projects", handler.Requests.Single().Path);
        Assert.Equal("Song", listing.Projects.Single().Name);
        Assert.Equal(2, listing.Refused);
    }

    [Fact]
    public async Task ACreateSendsTheNameAndReadsTheSummary()
    {
        StubHandler handler = new(HttpStatusCode.Created, "{\"id\":\"p2\",\"name\":\"New\",\"modified\":\"2026-11-22T10:00:00+00:00\"}");

        ProjectSummary created = await Api(handler).CreateAsync("New");

        (HttpMethod method, string path, string body) = handler.Requests.Single();
        Assert.Equal(HttpMethod.Post, method);
        Assert.Equal("/audiolab/api/projects", path);
        Assert.Equal("{\"name\":\"New\"}", body);
        Assert.Equal("p2", created.Id);
    }

    [Fact]
    public async Task AnOpenedProjectIsTheStrictlyReadDocumentWithTheRevisionTheHeaderSays()
    {
        StubHandler handler = new(HttpStatusCode.OK, ProjectJson.Write(Song.Empty()), "application/json", (AudioLabApi.RevisionHeader, "7"));

        OpenedProject opened = await Api(handler).OpenAsync("song");

        Assert.Equal("/audiolab/api/projects/song", handler.Requests.Single().Path);
        Assert.Equal(7, opened.Revision);
        Assert.Equal("Song", opened.Document.Name);
    }

    [Fact]
    public async Task AnOpenedProjectWithoutARevisionOrWithAnUnreadableDocumentIsRefused()
    {
        StubHandler noRevision = new(HttpStatusCode.OK, ProjectJson.Write(Song.Empty()));
        StubHandler unreadable = new(HttpStatusCode.OK, "{\"version\":4,\"surprise\":1}", "application/json", (AudioLabApi.RevisionHeader, "1"));

        await Assert.ThrowsAsync<ApiException>(() => Api(noRevision).OpenAsync("song"));
        ApiException error = await Assert.ThrowsAsync<ApiException>(() => Api(unreadable).OpenAsync("song"));

        Assert.StartsWith("This version of AudioLab cannot read the project", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ADeleteNamesTheProjectInTheAddress()
    {
        StubHandler handler = new(HttpStatusCode.NoContent, string.Empty);

        await Api(handler).DeleteAsync("p 3");

        Assert.Equal(HttpMethod.Delete, handler.Requests.Single().Method);
        Assert.Equal("/audiolab/api/projects/p%203", handler.Requests.Single().Path);
    }

    [Fact]
    public async Task ARefusalCarriesTheServersTitleAndItsFirstProblem()
    {
        StubHandler quota = new(HttpStatusCode.Conflict, "{\"title\":\"You have the most projects allowed.\"}", "application/problem+json");
        StubHandler invalid = new(
            HttpStatusCode.UnprocessableEntity,
            "{\"title\":\"The project is not valid.\",\"problems\":[\"name: a project needs a name.\"]}",
            "application/problem+json");

        ApiException full = await Assert.ThrowsAsync<ApiException>(() => Api(quota).CreateAsync("x"));
        ApiException problems = await Assert.ThrowsAsync<ApiException>(() => Api(invalid).CreateAsync("x"));

        Assert.Equal(HttpStatusCode.Conflict, full.Status);
        Assert.Equal("You have the most projects allowed.", full.Message);
        Assert.Equal("The project is not valid. name: a project needs a name.", problems.Message);
    }

    [Fact]
    public async Task AnAnswerThatIsNotProblemDetailsStillGivesAReadableRefusal()
    {
        StubHandler gateway = new(HttpStatusCode.BadGateway, "<html>bad gateway</html>", "text/html");

        ApiException error = await Assert.ThrowsAsync<ApiException>(() => Api(gateway).ListAsync());

        Assert.Equal("The server refused the request (502).", error.Message);
    }
}
