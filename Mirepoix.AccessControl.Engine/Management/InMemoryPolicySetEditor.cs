using Mirepoix.AccessControl.Policy;
using Mirepoix.AccessControl.Providers;

namespace Mirepoix.AccessControl.Management;

/// <summary>
/// Implements <see cref="IPolicySetEditor"/> over <see cref="MemoryPolicySource"/> via its internal whole-set <c>Replace</c>.
/// </summary>
public sealed class InMemoryPolicySetEditor : IPolicySetEditor
{
    private readonly MemoryPolicySource _source;

    /// <summary>
    /// Creates an editor bound to <paramref name="source"/>.
    /// </summary>
    /// <param name="source">In-memory policy source to mutate.</param>
    public InMemoryPolicySetEditor(MemoryPolicySource source)
    {
        _source = source;
    }

    /// <inheritdoc />
    public void Replace(PolicySet set) => _source.Replace(set);
}
