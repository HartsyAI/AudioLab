using System.Net;

namespace AudioLab.Server.Configuration;

/// <summary>Keeps the desktop and Swarm doors on the loopback interface, as section 9 of DESIGN.md requires.</summary>
public static class ListenGuard
{
    /// <summary>Every way the configuration makes a loopback-only door listen beyond the loopback interface.</summary>
    public static IReadOnlyList<string> Problems(DoorKind kind, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        if (kind == DoorKind.Hosted)
        {
            return [];
        }
        List<string> problems = [];
        List<string> urls = [.. Split(configuration["urls"])];
        urls.AddRange(configuration.GetSection("Kestrel:Endpoints").GetChildren().Select(endpoint => endpoint["Url"] ?? string.Empty));
        foreach (string url in urls.Where(url => !IsLoopbackUrl(url)))
        {
            problems.Add($"The {kind} door listens on loopback only, but the address \"{url}\" is not loopback.");
        }
        foreach (string key in new[] { "http_ports", "https_ports" })
        {
            if (!string.IsNullOrWhiteSpace(configuration[key]))
            {
                problems.Add($"The {kind} door listens on loopback only, but \"{key}\" binds every interface; use \"urls\" with a loopback address.");
            }
        }
        return problems;
    }

    /// <summary>Whether <paramref name="url"/> names the loopback interface: localhost, 127.x.x.x or [::1].</summary>
    public static bool IsLoopbackUrl(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out Uri? uri) || uri.Scheme is not ("http" or "https"))
        {
            return false;
        }
        string host = uri.Host.Trim('[', ']');
        return string.Equals(host, "localhost", StringComparison.OrdinalIgnoreCase)
            || (IPAddress.TryParse(host, out IPAddress? address) && IPAddress.IsLoopback(address));
    }

    private static IEnumerable<string> Split(string? urls)
    {
        return (urls ?? string.Empty).Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }
}
