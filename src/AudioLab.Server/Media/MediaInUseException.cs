namespace AudioLab.Server.Media;

/// <summary>A media file cannot be deleted because a project of the user still plays it.</summary>
public sealed class MediaInUseException(string projectName)
    : Exception($"The file is still used by your project \"{projectName}\"; remove it from your projects before deleting it.");
