using System.Net;
using System.Net.Http.Headers;

namespace AudioLab.Web.Tests;

/// <summary>Answers each request from a function and keeps what it was sent, the bytes of a body included.</summary>
internal sealed class RecordingHandler(Func<HttpRequestMessage, HttpResponseMessage> answer) : HttpMessageHandler
{
    public List<SentRequest> Requests { get; } = [];

    public static RecordingHandler Always(HttpStatusCode status, byte[] body, string contentType, params (string Name, string Value)[] headers)
    {
        return new RecordingHandler(_ =>
        {
            HttpResponseMessage response = new(status) { Content = new ByteArrayContent(body) };
            response.Content.Headers.ContentType = new MediaTypeHeaderValue(contentType);
            foreach ((string name, string value) in headers)
            {
                response.Headers.Add(name, value);
            }
            return response;
        });
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        byte[] sent = request.Content is null ? [] : await request.Content.ReadAsByteArrayAsync(cancellationToken);
        Requests.Add(new SentRequest(
            request.Method,
            request.RequestUri!.PathAndQuery,
            sent,
            request.Content?.Headers.ContentType?.MediaType,
            request.Content?.Headers.ContentLength,
            request.Headers.Range?.ToString()));
        return answer(request);
    }
}

internal sealed record SentRequest(HttpMethod Method, string Path, byte[] Body, string? ContentType, long? ContentLength, string? Range);
