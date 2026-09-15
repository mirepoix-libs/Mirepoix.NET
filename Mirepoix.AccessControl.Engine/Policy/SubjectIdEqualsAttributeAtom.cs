using Mirepoix.AccessControl;

namespace Mirepoix.AccessControl.Policy;

/// <summary>
/// True when <see cref="Subject.Id"/> equals the named attribute on the given target.
/// Ownership is <c>new SubjectIdEqualsAttributeAtom(AttributeTarget.Resource, "ownerId")</c>
/// — <c>resource.attr(ownerId) == subject.id</c>. Missing or null attribute is not satisfied.
/// </summary>
public sealed class SubjectIdEqualsAttributeAtom : IAtom
{
    public SubjectIdEqualsAttributeAtom(AttributeTarget target, string key)
    {
        Target = target;
        Key = key;
    }

    public AttributeTarget Target { get; }

    public string Key { get; }

    public string Name => "subject-id-equals-attribute";

    public bool IsSatisfied(AuthorizationBundle bundle)
    {
        if (!AttributeValueAtom.TryGetAttribute(bundle, Target, Key, out var actual) || actual is null)
            return false;

        return AttributeValueAtom.ValuesEqual(actual, bundle.Subject.Id);
    }
}
