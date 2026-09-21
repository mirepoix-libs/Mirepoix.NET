namespace Mirepoix.AccessControl.Management;

/// <summary>
/// Writes ownership as a resource attribute for evaluation atoms (e.g. subject-id-equals-attribute).
/// Management-plane helper; not called by the checker.
/// </summary>
public interface IOwnershipHelper
{
    /// <summary>
    /// Sets the owner subject id on the resource.
    /// </summary>
    /// <param name="resourceType">Resource type.</param>
    /// <param name="resourceId">Resource id.</param>
    /// <param name="ownerSubjectId">Subject id stored as owner.</param>
    void SetOwner(string resourceType, string resourceId, string ownerSubjectId);

    /// <summary>
    /// Clears ownership on the resource if present.
    /// </summary>
    /// <param name="resourceType">Resource type.</param>
    /// <param name="resourceId">Resource id.</param>
    void ClearOwner(string resourceType, string resourceId);

    /// <summary>
    /// Does the same as <see cref="SetOwner"/> asynchronously.
    /// </summary>
    /// <param name="resourceType">Resource type.</param>
    /// <param name="resourceId">Resource id.</param>
    /// <param name="ownerSubjectId">Subject id stored as owner.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task SetOwnerAsync(string resourceType, string resourceId, string ownerSubjectId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Does the same as <see cref="ClearOwner"/> asynchronously.
    /// </summary>
    /// <param name="resourceType">Resource type.</param>
    /// <param name="resourceId">Resource id.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task ClearOwnerAsync(string resourceType, string resourceId, CancellationToken cancellationToken = default);
}
