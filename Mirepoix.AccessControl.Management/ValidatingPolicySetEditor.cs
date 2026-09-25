using Mirepoix.AccessControl;
using Mirepoix.AccessControl.Policy;

namespace Mirepoix.AccessControl.Management;

/// <summary>
/// Pulls the operation catalog, rejects unsupported patterns, then forwards the policy set.
/// </summary>
/// <remarks>
/// A complete pull validates against that pull's union, then <see cref="OperationCatalogSnapshot.Apply"/>
/// stores it. A pull with failed apps validates against the stored union when
/// <see cref="OperationCatalogSnapshot.HasSnapshot"/> is true, then <c>Apply</c> records
/// <see cref="OperationCatalogSnapshot.LastPull"/> without replacing the union.
/// <see cref="OperationPatternValidator.EnsureSupported"/> runs before <c>Apply</c> and before
/// <see cref="Inner"/>. Either failure skips both.
/// </remarks>
public sealed class ValidatingPolicySetEditor : IPolicySetEditor
{
    private readonly OperationCatalogSnapshot _snapshot;
    private readonly IEnforcementCatalogClient _client;
    private readonly OperationPatternValidator _validator;

    /// <summary>
    /// Creates a decorator that pulls through <paramref name="client"/> and writes through <paramref name="inner"/>.
    /// </summary>
    /// <param name="inner">Editor invoked only after the catalog check passes.</param>
    /// <param name="snapshot">In-memory union. Updated only after <see cref="OperationPatternValidator.EnsureSupported"/> succeeds.</param>
    /// <param name="client">Source of the pull used for this replacement.</param>
    /// <param name="validator">Checks <see cref="OperationMatchAtom"/> patterns against the chosen union.</param>
    public ValidatingPolicySetEditor(
        IPolicySetEditor inner,
        OperationCatalogSnapshot snapshot,
        IEnforcementCatalogClient client,
        OperationPatternValidator validator)
    {
        ArgumentNullException.ThrowIfNull(inner);
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(validator);
        Inner = inner;
        _snapshot = snapshot;
        _client = client;
        _validator = validator;
    }

    /// <summary>
    /// Gets the editor called after the catalog check passes.
    /// </summary>
    public IPolicySetEditor Inner { get; }

    /// <summary>
    /// Blocks on <see cref="ReplaceAsync(PolicySet, CancellationToken)"/>.
    /// </summary>
    /// <param name="set">Complete replacement set.</param>
    public void Replace(PolicySet set) => ReplaceAsync(set).GetAwaiter().GetResult();

    /// <summary>
    /// Pulls, validates, updates the snapshot, then calls <see cref="Inner"/>.
    /// </summary>
    /// <param name="set">Complete replacement set.</param>
    /// <param name="cancellationToken">Cancels the pull and the inner replace. A canceled token propagates.</param>
    /// <exception cref="OperationCatalogUnavailableException">
    /// Thrown when the pull lists failed apps and no complete snapshot is stored.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// Thrown by <see cref="OperationPatternValidator.EnsureSupported"/> when a pattern is not supported.
    /// The snapshot and <see cref="Inner"/> stay unchanged.
    /// </exception>
    public async Task ReplaceAsync(PolicySet set, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(set);
        var pull = await _client.PullAsync(cancellationToken).ConfigureAwait(false);
        var (operations, maxSegments) = SelectCatalog(pull);
        _validator.EnsureSupported(set, operations, maxSegments);
        _snapshot.Apply(pull);
        await Inner.ReplaceAsync(set, cancellationToken).ConfigureAwait(false);
    }

    private (IReadOnlyList<string> Operations, int MaxSegments) SelectCatalog(OperationCatalogPull pull)
    {
        if (pull.FailedApps.Count == 0)
            return Union(pull);

        if (_snapshot.HasSnapshot)
            return (_snapshot.OperationValues, _snapshot.MaxSegments);

        throw new OperationCatalogUnavailableException(
            "Operation catalog snapshot is unavailable because the pull failed and no complete catalog is stored.");
    }

    private static (IReadOnlyList<string> Operations, int MaxSegments) Union(OperationCatalogPull pull)
    {
        var values = new List<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var max = 0;
        foreach (var app in pull.Apps)
        {
            foreach (var published in app.Operations)
            {
                if (!seen.Add(published.Operation))
                    continue;

                values.Add(published.Operation);
                var segments = Operation.Parse(published.Operation).Value.Split(':').Length;
                if (segments > max)
                    max = segments;
            }
        }

        return (values, max);
    }
}
