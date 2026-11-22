using System.Text.RegularExpressions;

namespace AudioLab.Core.Model;

/// <summary>The rules a stored media record keeps.</summary>
public static partial class MediaValidator
{
    /// <summary>The largest file the store keeps, in bytes: one tebibyte.</summary>
    public const long MaxLength = 1L << 40;

    /// <summary>Every rule the record breaks, each with the name of the value; empty when it is valid.</summary>
    public static IReadOnlyList<string> Validate(MediaRecord media)
    {
        ArgumentNullException.ThrowIfNull(media);
        ProblemList problems = new();
        problems.Id(media.Id.Value, "id");
        problems.Id(media.Owner.Value, "owner");
        if (!IsContentType(media.ContentType))
        {
            problems.Add("contentType: a media type such as audio/wav, in lower case.");
        }
        if (media.Length is < 1 or > MaxLength)
        {
            problems.Add($"length: a file is 1 to {MaxLength} bytes.");
        }
        return problems.ToList();
    }

    /// <summary>Whether <paramref name="contentType"/> is a lower-case media type such as audio/wav, no parameters.</summary>
    public static bool IsContentType(string? contentType)
    {
        return contentType is not null && ContentTypePattern().IsMatch(contentType);
    }

    [GeneratedRegex(
        @"\A[a-z0-9][a-z0-9!#$&^_.+-]{0,126}/[a-z0-9][a-z0-9!#$&^_.+-]{0,126}\z",
        RegexOptions.CultureInvariant)]
    private static partial Regex ContentTypePattern();
}
