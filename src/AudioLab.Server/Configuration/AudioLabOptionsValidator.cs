using System.Text.RegularExpressions;
using AudioLab.Core.Model;
using AudioLab.Server.Mcp;
using AudioLab.Server.Media;
using AudioLab.Server.Render;
using AudioLab.Server.Store;
using Microsoft.Extensions.Options;

namespace AudioLab.Server.Configuration;

/// <summary>Refuses a configuration the server must not start with.</summary>
public sealed partial class AudioLabOptionsValidator : IValidateOptions<AudioLabOptions>
{
    /// <inheritdoc />
    public ValidateOptionsResult Validate(string? name, AudioLabOptions options)
    {
        List<string> problems = [];
        DoorOptions door = options.Door;
        if (!Enum.IsDefined(door.Kind))
        {
            problems.Add("AudioLab:Door:Kind must be Desktop, Swarm or Hosted.");
        }
        if (door.Kind != DoorKind.Desktop && (door.ProxySecret?.Length ?? 0) < DoorOptions.MinSecretLength)
        {
            problems.Add($"AudioLab:Door:ProxySecret must be at least {DoorOptions.MinSecretLength} characters for the {door.Kind} door.");
        }
        if (!IsHeaderName(door.SecretHeader) || !IsHeaderName(door.UserHeader))
        {
            problems.Add("AudioLab:Door:SecretHeader and UserHeader must be header names.");
        }
        else if (string.Equals(door.SecretHeader, door.UserHeader, StringComparison.OrdinalIgnoreCase))
        {
            problems.Add("AudioLab:Door:SecretHeader and UserHeader must differ.");
        }
        if (!PathBasePattern().IsMatch(options.PathBase))
        {
            problems.Add("AudioLab:PathBase must be empty or a path such as /audiolab, with no trailing slash, dot segments or query.");
        }
        if (options.MaxRequestBodyBytes is < 1024 or > 16 * 1024 * 1024)
        {
            problems.Add("AudioLab:MaxRequestBodyBytes must be 1024 to 16777216.");
        }
        if (options.HubMaxCommandChars is < 256 or > ProjectLimits.MaxCommandLength)
        {
            problems.Add($"AudioLab:HubMaxCommandChars must be 256 to {ProjectLimits.MaxCommandLength}.");
        }
        if (options.HubSendTimeout < TimeSpan.FromMilliseconds(100) || options.HubSendTimeout > TimeSpan.FromMinutes(5))
        {
            problems.Add("AudioLab:HubSendTimeout must be 100 milliseconds to 5 minutes.");
        }
        ValidateStorage(options.Storage, problems);
        McpOptionsValidator.Validate(options.Mcp, problems);
        ValidateMedia(options.Media, problems);
        ValidateRender(options.Render, problems);
        return problems.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(problems);
    }

    private static void ValidateStorage(StoreOptions storage, List<string> problems)
    {
        if (storage.Root is not null && !Path.IsPathFullyQualified(storage.Root))
        {
            problems.Add("AudioLab:Storage:Root must be an absolute path.");
        }
        if (storage.AutosaveDelay <= TimeSpan.Zero || storage.AutosaveMaxDelay < storage.AutosaveDelay)
        {
            problems.Add("AudioLab:Storage:AutosaveDelay must be above zero and no longer than AutosaveMaxDelay.");
        }
        if (storage.MaxFileBytes is < 1024 or > StoreOptions.MaxFileBytesLimit)
        {
            problems.Add($"AudioLab:Storage:MaxFileBytes must be 1024 to {StoreOptions.MaxFileBytesLimit}.");
        }
        if (storage.MaxProjectsPerUser < 1)
        {
            problems.Add("AudioLab:Storage:MaxProjectsPerUser must be at least 1.");
        }
    }

    private static void ValidateMedia(MediaOptions media, List<string> problems)
    {
        if (media.MaxUploadBytes is < 1024 or > WavReader.MaxFileBytes)
        {
            problems.Add($"AudioLab:Media:MaxUploadBytes must be 1024 to {WavReader.MaxFileBytes}, the most a WAV file can hold.");
        }
        if (media.PeaksRebuildWait <= TimeSpan.Zero || media.PeaksRebuildWait > TimeSpan.FromMinutes(1))
        {
            problems.Add("AudioLab:Media:PeaksRebuildWait must be above zero and at most one minute.");
        }
        if (media.MaxConcurrentUploadsPerUser < 1)
        {
            problems.Add("AudioLab:Media:MaxConcurrentUploadsPerUser must be at least 1.");
        }
        if (media.MaxFilesPerUser < 1)
        {
            problems.Add("AudioLab:Media:MaxFilesPerUser must be at least 1.");
        }
        if (media.MaxBytesPerUser < media.MaxUploadBytes)
        {
            problems.Add("AudioLab:Media:MaxBytesPerUser must be at least MaxUploadBytes.");
        }
    }

    private static void ValidateRender(RenderOptions render, List<string> problems)
    {
        if (render.MaxDecodedBytesPerFile < 1024 * 1024)
        {
            problems.Add("AudioLab:Render:MaxDecodedBytesPerFile must be at least 1 MiB.");
        }
        if (render.MaxDecodedBytesPerRender < render.MaxDecodedBytesPerFile)
        {
            problems.Add("AudioLab:Render:MaxDecodedBytesPerRender must be at least MaxDecodedBytesPerFile.");
        }
        if (render.MaxConcurrentExportsPerUser < 1 || render.MaxConcurrentExports < 1)
        {
            problems.Add("AudioLab:Render:MaxConcurrentExportsPerUser and MaxConcurrentExports must be at least 1.");
        }
        if (!double.IsFinite(render.MaxSeconds) || render.MaxSeconds <= 0 || render.MaxSeconds > ProjectLimits.MaxSeconds)
        {
            problems.Add($"AudioLab:Render:MaxSeconds must be above 0 and at most {ProjectLimits.MaxSeconds} seconds.");
        }
        if (render.MaxBytes is < 1024 * 1024 or > uint.MaxValue)
        {
            problems.Add("AudioLab:Render:MaxBytes must be from 1 MiB to 4 GiB less one byte, the most a WAV or ZIP file holds.");
        }
        if (render.MaxTime <= TimeSpan.Zero || render.MaxTime > TimeSpan.FromHours(24))
        {
            problems.Add("AudioLab:Render:MaxTime must be above zero and at most 24 hours.");
        }
        if (!double.IsFinite(render.MaxTailSeconds) || render.MaxTailSeconds < 0 || render.MaxTailSeconds > 600)
        {
            problems.Add("AudioLab:Render:MaxTailSeconds must be from 0 to 600 seconds.");
        }
    }

    private static bool IsHeaderName(string? name) => name is not null && HeaderNamePattern().IsMatch(name);

    [GeneratedRegex(@"\A(/[A-Za-z0-9_~-][A-Za-z0-9._~-]*)*\z", RegexOptions.CultureInvariant)]
    private static partial Regex PathBasePattern();

    [GeneratedRegex(@"\A[A-Za-z0-9-]{1,64}\z", RegexOptions.CultureInvariant)]
    private static partial Regex HeaderNamePattern();
}
