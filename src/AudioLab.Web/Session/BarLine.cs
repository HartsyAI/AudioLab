namespace AudioLab.Web.Session;

/// <summary>The start of one bar on the ruler.</summary>
/// <param name="Number">The bar's number, counted from 1.</param>
/// <param name="Time">Where it starts, in seconds.</param>
/// <param name="Strong">Whether it is the first of a group of four, which the ruler and the grid draw stronger.</param>
public readonly record struct BarLine(int Number, double Time, bool Strong);
