namespace Mirepoix.AccessControl.Policy;

/// <summary>
/// Evaluates one named constraint over an <see cref="AuthorizationBundle"/>.
/// <see cref="Name"/> is both the hit-trace identity and the JSON polymorphic discriminator used by serde.
/// </summary>
public interface IAtom
{
    /// <summary>
    /// Names the stable atom kind (e.g. <c>role-membership</c>). Must match serde discriminators for known types.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Returns whether this constraint holds against <paramref name="bundle"/>.
    /// Missing attributes and nulls are handled per atom implementation.
    /// </summary>
    /// <param name="bundle">Fully hydrated evaluation input.</param>
    /// <returns><see langword="true"/> if the constraint is satisfied.</returns>
    bool IsSatisfied(AuthorizationBundle bundle);
}
