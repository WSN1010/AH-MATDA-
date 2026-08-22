namespace Ajure.Specification.Tests;

public sealed class RepairScopeGuardTests
{
    [Fact]
    public void ChangeToAllowedStableIdIsAccepted()
    {
        var before = SampleSpec.Create();
        var after = before with
        {
            Requirements =
            [
                before.Requirements[0] with { Statement = "A repaired measurable requirement." },
                .. before.Requirements.Skip(1)
            ]
        };

        Assert.True(RepairScopeGuard.OnlyTouches(before, after, [before.Requirements[0].Id]));
    }

    [Fact]
    public void ChangeOutsideAllowedStableIdIsRejected()
    {
        var before = SampleSpec.Create();
        var after = before with
        {
            Requirements =
            [
                before.Requirements[0],
                before.Requirements[1] with { Statement = "An out-of-scope repair." },
                .. before.Requirements.Skip(2)
            ]
        };

        Assert.False(RepairScopeGuard.OnlyTouches(before, after, [before.Requirements[0].Id]));
    }

    [Fact]
    public void GlobalOrStructuralChangesAreRejected()
    {
        var before = SampleSpec.Create();

        Assert.False(RepairScopeGuard.OnlyTouches(
            before,
            before with { Vision = "Changed global vision." },
            [before.Requirements[0].Id]));
        Assert.False(RepairScopeGuard.OnlyTouches(
            before,
            before with { Requirements = [.. before.Requirements.Skip(1)] },
            [before.Requirements[0].Id]));
    }
}
