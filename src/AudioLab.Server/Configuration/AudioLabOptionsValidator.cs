using System.Text.RegularExpressions;
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
        ValidateStorage(options.Storage, problems);
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
        if (storage.MaxFileBytes < 1024)
        {
            problems.Add("AudioLab:Storage:MaxFileBytes must be at least 1024.");
        }
    }

    private static bool IsHeaderName(string? name) => name is not null && HeaderNamePattern().IsMatch(name);

    [GeneratedRegex(@"\A(/[A-Za-z0-9_~-][A-Za-z0-9._~-]*)*\z", RegexOptions.CultureInvariant)]
    private static partial Regex PathBasePattern();

    [GeneratedRegex(@"\A[A-Za-z0-9-]{1,64}\z", RegexOptions.CultureInvariant)]
    private static partial Regex HeaderNamePattern();
}
