using Microsoft.AspNetCore.StaticFiles;

// usage: O9.StaticHost <folder holding index.html> <port>
// Serves a published Blazor folder and its precompressed .br files, as a static web host would.
string root = Path.GetFullPath(args[0]);
int port = int.Parse(args[1]);
WebApplicationBuilder builder = WebApplication.CreateBuilder();
builder.WebHost.UseUrls($"http://127.0.0.1:{port}");
WebApplication app = builder.Build();
FileExtensionContentTypeProvider types = new();
string saveTo = Path.GetFullPath(args.Length > 2 ? args[2] : ".");
app.MapPost("/save/{name}", async (string name, HttpRequest request) =>
{
    using StreamReader reader = new(request.Body);
    await File.WriteAllTextAsync(Path.Combine(saveTo, Path.GetFileName(name)), await reader.ReadToEndAsync());
    return Results.Ok();
});
app.Use(async (context, next) =>
{
    string path = context.Request.Path.Value is "/" or null ? "/index.html" : context.Request.Path.Value;
    string file = Path.GetFullPath(Path.Combine(root, path.TrimStart('/')));
    string brotli = file + ".br";
    if (file.StartsWith(root, StringComparison.Ordinal) && File.Exists(brotli)
        && context.Request.Headers.AcceptEncoding.ToString().Contains("br", StringComparison.Ordinal))
    {
        types.TryGetContentType(file, out string? type);
        context.Response.ContentType = type ?? "application/octet-stream";
        context.Response.Headers.ContentEncoding = "br";
        context.Response.Headers.CacheControl = "no-store";
        await context.Response.SendFileAsync(brotli);
        return;
    }
    await next();
});
Microsoft.Extensions.FileProviders.PhysicalFileProvider files = new(root);
app.UseDefaultFiles(new DefaultFilesOptions { FileProvider = files });
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = files,
    ServeUnknownFileTypes = true,
    OnPrepareResponse = ctx => ctx.Context.Response.Headers.CacheControl = "no-store",
});
app.Run();
