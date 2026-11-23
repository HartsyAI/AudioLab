namespace AudioLab.Audio;

/// <summary>A send of a track into an aux bus, with its gain already worked out from mute and solo.</summary>
/// <param name="BusSlot">The bus's place among the buses of the plan.</param>
/// <param name="Gain">The send gain; never zero, as a silent send is left out.</param>
/// <param name="Pre">Whether the send is taken before the fader.</param>
internal readonly record struct PlannedSend(int BusSlot, float Gain, bool Pre);
