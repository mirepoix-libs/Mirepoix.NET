using Mirepoix.AccessControl;
using Mirepoix.AccessControl.Management;
using Mirepoix.AccessControl.Policy;

public class AttributeCatalogValidatorTests
{
    private readonly AttributeCatalogValidator _validator = new();

    private static readonly PublishedAttribute[] Published =
    [
        new(AttributeTarget.Subject, "subject", "dept"),
        new(AttributeTarget.Subject, "employee", "dept"),
        new(AttributeTarget.Resource, "invoice", "ownerId"),
        new(AttributeTarget.Resource, "invoice", "status"),
        new(AttributeTarget.Context, null, "time"),
    ];

    [Fact]
    public void Allows_published_value_subject_id_and_both_sides_of_equals()
    {
        _validator.EnsureSupported(Set(
            new AttributeValueAtom(AttributeTarget.Subject, "subject", "dept", ComparisonOperator.Equals, "eng")),
            Published);
        _validator.EnsureSupported(Set(
            new AttributeValueAtom(AttributeTarget.Resource, "invoice", "status", ComparisonOperator.Equals, "open")),
            Published);
        _validator.EnsureSupported(Set(
            new SubjectIdEqualsAttributeAtom(AttributeTarget.Resource, "invoice", "ownerId")),
            Published);
        _validator.EnsureSupported(Set(
            new AttributeValueAtom(AttributeTarget.Context, null, "time", ComparisonOperator.GreaterThan, 0)),
            Published);
        _validator.EnsureSupported(Set(
            new AttributeEqualsAttributeAtom(
                AttributeTarget.Subject, "employee", "dept",
                AttributeTarget.Context, null, "time",
                ComparisonOperator.Equals)),
            Published);
        _validator.EnsureSupported(Set(
            new SubjectIdEqualsAttributeAtom(AttributeTarget.Context, null, "time")),
            Published);
        _validator.EnsureSupported(RoleOnly(), Published);
    }

    [Fact]
    public void Rejects_unknown_key_or_type_on_each_atom()
    {
        Assert.Throws<ArgumentException>(() => _validator.EnsureSupported(Set(
            new AttributeValueAtom(AttributeTarget.Subject, "subject", "title", ComparisonOperator.Equals, "x")),
            Published));
        Assert.Throws<ArgumentException>(() => _validator.EnsureSupported(Set(
            new AttributeValueAtom(AttributeTarget.Subject, "contractor", "dept", ComparisonOperator.Equals, "x")),
            Published));
        Assert.Throws<ArgumentException>(() => _validator.EnsureSupported(Set(
            new SubjectIdEqualsAttributeAtom(AttributeTarget.Resource, "invoice", "assignee")),
            Published));
        Assert.Throws<ArgumentException>(() => _validator.EnsureSupported(Set(
            new AttributeValueAtom(AttributeTarget.Context, null, "tenant", ComparisonOperator.Equals, "a")),
            Published));
        Assert.Throws<ArgumentException>(() => _validator.EnsureSupported(Set(
            new AttributeEqualsAttributeAtom(
                AttributeTarget.Subject, "subject", "dept",
                AttributeTarget.Resource, "invoice", "missing",
                ComparisonOperator.Equals)),
            Published));
    }

    [Fact]
    public void Empty_provider_list_skips_subject_and_resource_and_empty_enforcement_list_skips_context()
    {
        _validator.EnsureSupported(
            Set(new AttributeValueAtom(AttributeTarget.Subject, "subject", "title", ComparisonOperator.Equals, "x")),
            Published,
            gateSubjectAndResource: false);
        _validator.EnsureSupported(
            Set(new AttributeValueAtom(AttributeTarget.Context, null, "tenant", ComparisonOperator.Equals, "a")),
            Published,
            gateContext: false);
        _validator.EnsureSupported(
            Set(
                new AttributeValueAtom(AttributeTarget.Resource, "invoice", "missing", ComparisonOperator.Equals, "x"),
                new AttributeValueAtom(AttributeTarget.Context, null, "tenant", ComparisonOperator.Equals, "a")),
            [],
            gateSubjectAndResource: false,
            gateContext: false);

        Assert.Throws<ArgumentException>(() => _validator.EnsureSupported(
            Set(new AttributeEqualsAttributeAtom(
                AttributeTarget.Subject, "subject", "title",
                AttributeTarget.Context, null, "time",
                ComparisonOperator.Equals)),
            Published,
            gateContext: false));
    }

    private static PolicySet Set(params IAtom[] atoms) => new(
        "v1",
        [new Policy("p", AuthorizationResult.Allow, null, atoms)]);

    private static PolicySet RoleOnly() => new(
        "v1",
        [new Policy("p", AuthorizationResult.Allow, null, [new RoleMembershipAtom(["admin"])])]);
}
