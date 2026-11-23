namespace AudioLab.Web.Interop;

/// <summary>The browser could not start the audio engine: no audio context, a worker that would not boot, or an engine that failed.</summary>
public sealed class AudioHostException(string reason) : Exception(reason);
