namespace Mirepoix.AccessControl.Management;

public sealed record SodConstraint(string Id, IReadOnlySet<string> MutuallyExclusiveRoles);
