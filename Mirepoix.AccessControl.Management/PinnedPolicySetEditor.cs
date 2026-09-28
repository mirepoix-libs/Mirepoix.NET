using Mirepoix.AccessControl.Policy;
using PolicyModel = Mirepoix.AccessControl.Policy.Policy;

namespace Mirepoix.AccessControl.Management;

/// <summary>
/// Strips an exact pin from a replacement and rejects a pin id whose canonical JSON differs.
/// </summary>
public sealed class PinnedPolicySetEditor : IPolicySetEditor
{
    private readonly IPolicySetEditor _inner;
    private readonly IReadOnlyList<PolicyModel> _pins;

    /// <summary>
    /// Creates an editor that filters pins before calling <paramref name="inner"/>.
    /// </summary>
    /// <param name="inner">Editor called only after every pin id is absent or an exact copy.</param>
    /// <param name="pins">Pins in registration order. Ids are compared with ordinal equality.</param>
    public PinnedPolicySetEditor(IPolicySetEditor inner, IReadOnlyList<PolicyModel> pins)
    {
        ArgumentNullException.ThrowIfNull(inner);
        ArgumentNullException.ThrowIfNull(pins);
        _inner = inner;
        _pins = pins;
    }

    /// <summary>
    /// Drops exact pins from <paramref name="set"/> and replaces the stored set. Does not call the inner editor after a pin mismatch.
    /// </summary>
    /// <param name="set">Incoming replacement. Version is kept. Exact pins are not stored.</param>
    /// <exception cref="ArgumentException">Thrown with message "Policy '{id}' is pinned." when a pin id is not an exact copy.</exception>
    public void Replace(PolicySet set) => _inner.Replace(Prepare(set));

    /// <summary>
    /// Does the same as <see cref="Replace"/> asynchronously.
    /// </summary>
    /// <param name="set">Incoming replacement. Version is kept. Exact pins are not stored.</param>
    /// <param name="cancellationToken">Cancellation forwarded only when the inner editor is called.</param>
    /// <exception cref="ArgumentException">Thrown with message "Policy '{id}' is pinned." when a pin id is not an exact copy.</exception>
    public Task ReplaceAsync(PolicySet set, CancellationToken cancellationToken = default) =>
        _inner.ReplaceAsync(Prepare(set), cancellationToken);

    private PolicySet Prepare(PolicySet set)
    {
        ArgumentNullException.ThrowIfNull(set);
        var kept = new List<PolicyModel>(set.Policies.Count);
        foreach (var policy in set.Policies)
        {
            PolicyModel? pin = null;
            foreach (var candidate in _pins)
            {
                if (string.Equals(candidate.Id, policy.Id, StringComparison.Ordinal))
                {
                    pin = candidate;
                    break;
                }
            }

            if (pin is null)
            {
                kept.Add(policy);
                continue;
            }

            if (Canonical(policy) == Canonical(pin))
                continue;

            throw new ArgumentException($"Policy '{policy.Id}' is pinned.");
        }

        return new PolicySet(set.Version, kept);
    }

    private static string Canonical(PolicyModel policy) =>
        PolicySerializers.ToJson(new PolicySet("", [policy]));
}
