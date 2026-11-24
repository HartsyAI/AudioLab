using System.Text;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Routing.Template;
using Microsoft.Extensions.FileProviders;

namespace AudioLab.Server.Routes;

/// <summary>Serves the Blazor WebAssembly UI: its static files, and its start page for any address a client-side route may own.</summary>
public static class UiRoutes
{
    private const string BaseTag = "<base href=\"/\" />";

    private const string IndexFile = "index.html";

    // Another origin may not frame the program, so a page of it cannot be laid under a click (D30).
    // Showing it inside another site is out of scope for now (section 9).
    private const string FrameAncestors = "frame-ancestors 'self'";

    /// <summary>Maps the UI's static files and a fallback to its start page into <paramref name="app"/>, which must use the path base.</summary>
    /// <remarks>
    /// These endpoints carry no <c>DoorRequired</c>: they hold the program, never a project. The path base is written into the start
    /// page, because Blazor resolves every address against its base tag. Each answer forbids framing by another origin and sniffing.
    /// </remarks>
    public static void Map(WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);
        app.Use(async (context, next) =>
        {
            if (context.GetEndpoint()?.Metadata.GetMetadata<UiEndpoint>() is not null)
            {
                SetSecurityHeaders(context.Response.Headers);
            }
            await next();
        });
        app.Use(async (context, next) =>
        {
            string[] allowed = IsGetOrHead(context.Request) && context.GetEndpoint() is null && IsApi(context.Request.Path)
                ? RoutesThatRefuseTheMethod(app, context.Request.Path)
                : [];
            if (allowed.Length == 0)
            {
                await next();
                return;
            }
            context.Response.StatusCode = StatusCodes.Status405MethodNotAllowed;
            context.Response.Headers.Allow = string.Join(", ", allowed);
        });
        RouteGroupBuilder ui = app.MapGroup(string.Empty).WithMetadata(new UiEndpoint());
        ui.MapStaticAssets();
        IFileProvider files = app.Environment.WebRootFileProvider;
        Lazy<string> template = new(() => ReadIndex(files));
        // The start page is a middleware and not an endpoint, so it answers only what no endpoint claims: an address under /api or
        // /health that a route refuses for its method stays that refusal (a 405), and an unknown one stays a 404.
        app.Use(async (context, next) =>
        {
            if (!IsStartPageRequest(context))
            {
                await next();
                return;
            }
            string prefix = HtmlEncoder.Default.Encode(context.Request.PathBase.Value ?? string.Empty);
            string page = template.Value.Replace(BaseTag, $"<base href=\"{prefix}/\" />", StringComparison.Ordinal);
            context.Response.ContentType = "text/html; charset=utf-8";
            context.Response.Headers.CacheControl = "no-cache";
            SetSecurityHeaders(context.Response.Headers);
            await context.Response.WriteAsync(page);
        });
    }

    // The static files' wildcard is a GET candidate for every address, so the router answers an unknown GET with a 404 even where a route of
    // another method owns the address. A GET under /api or /health that no route answers is given the 405 and Allow that the router would give.
    private static string[] RoutesThatRefuseTheMethod(WebApplication app, PathString path)
    {
        IEnumerable<RouteEndpoint> routes = ((IEndpointRouteBuilder)app).DataSources.SelectMany(source => source.Endpoints).OfType<RouteEndpoint>();
        List<string> methods = [];
        foreach (RouteEndpoint route in routes)
        {
            TemplateMatcher matcher = new(new RouteTemplate(route.RoutePattern), []);
            bool matches = matcher.TryMatch(path, []) || matcher.TryMatch(path + "/", []);
            // A route that takes GET (the static files' wildcard does) is not one that refuses it.
            if (matches && route.Metadata.GetMetadata<HttpMethodMetadata>() is { HttpMethods: { } own } && !own.Contains("GET"))
            {
                methods.AddRange(own);
            }
        }
        return [.. methods.Distinct().Order()];
    }

    private static bool IsGetOrHead(HttpRequest request) => HttpMethods.IsGet(request.Method) || HttpMethods.IsHead(request.Method);

    private static bool IsApi(PathString path) => path.StartsWithSegments("/api") || path.StartsWithSegments("/health");

    private static void SetSecurityHeaders(IHeaderDictionary headers)
    {
        headers.ContentSecurityPolicy = FrameAncestors;
        headers.XFrameOptions = "SAMEORIGIN";
        headers.XContentTypeOptions = "nosniff";
    }

    // A GET or HEAD that no endpoint answers, for an address that is not a file and not the API's or the health route's.
    private static bool IsStartPageRequest(HttpContext context)
    {
        HttpRequest request = context.Request;
        if (!IsGetOrHead(request))
        {
            return false;
        }
        PathString path = request.Path;
        bool isApi = IsApi(path);
        bool isFile = path.HasValue && path.Value!.AsSpan(path.Value.LastIndexOf('/') + 1).Contains('.');
        return context.GetEndpoint() is null && !isApi && !isFile;
    }

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
