namespace Mirepoix.AccessControl.Policy;

/// <summary>
/// Holds a versioned, ordered collection of policies. Unit of load/replace for <c>IPolicySource</c>.
/// Order matters for <c>FirstApplicableStrategy</c>; override strategies only care about hit effects.
/// </summary>
/// <param name="Version">Names the opaque version string returned on <see cref="AccessDecision.PolicySetVersion"/> for audit.</param>
/// <param name="Policies">Lists policies in evaluation order.</param>
public sealed record PolicySet(
    string Version,
    IReadOnlyList<Policy> Policies);
