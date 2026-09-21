namespace Mirepoix.AccessControl.Evaluation;

/// <summary>
/// Combines ordered policy hits into a single <see cref="AuthorizationResult"/>.
/// Pure: sees hits only, not the bundle. The default-result argument is used when the
/// strategy finds no decisive effect (callers typically pass Deny).
/// </summary>
public interface ICombinationStrategy
{
    /// <summary>
    /// Combines <paramref name="hits"/> into one effect.
    /// </summary>
    /// <param name="hits">Policies that matched, in policy-set order.</param>
    /// <param name="defaultResult">Effect when hits are empty or no override applies.</param>
    /// <returns>Final allow or deny for the decision.</returns>
    AuthorizationResult Combine(IReadOnlyList<PolicyHit> hits, AuthorizationResult defaultResult);
}
