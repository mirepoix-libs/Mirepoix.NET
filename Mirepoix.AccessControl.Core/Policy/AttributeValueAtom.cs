using System.Collections;
using System.Globalization;

namespace Mirepoix.AccessControl.Policy;

/// <summary>
/// Compares one bundle attribute against an expected value.
/// For <see cref="AttributeTarget.Context"/>, resolution order is:
/// <see cref="AccessContext.Time"/> when the key is <c>"time"</c> (ordinal ignore case) and Time is set,
/// then <see cref="AccessContext.Values"/>, then <see cref="AccessContext.Claims"/> (Values win when both contain the key).
/// Comparison coerces JSON-round-tripped values: ISO-8601 strings to
/// <see cref="DateTimeOffset"/>, and numeric widening via <see cref="Convert.ToDouble(object, IFormatProvider)"/>.
/// Missing attribute: satisfied only for <see cref="ComparisonOperator.NotEquals"/>.
/// </summary>
public sealed class AttributeValueAtom : IAtom
{
    /// <summary>
    /// Creates an attribute-vs-expected comparison atom.
    /// </summary>
    /// <param name="target">Names which bundle section holds the attribute.</param>
    /// <param name="key">Names the attribute key (or <c>time</c> for context time).</param>
    /// <param name="op">Holds the comparison operator.</param>
    /// <param name="expected">Holds the expected operand; may be null. For <see cref="ComparisonOperator.In"/>, a non-string enumerable.</param>
    public AttributeValueAtom(AttributeTarget target, string key, ComparisonOperator op, object? expected)
    {
        Target = target;
        Key = key;
        Op = op;
        Expected = expected;
    }

    /// <summary>Names the bundle section to read.</summary>
    public AttributeTarget Target { get; }

    /// <summary>Names the attribute key within the target.</summary>
    public string Key { get; }

    /// <summary>Holds the comparison operator applied to actual vs <see cref="Expected"/>.</summary>
    public ComparisonOperator Op { get; }

    /// <summary>Holds the expected value (or collection for <see cref="ComparisonOperator.In"/>).</summary>
    public object? Expected { get; }

    /// <inheritdoc />
    public string Name => "attribute-value";

    /// <summary>
    /// Resolves the attribute and compares it to <see cref="Expected"/>.
    /// If the attribute is missing, returns <see langword="true"/> only when <see cref="Op"/> is
    /// <see cref="ComparisonOperator.NotEquals"/>.
    /// </summary>
    /// <param name="bundle">Hydrated bundle.</param>
    public bool IsSatisfied(AuthorizationBundle bundle)
    {
        if (!TryGetAttribute(bundle, Target, Key, out var actual))
            return Op == ComparisonOperator.NotEquals;

        return Compare(actual, Op, Expected);
    }

    /// <summary>
    /// Resolves an attribute from the bundle for the given target/key.
    /// Context uses time / Values / Claims order documented on the type. Shared by other atoms.
    /// </summary>
    internal static bool TryGetAttribute(AuthorizationBundle bundle, AttributeTarget target, string key, out object? value)
    {
        switch (target)
        {
            case AttributeTarget.Subject:
                return bundle.Subject.Attributes.TryGetValue(key, out value);
            case AttributeTarget.Resource:
                return bundle.Resource.Attributes.TryGetValue(key, out value);
            case AttributeTarget.Context:
                // Time (key "time"), then Values, then Claims. Values win over Claims.
                if (string.Equals(key, "time", StringComparison.OrdinalIgnoreCase)
                    && bundle.Context.Time is { } time)
                {
                    value = time;
                    return true;
                }

                if (bundle.Context.Values.TryGetValue(key, out value))
                    return true;
                return bundle.Context.Claims.TryGetValue(key, out value);
            default:
                value = null;
                return false;
        }
    }

