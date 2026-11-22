using System.Runtime.InteropServices.JavaScript;

namespace AudioLab.Web.Interop;

/// <summary>Bindings to the browser's audio, worker and messaging built-ins. Every name resolves on globalThis, so no script file ships with them.</summary>
internal static partial class AudioImports
{
    [JSImport("globalThis.Reflect.construct")]
    internal static partial JSObject Construct(JSObject constructor, [JSMarshalAs<JSType.Array<JSType.Any>>] object?[] arguments);

    [JSImport("globalThis.Reflect.set")]
    internal static partial bool SetHandler(JSObject target, string key, [JSMarshalAs<JSType.Function<JSType.Object>>] Action<JSObject> handler);

    [JSImport("globalThis.Array.of")]
    internal static partial JSObject ArrayOf(int first);

    [JSImport("globalThis.JSON.stringify")]
    internal static partial string Stringify(JSObject value);

    [JSImport("globalThis.Object.is")]
    internal static partial bool SameObject(JSObject first, JSObject second);

    [JSImport("globalThis.document.hasFocus")]
    internal static partial bool DocumentHasFocus();

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

    [JSImport("globalThis.MessagePort.prototype.postMessage.call")]
    internal static partial void PostTextToPort(JSObject port, string message);

    [JSImport("globalThis.Worker.prototype.postMessage.call")]
    internal static partial void PostToWorker(JSObject worker, JSObject message, [JSMarshalAs<JSType.Array<JSType.Object>>] JSObject[] transfer);

    [JSImport("globalThis.Worker.prototype.postMessage.call")]
    internal static partial void PostTextToWorker(JSObject worker, string message);

    [JSImport("globalThis.Uint8Array.prototype.slice.call")]
    internal static partial JSObject CopyBytes([JSMarshalAs<JSType.MemoryView>] Span<byte> view);
}
