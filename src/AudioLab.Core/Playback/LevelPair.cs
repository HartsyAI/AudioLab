namespace AudioLab.Core.Playback;

/// <summary>A left and a right peak level, linear, where 1 is full scale.</summary>
/// <param name="Left">The left peak.</param>
/// <param name="Right">The right peak.</param>
public readonly record struct LevelPair(double Left, double Right);
