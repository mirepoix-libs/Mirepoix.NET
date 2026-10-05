namespace Mirepoix.AccessControl.Policy;

/// <summary>
/// Matches when <see cref="Subject.Id"/> equals the named attribute on the given target.
/// Typical ownership check: target <see cref="AttributeTarget.Resource"/> and key <c>ownerId</c>
/// (resource attribute equals subject id). Missing or null attribute is not satisfied.
/// </summary>
public sealed class SubjectIdEqualsAttributeAtom : IAtom
{
    /// <summary>
    /// Creates a subject-id-equals-attribute atom.
    /// </summary>
    /// <param name="target">Names where to read the attribute (usually <see cref="AttributeTarget.Resource"/>).</param>
    /// <param name="type">Names the subject or resource type. Required and non-blank for subject and resource. Must be null for context.</param>
    /// <param name="key">Names the attribute key to compare to <see cref="Subject.Id"/>.</param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="type"/> is null or blank for subject or resource, or when context <paramref name="type"/> is not null.</exception>
    public SubjectIdEqualsAttributeAtom(AttributeTarget target, string? type, string key)
    {
        AttributeValueAtom.ValidateType(target, type, nameof(type));
        Target = target;
        Type = type;
        Key = key;
    }

    /// <summary>Names the attribute target to read.</summary>
    public AttributeTarget Target { get; }

    /// <summary>Names the subject or resource type compared to the bundle type. Null for context.</summary>
    public string? Type { get; }

    /// <summary>Names the attribute key compared to <see cref="Subject.Id"/>.</summary>
    public string Key { get; }

    /// <inheritdoc />
    public string Name => "subject-id-equals-attribute";

    /// <summary>
    /// Returns <see langword="false"/> when the bundle type does not equal <see cref="Type"/> (ordinal), before the value compare.
    /// Returns whether the resolved attribute equals <see cref="Subject.Id"/> using
    /// <see cref="AttributeValueAtom.ValuesEqual"/>. Null or missing attribute fails.
    /// </summary>
    /// <param name="bundle">Hydrated bundle.</param>
    public bool IsSatisfied(AuthorizationBundle bundle)
    {
        if (!AttributeValueAtom.TypeMatches(bundle, Target, Type))
            return false;

        if (!AttributeValueAtom.TryGetAttribute(bundle, Target, Key, out var actual) || actual is null)
            return false;

        return AttributeValueAtom.ValuesEqual(actual, bundle.Subject.Id);
    }
}
