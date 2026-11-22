using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using AudioLab.Core.Model;
using AudioLab.Server.Json;
using AudioLab.Server.Routes;
using AudioLab.Server.Services;
using AudioLab.Server.Store;
using Microsoft.Extensions.DependencyInjection;

namespace AudioLab.Server.Tests;

public sealed class ProjectRouteTests
{
    private const string Secret = "0123456789abcdef0123456789abcdef";

    [Fact]
    public async Task CreateOpenSaveDuplicateListAndDeleteWorkOverHttp()
    {
        await using RunningServer server = await RunningServer.StartAsync();
        HttpClient client = server.Client;

        using HttpResponseMessage created = await client.PostAsJsonAsync("/api/projects", new CreateProjectRequest("Over HTTP"), ServerJsonContext.Default.CreateProjectRequest);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        ProjectSummary summary = (await created.Content.ReadFromJsonAsync(ServerJsonContext.Default.ProjectSummary))!;
        Assert.Equal($"/api/projects/{summary.Id}", created.Headers.Location?.ToString());

        using HttpResponseMessage opened = await client.GetAsync($"/api/projects/{summary.Id}");
        Assert.Equal(HttpStatusCode.OK, opened.StatusCode);
        Assert.Equal("1", opened.Headers.GetValues(ProjectRoutes.RevisionHeader).Single());
        ProjectDocument document = ProjectJson.Read(await opened.Content.ReadAsStringAsync());
        Assert.Equal(UserId.Local, document.Owner);
        Assert.Equal("Over HTTP", document.Name);

        SaveResult? saved = await (await client.PostAsync($"/api/projects/{summary.Id}/save", null)).Content.ReadFromJsonAsync(ServerJsonContext.Default.SaveResult);
        Assert.Equal(new SaveResult(summary.Id, 1), saved);

        using HttpResponseMessage copied = await client.PostAsync($"/api/projects/{summary.Id}/duplicate", null);
        Assert.Equal(HttpStatusCode.Created, copied.StatusCode);
        ProjectSummary copy = (await copied.Content.ReadFromJsonAsync(ServerJsonContext.Default.ProjectSummary))!;
        Assert.Equal("Copy of Over HTTP", copy.Name);
        Assert.NotEqual(summary.Id, copy.Id);
        using HttpResponseMessage named = await client.PostAsJsonAsync($"/api/projects/{summary.Id}/duplicate", new DuplicateProjectRequest("Second copy"), ServerJsonContext.Default.DuplicateProjectRequest);
        Assert.Equal("Second copy", (await named.Content.ReadFromJsonAsync(ServerJsonContext.Default.ProjectSummary))!.Name);

        ProjectListing listing = (await client.GetFromJsonAsync("/api/projects", ServerJsonContext.Default.ProjectListing))!;
        Assert.Equal(3, listing.Projects.Count);
        Assert.Equal(0, listing.Refused);

        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync($"/api/projects/{summary.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/projects/{summary.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.DeleteAsync($"/api/projects/{summary.Id}")).StatusCode);
        Assert.Equal(2, (await client.GetFromJsonAsync("/api/projects", ServerJsonContext.Default.ProjectListing))!.Projects.Count);
    }

    [Fact]
    public async Task ARequestCannotNameTheOwnerOrAnythingElseItDoesNotKnow()
    {
        await using RunningServer server = await RunningServer.StartAsync();
        using StringContent body = new("""{"name":"x","owner":"swarm-someoneelse"}""", Encoding.UTF8, "application/json");
        using HttpResponseMessage response = await server.Client.PostAsync("/api/projects", body);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Empty(Directory.GetFiles(server.StoreFolder, "p*", SearchOption.AllDirectories));
    }

    [Fact]
    public async Task AnInvalidNameIsAnswered422WithTheProblemList()
    {
        await using RunningServer server = await RunningServer.StartAsync();
        using HttpResponseMessage response = await server.Client.PostAsJsonAsync("/api/projects", new CreateProjectRequest(""), ServerJsonContext.Default.CreateProjectRequest);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        string[] problems = await ProblemsAsync(response);
        Assert.Contains(problems, problem => problem.StartsWith("name", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ACorruptFileIsAnswered422WithItsErrorListAndStaysOnDisk()
    {
        await using RunningServer server = await RunningServer.StartAsync();
        ProjectSummary created = (await (await server.Client.PostAsJsonAsync("/api/projects", new CreateProjectRequest("Song"), ServerJsonContext.Default.CreateProjectRequest))
            .Content.ReadFromJsonAsync(ServerJsonContext.Default.ProjectSummary))!;
        string file = Directory.GetFiles(server.StoreFolder, "p*.json", SearchOption.AllDirectories).Single();
        string json = await File.ReadAllTextAsync(file);
        await File.WriteAllTextAsync(file, json.Replace("\"bpm\":120", "\"bpm\":-5", StringComparison.Ordinal));

        using HttpResponseMessage response = await server.Client.GetAsync($"/api/projects/{created.Id}");
        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Contains(await ProblemsAsync(response), problem => problem.StartsWith("bpm", StringComparison.Ordinal));
        ProjectListing listing = (await server.Client.GetFromJsonAsync("/api/projects", ServerJsonContext.Default.ProjectListing))!;
        Assert.Single(listing.Projects);
        Assert.Equal(0, listing.Refused);
    }

    [Theory]
    [InlineData("..%2F..%2Fsecret")]
    [InlineData("%2e%2e")]
    [InlineData("..")]
    [InlineData("CON")]
    [InlineData("PRJ_UPPER")]
    [InlineData("a%00b")]
    [InlineData("a%5Cb")]
    [InlineData("%2Fetc%2Fpasswd")]
    [InlineData("C%3A%5CWindows")]
    public async Task AnIdThatIsNotAnIdIsRefusedAndTouchesNothing(string hostile)
    {
        using TempFolder parent = new();
        string root = Path.Combine(parent.Path, "store");
        await using RunningServer server = await RunningServer.StartAsync(new Dictionary<string, string?> { ["AudioLab:Storage:Root"] = root });
        string[] before = Directory.GetFileSystemEntries(parent.Path);
        foreach (HttpMethod method in new[] { HttpMethod.Get, HttpMethod.Delete, HttpMethod.Post })
        {
            foreach (string suffix in new[] { string.Empty, "/save", "/duplicate" })
            {
                if (method == HttpMethod.Get && suffix.Length > 0)
                {
                    continue;
                }
                using HttpRequestMessage request = new(method, $"/api/projects/{hostile}{suffix}");
                using HttpResponseMessage response = await server.Client.SendAsync(request);
                Assert.True(
                    response.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.NotFound or HttpStatusCode.MethodNotAllowed,
                    $"{method} {hostile}{suffix} answered {(int)response.StatusCode}");
            }
        }
        Assert.Equal(before.Order(), Directory.GetFileSystemEntries(parent.Path).Order());
        Assert.Empty(Directory.GetFiles(root, "p*", SearchOption.AllDirectories));
    }

    [Fact]
    public async Task OneUserCannotReachAnotherUsersProjectsOverHttp()
    {
        await using RunningServer server = await RunningServer.StartAsync(new Dictionary<string, string?>
        {
            ["AudioLab:Door:Kind"] = "Swarm",
            ["AudioLab:Door:ProxySecret"] = Secret,
        });
        using HttpResponseMessage made = await Send(server, HttpMethod.Post, "/api/projects", "alice", JsonContent.Create(new CreateProjectRequest("Alice's"), ServerJsonContext.Default.CreateProjectRequest));
        ProjectSummary mine = (await made.Content.ReadFromJsonAsync(ServerJsonContext.Default.ProjectSummary))!;

        Assert.Equal(HttpStatusCode.OK, (await Send(server, HttpMethod.Get, $"/api/projects/{mine.Id}", "alice")).StatusCode);
        foreach ((HttpMethod method, string path) in new[]
        {
            (HttpMethod.Get, $"/api/projects/{mine.Id}"),
            (HttpMethod.Post, $"/api/projects/{mine.Id}/save"),
            (HttpMethod.Post, $"/api/projects/{mine.Id}/duplicate"),
            (HttpMethod.Delete, $"/api/projects/{mine.Id}"),
        })
        {
            Assert.Equal(HttpStatusCode.NotFound, (await Send(server, method, path, "bob")).StatusCode);
        }
        using HttpResponseMessage bobsList = await Send(server, HttpMethod.Get, "/api/projects", "bob");
        Assert.Empty((await bobsList.Content.ReadFromJsonAsync(ServerJsonContext.Default.ProjectListing))!.Projects);
        Assert.Equal(HttpStatusCode.OK, (await Send(server, HttpMethod.Get, $"/api/projects/{mine.Id}", "alice")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await server.Client.GetAsync("/api/projects")).StatusCode);
    }

    [Fact]
    public async Task StoppingTheServerWritesAnUnsavedProject()
    {
        TempFolder folder = new();
        try
        {
            Dictionary<string, string?> settings = new() { ["AudioLab:Storage:Root"] = folder.Path, ["urls"] = "http://127.0.0.1:0" };
            string id;
            await using (RunningServer server = await RunningServer.StartAsync(settings))
            {
                ProjectSummary created = (await (await server.Client.PostAsJsonAsync("/api/projects", new CreateProjectRequest("Song"), ServerJsonContext.Default.CreateProjectRequest))
                    .Content.ReadFromJsonAsync(ServerJsonContext.Default.ProjectSummary))!;
                id = created.Id;
                (await server.Services.GetRequiredService<OpenProjects>().OpenAsync(UserId.Local, ProjectId.From(id), CancellationToken.None))
                    .Update(document => document with { Name = "Edited before stop" });
            }
            await using TestStore reopened = TestStore.On(folder.Path);
            Assert.Equal("Edited before stop", (await reopened.Projects.ReadAsync(UserId.Local, ProjectId.From(id), CancellationToken.None)).Name);
        }
        finally
        {
            folder.Dispose();
        }
    }

    private static async Task<string[]> ProblemsAsync(HttpResponseMessage response)
    {
        using JsonDocument body = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        return [.. body.RootElement.GetProperty("problems").EnumerateArray().Select(item => item.GetString()!)];
    }

    private static async Task<HttpResponseMessage> Send(RunningServer server, HttpMethod method, string path, string user, HttpContent? content = null)
    {
        using HttpRequestMessage request = new(method, path) { Content = content };
        request.Headers.Add("X-AudioLab-Secret", Secret);
        request.Headers.Add("X-AudioLab-User", user);
        return await server.Client.SendAsync(request);
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("""{"name":null}""")]
    public async Task ACreateWithNoNameIsAnswered422NotAnError(string json)
    {
        await using RunningServer server = await RunningServer.StartAsync();
        using StringContent body = new(json, Encoding.UTF8, "application/json");
        using HttpResponseMessage response = await server.Client.PostAsync("/api/projects", body);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Contains(await ProblemsAsync(response), problem => problem.StartsWith("name", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("null")]
    [InlineData("[]")]
    [InlineData("""{"name":5}""")]
    [InlineData("not json")]
    public async Task ABodyThatIsNotAProjectRequestIsAnswered4xxNeverAnError(string json)
    {
        await using RunningServer server = await RunningServer.StartAsync();
        ProjectSummary made = (await (await server.Client.PostAsJsonAsync("/api/projects", new CreateProjectRequest("Song"), ServerJsonContext.Default.CreateProjectRequest))
            .Content.ReadFromJsonAsync(ServerJsonContext.Default.ProjectSummary))!;
        foreach (string path in new[] { "/api/projects", $"/api/projects/{made.Id}/duplicate" })
        {
            using StringContent body = new(json, Encoding.UTF8, "application/json");
            using HttpResponseMessage response = await server.Client.PostAsync(path, body);
            int status = (int)response.StatusCode;
            bool optionalBody = path.EndsWith("/duplicate", StringComparison.Ordinal) && json == "null";
            Assert.True(status is >= 400 and < 500 || (optionalBody && status == 201), $"{path} with '{json}' answered {status}");
        }
    }

    [Fact]
    public async Task ADuplicateWithANullNameUsesTheDefaultName()
    {
        await using RunningServer server = await RunningServer.StartAsync();
        ProjectSummary made = (await (await server.Client.PostAsJsonAsync("/api/projects", new CreateProjectRequest("Song"), ServerJsonContext.Default.CreateProjectRequest))
            .Content.ReadFromJsonAsync(ServerJsonContext.Default.ProjectSummary))!;
        using StringContent body = new("""{"name":null}""", Encoding.UTF8, "application/json");
        using HttpResponseMessage response = await server.Client.PostAsync($"/api/projects/{made.Id}/duplicate", body);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal("Copy of Song", (await response.Content.ReadFromJsonAsync(ServerJsonContext.Default.ProjectSummary))!.Name);
    }

    [Fact]
    public async Task ARequestBodyOverTheLimitIsRefusedBeforeItIsRead()
    {
        await using RunningServer server = await RunningServer.StartAsync(new Dictionary<string, string?> { ["AudioLab:MaxRequestBodyBytes"] = "2048" });
        using StringContent big = new("{\"name\":\"" + new string('x', 100_000) + "\"}", Encoding.UTF8, "application/json");
        using HttpResponseMessage response = await server.Client.PostAsync("/api/projects", big);
        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, response.StatusCode);
        Assert.Empty(Directory.GetFiles(server.StoreFolder, "p*", SearchOption.AllDirectories));
    }

    [Fact]
    public async Task AFileTheServerCannotUseIsAnswered503WithNoPathInTheBody()
    {
        await using RunningServer server = await RunningServer.StartAsync();
        ProjectSummary made = (await (await server.Client.PostAsJsonAsync("/api/projects", new CreateProjectRequest("Song"), ServerJsonContext.Default.CreateProjectRequest))
            .Content.ReadFromJsonAsync(ServerJsonContext.Default.ProjectSummary))!;
        string file = Directory.GetFiles(server.StoreFolder, "p*.json", SearchOption.AllDirectories).Single();
        using FileStream hold = new(file, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        using HttpResponseMessage response = await server.Client.GetAsync($"/api/projects/{made.Id}");
        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        string text = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain(server.StoreFolder, text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("users", text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AFullQuotaIsAnswered409()
    {
        await using RunningServer server = await RunningServer.StartAsync(new Dictionary<string, string?> { ["AudioLab:Storage:MaxProjectsPerUser"] = "1" });
        Assert.Equal(HttpStatusCode.Created, (await server.Client.PostAsJsonAsync("/api/projects", new CreateProjectRequest("One"), ServerJsonContext.Default.CreateProjectRequest)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await server.Client.PostAsJsonAsync("/api/projects", new CreateProjectRequest("Two"), ServerJsonContext.Default.CreateProjectRequest)).StatusCode);
    }
}
