namespace AudioLab.Audio.Tests;

public sealed class DependencyTests
{
    [Fact]
    public void TheEngineReferencesOnlyCoreAndTheFramework()
    {
        string[] references = [.. typeof(AudioFormat).Assembly.GetReferencedAssemblies().Select(name => name.Name ?? "")];

        string[] others = [.. references.Where(IsOutsideTheAllowedSet)];

        Assert.Empty(others);
    }

    private static bool IsOutsideTheAllowedSet(string name) =>
        name != "AudioLab.Core" && name != "netstandard" && name != "mscorlib" && !name.StartsWith("System.", StringComparison.Ordinal);
}
