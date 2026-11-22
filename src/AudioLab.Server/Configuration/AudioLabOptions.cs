namespace AudioLab.Server.Configuration;

/// <summary>Everything the server reads from configuration, under the AudioLab section.</summary>
public sealed record AudioLabOptions
{
    /// <summary>The name of the configuration section.</summary>
    public const string Section = "AudioLab";

    /// <summary>The door this instance serves.</summary>
    public DoorOptions Door { get; init; } = new();

    /// <summary>The path the site is served under, such as /audiolab, or empty for the root.</summary>
    public string PathBase { get; init; } = string.Empty;
}
