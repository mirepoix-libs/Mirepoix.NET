namespace Mirepoix.AccessControl;

public sealed record Resource(
    string Type,
    string Id,
    IReadOnlyDictionary<string, object?> Attributes);
