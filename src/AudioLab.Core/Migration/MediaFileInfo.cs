namespace AudioLab.Core.Migration;

/// <summary>What is known about one audio file a version 3 project refers to.</summary>
/// <param name="ContentType">The media type, such as audio/wav; parameters after a semicolon are dropped.</param>
/// <param name="Length">The size of the file in bytes.</param>
public readonly record struct MediaFileInfo(string ContentType, long Length);
