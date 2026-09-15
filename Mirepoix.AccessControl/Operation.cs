namespace Mirepoix.AccessControl;

public sealed class Operation
{
    private readonly string[] _segments;

    public string Value { get; }

    private Operation(string value, string[] segments)
    {
        Value = value;
        _segments = segments;
    }

    public static Operation Parse(string value)
    {
        if (string.IsNullOrEmpty(value))
            throw new ArgumentException("Operation value cannot be empty.", nameof(value));

        return new Operation(value, value.Split(':'));
    }

    public bool Matches(Operation pattern)
    {
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
