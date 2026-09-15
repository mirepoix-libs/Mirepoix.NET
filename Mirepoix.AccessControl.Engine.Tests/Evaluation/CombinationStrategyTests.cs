using Mirepoix.AccessControl;
using Mirepoix.AccessControl.Evaluation;

public class CombinationStrategyTests
{
    [Fact]
    public void DenyOverrides_prefer_deny_hit()
    {
        var strategy = new DenyOverridesStrategy();
        var hits = new[]
        {
            new PolicyHit("a", AuthorizationResult.Allow, null),
            new PolicyHit("d", AuthorizationResult.Deny, null)
        };
        Assert.Equal(AuthorizationResult.Deny, strategy.Combine(hits, AuthorizationResult.Deny));
    }

    [Fact]
    public void DenyOverrides_allow_when_only_allow_hits()
    {
        var strategy = new DenyOverridesStrategy();
        var hits = new[]
        {
            new PolicyHit("a", AuthorizationResult.Allow, null)
        };
        Assert.Equal(AuthorizationResult.Allow, strategy.Combine(hits, AuthorizationResult.Deny));
    }

    [Fact]
    public void DenyOverrides_default_when_no_hits()
    {
        var strategy = new DenyOverridesStrategy();
        Assert.Equal(AuthorizationResult.Allow, strategy.Combine(Array.Empty<PolicyHit>(), AuthorizationResult.Allow));
        Assert.Equal(AuthorizationResult.Deny, strategy.Combine(Array.Empty<PolicyHit>(), AuthorizationResult.Deny));
    }

    [Fact]
    public void PermitOverrides_prefer_allow_hit()
    {
        var strategy = new PermitOverridesStrategy();
        var hits = new[]
        {
            new PolicyHit("d", AuthorizationResult.Deny, null),
            new PolicyHit("a", AuthorizationResult.Allow, null)
        };
        Assert.Equal(AuthorizationResult.Allow, strategy.Combine(hits, AuthorizationResult.Deny));
    }

    [Fact]
    public void PermitOverrides_deny_when_only_deny_hits()
    {
        var strategy = new PermitOverridesStrategy();
        var hits = new[]
        {
            new PolicyHit("d", AuthorizationResult.Deny, null)
        };
        Assert.Equal(AuthorizationResult.Deny, strategy.Combine(hits, AuthorizationResult.Allow));
    }

    [Fact]
    public void PermitOverrides_default_when_no_hits()
    {
        var strategy = new PermitOverridesStrategy();
        Assert.Equal(AuthorizationResult.Allow, strategy.Combine(Array.Empty<PolicyHit>(), AuthorizationResult.Allow));
    }

    [Fact]
    public void FirstApplicable_uses_first_hit_effect()
    {
        var strategy = new FirstApplicableStrategy();
        var allowFirst = new[]
        {
            new PolicyHit("a", AuthorizationResult.Allow, null),
            new PolicyHit("d", AuthorizationResult.Deny, null)
        };
        var denyFirst = new[]
        {
            new PolicyHit("d", AuthorizationResult.Deny, null),
            new PolicyHit("a", AuthorizationResult.Allow, null)
        };
        Assert.Equal(AuthorizationResult.Allow, strategy.Combine(allowFirst, AuthorizationResult.Deny));
        Assert.Equal(AuthorizationResult.Deny, strategy.Combine(denyFirst, AuthorizationResult.Allow));
    }

    [Fact]
    public void FirstApplicable_default_when_no_hits()
    {
        var strategy = new FirstApplicableStrategy();
        Assert.Equal(AuthorizationResult.Deny, strategy.Combine(Array.Empty<PolicyHit>(), AuthorizationResult.Deny));
    }
}
