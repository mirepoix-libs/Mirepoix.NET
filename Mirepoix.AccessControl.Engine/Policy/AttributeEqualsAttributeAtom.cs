using Mirepoix.AccessControl;

namespace Mirepoix.AccessControl.Policy;

/// <summary>
/// Compares one bundle attribute against another.
/// When <paramref name="strict"/> is <see langword="true"/> (default), either side missing is not satisfied.
/// When <see langword="false"/>, missing attributes mirror <see cref="AttributeValueAtom"/>:
/// satisfied only for <see cref="ComparisonOperator.NotEquals"/>.
/// </summary>
public sealed class AttributeEqualsAttributeAtom : IAtom
{
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

    public AttributeTarget LeftTarget { get; }

    public string LeftKey { get; }

    public AttributeTarget RightTarget { get; }

    public string RightKey { get; }

    public ComparisonOperator Op { get; }

    public bool Strict { get; }

    public string Name => "attribute-equals-attribute";

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
