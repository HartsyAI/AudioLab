using AudioLab.Core.Commands;
using AudioLab.Core.Model;
using AudioLab.Web.Session;

namespace AudioLab.Web.Tests;

/// <summary>Holds the checks a recorded pass passes before anything is sent (D25).</summary>
public sealed class AutomationPassRulesTests
{
    /// <summary>A pass with an empty segment, a point outside the lane, or points out of order has a reason and cannot be committed.</summary>
    [Fact]
    public void APassThatCannotBeWrittenHasAReason()
    {
        AutomationPass empty = new(Array.Empty<IReadOnlyList<WrittenPoint>>(), true, false);
        AutomationPass outside = new([[new WrittenPoint(1, 2)]], false, false);
        AutomationPass backwards = new([[new WrittenPoint(2, 0.1)], [new WrittenPoint(1, 0.1)]], true, false);
        AutomationPass valid = new([[new WrittenPoint(1, 0.1)], [new WrittenPoint(2, 0.1)]], true, false);

        Assert.NotNull(AutomationPassRules.Problem(empty, AutomationParameter.Volume));
        Assert.NotNull(AutomationPassRules.Problem(outside, AutomationParameter.Pan));
        Assert.NotNull(AutomationPassRules.Problem(backwards, AutomationParameter.Volume));
        Assert.Null(AutomationPassRules.Problem(valid, AutomationParameter.Volume));
    }
}
