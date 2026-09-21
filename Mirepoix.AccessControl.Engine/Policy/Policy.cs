namespace Mirepoix.AccessControl.Policy;

/// <summary>
/// Holds one authorization rule: an effect plus one or more atoms that must all hold for the policy to hit.
/// Atoms are ANDed. An empty atom list is rejected at construction.
/// </summary>
public sealed record Policy
{
    /// <summary>
    /// Creates a policy. Throws if <paramref name="atoms"/> is empty.
    /// </summary>
    /// <param name="id">Names the stable policy id used in <see cref="PolicyHit.PolicyId"/> and serde.</param>
    /// <param name="effect">Holds the effect contributed when all atoms are satisfied.</param>
    /// <param name="description">Holds an optional human-readable description copied onto hits; may be null.</param>
    /// <param name="atoms">Lists non-empty constraints; all must be satisfied for a hit.</param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="atoms"/> has count 0.</exception>
    public Policy(string id, AuthorizationResult effect, string? description, IReadOnlyList<IAtom> atoms)
    {
        if (atoms.Count == 0)
            throw new ArgumentException("Policy must have at least one atom.", nameof(atoms));

        Id = id;
        Effect = effect;
        Description = description;
        Atoms = atoms;
    }

    /// <summary>Names the stable policy identifier.</summary>
    public string Id { get; init; }

    /// <summary>Holds Allow or Deny contributed when this policy hits.</summary>
    public AuthorizationResult Effect { get; init; }

    /// <summary>Holds an optional description for traces; may be null.</summary>
    public string? Description { get; init; }

    /// <summary>Lists atoms evaluated with AND semantics (all must be satisfied).</summary>
    public IReadOnlyList<IAtom> Atoms { get; init; }
}
