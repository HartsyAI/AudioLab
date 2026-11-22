using System.Collections.Concurrent;
using System.Net;
using System.Text;
using System.Text.Encodings.Web;
using AudioLab.SpikeServer;
using Microsoft.Extensions.FileProviders;

// Usage: AudioLabSpikeServer <web-publish-wwwroot> <worker-wwwroot> <reports-dir> <port> [clip-seconds]
string webRoot = args[0];
string workerRoot = args[1];
string reportsDirectory = args[2];
int port = int.Parse(args[3], System.Globalization.CultureInfo.InvariantCulture);
int clipSeconds = args.Length > 4 ? int.Parse(args[4], System.Globalization.CultureInfo.InvariantCulture) : 30;
Directory.CreateDirectory(reportsDirectory);

byte[][] clips = new byte[8][];
for (int i = 0; i < clips.Length; i++)
{
    clips[i] = ClipSynth.Create(i, clipSeconds);
}
ConcurrentDictionary<string, string> lastReport = new();
long mediaBytes = 0;
long mediaRequests = 0;

WebApplicationBuilder builder = WebApplication.CreateBuilder();
builder.Logging.ClearProviders();
builder.WebHost.UseSockets(options => options.Backlog = 4096);
builder.WebHost.ConfigureKestrel(options => options.Listen(IPAddress.Loopback, port));
WebApplication app = builder.Build();
app.UseDefaultFiles(new DefaultFilesOptions { FileProvider = new PhysicalFileProvider(webRoot) });
app.UseStaticFiles(new StaticFileOptions { FileProvider = new PhysicalFileProvider(webRoot), ServeUnknownFileTypes = true });
app.UseStaticFiles(new StaticFileOptions { FileProvider = new PhysicalFileProvider(workerRoot), RequestPath = "/worker", ServeUnknownFileTypes = true });

app.MapGet("/host", (HttpContext context) =>
{
    string query = HtmlEncoder.Default.Encode(context.Request.QueryString.Value ?? string.Empty);
    string page = "<!DOCTYPE html><html><head><meta charset=\"utf-8\"><title>O1 host</title></head>"
        + "<body style=\"margin:0;background:#14171b\"><iframe src=\"/" + query + "\" allow=\"autoplay\" "
        + "style=\"border:0;width:100%;height:100vh\"></iframe></body></html>";
    return Results.Content(page, "text/html", Encoding.UTF8);
});

app.MapGet("/media/clip-{index:int}.wav", (int index, HttpContext context) =>
{
    if (index < 0 || index >= clips.Length)
    {
        return Results.NotFound();
    }
    Interlocked.Add(ref mediaBytes, clips[index].Length);
    Interlocked.Increment(ref mediaRequests);
    context.Response.Headers.CacheControl = "no-store";
    return Results.Bytes(clips[index], "audio/wav");
});

app.MapPost("/report", async (HttpContext context) =>
{
    using StreamReader reader = new(context.Request.Body);
    string body = await reader.ReadToEndAsync();
    string label = SafeLabel(context.Request.Query["label"].ToString());
    lastReport[label] = body;
    await File.AppendAllTextAsync(Path.Combine(reportsDirectory, label + ".jsonl"), body.ReplaceLineEndings(" ") + "\n");
    return Results.Ok();
});

app.MapGet("/last", (string label) => lastReport.TryGetValue(SafeLabel(label), out string? body)
    ? Results.Content(body, "application/json")
    : Results.NotFound());

app.MapGet("/status", () => Results.Json(new { mediaBytes = Interlocked.Read(ref mediaBytes), mediaRequests = Interlocked.Read(ref mediaRequests), clipBytes = clips[0].Length }));

app.Run();

static string SafeLabel(string label)
{
    StringBuilder safe = new();
    foreach (char c in label)
    {
        safe.Append(char.IsAsciiLetterOrDigit(c) || c is '-' or '_' ? c : '_');
    }
    return safe.Length == 0 ? "unlabelled" : safe.ToString();
}
