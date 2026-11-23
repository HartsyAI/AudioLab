namespace AudioLab.Web.Components;

/// <summary>One cell of the counter: a micro caption over its value.</summary>
/// <param name="Caption">The caption, in capitals: TIME, BARS, TEMPO, METER.</param>
/// <param name="Value">The value, written as the interface writes numbers: fixed width, tabular figures.</param>
/// <param name="Large">True for the time and bars cells, which are written at readout size.</param>
public readonly record struct CounterCell(string Caption, string Value, bool Large = false);
