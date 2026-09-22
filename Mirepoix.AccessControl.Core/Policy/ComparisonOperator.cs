namespace Mirepoix.AccessControl.Policy;

/// <summary>
/// Names the comparison used by attribute atoms. Ordered operators require comparable numeric or time values
/// (with coercion); <see cref="In"/> treats the expected side as an enumerable (not a string).
/// </summary>
public enum ComparisonOperator
{
    /// <summary>Succeeds when values equal after coercion rules in <see cref="AttributeValueAtom"/>.</summary>
    Equals,

    /// <summary>Succeeds when values are not equal. Also the only operator that can succeed when an attribute is missing.</summary>
    NotEquals,

    /// <summary>Succeeds when actual is greater than expected (numbers/times/<see cref="IComparable"/>).</summary>
    GreaterThan,

    /// <summary>Succeeds when actual is greater than or equal to expected.</summary>
    GreaterThanOrEqual,

    /// <summary>Succeeds when actual is less than expected.</summary>
    LessThan,

    /// <summary>Succeeds when actual is less than or equal to expected.</summary>
    LessThanOrEqual,

    /// <summary>
    /// Succeeds when actual equals any element of the expected collection. Expected must be a non-string
    /// <see cref="System.Collections.IEnumerable"/>; otherwise the comparison fails.
    /// </summary>
    In
}
