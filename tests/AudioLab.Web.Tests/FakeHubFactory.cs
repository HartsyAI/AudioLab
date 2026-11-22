using AudioLab.Web.Services;

namespace AudioLab.Web.Tests;

/// <summary>Hands out one fake hub, and counts how many connections were asked for.</summary>
internal sealed class FakeHubFactory(FakeProjectHub hub) : IProjectHubFactory
{
    public int Created { get; private set; }

    public bool? Reconnects { get; private set; }

    public IProjectHub Create(bool reconnects)
    {
        Created++;
        Reconnects = reconnects;
        return hub;
    }
}
