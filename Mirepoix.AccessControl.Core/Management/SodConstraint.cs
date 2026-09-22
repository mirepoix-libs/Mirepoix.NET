namespace Mirepoix.AccessControl.Management;

/// <summary>
/// Enforces separation of duty: roles in <see cref="MutuallyExclusiveRoles"/> must not be
/// held together (enforced at assignment time, not at check time).
/// </summary>
/// <param name="Id">Names the stable constraint id returned on conflict.</param>
/// <param name="MutuallyExclusiveRoles">Lists role ids that conflict when two or more appear together.</param>
public sealed record SodConstraint(string Id, IReadOnlySet<string> MutuallyExclusiveRoles);
