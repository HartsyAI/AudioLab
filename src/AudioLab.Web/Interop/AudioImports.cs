using System.Runtime.InteropServices.JavaScript;

namespace AudioLab.Web.Interop;

/// <summary>Bindings to the audio, worker and messaging built-ins of the browser; every name is on globalThis, no script ships.</summary>
internal static partial class AudioImports
{
    [JSImport("globalThis.Reflect.construct")]
    internal static partial JSObject Construct(JSObject constructor, [JSMarshalAs<JSType.Array<JSType.Any>>] object?[] arguments);

    [JSImport("globalThis.Reflect.set")]
    internal static partial bool SetHandler(JSObject target, string key, [JSMarshalAs<JSType.Function<JSType.Object>>] Action<JSObject> handler);

    [JSImport("globalThis.Array.of")]
    internal static partial JSObject ArrayOf(int first);

    [JSImport("globalThis.Array.prototype.push.call")]
    internal static partial int Push(JSObject array, [JSMarshalAs<JSType.Any>] object? value);

    [JSImport("globalThis.AudioWorklet.prototype.addModule.call")]
    [return: JSMarshalAs<JSType.Promise<JSType.Void>>]
    internal static partial Task AddModule(JSObject worklet, string url);

    [JSImport("globalThis.AudioContext.prototype.resume.call")]
    [return: JSMarshalAs<JSType.Promise<JSType.Void>>]
    internal static partial Task Resume(JSObject context);

    [JSImport("globalThis.AudioNode.prototype.connect.call")]
    internal static partial JSObject Connect(JSObject node, JSObject destination);

    [JSImport("globalThis.MessagePort.prototype.postMessage.call")]
    internal static partial void PostToPort(JSObject port, JSObject message, [JSMarshalAs<JSType.Array<JSType.Object>>] JSObject[] transfer);

    [JSImport("globalThis.Worker.prototype.postMessage.call")]
    internal static partial void PostToWorker(JSObject worker, JSObject message, [JSMarshalAs<JSType.Array<JSType.Object>>] JSObject[] transfer);

    [JSImport("globalThis.Worker.prototype.terminate.call")]
    internal static partial void Terminate(JSObject worker);

    [JSImport("globalThis.AudioContext.prototype.close.call")]
    [return: JSMarshalAs<JSType.Promise<JSType.Void>>]
    internal static partial Task Close(JSObject context);
}
