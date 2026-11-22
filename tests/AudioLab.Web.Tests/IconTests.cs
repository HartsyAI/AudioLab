using AudioLab.Web.Components;

namespace AudioLab.Web.Tests;

/// <summary>Holds the icon tests.</summary>
public sealed class IconTests
{
    /// <summary>Every icon of the set has an outline.</summary>
    [Fact]
    public void EveryIconOfTheSetHasAnOutline()
    {
        foreach (IconName name in Enum.GetValues<IconName>())
        {
            Assert.False(string.IsNullOrWhiteSpace(IconPaths.Of(name)), name.ToString());
        }
    }
}
