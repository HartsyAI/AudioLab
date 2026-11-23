using AudioLab.Audio;

namespace AudioLab.Server.Render;

/// <summary>The media of one render: the source that reads one user's files, and the cache that holds them at the render's rate.</summary>
/// <param name="Source">Supplies the user's files; it knows nobody else's.</param>
/// <param name="Cache">Keeps what the source supplied for this render only, at the render's sample rate.</param>
public readonly record struct RenderMedia(IMediaSource Source, MediaCache Cache);
