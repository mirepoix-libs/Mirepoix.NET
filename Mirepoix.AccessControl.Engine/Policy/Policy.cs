using Mirepoix.AccessControl;

namespace Mirepoix.AccessControl.Policy;

public sealed record Policy
{
    public Policy(string id, AuthorizationResult effect, string? description, IReadOnlyList<IAtom> atoms)
    {
        if (atoms.Count == 0)
            throw new ArgumentException("Policy must have at least one atom.", nameof(atoms));

        Id = id;
        Effect = effect;
        Description = description;
        Atoms = atoms;
    }

    public string Id { get; init; }

    public AuthorizationResult Effect { get; init; }

    public string? Description { get; init; }

    public IReadOnlyList<IAtom> Atoms { get; init; }
}
