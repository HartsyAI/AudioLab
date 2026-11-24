using AudioLab.Core.Model;
using AudioLab.Core.Rules;
using AudioLab.Web.Services;
using AudioLab.Web.Session;
using AudioLab.Web.Session.Effects;

namespace AudioLab.Web.Tests;

/// <summary>The rules of the effects panel: what a chain allows and says, and where a drag of an effect lands.</summary>
public sealed class EffectChainTests
{
    private static Track WithEffects(int count)
    {
        Track track = Song.NewTrack("t1");
        EffectId[] ids = [.. Enumerable.Range(0, count).Select(index => EffectId.From($"e{index}"))];
        return track with { Effects = [.. ids.Select(id => EffectRules.Create(EffectKind.Eq, id))] };
    }

    /// <summary>A chain at the most Core allows says so in Core's words, and one below it takes another.</summary>
    [Fact]
    public void AFullChainSaysSoInCoresWords()
    {
        Assert.Null(EffectChainRules.AddBlock(WithEffects(ProjectLimits.MaxEffects - 1)));
        string? block = EffectChainRules.AddBlock(WithEffects(ProjectLimits.MaxEffects));

        Assert.Equal($"A track has at most {ProjectLimits.MaxEffects} effects.", block);
        Assert.Equal(EffectRules.AddRefusal(ProjectLimits.MaxEffects), block);
    }

    /// <summary>The first effect cannot go earlier and the last cannot go later, and each says why.</summary>
    [Fact]
    public void TheEndsOfTheChainSayWhyTheyCannotMove()
    {
        Assert.NotNull(EffectChainRules.MoveBlock(0, 3, MoveDirection.Up));
        Assert.Null(EffectChainRules.MoveBlock(0, 3, MoveDirection.Down));
        Assert.Null(EffectChainRules.MoveBlock(2, 3, MoveDirection.Up));
        Assert.NotNull(EffectChainRules.MoveBlock(2, 3, MoveDirection.Down));
        Assert.NotNull(EffectChainRules.MoveBlock(0, 1, MoveDirection.Down));
    }

    /// <summary>Every kind has a name, and the slot, the chain and the menu all read it from one place.</summary>
    [Fact]
    public void EveryKindHasANameFromOnePlace()
    {
        foreach (EffectKind kind in Enum.GetValues<EffectKind>())
        {
            Effect effect = EffectRules.Create(kind, EffectId.From("e"));

            Assert.Equal(EffectText.Name(kind), TrackText.Effect(effect));
        }
        Assert.Equal(Enum.GetValues<EffectKind>(), EffectText.Kinds);
        Assert.All(EffectText.Kinds, kind => Assert.True(Shortcuts.EffectAdd.ContainsKey(kind)));
    }

    /// <summary>A press that never travels is not a drag, and a release where it began sends nothing.</summary>
    [Fact]
    public void APressThatNeverTravelsSendsNothing()
    {
        EffectDrag drag = new("e1", 1, 3, 40);

        drag.Move(41, 30);

        Assert.False(drag.Active);
        Assert.Equal(1, drag.Target);
        Assert.Null(drag.Commit());
    }

    /// <summary>The pointer's row is the target, clamped to the chain, and the release says how many places it is from the start.</summary>
    [Fact]
    public void TheReleaseSaysHowManyPlacesTheTargetIsFromTheStart()
    {
        EffectDrag later = new("e0", 0, 3, 10);
        later.Move(75, 30);
        Assert.True(later.Active);
        Assert.Equal(2, later.Target);
        Assert.Equal(2, later.Commit());

        EffectDrag earlier = new("e2", 2, 3, 80);
        earlier.Move(-500, 30);
        Assert.Equal(0, earlier.Target);
        Assert.Equal(-2, earlier.Commit());

        EffectDrag past = new("e0", 0, 3, 10);
        past.Move(5000, 30);
        Assert.Equal(2, past.Target);
    }

    /// <summary>A drag that went away and came back to its place lands where it began and sends nothing.</summary>
    [Fact]
    public void ADragThatCameBackSendsNothing()
    {
        EffectDrag drag = new("e1", 1, 3, 40);

        drag.Move(100, 30);
        drag.Move(45, 30);

        Assert.True(drag.Active);
        Assert.Equal(1, drag.Target);
        Assert.Null(drag.Commit());
    }
}
