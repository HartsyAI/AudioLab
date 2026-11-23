using System.Text.RegularExpressions;

namespace AudioLab.Core.Model;

/// <summary>The rules a stored media record keeps.</summary>
public static partial class MediaValidator
{
    /// <summary>The largest file the store keeps, in bytes: one tebibyte.</summary>
    public const long MaxLength = 1L << 40;

    /// <summary>The lowest sample rate of a media file, in frames a second.</summary>
    public const int MinSampleRate = 8000;

    /// <summary>The highest sample rate of a media file, in frames a second.</summary>
    public const int MaxSampleRate = 384_000;

    /// <summary>The most channels a media file has.</summary>
    public const int MaxChannels = 8;

    /// <summary>The length of a SHA-256 in hexadecimal digits.</summary>
    public const int HashLength = 64;

    /// <summary>Every rule the record breaks, each with the name of the value; empty when it is valid.</summary>
    public static IReadOnlyList<string> Validate(MediaRecord media)
    {
        ArgumentNullException.ThrowIfNull(media);
        ProblemList problems = new();
        problems.Id(media.Id.Value, "id");
        problems.Id(media.Owner.Value, "owner");
        problems.Text(media.Name, ProjectLimits.MaxName, "name");
        RequireDefined(media.Kind, "kind", problems);
        RequireDefined(media.Format, "format", problems);
        RequireDefined(media.Encoding, "encoding", problems);
        problems.Positive(media.Duration, ProjectLimits.MaxSeconds, "duration");
        problems.Range(media.SampleRate, MinSampleRate, MaxSampleRate, "sampleRate");
        problems.Range(media.Channels, 1, MaxChannels, "channels");
        if (media.Bytes is < 1 or > MaxLength)
        {
            problems.Add($"bytes: a file is 1 to {MaxLength} bytes.");
        }
        if (!IsHash(media.Sha256))
        {
            problems.Add("sha256: 64 lower-case hexadecimal digits.");
        }
        if (media.Created.Offset != TimeSpan.Zero)
        {
            problems.Add("created: a time in UTC.");
        }
        return problems.ToList();
    }

    /// <summary>Every rule a file's name breaks; empty when it is a valid label.</summary>
    public static IReadOnlyList<string> ValidateName(string? name)
    {
        ProblemList problems = new();
        problems.Text(name, ProjectLimits.MaxName, "name");
        return problems.ToList();
    }

    /// <summary>Whether <paramref name="text"/> is a SHA-256 as 64 lower-case hexadecimal digits.</summary>
    public static bool IsHash(string? text) => text is not null && HashPattern().IsMatch(text);

    private static void RequireDefined<TEnum>(TEnum value, string path, ProblemList problems)
        where TEnum : struct, Enum
    {
        if (!Enum.IsDefined(value))
        {
            problems.Add($"{path}: not a known value.");
        }
    }

    [GeneratedRegex(@"\A[0-9a-f]{64}\z", RegexOptions.CultureInvariant)]
    private static partial Regex HashPattern();
}
