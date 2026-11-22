namespace AudioLab.Server.Doors;

/// <summary>One of the three doors: it decides which user, if any, a request belongs to.</summary>
public interface IDoor
{
    /// <summary>The door's name, which is also the first part of every user id it mints.</summary>
    public string Name { get; }

    /// <summary>Resolves the user of a request, or says why there is none.</summary>
    public DoorResult Identify(DoorRequest request);
}
