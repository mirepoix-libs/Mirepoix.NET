namespace Mirepoix.AccessControl;

public sealed record Subject(
    string Id,
    IReadOnlySet<string> Roles,
    IReadOnlyDictionary<string, object?> Attributes);
