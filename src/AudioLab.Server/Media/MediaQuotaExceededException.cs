namespace AudioLab.Server.Media;

/// <summary>The user has used up what the quota allows; the message says which limit and is safe to show the user.</summary>
public sealed class MediaQuotaExceededException(string message) : Exception(message);
