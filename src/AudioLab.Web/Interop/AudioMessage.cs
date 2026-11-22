namespace AudioLab.Web.Interop;

/// <summary>A message from the worklet or the worker: a number of chunks asked for, or text.</summary>
public readonly record struct AudioMessage(double Number, string? Text)
{
    /// <summary>Whether the message carries text.</summary>
    public bool IsText => Text is not null;
}
