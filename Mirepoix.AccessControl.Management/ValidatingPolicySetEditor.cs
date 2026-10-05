using Mirepoix.AccessControl;
using Mirepoix.AccessControl.Policy;

namespace Mirepoix.AccessControl.Management;

/// <summary>
/// Pulls configured catalogs, rejects unsupported operations and attributes, then forwards the policy set.
/// </summary>
/// <remarks>
/// Operations are pulled only when an <see cref="IEnforcementCatalogClient"/> is supplied
/// (enforcement apps are configured). Attributes are pulled only when an
/// <see cref="IAttributeCatalogClient"/> is supplied and at least one of
/// <see cref="AttributeCatalogOptions.ProviderApps"/> or <see cref="AttributeCatalogOptions.EnforcementApps"/>
/// is non-empty. An empty provider list leaves subject and resource atoms ungated.
/// An empty enforcement attribute list leaves context atoms ungated.
/// Both lists empty skips the attribute pull.
/// <para>
/// A complete pull validates against that pull's union, then <c>Apply</c> stores it.
/// A pull with failed apps validates against the stored union when a snapshot exists,
/// then <c>Apply</c> records the pull without replacing the union.
/// Validation runs after both selections and before either <c>Apply</c>.
/// A validation failure or a missing snapshot skips both applies and <see cref="Inner"/>.
/// </para>
/// <para>
/// When the operation pull failed and no operation snapshot is stored, this throws
/// <see cref="OperationCatalogUnavailableException"/> before the attribute selection.
/// A missing attribute snapshot throws <see cref="AttributeCatalogUnavailableException"/>
/// only when the operation selection did not already throw.
/// </para>
/// <para>
/// Pinned policies are removed by <see cref="PinnedPolicySetEditor"/> when that decorator
/// is registered outside this one. This type does not strip pins.
/// </para>
/// </remarks>
public sealed class ValidatingPolicySetEditor : IPolicySetEditor
{
    private readonly OperationCatalogSnapshot? _snapshot;
    private readonly IEnforcementCatalogClient? _client;
    private readonly OperationPatternValidator? _validator;
    private readonly AttributeCatalogSnapshot? _attributeSnapshot;
    private readonly IAttributeCatalogClient? _attributeClient;
    private readonly AttributeCatalogValidator? _attributeValidator;
    private readonly AttributeCatalogOptions? _attributeOptions;

    /// <summary>
    /// Creates a decorator that pulls configured catalogs and writes through <paramref name="inner"/>.
    /// </summary>
    /// <param name="inner">Editor invoked only after every configured catalog check passes.</param>
    /// <param name="snapshot">
    /// Operation union. Required when <paramref name="client"/> is non-null.
    /// Updated only after both validators succeed.
    /// </param>
    /// <param name="client">Operation pull. Null skips operation gating.</param>
    /// <param name="validator">
    /// Checks <see cref="OperationMatchAtom"/> patterns. Required when <paramref name="client"/> is non-null.
    /// </param>
    /// <param name="attributeSnapshot">
    /// Attribute union. Required when <paramref name="attributeClient"/> is non-null.
    /// Updated only after both validators succeed.
    /// </param>
    /// <param name="attributeClient">Attribute pull. Null skips attribute gating.</param>
    /// <param name="attributeValidator">
    /// Checks the three attribute atom types. Required when <paramref name="attributeClient"/> is non-null.
    /// </param>
    /// <param name="attributeOptions">
    /// Provider and enforcement attribute apps. Required when <paramref name="attributeClient"/> is non-null.
    /// Empty lists leave the matching slice ungated and, when both are empty, skip the pull.
    /// </param>
    public ValidatingPolicySetEditor(
        IPolicySetEditor inner,
        OperationCatalogSnapshot? snapshot,
        IEnforcementCatalogClient? client,
        OperationPatternValidator? validator,
        AttributeCatalogSnapshot? attributeSnapshot = null,
        IAttributeCatalogClient? attributeClient = null,
        AttributeCatalogValidator? attributeValidator = null,
        AttributeCatalogOptions? attributeOptions = null)
    {
        ArgumentNullException.ThrowIfNull(inner);
        if (client is not null)
        {
            ArgumentNullException.ThrowIfNull(snapshot);
            ArgumentNullException.ThrowIfNull(validator);
        }

        if (attributeClient is not null)
        {
            ArgumentNullException.ThrowIfNull(attributeSnapshot);
            ArgumentNullException.ThrowIfNull(attributeValidator);
            ArgumentNullException.ThrowIfNull(attributeOptions);
        }

        Inner = inner;
        _snapshot = snapshot;
        _client = client;
        _validator = validator;
        _attributeSnapshot = attributeSnapshot;
        _attributeClient = attributeClient;
        _attributeValidator = attributeValidator;
        _attributeOptions = attributeOptions;
    }

