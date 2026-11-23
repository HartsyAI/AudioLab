namespace AudioLab.Web.Components;

/// <summary>What a dialog tells the fields inside it, so a refused form marks its fields and points them at the reason.</summary>
/// <param name="ErrorId">The id of the element that says why the last confirm failed.</param>
/// <param name="HasError">Whether the dialog is showing a failure now.</param>
public sealed record DialogContext(string ErrorId, bool HasError);
