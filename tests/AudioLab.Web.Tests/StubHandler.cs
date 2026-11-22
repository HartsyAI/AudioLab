using System.Net;
using System.Text;

namespace AudioLab.Web.Tests;

/// <summary>Answers every request with one prepared response and keeps what it was asked.</summary>
public sealed class StubHandler(
    HttpStatusCode status,
    string body,
    string contentType = "application/json",
    params (string Name, string Value)[] headers) : HttpMessageHandler
{
    public List<(HttpMethod Method, string Path, string Body)> Requests { get; } = [];

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        string sent = request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken);
        Requests.Add((request.Method, request.RequestUri!.PathAndQuery, sent));
        HttpResponseMessage response = new(status) { Content = new StringContent(body, Encoding.UTF8, contentType) };
        foreach ((string name, string value) in headers)
        {
            response.Headers.Add(name, value);
        }
        return response;
    }
}
