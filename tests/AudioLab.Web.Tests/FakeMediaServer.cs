using System.Globalization;
using System.Net;
using System.Text;

namespace AudioLab.Web.Tests;

/// <summary>A media store in memory behind a handler: it lists, takes uploads, deletes, enforces its limits, and can refuse or stall.</summary>
internal sealed class FakeMediaServer : HttpMessageHandler
{
    /// <summary>What a file that was there from the start weighs.</summary>
    public const long SeedBytes = 264644;

    private readonly List<(string Id, string Name, long Bytes)> _files = [];

    private readonly Lock _gate = new();

    private int _next;

    private int _running;

    private long _runningBytes;

    public FakeMediaServer(IEnumerable<string> ids)
    {
        foreach (string id in ids)
        {
            _files.Add((id, "a.wav", SeedBytes));
            _next = Math.Max(_next, int.Parse(id[1..], CultureInfo.InvariantCulture));
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

    /// <summary>What the limits route answers with.</summary>
    public HttpStatusCode LimitsStatus { get; set; } = HttpStatusCode.OK;

    /// <summary>Whether a request for the limits ends in the client timeout.</summary>
    public bool LimitsTimeOut { get; set; }

    /// <summary>Called with the number of each limits request once its answer is worked out; the answer is sent when the task ends.</summary>
    public Func<int, Task>? BeforeLimitsAnswer { get; set; }

    /// <summary>The largest file one upload may carry; the limits route says it and an upload over it is refused.</summary>
    public long MaxUploadBytes { get; set; } = 512L * 1024 * 1024;

    /// <summary>The most files the user may keep; the limits route says it and an upload that would pass it is refused.</summary>
    public int MaxFiles { get; set; } = 5000;

    /// <summary>The most bytes of media the user may keep; the limits route says it and an upload that would pass it is refused.</summary>
    public long MaxBytes { get; set; } = 20L * 1024 * 1024 * 1024;

    /// <summary>The most uploads the user may have running; the limits route says it and one more is refused with a 429.</summary>
    public int MaxConcurrentUploads { get; set; } = 4;

    /// <summary>How many times the limits were asked for.</summary>
    public int LimitReads { get; private set; }

    /// <summary>How many list reads were asked.</summary>
    public int Reads { get; private set; }

    /// <summary>The most uploads that were in flight at once.</summary>
    public int MostAtOnce { get; private set; }

    /// <summary>The uploads that arrived, with their address and bytes.</summary>
    public List<(string Path, byte[] Body)> Uploads { get; } = [];

    /// <summary>How many files the user has now, which a limits answer counts.</summary>
    public int FileCount
    {
        get
        {
            lock (_gate)
            {
                return _files.Count;
            }
        }
    }

    public void Add(string id, string name, long bytes = SeedBytes)
    {
        lock (_gate)
        {
            _files.Add((id, name, bytes));
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

    private static string Summary(string id, string name, long bytes)
    {
        return $"{{\"id\":\"{id}\",\"name\":\"{name}\",\"kind\":\"audio\",\"format\":\"wav\",\"encoding\":\"pcm16\",\"duration\":1.5,"
            + $"\"sampleRate\":44100,\"channels\":2,\"bytes\":{bytes},"
            + "\"sha256\":\"0000000000000000000000000000000000000000000000000000000000000000\","
            + "\"created\":\"2026-11-22T10:00:00+00:00\"}";
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (request.Method == TimeOut)
        {
            throw new TaskCanceledException("The request timed out.");
        }
        if (request.Method == HttpMethod.Get && request.RequestUri!.AbsolutePath.EndsWith("/limits", StringComparison.Ordinal))
        {
            return await LimitsAsync(cancellationToken);
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

    private async Task<HttpResponseMessage> LimitsAsync(CancellationToken cancellationToken)
    {
        int read;
        int files;
        long bytes;
        int running;
        lock (_gate)
        {
            read = ++LimitReads;
            files = _files.Count + _running;
            bytes = _files.Sum(file => file.Bytes) + _runningBytes;
            running = _running;
        }
        if (LimitsTimeOut)
        {
            throw new TaskCanceledException("The request timed out.");
        }
        if (BeforeLimitsAnswer is { } gate)
        {
            await gate(read).WaitAsync(cancellationToken);
        }
        if (LimitsStatus != HttpStatusCode.OK)
        {
            return Problem(LimitsStatus, "There is no such route.");
        }
        string json = $"{{\"maxUploadBytes\":{MaxUploadBytes},\"maxFiles\":{MaxFiles},\"maxBytes\":{MaxBytes},"
            + $"\"maxConcurrentUploads\":{MaxConcurrentUploads},\"files\":{files},\"bytes\":{bytes},"
            + $"\"filesRemaining\":{Math.Max(0, MaxFiles - files)},\"bytesRemaining\":{Math.Max(0L, MaxBytes - bytes)},"
            + $"\"uploadsRunning\":{running}}}";
        return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(json, Encoding.UTF8, "application/json") };
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
            files = [.. Enumerable.Reverse(_files).Select(file => Summary(file.Id, file.Name, file.Bytes))];
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

    // The limits are enforced as the real server does, before the body is read, from the length that was announced.
    private HttpResponseMessage? Refusal(long length)
    {
        lock (_gate)
        {
            if (_running > MaxConcurrentUploads)
            {
                return Problem(HttpStatusCode.TooManyRequests, "You have too many uploads running; try again in a moment.");
            }
            if (length > MaxUploadBytes)
            {
                return Problem(HttpStatusCode.RequestEntityTooLarge, "The file is larger than the server accepts.");
            }
            if (_files.Count + _running > MaxFiles)
            {
                return Problem(HttpStatusCode.InsufficientStorage, $"You already have {MaxFiles} media files, the most allowed; delete one first.");
            }
            if (_files.Sum(file => file.Bytes) + _runningBytes + length > MaxBytes)
            {
                return Problem(HttpStatusCode.InsufficientStorage, "This file would take your media past what is allowed; delete some first.");
            }
            return null;
        }
    }

    private async Task<HttpResponseMessage> UploadAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        long length = request.Content!.Headers.ContentLength ?? 0;
        int now;
        HttpResponseMessage? refused;
        lock (_gate)
        {
            now = ++_running;
            MostAtOnce = Math.Max(MostAtOnce, now);
            refused = Refusal(length);
            if (refused is null)
            {
                _runningBytes += length;
            }
        }
        try
        {
            if (refused is not null)
            {
                return refused;
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
                _files.Add((id, name, body.Length));
                return new HttpResponseMessage(HttpStatusCode.Created)
                {
                    Content = new StringContent(Summary(id, name, body.Length), Encoding.UTF8, "application/json"),
                };
            }
        }
        finally
        {
            lock (_gate)
            {
                _running--;
                if (refused is null)
                {
                    _runningBytes -= length;
                }
            }
        }
    }
}
