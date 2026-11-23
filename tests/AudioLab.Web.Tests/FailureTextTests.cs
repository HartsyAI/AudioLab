using System.Net;
using System.Net.WebSockets;
using System.Text.Json;
using AudioLab.Web.Services;
using Microsoft.AspNetCore.SignalR;

namespace AudioLab.Web.Tests;

/// <summary>Holds the failure text tests.</summary>
public sealed class FailureTextTests
{
    /// <summary>Failures.</summary>
    public static TheoryData<Exception, string> Failures { get; } = new()
    {
        { new ApiException(HttpStatusCode.NotFound, "That project is gone."), "That project is gone." },
        { new HubException("The project could not be found."), "The project could not be found." },
        { new HttpRequestException("Failed to fetch"), "The server did not answer. Check that AudioLab is still running." },
        { new WebSocketException("closed"), "The server did not answer. Check that AudioLab is still running." },
        { new TaskCanceledException("The request was canceled due to the configured timeout"), "The server took too long to answer. Try again." },
        { new TimeoutException(), "The server took too long to answer. Try again." },
        { new JsonException("bad json"), "The server sent an answer this page cannot read." },
        {
            new InvalidOperationException("The 'InvokeCoreAsync' method cannot be called if the connection is not active"),
            "The connection to the server is not ready. Try again in a moment."
        },
        { new NullReferenceException(), "Something went wrong. Try again." },
    };

    /// <summary>Every failure is one sentence without a type name or a stack.</summary>
    [Theory]
    [MemberData(nameof(Failures))]
    public void EveryFailureIsOneSentenceWithoutATypeNameOrAStack(Exception failure, string expected)
    {
        Assert.Equal(expected, FailureText.Of(failure));
    }
}
