using System.Text.RegularExpressions;
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
        return problems.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(problems);
    }

    private static bool IsHeaderName(string? name) => name is not null && HeaderNamePattern().IsMatch(name);

    [GeneratedRegex(@"\A(/[A-Za-z0-9_~-][A-Za-z0-9._~-]*)*\z", RegexOptions.CultureInvariant)]
    private static partial Regex PathBasePattern();

    [GeneratedRegex(@"\A[A-Za-z0-9-]{1,64}\z", RegexOptions.CultureInvariant)]
    private static partial Regex HeaderNamePattern();
}
