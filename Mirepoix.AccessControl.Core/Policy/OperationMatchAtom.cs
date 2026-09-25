namespace Mirepoix.AccessControl.Policy;

/// <summary>
/// Matches when <see cref="AuthorizationBundle.Operation"/> equals <see cref="Pattern"/>
/// via <see cref="Operation.Matches"/> (pattern holds wildcards; all-wildcard patterns match any
/// length, other patterns require equal segment counts).
/// </summary>
public sealed class OperationMatchAtom : IAtom
{
    /// <summary>
    /// Creates an operation-match atom.
    /// </summary>
    /// <param name="pattern">Holds the policy pattern operation (may include <c>*</c> segments).</param>
    public OperationMatchAtom(Operation pattern)
    {
        Pattern = pattern;
    }

    /// <summary>Holds the pattern passed to <see cref="Operation.Matches"/> as the pattern argument.</summary>
    public Operation Pattern { get; }

    /// <inheritdoc />
    public string Name => "operation-match";

    /// <summary>
    /// Returns <c>bundle.Operation.Matches(Pattern)</c>.
    /// </summary>
    /// <param name="bundle">Hydrated bundle whose operation is tested.</param>
    public bool IsSatisfied(AuthorizationBundle bundle) =>
        bundle.Operation.Matches(Pattern);
}
