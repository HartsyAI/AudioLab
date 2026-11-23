namespace AudioLab.Web.Session.Roll;

/// <summary>The steps of velocity the roll draws as brightness, and which step a velocity falls in.</summary>
public static class VelocityLevels
{
    /// <summary>How many steps of velocity are drawn as brightness.</summary>
    public const int Count = 8;

    /// <summary>The step a velocity falls in, 0 for the softest to <see cref="Count"/> less one.</summary>
    public static int Of(int velocity) => Math.Clamp((velocity - 1) * Count / 127, 0, Count - 1);
}