    /// <summary>
    /// Applies <paramref name="op"/> to <paramref name="actual"/> vs <paramref name="expected"/>
    /// with equality/order/In coercion rules. Unknown operators return false.
    /// </summary>
    internal static bool Compare(object? actual, ComparisonOperator op, object? expected)
    {
        switch (op)
        {
            case ComparisonOperator.Equals:
                return ValuesEqual(actual, expected);
            case ComparisonOperator.NotEquals:
                return !ValuesEqual(actual, expected);
            case ComparisonOperator.In:
                return IsIn(actual, expected);
            case ComparisonOperator.GreaterThan:
            case ComparisonOperator.GreaterThanOrEqual:
            case ComparisonOperator.LessThan:
            case ComparisonOperator.LessThanOrEqual:
                return CompareOrdered(actual, expected, op);
            default:
                return false;
        }
    }

    /// <summary>
    /// Compares equality with DateTimeOffset/string and numeric widening coercion.
    /// Falls back to <see cref="object.Equals(object?, object?)"/> first.
    /// </summary>
    internal static bool ValuesEqual(object? actual, object? expected)
    {
        if (Equals(actual, expected))
            return true;

        if (TryToDateTimeOffset(actual, out var actualTime) && TryToDateTimeOffset(expected, out var expectedTime))
            return actualTime.Equals(expectedTime);

        if (TryToDouble(actual, out var actualNumber) && TryToDouble(expected, out var expectedNumber))
            return actualNumber == expectedNumber;

        return false;
    }

    private static bool IsIn(object? actual, object? expected)
    {
        if (expected is string || expected is not IEnumerable enumerable)
            return false;

        foreach (var item in enumerable)
        {
            if (ValuesEqual(actual, item))
                return true;
        }

        return false;
    }

    private static bool CompareOrdered(object? actual, object? expected, ComparisonOperator op)
    {
        if (actual is null || expected is null)
            return false;

        if (!TryCompare(actual, expected, out var cmp))
            return false;

        return op switch
        {
            ComparisonOperator.GreaterThan => cmp > 0,
            ComparisonOperator.GreaterThanOrEqual => cmp >= 0,
            ComparisonOperator.LessThan => cmp < 0,
            ComparisonOperator.LessThanOrEqual => cmp <= 0,
            _ => false
        };
    }

    private static bool TryCompare(object actual, object expected, out int cmp)
    {
        if (TryToDouble(actual, out var actualNumber) && TryToDouble(expected, out var expectedNumber))
        {
            cmp = actualNumber.CompareTo(expectedNumber);
            return true;
        }

        if (TryToDateTimeOffset(actual, out var actualTime) && TryToDateTimeOffset(expected, out var expectedTime))
        {
            cmp = actualTime.CompareTo(expectedTime);
            return true;
        }

        if (actual is not IComparable comparable)
        {
            cmp = 0;
            return false;
        }

        try
        {
            cmp = comparable.CompareTo(expected);
            return true;
        }
        catch (ArgumentException)
        {
        }
        catch (InvalidCastException)
        {
            cmp = 0;
            return false;
        }

        try
        {
            var converted = Convert.ChangeType(expected, actual.GetType(), CultureInfo.InvariantCulture);
            cmp = comparable.CompareTo(converted);
            return true;
        }
        catch (ArgumentException)
        {
        }
        catch (InvalidCastException)
        {
        }
        catch (FormatException)
        {
        }
        catch (OverflowException)
        {
        }

        cmp = 0;
        return false;
    }

    private static bool TryToDouble(object? value, out double number)
    {
        switch (value)
        {
            case sbyte or byte or short or ushort or int or uint or long or ulong or float or double or decimal:
                number = Convert.ToDouble(value, CultureInfo.InvariantCulture);
                return true;
            default:
                number = default;
                return false;
        }
    }

    private static bool TryToDateTimeOffset(object? value, out DateTimeOffset time)
    {
        switch (value)
        {
            case DateTimeOffset dto:
                time = dto;
                return true;
            case DateTime dt:
                try
                {
                    time = new DateTimeOffset(dt);
                    return true;
                }
                catch (ArgumentOutOfRangeException)
                {
                    time = default;
                    return false;
                }
            case string s when DateTimeOffset.TryParse(
                s,
                CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind,
                out var parsed):
                time = parsed;
                return true;
            default:
                time = default;
                return false;
        }
    }
}
