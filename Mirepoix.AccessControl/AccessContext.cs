namespace Mirepoix.AccessControl;

public sealed record AccessContext(
    DateTimeOffset? Time,
    IReadOnlyDictionary<string, object?> Claims,
    IReadOnlyDictionary<string, object?> Values);
