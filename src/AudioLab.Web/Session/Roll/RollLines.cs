namespace AudioLab.Web.Session.Roll;

/// <summary>The three kinds of vertical line under the notes, each as one path.</summary>
/// <param name="Steps">The grid steps that are not beats or bars, empty when they would be too close to read.</param>
/// <param name="Beats">The beats that do not start a bar.</param>
/// <param name="Bars">The bar lines.</param>
public readonly record struct RollLines(string Steps, string Beats, string Bars);
