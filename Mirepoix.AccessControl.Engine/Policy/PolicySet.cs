namespace Mirepoix.AccessControl.Policy;

public sealed record PolicySet(
    string Version,
    IReadOnlyList<Policy> Policies);
