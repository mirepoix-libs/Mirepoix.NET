using Mirepoix.AccessControl.Policy;

namespace Mirepoix.AccessControl.Management;

/// <summary>
/// Replaces the active policy set as a whole (no partial merge). Management plane only.
/// </summary>
public interface IPolicySetEditor
{
    /// <summary>
    /// Installs <paramref name="set"/> as the current policy set.
    /// </summary>
    /// <param name="set">Complete replacement set.</param>
    void Replace(PolicySet set);

    /// <summary>
    /// Does the same as <see cref="Replace"/> asynchronously.
    /// </summary>
    /// <param name="set">Complete replacement set.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task ReplaceAsync(PolicySet set, CancellationToken cancellationToken = default);
}
