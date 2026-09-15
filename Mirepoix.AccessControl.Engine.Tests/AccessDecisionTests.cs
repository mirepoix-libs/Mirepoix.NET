using Mirepoix.AccessControl;

public class AccessDecisionTests
{
    [Fact]
    public void AccessDecision_carries_status_not_defaulted_bool()
    {
        var decision = new AccessDecision(
            AuthorizationResult.Deny,
            Array.Empty<PolicyHit>(),
            DecisionStatus.Defaulted,
            "v1");

        Assert.Equal(DecisionStatus.Defaulted, decision.Status);
        Assert.Equal("v1", decision.PolicySetVersion);
    }
}