    /// <summary>
    /// Gets the editor called after the catalog checks pass.
    /// </summary>
    public IPolicySetEditor Inner { get; }

    /// <summary>
    /// Blocks on <see cref="ReplaceAsync(PolicySet, CancellationToken)"/>.
    /// </summary>
    /// <param name="set">Complete replacement set.</param>
    public void Replace(PolicySet set) => ReplaceAsync(set).GetAwaiter().GetResult();

    /// <summary>
    /// Pulls each configured catalog, validates, updates snapshots, then calls <see cref="Inner"/>.
    /// </summary>
    /// <param name="set">Complete replacement set.</param>
    /// <param name="cancellationToken">Cancels the pulls and the inner replace. A canceled token propagates.</param>
    /// <exception cref="OperationCatalogUnavailableException">
    /// Thrown when the operation pull lists failed apps and no complete operation snapshot is stored.
    /// Also thrown in that case when the attribute snapshot is missing.
    /// </exception>
    /// <exception cref="AttributeCatalogUnavailableException">
    /// Thrown when attribute apps are configured, the attribute pull lists failed apps,
    /// and no complete attribute snapshot is stored. Not thrown when the operation selection already failed.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// Thrown when a gated operation pattern or attribute key or type is not supported.
    /// Neither snapshot is updated and <see cref="Inner"/> is not called.
    /// </exception>
    public async Task ReplaceAsync(PolicySet set, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(set);

        OperationCatalogPull? operationPull = null;
        IReadOnlyList<string>? operations = null;
        var maxSegments = 0;
        if (_client is not null)
        {
            operationPull = await _client.PullAsync(cancellationToken).ConfigureAwait(false);
            (operations, maxSegments) = SelectCatalog(operationPull);
        }

        var gateSubjectAndResource = _attributeOptions is { ProviderApps.Count: > 0 };
        var gateContext = _attributeOptions is { EnforcementApps.Count: > 0 };
        AttributeCatalogPull? attributePull = null;
        IReadOnlyList<PublishedAttribute>? published = null;
        if (_attributeClient is not null && (gateSubjectAndResource || gateContext))
        {
            attributePull = await _attributeClient.PullAsync(cancellationToken).ConfigureAwait(false);
            published = _attributeSnapshot!.SelectAttributes(attributePull);
        }

        if (operationPull is not null)
            _validator!.EnsureSupported(set, operations!, maxSegments);

        if (attributePull is not null)
            _attributeValidator!.EnsureSupported(set, published!, gateSubjectAndResource, gateContext);

        if (operationPull is not null)
            _snapshot!.Apply(operationPull);

        if (attributePull is not null)
            _attributeSnapshot!.Apply(attributePull);

        await Inner.ReplaceAsync(set, cancellationToken).ConfigureAwait(false);
    }

    private (IReadOnlyList<string> Operations, int MaxSegments) SelectCatalog(OperationCatalogPull pull)
    {
        if (pull.FailedApps.Count == 0)
            return Union(pull);

        if (_snapshot!.HasSnapshot)
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
