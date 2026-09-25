namespace Mirepoix.AccessControl;

/// <summary>
/// Models a hierarchical operation string split on <c>:</c> (e.g. <c>document:read</c>, <c>admin:*</c>).
/// Matching is segment-wise: a pattern segment of <c>*</c> matches any value in that position;
/// otherwise comparison is ordinal. When every pattern segment is <c>*</c>, any concrete length
/// matches; all other patterns require equal segment counts. Call
/// <c>concrete.Matches(pattern)</c>; the receiver is the request operation, the argument is
/// the policy pattern (wildcards live on the pattern).
/// </summary>
/// <remarks>
/// Not a record: equality is reference-based unless callers compare <see cref="Value"/>.
/// Empty or null input to <see cref="Parse"/> throws. A single segment (no colon) is valid.
/// <c>*</c> is only special as a whole segment, not as a substring inside a segment.
/// Different segment counts never match except for all-wildcard patterns (e.g. <c>a:b</c> vs
/// <c>a:b:c</c>, but <c>*</c> matches any concrete operation).
/// </remarks>
public sealed class Operation
{
    private readonly string[] _segments;

    /// <summary>Holds the original operation string passed to <see cref="Parse"/> (not re-normalized).</summary>
    public string Value { get; }

    private Operation(string value, string[] segments)
    {
        Value = value;
        _segments = segments;
    }

    /// <summary>
    /// Parses <paramref name="value"/> into an operation by splitting on <c>:</c>.
    /// </summary>
    /// <param name="value">Non-empty operation string. Not trimmed; whitespace is significant.</param>
    /// <returns>An operation whose <see cref="Value"/> is exactly <paramref name="value"/>.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="value"/> is null or empty.</exception>
    public static Operation Parse(string value)
    {
        if (string.IsNullOrEmpty(value))
            throw new ArgumentException("Operation value cannot be empty.", nameof(value));

        return new Operation(value, value.Split(':'));
    }

    /// <summary>
    /// Returns whether this operation matches <paramref name="pattern"/> segment-by-segment.
    /// </summary>
    /// <param name="pattern">
    /// Pattern operation (typically from a policy). Segments equal to <c>*</c> match any
    /// corresponding segment on this instance; other segments require ordinal equality.
    /// </param>
    /// <returns>
    /// <see langword="true"/> when every pattern segment is <c>*</c>, or when segment counts are
    /// equal and every non-wildcard segment matches; otherwise <see langword="false"/>.
    /// </returns>
    public bool Matches(Operation pattern)
    {
        if (pattern._segments.Length > 0 && pattern._segments.All(segment => segment == "*"))
            return true;

        if (_segments.Length != pattern._segments.Length)
            return false;

        for (var i = 0; i < _segments.Length; i++)
        {
            var patternSegment = pattern._segments[i];
            if (patternSegment != "*" &&
                !string.Equals(_segments[i], patternSegment, StringComparison.Ordinal))
                return false;
        }

        return true;
    }
}
