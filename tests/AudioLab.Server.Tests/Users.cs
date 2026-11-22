using AudioLab.Core.Model;

namespace AudioLab.Server.Tests;

/// <summary>Two users of one door, for tests that need someone else.</summary>
public static class Users
{
    public static UserId Alice { get; } = UserId.ForDoor("swarm", "alice");

    public static UserId Bob { get; } = UserId.ForDoor("swarm", "bob");
}
