using System.Net;

namespace AudioLab.Server.Doors;

/// <summary>What a door may look at: the headers, the address of the peer that opened the connection and the scheme the server was reached by.</summary>
/// <param name="Headers">The request headers.</param>
/// <param name="RemoteAddress">The address of the connected peer, or null when it is not known.</param>
/// <param name="Scheme">http or https, as the request arrived.</param>
public readonly record struct DoorRequest(IHeaderDictionary Headers, IPAddress? RemoteAddress, string Scheme = "http");
