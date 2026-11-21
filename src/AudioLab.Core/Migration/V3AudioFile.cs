namespace AudioLab.Core.Migration;

/// <summary>One audio file of a version 3 project, as the migration will describe it to the store.</summary>
/// <param name="ContentType">The bare media type.</param>
/// <param name="Length">The size in bytes; at least 1.</param>
/// <param name="InlineBase64">The audio as base64 text when the project carried it inline; otherwise null.</param>
internal sealed record V3AudioFile(string ContentType, long Length, string? InlineBase64);
