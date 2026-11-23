namespace AudioLab.Web.Services;

/// <summary>What an import of a .mid file came to: whether it was done, and what the user is told about it.</summary>
/// <param name="Edit">Applied, with the new clip's id in <see cref="EditResult.Created"/> once the page holds it, or refused with the reason.</param>
/// <param name="Notice">What the server left out or did not take over from the file, in sentences, or null when there is nothing to say.</param>
public readonly record struct MidiImportOutcome(EditResult Edit, string? Notice);
