namespace AudioLab.Web.Components;

/// <summary>One toast that is showing.</summary>
/// <param name="Id">Names the toast to dismiss or undo.</param>
/// <param name="Kind">Which variant it is.</param>
/// <param name="Message">What happened.</param>
/// <param name="Undo">What its Undo button does; null when it has none.</param>
public sealed record ToastItem(string Id, ToastKind Kind, string Message, Func<Task>? Undo);
