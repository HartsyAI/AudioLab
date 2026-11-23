using System.Net;
using System.Text;

namespace AudioLab.Web.Tests;

/// <summary>A media store in memory behind a handler: it lists, takes uploads, deletes, and can be told to refuse, stall or time out.</summary>
internal sealed class FakeMediaServer : HttpMessageHandler
{
    private readonly List<(string Id, string Name)> _files = [];

    private readonly Lock _gate = new();

    private int _next;

    private int _running;

    public FakeMediaServer(IEnumerable<string> ids)
    {
        foreach (string id in ids)
        {
            _files.Add((id, "a.wav"));
            _next = Math.Max(_next, int.Parse(id[1..], System.Globalization.CultureInfo.InvariantCulture));
        }
    }

    /// <summary>What the list answers with.</summary>
    public HttpStatusCode ListStatus { get; set; } = HttpStatusCode.OK;

    /// <summary>A method whose requests end in the client timeout.</summary>
    public HttpMethod? TimeOut { get; set; }

    /// <summary>Whether an upload waits for the caller to cancel.</summary>
    public bool Stall { get; set; }

    /// <summary>Whether an upload is waiting now.</summary>
    public bool Stalled { get; private set; }

    /// <summary>The statuses of the next uploads, taken one each.</summary>
    public List<HttpStatusCode> NextUploads { get; set; } = [];

    /// <summary>How long an upload takes.</summary>
    public TimeSpan UploadDelay { get; set; }

    /// <summary>A task that the first list read waits for.</summary>
    public Task? HoldFirstList { get; set; }

    /// <summary>Files a delete refuses because a project uses them.</summary>
    public HashSet<string> InUse { get; set; } = [];

    /// <summary>Files that are no longer there when a delete asks.</summary>
    public HashSet<string> Gone { get; set; } = [];

    /// <summary>How many list reads were asked.</summary>
    public int Reads { get; private set; }

    /// <summary>The most uploads that were in flight at once.</summary>
    public int MostAtOnce { get; private set; }

    /// <summary>The uploads that arrived, with their address and bytes.</summary>
    public List<(string Path, byte[] Body)> Uploads { get; } = [];

    public void Add(string id, string name)
    {
        lock (_gate)
        {
            _files.Add((id, name));
        }
    }

    private const string InUseText = "The file is still used by your project \"Song\"; remove it from your projects before deleting it.";

    private static string Json(string title) => $"{{\"title\":{System.Text.Json.JsonSerializer.Serialize(title)}}}";

    private static HttpResponseMessage Problem(HttpStatusCode status, string title)
    {
        return new HttpResponseMessage(status)
        {
            Content = new StringContent(Json(title), Encoding.UTF8, "application/problem+json"),
        };
    }

    private static string Summary(string id, string name)
    {
        return $"{{\"id\":\"{id}\",\"name\":\"{name}\",\"kind\":\"audio\",\"format\":\"wav\",\"encoding\":\"pcm16\",\"duration\":1.5,"
            + "\"sampleRate\":44100,\"channels\":2,\"bytes\":264644,\"sha256\":\"0000000000000000000000000000000000000000000000000000000000000000\","
            + "\"created\":\"2026-11-22T10:00:00+00:00\"}";
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (request.Method == TimeOut)
        {
            throw new TaskCanceledException("The request timed out.");
        }
        if (request.Method == HttpMethod.Get)
        {
            return await ListAsync(cancellationToken);
        }
        if (request.Method == HttpMethod.Post)
        {
            return await UploadAsync(request, cancellationToken);
        }
        string id = request.RequestUri!.AbsolutePath.Split('/')[^1];
        if (Gone.Contains(id))
        {
            lock (_gate)
            {
                _files.RemoveAll(file => file.Id == id);
            }
            return Problem(HttpStatusCode.NotFound, "There is no such media file.");
        }
        if (InUse.Contains(id))
        {
            return Problem(HttpStatusCode.Conflict, InUseText);
        }
        lock (_gate)
        {
            _files.RemoveAll(file => file.Id == id);
        }
        return new HttpResponseMessage(HttpStatusCode.NoContent);
    }

    private async Task<HttpResponseMessage> ListAsync(CancellationToken cancellationToken)
    {
        int read;
        lock (_gate)
        {
            read = ++Reads;
        }
        if (ListStatus != HttpStatusCode.OK)
        {
            return Problem(ListStatus, "The media store is not available; try again.");
        }
        string[] files;
        lock (_gate)
        {
            files = [.. Enumerable.Reverse(_files).Select(file => Summary(file.Id, file.Name))];
        }
        if (read == 1 && HoldFirstList is { } hold)
        {
            await hold.WaitAsync(cancellationToken);
        }
        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent($"{{\"files\":[{string.Join(',', files)}],\"refused\":0}}", Encoding.UTF8, "application/json"),
        };
    }

    private async Task<HttpResponseMessage> UploadAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        int now = Interlocked.Increment(ref _running);
        try
        {
            lock (_gate)
            {
                MostAtOnce = Math.Max(MostAtOnce, now);
            }
            byte[] body = await request.Content!.ReadAsByteArrayAsync(cancellationToken);
            if (Stall)
            {
                Stalled = true;
                await Task.Delay(Timeout.Infinite, cancellationToken);
            }
            if (UploadDelay > TimeSpan.Zero)
            {
                await Task.Delay(UploadDelay, cancellationToken);
            }
            lock (_gate)
            {
                Uploads.Add((request.RequestUri!.PathAndQuery, body));
                if (NextUploads.Count > 0)
                {
                    HttpStatusCode status = NextUploads[0];
                    NextUploads.RemoveAt(0);
                    return Problem(status, "You already have 5000 media files, the most allowed; delete one first.");
                }
                string name = Uri.UnescapeDataString(request.RequestUri.Query.Split('=')[1]);
                string id = $"m{++_next}";
                _files.Add((id, name));
                return new HttpResponseMessage(HttpStatusCode.Created)
                {
                    Content = new StringContent(Summary(id, name), Encoding.UTF8, "application/json"),
                };
            }
        }
        finally
        {
            Interlocked.Decrement(ref _running);
        }
    }
}
