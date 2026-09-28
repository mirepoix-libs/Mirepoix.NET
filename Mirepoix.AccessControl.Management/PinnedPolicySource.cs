using Mirepoix.AccessControl.Policy;
using Mirepoix.AccessControl.Providers;
using PolicyModel = Mirepoix.AccessControl.Policy.Policy;

namespace Mirepoix.AccessControl.Management;

/// <summary>
/// Loads the stored policy set, drops stored copies of pin ids, and appends the pins.
/// </summary>
public sealed class PinnedPolicySource : IPolicySource
{
    private readonly IPolicySource _inner;
    private readonly IReadOnlyList<PolicyModel> _pins;

    /// <summary>
    /// Creates a source that reads <paramref name="inner"/> and then appends <paramref name="pins"/>.
    /// </summary>
    /// <param name="inner">Stored source. Its version is returned unchanged.</param>
    /// <param name="pins">Pins in registration order. Ids are compared with ordinal equality.</param>
    public PinnedPolicySource(IPolicySource inner, IReadOnlyList<PolicyModel> pins)
    {
        ArgumentNullException.ThrowIfNull(inner);
        ArgumentNullException.ThrowIfNull(pins);
        _inner = inner;
        _pins = pins;
    }

    /// <summary>
    /// Returns the inner set with stored policies whose id matches a pin removed, then the pins appended.
    /// </summary>
    /// <param name="cancellationToken">Cancellation forwarded to the inner source.</param>
    /// <returns>Effective set. <see cref="PolicySet.Version"/> is the stored version.</returns>
    public async Task<PolicySet> GetPolicySetAsync(CancellationToken cancellationToken)
    {
        var set = await _inner.GetPolicySetAsync(cancellationToken).ConfigureAwait(false);
        var pinIds = new HashSet<string>(_pins.Select(pin => pin.Id), StringComparer.Ordinal);
        var kept = new List<PolicyModel>(set.Policies.Count + _pins.Count);
        foreach (var policy in set.Policies)
        {
            if (!pinIds.Contains(policy.Id))
                kept.Add(policy);
        }

        kept.AddRange(_pins);
        return new PolicySet(set.Version, kept);
    }
}
