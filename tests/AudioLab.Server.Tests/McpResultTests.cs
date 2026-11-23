using AudioLab.Core.Commands;
using AudioLab.Core.Realtime;
using AudioLab.Server.Mcp;
using ModelContextProtocol.Protocol;

namespace AudioLab.Server.Tests;

public sealed class McpResultTests
{
    [Fact]
    public void EveryRefusalClassIsAnErrorResultThatCarriesItsClassAndReason()
    {
        foreach (CommandErrorCode code in Enum.GetValues<CommandErrorCode>())
        {
            CallToolResult result = McpResults.Refused(code, "Because.", revision: 7);

            Assert.True(result.IsError);
            CommandReply reply = McpTestClient.Reply(result, HubJsonContext.Default.CommandReply);
            Assert.False(reply.Succeeded);
            Assert.False(reply.Changed);
            Assert.Equal(7, reply.Revision);
            Assert.Equal(new HubCommandError(code, "Because."), Assert.Single(reply.Errors));
            Assert.Null(result.StructuredContent);
        }
    }

    [Fact]
    public void ASuccessIsNotAnErrorAndCarriesTheNewRevisionAndTheCreatedIds()
    {
        CallToolResult result = McpResults.Reply(new CommandReply(true, true, 9, [], ["m1", "m2"]));

        Assert.NotEqual(true, result.IsError);
        CommandReply reply = McpTestClient.Reply(result, HubJsonContext.Default.CommandReply);
        Assert.Equal((9L, true, true), (reply.Revision, reply.Changed, reply.Succeeded));
        Assert.Equal(["m1", "m2"], reply.Created);
    }

    [Fact]
    public void ARefusalClassIsWrittenAsItsWireName()
    {
        string text = McpTestClient.Text(McpResults.Refused(CommandErrorCode.NotFound, "Gone."));

        Assert.Contains("\"code\":\"not_found\"", text, StringComparison.Ordinal);
    }
}
