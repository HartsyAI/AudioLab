using System.Text;
using System.Text.Json;
using AudioLab.Core.Api.Decoder;

namespace AudioLab.Web.Services;

/// <summary>The typed client of the decoder routes (D101): the status, and the install the user confirmed.</summary>
/// <remarks>A refusal is an <see cref="ApiException"/>.</remarks>
/// <param name="http">The client with the site's base address and the ordinary timeout; the install runs on the server in the background.</param>
public sealed class DecoderApi(HttpClient http)
{
    private const string Decoder = "api/decoder";

    /// <summary>What the server can install for this platform, and what is installed now.</summary>
    public async Task<DecoderStatus> StatusAsync(CancellationToken cancellationToken = default)
    {
        using HttpResponseMessage response = await http.GetAsync(Decoder, cancellationToken);
        await ApiAnswers.EnsureAsync(response, cancellationToken);
        return await ApiAnswers.ReadAsync(response, DecoderJsonContext.Default.DecoderStatus, cancellationToken);
    }

    /// <summary>Starts the install of the build the user confirmed; the answer is the status at its start.</summary>
    /// <remarks>
    /// The server refuses (<c>422</c>) any version or checksum other than the one it pins, so a stale page cannot install another build.
    /// </remarks>
    public async Task<DecoderStatus> InstallAsync(DecoderPackage package, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(package);
        DecoderInstallRequest request = new(package.Version, package.Sha256);
        string body = JsonSerializer.Serialize(request, DecoderJsonContext.Default.DecoderInstallRequest);
        using HttpRequestMessage message = new(HttpMethod.Post, $"{Decoder}/install")
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json"),
        };
        using HttpResponseMessage response = await http.SendAsync(message, cancellationToken);
        await ApiAnswers.EnsureAsync(response, cancellationToken);
        return await ApiAnswers.ReadAsync(response, DecoderJsonContext.Default.DecoderStatus, cancellationToken);
    }
}
