namespace AudioLab.Server.Services;

/// <summary>What the health route answers: enough for a parent process or a proxy to know the server is up.</summary>
/// <param name="Status">Always ok while the server answers.</param>
/// <param name="ApiVersion">The contract version, <see cref="AudioLab.Core.AudioLabApi.Version"/>.</param>
/// <param name="Door">The door this instance serves.</param>
public sealed record HealthReport(string Status, int ApiVersion, string Door);
