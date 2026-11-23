using System.Runtime.InteropServices.JavaScript;
using System.Runtime.Versioning;

namespace AudioLab.Web.Interop.Worker;

/// <summary>What the worker script calls into the engine host.</summary>
[SupportedOSPlatform("browser")]
public static partial class WorkerExports
{
    /// <summary>Says the runtime is up.</summary>
    [JSExport]
    public static string Ping() => "pong";
}
