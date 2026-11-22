namespace AudioLab.Core.Model;

/// <summary>Where a track records from.</summary>
/// <param name="DeviceId">The input device, or null for the system default.</param>
/// <param name="Channels">Which channels of the device are recorded.</param>
/// <param name="Channel">The 1-based input when <paramref name="Channels"/> is single; otherwise null.</param>
public readonly record struct TrackInput(string? DeviceId, InputChannelMode Channels, int? Channel = null);
