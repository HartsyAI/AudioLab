namespace AudioLab.Core.Model;

/// <summary>A send from a track to an aux bus.</summary>
/// <param name="Bus">The bus track that receives it.</param>
/// <param name="Level">The send gain, from 0 up; 1 is unity.</param>
/// <param name="Pre">Whether the send is taken before the track fader.</param>
/// <param name="Muted">Whether the send is silent.</param>
public readonly record struct Send(TrackId Bus, double Level, bool Pre, bool Muted);
