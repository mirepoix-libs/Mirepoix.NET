namespace Mirepoix.AccessControl.Management;

/// <summary>
/// Assigns and revokes roles on subjects. SoD is enforced at assign time (result, not exception).
/// Not on the check hot path; <see cref="IAccessChecker"/> never calls this.
/// </summary>
public interface IRoleAssignmentStore
{
    /// <summary>
    /// Assigns <paramref name="roleId"/> to <paramref name="subjectId"/> if SoD allows.
    /// </summary>
    /// <param name="subjectId">Subject receiving the role.</param>
    /// <param name="roleId">Role to assign.</param>
    /// <returns>
    /// <see cref="AssignmentOutcome.Assigned"/> on success (including idempotent re-assign),
    /// or <see cref="AssignmentOutcome.SodConflict"/> without mutating when a constraint blocks.
    /// </returns>
    AssignmentResult Assign(string subjectId, string roleId);

    /// <summary>
    /// Removes <paramref name="roleId"/> from <paramref name="subjectId"/> if present. No-op if unknown.
    /// </summary>
    /// <param name="subjectId">Subject to update.</param>
    /// <param name="roleId">Role to revoke.</param>
    void Revoke(string subjectId, string roleId);

    /// <summary>
    /// Returns the roles currently assigned to <paramref name="subjectId"/> (empty set if none).
    /// </summary>
    /// <param name="subjectId">Subject to query.</param>
    IReadOnlySet<string> GetRoles(string subjectId);

    /// <summary>
    /// Does the same as <see cref="Assign"/> asynchronously.
    /// </summary>
    /// <param name="subjectId">Subject receiving the role.</param>
    /// <param name="roleId">Role to assign.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<AssignmentResult> AssignAsync(string subjectId, string roleId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Does the same as <see cref="Revoke"/> asynchronously.
    /// </summary>
    /// <param name="subjectId">Subject to update.</param>
    /// <param name="roleId">Role to revoke.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task RevokeAsync(string subjectId, string roleId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Does the same as <see cref="GetRoles"/> asynchronously.
    /// </summary>
    /// <param name="subjectId">Subject to query.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<IReadOnlySet<string>> GetRolesAsync(string subjectId, CancellationToken cancellationToken = default);
}
