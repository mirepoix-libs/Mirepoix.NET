using System.Collections;
using System.Globalization;
using Mirepoix.AccessControl;

namespace Mirepoix.AccessControl.Policy;

/// <summary>
/// Compares one bundle attribute against an expected value.
/// For <see cref="AttributeTarget.Context"/>, resolution order is:
/// <c>AccessContext.Time</c> when the key is <c>"time"</c> (ordinal ignore case) and Time is set,
/// then <c>Values</c>, then <c>Claims</c> (Values win when both contain the key).
/// Comparison coerces JSON-round-tripped values: ISO-8601 strings to
/// <see cref="DateTimeOffset"/>, and numeric widening via <see cref="Convert.ToDouble(object, IFormatProvider)"/>.
/// </summary>
public sealed class AttributeValueAtom : IAtom
{
    public AttributeValueAtom(AttributeTarget target, string key, ComparisonOperator op, object? expected)
    {
        Target = target;
        Key = key;
        Op = op;
        Expected = expected;
    }

    public AttributeTarget Target { get; }

    public string Key { get; }

    public ComparisonOperator Op { get; }

    public object? Expected { get; }

    public string Name => "attribute-value";

    public bool IsSatisfied(AuthorizationBundle bundle)
    {
        if (!TryGetAttribute(bundle, Target, Key, out var actual))
            return Op == ComparisonOperator.NotEquals;

        return Compare(actual, Op, Expected);
    }

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
