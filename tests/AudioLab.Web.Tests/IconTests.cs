using AudioLab.Web.Components;

namespace AudioLab.Web.Tests;

public sealed class IconTests
{
    [Fact]
    public void EveryIconOfTheSetHasAnOutline()
    {
        foreach (IconName name in Enum.GetValues<IconName>())
        {
            Assert.False(string.IsNullOrWhiteSpace(IconPaths.Of(name)), name.ToString());
        }
    }
}
