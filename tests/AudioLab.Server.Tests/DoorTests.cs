using System.Net;
using AudioLab.Core.Model;
using AudioLab.Server.Configuration;
using AudioLab.Server.Doors;
using Microsoft.AspNetCore.Http;

namespace AudioLab.Server.Tests;

public sealed class DoorTests
{
    private const string Secret = "0123456789abcdef0123456789abcdef";

    private static readonly DoorOptions _swarm = new() { Kind = DoorKind.Swarm, ProxySecret = Secret };

    [Fact]
    public void DesktopDoorGivesTheOneLocalUserToLoopbackPeersOnly()
    {
        DesktopDoor door = new();
        foreach (string address in new[] { "127.0.0.1", "::1", "127.9.9.9", "::ffff:127.0.0.1" })
        {
            Assert.Equal(UserId.Local, door.Identify(Request(IPAddress.Parse(address), ("Host", "127.0.0.1:5280"))).User);
        }
        foreach (string address in new[] { "10.0.0.5", "192.168.1.2", "2001:db8::1" })
        {
            DoorResult denied = door.Identify(Request(IPAddress.Parse(address), ("Host", "127.0.0.1:5280")));
            Assert.Null(denied.User);
            Assert.Equal(StatusCodes.Status403Forbidden, denied.StatusCode);
        }
        Assert.Null(door.Identify(Request(null, ("Host", "127.0.0.1:5280"))).User);
    }

    [Fact]
    public void DesktopDoorIgnoresAnIdentityHeader()
    {
        DoorResult result = new DesktopDoor().Identify(Request(IPAddress.Loopback, ("Host", "localhost:5280"), ("X-AudioLab-User", "mallory")));
        Assert.Equal(UserId.Local, result.User);
    }

    [Theory]
    [InlineData("127.0.0.1:5280")]
    [InlineData("localhost:5280")]
    [InlineData("LOCALHOST:5280")]
    [InlineData("[::1]:5280")]
    [InlineData("127.0.0.1")]
    public void DesktopDoorAnswersALoopbackHost(string host)
    {
        Assert.Equal(UserId.Local, new DesktopDoor().Identify(Request(IPAddress.Loopback, ("Host", host))).User);
    }

    [Theory]
    [InlineData("evil.example")]
    [InlineData("evil.example:5280")]
    [InlineData("127.0.0.1.evil.example:5280")]
    [InlineData("localhost.evil.example")]
    [InlineData("192.168.1.4:5280")]
    [InlineData("")]
    public void DesktopDoorRefusesAForeignHostWhichIsHowDnsRebindingArrives(string host)
    {
        DoorResult denied = new DesktopDoor().Identify(Request(IPAddress.Loopback, ("Host", host)));
        Assert.Null(denied.User);
        Assert.Equal(StatusCodes.Status403Forbidden, denied.StatusCode);
    }

    [Fact]
    public void DesktopDoorRefusesARequestWithNoOrRepeatedHost()
    {
        Assert.Null(new DesktopDoor().Identify(Request(IPAddress.Loopback)).User);
        Assert.Null(new DesktopDoor().Identify(Request(IPAddress.Loopback, ("Host", "127.0.0.1:5280"), ("Host", "127.0.0.1:5280"))).User);
    }

    [Theory]
    [InlineData("http://evil.example")]
    [InlineData("https://127.0.0.1:5280")]
    [InlineData("http://127.0.0.1:5281")]
    [InlineData("http://localhost:5280")]
    [InlineData("null")]
    [InlineData("file://")]
    public void DesktopDoorRefusesAnOriginThatIsNotItsOwn(string origin)
    {
        DoorResult denied = new DesktopDoor().Identify(Request(IPAddress.Loopback, ("Host", "127.0.0.1:5280"), ("Origin", origin)));
        Assert.Null(denied.User);
        Assert.Equal(StatusCodes.Status403Forbidden, denied.StatusCode);
    }

    [Fact]
    public void DesktopDoorAnswersItsOwnOriginAndAClientThatSendsNone()
    {
        DesktopDoor door = new();
        Assert.Equal(UserId.Local, door.Identify(Request(IPAddress.Loopback, ("Host", "127.0.0.1:5280"), ("Origin", "http://127.0.0.1:5280"))).User);
        Assert.Equal(UserId.Local, door.Identify(Request(IPAddress.Loopback, ("Host", "localhost:5280"), ("Origin", "http://LOCALHOST:5280"))).User);
        Assert.Equal(UserId.Local, door.Identify(Request(IPAddress.Loopback, ("Host", "127.0.0.1:5280"))).User);
        Assert.Null(door.Identify(Request(IPAddress.Loopback, ("Host", "127.0.0.1:5280"), ("Origin", "http://127.0.0.1:5280"), ("Origin", "http://127.0.0.1:5280"))).User);
    }

    [Theory]
    [InlineData("cross-site")]
    [InlineData("same-site")]
    [InlineData("something-new")]
    public void DesktopDoorRefusesARequestABrowserMarksAsFromAnotherSite(string site)
    {
        DoorResult denied = new DesktopDoor().Identify(Request(IPAddress.Loopback, ("Host", "127.0.0.1:5280"), ("Sec-Fetch-Site", site)));
        Assert.Null(denied.User);
        Assert.Equal(StatusCodes.Status403Forbidden, denied.StatusCode);
    }

    [Theory]
    [InlineData("same-origin")]
    [InlineData("none")]
    public void DesktopDoorAnswersARequestABrowserMarksAsSameOriginOrUserInitiated(string site)
    {
        Assert.Equal(UserId.Local, new DesktopDoor().Identify(Request(IPAddress.Loopback, ("Host", "127.0.0.1:5280"), ("Sec-Fetch-Site", site))).User);
    }

