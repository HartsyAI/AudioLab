using System.Net;

namespace AudioLab.Server.Doors;

/// <summary>What a door may look at: the headers and the address of the peer that opened the connection.</summary>
/// <param name="Headers">The request headers.</param>
/// <param name="RemoteAddress">The address of the connected peer, or null when it is not known.</param>
public readonly record struct DoorRequest(IHeaderDictionary Headers, IPAddress? RemoteAddress);
