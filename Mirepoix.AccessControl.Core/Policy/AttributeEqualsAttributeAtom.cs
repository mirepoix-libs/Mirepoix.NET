namespace Mirepoix.AccessControl.Policy;

/// <summary>
/// Compares one bundle attribute against another using the same coercion rules as
/// <see cref="AttributeValueAtom"/>. When <see cref="Strict"/> is true (default), either side
/// missing means not satisfied. When false, missing attributes mirror
/// <see cref="AttributeValueAtom"/>: satisfied only for <see cref="ComparisonOperator.NotEquals"/>.
/// </summary>
public sealed class AttributeEqualsAttributeAtom : IAtom
{
    /// <summary>
    /// Creates an attribute-vs-attribute comparison.
    /// </summary>
    /// <param name="leftTarget">Names the target for the left attribute.</param>
    /// <param name="leftType">Names the left subject or resource type. Required and non-blank for subject and resource. Must be null for context.</param>
    /// <param name="leftKey">Names the key for the left attribute.</param>
    /// <param name="rightTarget">Names the target for the right attribute.</param>
    /// <param name="rightType">Names the right subject or resource type. Required and non-blank for subject and resource. Must be null for context.</param>
    /// <param name="rightKey">Names the key for the right attribute.</param>
    /// <param name="op">Holds the comparison operator.</param>
    /// <param name="strict">Marks when true (default) that missing either side fails. When false, missing behaves like NotEquals-only success.</param>
    /// <exception cref="ArgumentException">Thrown when a subject or resource type is null or blank, or when a context type is not null.</exception>
    public AttributeEqualsAttributeAtom(
        AttributeTarget leftTarget,
        string? leftType,
        string leftKey,
        AttributeTarget rightTarget,
        string? rightType,
        string rightKey,
        ComparisonOperator op,
        bool strict = true)
    {
        AttributeValueAtom.ValidateType(leftTarget, leftType, nameof(leftType));
        AttributeValueAtom.ValidateType(rightTarget, rightType, nameof(rightType));
        LeftTarget = leftTarget;
        LeftType = leftType;
        LeftKey = leftKey;
        RightTarget = rightTarget;
        RightType = rightType;
        RightKey = rightKey;
        Op = op;
        Strict = strict;
    }

    /// <summary>Names the left attribute target.</summary>
    public AttributeTarget LeftTarget { get; }

    /// <summary>Names the left subject or resource type. Null when <see cref="LeftTarget"/> is context.</summary>
    public string? LeftType { get; }

    /// <summary>Names the left attribute key.</summary>
    public string LeftKey { get; }

    /// <summary>Names the right attribute target.</summary>
    public AttributeTarget RightTarget { get; }

    /// <summary>Names the right subject or resource type. Null when <see cref="RightTarget"/> is context.</summary>
    public string? RightType { get; }

    /// <summary>Names the right attribute key.</summary>
    public string RightKey { get; }

    /// <summary>Holds the comparison operator between left and right values.</summary>
    public ComparisonOperator Op { get; }

    /// <summary>
    /// Marks missing-attribute policy. True: either side missing => not satisfied.
    /// False: missing => satisfied only when <see cref="Op"/> is <see cref="ComparisonOperator.NotEquals"/>.
    /// </summary>
    public bool Strict { get; }

    /// <inheritdoc />
    public string Name => "attribute-equals-attribute";

    /// <summary>
    /// Returns <see langword="false"/> when either subject or resource side type does not equal the bundle type (ordinal), before value compare.
    /// Resolves both attributes via <see cref="AttributeValueAtom.TryGetAttribute"/> and compares them.
    /// </summary>
    /// <param name="bundle">Hydrated bundle.</param>
    public bool IsSatisfied(AuthorizationBundle bundle)
    {
        if (!AttributeValueAtom.TypeMatches(bundle, LeftTarget, LeftType)
            || !AttributeValueAtom.TypeMatches(bundle, RightTarget, RightType))
            return false;

        var leftPresent = AttributeValueAtom.TryGetAttribute(bundle, LeftTarget, LeftKey, out var left);
        var rightPresent = AttributeValueAtom.TryGetAttribute(bundle, RightTarget, RightKey, out var right);

        if (!leftPresent || !rightPresent)
        {
            if (Strict)
                return false;

            return Op == ComparisonOperator.NotEquals;
        }

        return AttributeValueAtom.Compare(left, Op, right);
    }
}
