using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

namespace AudioLab.Web.Services;

/// <summary>What every typed route client does with an answer: refuse a failure with the server's own words, read a body as the record it must be.</summary>
internal static class ApiAnswers
{
    private static readonly TimeSpan _longestRetry = TimeSpan.FromHours(1);

    /// <summary>Reads the body as <typeparamref name="T"/>; a body that is not one is an <see cref="ApiException"/>, never a parser error.</summary>
    public static async Task<T> ReadAsync<T>(HttpResponseMessage response, JsonTypeInfo<T> type, CancellationToken cancellationToken)
    {
        T? value;
        try
        {
            value = await response.Content.ReadFromJsonAsync(type, cancellationToken);
        }
        catch (JsonException)
        {
            throw new ApiException(response.StatusCode, "The server answered with something this page cannot read.");
        }
        return value ?? throw new ApiException(response.StatusCode, "The server answered with nothing.");
    }

    /// <summary>Throws an <see cref="ApiException"/> carrying the server's reason and its Retry-After when the answer is not a success.</summary>
    public static async Task EnsureAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }
        TimeSpan? retryAfter = RetryAfterOf(response);
        string reason = await DescribeAsync(response, cancellationToken);
        throw new ApiException(response.StatusCode, retryAfter is TimeSpan wait ? $"{reason} {RetryText(wait)}" : reason, retryAfter);
    }

    private static TimeSpan? RetryAfterOf(HttpResponseMessage response)
    {
        if (response.StatusCode is not (HttpStatusCode.ServiceUnavailable or HttpStatusCode.TooManyRequests))
        {
            return null;
        }
        TimeSpan? wait = response.Headers.RetryAfter switch
        {
            { Delta: TimeSpan delta } => delta,
            { Date: DateTimeOffset date } => date - DateTimeOffset.UtcNow,
            _ => null,
        };
        return wait is TimeSpan span ? TimeSpan.FromSeconds(Math.Clamp(Math.Ceiling(span.TotalSeconds), 1, _longestRetry.TotalSeconds)) : null;
    }

    private static string RetryText(TimeSpan wait)
    {
        double seconds = wait.TotalSeconds;
        return seconds < 90
            ? string.Create(System.Globalization.CultureInfo.InvariantCulture, $"Try again in {seconds:0} s.")
            : string.Create(System.Globalization.CultureInfo.InvariantCulture, $"Try again in {Math.Ceiling(seconds / 60):0} min.");
    }

    private static async Task<string> DescribeAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        try
        {
            ApiProblem? problem = await response.Content.ReadFromJsonAsync(WebJsonContext.Default.ApiProblem, cancellationToken);
            if (problem is not null)
            {
                return problem.Message;
            }
        }
        catch (JsonException)
        {
            // A proxy or a gateway may answer with something that is not problem details; the status says enough.
        }
        return response.StatusCode == HttpStatusCode.Forbidden
            ? "The server did not accept this page's request."
            : $"The server refused the request ({(int)response.StatusCode}).";
    }
}
