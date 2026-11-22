using AudioLab.Web.Services;

namespace AudioLab.Web.Tests;

/// <summary>Hands out one fake hub, and counts how many connections were asked for.</summary>
public sealed class FakeHubFactory(FakeProjectHub hub) : IProjectHubFactory
{
    public int Created { get; private set; }

    public IProjectHub Create()
    {
        Created++;
        return hub;
    }
}
