using Mirepoix.AccessControl;
using Mirepoix.AccessControl.Evaluation;
using Mirepoix.AccessControl.Policy;

public class AuthorizerTests
{
    [Fact]
    public void Authorizer_defaulted_when_no_policy_hits()
    {
        var authorizer = new Authorizer(new DenyOverridesStrategy());
        var set = new PolicySet("v1", new[]
        {
            new Policy("p1", AuthorizationResult.Allow, null, new IAtom[]
            {
                new RoleMembershipAtom(new[] { "ADMIN" })
            })
        });
        var bundle = Bundle(roles: new[] { "USER" }, operation: "doc:read");
        var decision = authorizer.Authorize(bundle, set);
        Assert.Equal(AuthorizationResult.Deny, decision.Result);
        Assert.Equal(DecisionStatus.Defaulted, decision.Status);
    }

    [Fact]
    public void Authorizer_success_when_policy_hits()
    {
        var authorizer = new Authorizer(new DenyOverridesStrategy());
        var set = new PolicySet("v1", new[]
        {
            new Policy("p1", AuthorizationResult.Allow, "admins", new IAtom[]
            {
                new RoleMembershipAtom(new[] { "ADMIN" })
            })
        });
        var decision = authorizer.Authorize(Bundle(roles: new[] { "ADMIN" }), set);

        Assert.Equal(AuthorizationResult.Allow, decision.Result);
        Assert.Equal(DecisionStatus.Success, decision.Status);
        var hit = Assert.Single(decision.PolicyHits);
        Assert.Equal("p1", hit.PolicyId);
        Assert.Equal(AuthorizationResult.Allow, hit.Effect);
        Assert.Equal("admins", hit.Description);
    }

    [Fact]
    public void Authorizer_copies_policy_set_version()
    {
        var authorizer = new Authorizer(new DenyOverridesStrategy());
        var set = new PolicySet("set-42", Array.Empty<Policy>());
        var decision = authorizer.Authorize(Bundle(), set);
        Assert.Equal("set-42", decision.PolicySetVersion);
    }

    [Fact]
    public void Authorizer_hits_in_policy_set_order()
    {
        var authorizer = new Authorizer(new DenyOverridesStrategy());
        var set = new PolicySet("v1", new[]
        {
            new Policy("later", AuthorizationResult.Allow, null, new IAtom[]
            {
                new RoleMembershipAtom(new[] { "USER" })
            }),
            new Policy("deny", AuthorizationResult.Deny, null, new IAtom[]
            {
                new RoleMembershipAtom(new[] { "USER" })
            })
        });
        var decision = authorizer.Authorize(Bundle(roles: new[] { "USER" }), set);

        Assert.Equal(new[] { "later", "deny" }, decision.PolicyHits.Select(h => h.PolicyId));
        Assert.Equal(AuthorizationResult.Deny, decision.Result);
        Assert.Equal(DecisionStatus.Success, decision.Status);
    }

    [Fact]
    public void Authorizer_requires_all_atoms_satisfied()
    {
        var authorizer = new Authorizer(new DenyOverridesStrategy());
        var set = new PolicySet("v1", new[]
        {
            new Policy("p1", AuthorizationResult.Allow, null, new IAtom[]
            {
                new RoleMembershipAtom(new[] { "ADMIN" }),
                new OperationMatchAtom(Operation.Parse("doc:read"))
            })
        });

        var roleOnly = authorizer.Authorize(Bundle(roles: new[] { "ADMIN" }, operation: "doc:write"), set);
        Assert.Equal(DecisionStatus.Defaulted, roleOnly.Status);
        Assert.Empty(roleOnly.PolicyHits);

        var both = authorizer.Authorize(Bundle(roles: new[] { "ADMIN" }, operation: "doc:read"), set);
        Assert.Equal(DecisionStatus.Success, both.Status);
        Assert.Equal("p1", Assert.Single(both.PolicyHits).PolicyId);
    }

    [Fact]
    public void Authorizer_permit_overrides_prefers_allow_among_hits()
    {
        var authorizer = new Authorizer(new PermitOverridesStrategy());
        var set = new PolicySet("v1", new[]
        {
            new Policy("d", AuthorizationResult.Deny, null, new IAtom[]
            {
                new RoleMembershipAtom(new[] { "USER" })
            }),
            new Policy("a", AuthorizationResult.Allow, null, new IAtom[]
            {
                new RoleMembershipAtom(new[] { "USER" })
            })
        });
        var decision = authorizer.Authorize(Bundle(roles: new[] { "USER" }), set);
        Assert.Equal(AuthorizationResult.Allow, decision.Result);
        Assert.Equal(DecisionStatus.Success, decision.Status);
    }

    [Fact]
    public void Authorizer_first_applicable_uses_first_hit()
    {
        var authorizer = new Authorizer(new FirstApplicableStrategy());
        var set = new PolicySet("v1", new[]
        {
            new Policy("first", AuthorizationResult.Allow, null, new IAtom[]
            {
                new RoleMembershipAtom(new[] { "USER" })
            }),
            new Policy("second", AuthorizationResult.Deny, null, new IAtom[]
            {
                new RoleMembershipAtom(new[] { "USER" })
            })
        });
        var decision = authorizer.Authorize(Bundle(roles: new[] { "USER" }), set);
        Assert.Equal(AuthorizationResult.Allow, decision.Result);
        Assert.Equal(new[] { "first", "second" }, decision.PolicyHits.Select(h => h.PolicyId));
    }

    private static AuthorizationBundle Bundle(
        IEnumerable<string>? roles = null,
        string operation = "doc:read")
    {
        return new AuthorizationBundle(
            new Subject("u1", new HashSet<string>(roles ?? Array.Empty<string>()), new Dictionary<string, object?>()),
            new Resource("doc", "1", new Dictionary<string, object?>()),
            Operation.Parse(operation),
            new AccessContext(null, new Dictionary<string, object?>(), new Dictionary<string, object?>()));
    }
}
