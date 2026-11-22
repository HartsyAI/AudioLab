using System.Text;
using System.Text.Encodings.Web;
using Microsoft.Extensions.FileProviders;

namespace AudioLab.Server.Routes;

/// <summary>Serves the Blazor WebAssembly UI: its static files, and its start page for any address a client-side route may own.</summary>
public static class UiRoutes
{
    private const string BaseTag = "<base href=\"/\" />";

    private const string IndexFile = "index.html";

    /// <summary>Maps the UI's static files and a fallback to its start page into <paramref name="app"/>, which must use the path base.</summary>
    /// <remarks>
    /// These endpoints carry no <c>DoorRequired</c>: they hold the program, never a project. The path base is written into the start
    /// page, because Blazor resolves every address against its base tag.
    /// </remarks>
    public static void Map(WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);
        RouteGroupBuilder ui = app.MapGroup(string.Empty).WithMetadata(new UiEndpoint());
        ui.MapStaticAssets();
        IFileProvider files = app.Environment.WebRootFileProvider;
        Lazy<string> template = new(() => ReadIndex(files));
        // Only GET: an address under /api that a route refuses for its method or media type must stay that refusal, not become a 404.
        ui.MapGet("{*path:nonfile}", (HttpContext http) =>
        {
            if (IsApi(http.Request.Path))
            {
                return Results.NotFound();
            }
            string prefix = HtmlEncoder.Default.Encode(http.Request.PathBase.Value ?? string.Empty);
            string page = template.Value.Replace(BaseTag, $"<base href=\"{prefix}/\" />", StringComparison.Ordinal);
            http.Response.Headers.CacheControl = "no-cache";
            return Results.Text(page, "text/html", Encoding.UTF8);
        });
    }

    private static bool IsApi(PathString path) => path.StartsWithSegments("/api") || path.StartsWithSegments("/health");

    private static string ReadIndex(IFileProvider files)
    {
        IFileInfo index = files.GetFileInfo(IndexFile);
        if (!index.Exists)
        {
            throw new FileNotFoundException("The UI is not part of this build: wwwroot has no index.html.");
        }
        using StreamReader reader = new(index.CreateReadStream(), Encoding.UTF8);
        string page = reader.ReadToEnd();
        return page.Contains(BaseTag, StringComparison.Ordinal)
            ? page
            : throw new InvalidOperationException($"index.html must contain {BaseTag}, which the server rewrites for the path base.");
    }
}
