using AudioLab.Core.Model;

namespace AudioLab.Core.Rules;

/// <summary>A numeric setting of an effect: its name on the wire, its range, and how to read and change it.</summary>
/// <param name="Name">The camel-case name, as in the project file.</param>
/// <param name="Min">The lowest value the project keeps.</param>
/// <param name="Max">The highest value the project keeps.</param>
/// <param name="Get">Reads the setting from an effect of the right kind.</param>
/// <param name="Set">Returns a copy of an effect of the right kind with the setting changed.</param>
public sealed record EffectParameter(string Name, double Min, double Max, Func<Effect, double> Get, Func<Effect, double, Effect> Set);
