using System.Net.WebSockets;
using System.Text.Json;
using Microsoft.AspNetCore.SignalR;

namespace AudioLab.Web.Services;

/// <summary>Turns whatever went wrong in a request into one sentence for the person who asked, never a stack or a type name.</summary>
public static class FailureText
{
    /// <summary>The sentence for a failure; the caller handles a cancellation the user asked for before it asks for the text.</summary>
    public static string Of(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);
        return exception switch
        {
            ApiException or HubException or UploadInterruptedException => exception.Message,
            HttpRequestException or WebSocketException => "The server did not answer. Check that AudioLab is still running.",
            OperationCanceledException or TimeoutException => "The server took too long to answer. Try again.",
            JsonException => "The server sent an answer this page cannot read.",
            InvalidOperationException => "The connection to the server is not ready. Try again in a moment.",
            _ => "Something went wrong. Try again.",
        };
    }
}