    [Fact]
    public void ProxyDoorTakesTheUserFromAProxyThatKnowsTheSecret()
    {
        ProxyDoor door = new("swarm", _swarm);
        DoorResult result = door.Identify(Request(IPAddress.Loopback, ("X-AudioLab-Secret", Secret), ("X-AudioLab-User", "alice")));
        Assert.Equal(ProxyDoor.UserFor("swarm", "alice"), result.User);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("0123456789abcdef0123456789abcdee")]
    [InlineData("0123456789abcdef0123456789abcdef0")]
    [InlineData("0123456789abcdef0123456789abcde")]
    public void ProxyDoorRefusesAMissingOrWrongSecretAndNeverReadsTheUser(string? offered)
    {
        ProxyDoor door = new("swarm", _swarm);
        List<(string, string)> headers = [("X-AudioLab-User", "alice")];
        if (offered is not null)
        {
            headers.Add(("X-AudioLab-Secret", offered));
        }
        DoorResult result = door.Identify(Request(IPAddress.Loopback, [.. headers]));
        Assert.Null(result.User);
        Assert.Equal(StatusCodes.Status403Forbidden, result.StatusCode);
    }

    [Fact]
    public void ProxyDoorRefusesRepeatedSecretAndUserHeaders()
    {
        ProxyDoor door = new("swarm", _swarm);
        Assert.Null(door.Identify(Request(IPAddress.Loopback, ("X-AudioLab-Secret", Secret), ("X-AudioLab-Secret", Secret), ("X-AudioLab-User", "a"))).User);
        Assert.Null(door.Identify(Request(IPAddress.Loopback, ("X-AudioLab-Secret", Secret), ("X-AudioLab-User", "a"), ("X-AudioLab-User", "b"))).User);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" alice")]
    [InlineData("alice bob")]
    [InlineData("ali\u00e7e")]
    [InlineData("ali\u0000ce")]
    public void ProxyDoorRefusesASubjectThatIsNotPrintableAsciiWithoutSpaces(string subject)
    {
        DoorResult denied = new ProxyDoor("swarm", _swarm).Identify(Request(IPAddress.Loopback, ("X-AudioLab-Secret", Secret), ("X-AudioLab-User", subject)));
        Assert.Null(denied.User);
        Assert.Equal(StatusCodes.Status401Unauthorized, denied.StatusCode);
    }

    [Theory]
    [InlineData("../alice")]
    [InlineData("a/b\\c")]
    [InlineData("CON")]
    [InlineData("alice@example.com")]
    public void ProxyDoorMapsAnyOtherSubjectToAnId(string subject)
    {
        DoorResult allowed = new ProxyDoor("swarm", _swarm).Identify(Request(IPAddress.Loopback, ("X-AudioLab-Secret", Secret), ("X-AudioLab-User", subject)));
        Assert.True(IdRules.IsValid(allowed.User?.Value));
    }

    [Fact]
    public void ProxyDoorRefusesASubjectOverTheLimit()
    {
        string subject = new('a', ProxyDoor.MaxSubjectLength + 1);
        Assert.Null(new ProxyDoor("swarm", _swarm).Identify(Request(IPAddress.Loopback, ("X-AudioLab-Secret", Secret), ("X-AudioLab-User", subject))).User);
    }

    [Fact]
    public void SubjectsMapToDistinctValidIdsAndDoorsNeverShareOne()
    {
        HashSet<UserId> seen = [];
        foreach (string subject in new[] { "alice", "Alice", "alice ", "a", "A", "0", "alice-1", "alice_1", new string('z', ProxyDoor.MaxSubjectLength) })
        {
            UserId user = ProxyDoor.UserFor("swarm", subject);
            Assert.True(IdRules.IsValid(user.Value));
            Assert.StartsWith("swarm-", user.Value, StringComparison.Ordinal);
            Assert.True(seen.Add(user), subject);
        }
        Assert.NotEqual(ProxyDoor.UserFor("swarm", "alice"), ProxyDoor.UserFor("hosted", "alice"));
        Assert.Equal(ProxyDoor.UserFor("swarm", "alice"), ProxyDoor.UserFor("swarm", "alice"));
        Assert.NotEqual(UserId.Local, ProxyDoor.UserFor("desktop", "local"));
    }

    [Fact]
    public void ADoorThatNeedsASecretCannotBeBuiltWithoutOne()
    {
        Assert.Throws<ArgumentException>(() => new ProxyDoor("swarm", new DoorOptions { Kind = DoorKind.Swarm }));
        Assert.Throws<ArgumentException>(() => new ProxyDoor("swarm", new DoorOptions { Kind = DoorKind.Swarm, ProxySecret = "short" }));
    }

    [Fact]
    public void TheFactoryBuildsTheDoorTheConfigurationNames()
    {
        Assert.Equal("desktop", DoorFactory.Create(new DoorOptions()).Name);
        Assert.Equal("swarm", DoorFactory.Create(_swarm).Name);
        Assert.Equal("hosted", DoorFactory.Create(_swarm with { Kind = DoorKind.Hosted }).Name);
    }

    [Fact]
    public void OptionsNeverPrintTheSecret()
    {
        Assert.DoesNotContain(Secret, _swarm.ToString(), StringComparison.Ordinal);
    }

    private static DoorRequest Request(IPAddress? remote, params (string Name, string Value)[] headers)
    {
        HeaderDictionary dictionary = [];
        foreach ((string name, string value) in headers)
        {
            dictionary.Append(name, value);
        }
        return new DoorRequest(dictionary, remote);
    }
}
