using Mirepoix.AccessControl;
using Mirepoix.AccessControl.Management;
using Mirepoix.AccessControl.Policy;

public class OperationPatternValidatorTests
{
    private readonly OperationPatternValidator _validator = new();

    [Fact]
    public void Allows_exact_partial_and_full_wildcard_within_max()
    {
        var published = new[] { "invoice:post", "doc:read" };
        _validator.EnsureSupported(Set("invoice:post"), published, 2);
        _validator.EnsureSupported(Set("invoice:*"), published, 2);
        _validator.EnsureSupported(Set("*:read"), published, 2);
        _validator.EnsureSupported(Set("*"), published, 2);
        _validator.EnsureSupported(Set("*:*"), published, 2);
        _validator.EnsureSupported(RoleOnlySet(), published, 2);
    }

    [Fact]
    public void Rejects_missing_concrete_unaligned_partial_and_wildcard_past_max()
    {
        var published = new[] { "invoice:post" };
        Assert.Throws<ArgumentException>(() => _validator.EnsureSupported(Set("invoice:archive"), published, 2));
        Assert.Throws<ArgumentException>(() => _validator.EnsureSupported(Set("doc:*"), published, 2));
        Assert.Throws<ArgumentException>(() => _validator.EnsureSupported(Set("*:*:*"), published, 2));
    }

    [Fact]
    public void Rejects_pattern_index_past_published_length_even_when_wildcard()
    {
        var published = new[] { "invoice:post" };
        Assert.Throws<ArgumentException>(() => _validator.EnsureSupported(Set("invoice:*:*"), published, 3));
    }

    private static PolicySet Set(string pattern) => new(
        "v1",
        [new Policy("p", AuthorizationResult.Allow, null, [new OperationMatchAtom(Operation.Parse(pattern))])]);

    private static PolicySet RoleOnlySet() => new(
        "v1",
        [new Policy("p", AuthorizationResult.Allow, null, [new RoleMembershipAtom(["admin"])])]);
}
