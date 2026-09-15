using Mirepoix.AccessControl;
using Mirepoix.AccessControl.Policy;

public class PolicyTests
{
    [Fact]
    public void Policy_rejects_empty_atoms()
    {
        Assert.Throws<ArgumentException>(() =>
            new Policy("p1", AuthorizationResult.Allow, null, Array.Empty<IAtom>()));
    }

    [Fact]
    public void Policy_accepts_at_least_one_atom()
    {
        var atoms = new IAtom[] { new RoleMembershipAtom(new[] { "EDITOR" }) };
        var policy = new Policy("p1", AuthorizationResult.Allow, "editors", atoms);

        Assert.Equal("p1", policy.Id);
        Assert.Equal(AuthorizationResult.Allow, policy.Effect);
        Assert.Equal("editors", policy.Description);
        Assert.Same(atoms, policy.Atoms);
    }

    [Fact]
    public void PolicySet_allows_empty_policies()
    {
        var set = new PolicySet("v1", Array.Empty<Policy>());

        Assert.Equal("v1", set.Version);
        Assert.Empty(set.Policies);
    }
}
