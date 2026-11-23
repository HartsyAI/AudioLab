using AudioLab.Core.Model;
using ModelContextProtocol;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

namespace AudioLab.Server.Tests;

public sealed class McpResourceTests
{
    [Fact]
    public async Task EachProjectOfTheUserIsAResourceWhoseTextIsTheStrictDocument()
    {
        await using RunningServer server = await McpTestClient.StartAsync(McpTestClient.SwarmSettings());
        string alices = await McpTestClient.CreateProjectAsync(server, McpTestClient.SwarmUser("alice"), "Alice's song");
        await McpTestClient.CreateProjectAsync(server, McpTestClient.SwarmUser("bob"), "Bob's song");
        await using McpClient alice = await McpTestClient.ConnectAsync(server, McpTestClient.AsUser("alice"));

        IList<McpClientResource> resources = await alice.ListResourcesAsync();
        McpClientResource listed = Assert.Single(resources);
        Assert.Equal(($"audiolab://projects/{alices}", "Alice's song", "application/json"), (listed.Uri, listed.Name, listed.MimeType));
        IList<McpClientResourceTemplate> templates = await alice.ListResourceTemplatesAsync();
        Assert.Equal("audiolab://projects/{id}", Assert.Single(templates).UriTemplate);

        ReadResourceResult read = await alice.ReadResourceAsync(listed.Uri);
        TextResourceContents contents = Assert.IsType<TextResourceContents>(Assert.Single(read.Contents));
        ProjectDocument document = ProjectJson.Read(contents.Text);
        Assert.Equal((alices, "Alice's song"), (document.Id.Value, document.Name));
    }

    [Fact]
    public async Task AnotherUsersProjectAndAnUnknownOneAreTheSameNotFoundAndABadUriIsInvalid()
    {
        await using RunningServer server = await McpTestClient.StartAsync(McpTestClient.SwarmSettings());
        string alices = await McpTestClient.CreateProjectAsync(server, McpTestClient.SwarmUser("alice"));
        await using McpClient bob = await McpTestClient.ConnectAsync(server, McpTestClient.AsUser("bob"));

        McpProtocolException other = await Assert.ThrowsAsync<McpProtocolException>(
            () => bob.ReadResourceAsync($"audiolab://projects/{alices}").AsTask());
        McpProtocolException missing = await Assert.ThrowsAsync<McpProtocolException>(
            () => bob.ReadResourceAsync("audiolab://projects/nosuchproject").AsTask());
        Assert.Equal(McpErrorCode.ResourceNotFound, other.ErrorCode);
        Assert.Equal((missing.ErrorCode, missing.Message), (other.ErrorCode, other.Message));
        Assert.Empty(await bob.ListResourcesAsync());

        foreach (string uri in new[] { "file:///etc/passwd", "audiolab://projects/", "audiolab://projects/../alice", "audiolab://other/p1" })
        {
            McpProtocolException bad = await Assert.ThrowsAsync<McpProtocolException>(() => bob.ReadResourceAsync(uri).AsTask());
            Assert.Equal(McpErrorCode.InvalidParams, bad.ErrorCode);
        }
    }
}
