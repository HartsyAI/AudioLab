using System.Reflection;

// The HartsyInference id itself is a meta-package with no assembly (see results/packages.md), so it is checked by restore only.
// Loads every HartsyInference assembly that restored and counts its public types. It loads no model and downloads nothing.
string[] ids =
[
    "HartsyInference.Core", "HartsyInference.Cuda", "HartsyInference.Vulkan", "HartsyInference.ModelAssets",
    "HartsyInference.Diffusion", "HartsyInference.Audio", "HartsyInference.Vision", "HartsyInference.Video",
    "HartsyInference.World", "HartsyInference.ThreeD", "HartsyInference.LLM", "HartsyInference.Engine", "HartsyInference.Voice",
];
int failures = 0;
foreach (string id in ids)
{
    try
    {
        Assembly assembly = Assembly.Load(id);
        Type[] types = assembly.GetExportedTypes();
        long resources = 0;
        foreach (string name in assembly.GetManifestResourceNames())
        {
            using Stream? s = assembly.GetManifestResourceStream(name);
            resources += s?.Length ?? 0;
        }
        Console.WriteLine($"RESOURCES|{id}|{assembly.GetManifestResourceNames().Length} embedded resources|{resources / 1048576.0:F1} MiB");
        Console.WriteLine($"LOAD|{id}|{assembly.GetName().Version}|{types.Length} public types|"
            + $"{assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion}");
    }
    catch (Exception e) when (e is FileNotFoundException or FileLoadException or BadImageFormatException or ReflectionTypeLoadException)
    {
        failures++;
        Console.WriteLine($"FAIL|{id}|{e.GetType().Name}: {e.Message}");
    }
}
Console.WriteLine($"{(failures == 0 ? "PASS" : "FAIL")}|{ids.Length - failures} of {ids.Length} assemblies loaded on {System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription}");
return failures;
