namespace AudioLab.Core.Api;

/// <summary>Who the server takes the caller to be, as the door resolved it.</summary>
/// <param name="Door">The door the request came through.</param>
/// <param name="User">The user id the door resolved.</param>
public sealed record SessionInfo(string Door, string User);
