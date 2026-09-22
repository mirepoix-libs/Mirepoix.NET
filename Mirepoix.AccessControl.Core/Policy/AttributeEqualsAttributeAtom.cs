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
    /// <param name="leftKey">Names the key for the left attribute.</param>
    /// <param name="rightTarget">Names the target for the right attribute.</param>
    /// <param name="rightKey">Names the key for the right attribute.</param>
    /// <param name="op">Holds the comparison operator.</param>
    /// <param name="strict">Marks when true (default) that missing either side fails. When false, missing behaves like NotEquals-only success.</param>
    public AttributeEqualsAttributeAtom(
        AttributeTarget leftTarget,
        string leftKey,
        AttributeTarget rightTarget,
        string rightKey,
        ComparisonOperator op,
        bool strict = true)
    {
        LeftTarget = leftTarget;
        LeftKey = leftKey;
        RightTarget = rightTarget;
        RightKey = rightKey;
        Op = op;
        Strict = strict;
    }

    /// <summary>Names the left attribute target.</summary>
    public AttributeTarget LeftTarget { get; }

    /// <summary>Names the left attribute key.</summary>
    public string LeftKey { get; }

    /// <summary>Names the right attribute target.</summary>
    public AttributeTarget RightTarget { get; }

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
    /// Resolves both attributes via <see cref="AttributeValueAtom.TryGetAttribute"/> and compares them.
    /// </summary>
    /// <param name="bundle">Hydrated bundle.</param>
    public bool IsSatisfied(AuthorizationBundle bundle)
    {
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
